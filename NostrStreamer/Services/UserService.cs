using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Nostr.Client.Utils;
using NostrStreamer.ApiModel;
using NostrStreamer.Database;

namespace NostrStreamer.Services;

public class UserService
{
    private readonly StreamerContext _db;
    private readonly LndNode _lnd;
    private readonly IDataProtectionProvider _dataProtector;

    public UserService(StreamerContext db, LndNode lnd, IDataProtectionProvider dataProtector)
    {
        _db = db;
        _lnd = lnd;
        _dataProtector = dataProtector;
    }

    /// <summary>
    /// Create new user account
    /// </summary>
    /// <param name="pubkey"></param>
    /// <returns></returns>
    public async Task<User> CreateAccount(string pubkey)
    {
        var user = new User()
        {
            PubKey = pubkey,
            Balance = 1000_000,
            StreamKey = Guid.NewGuid().ToString()
        };

        _db.Users.Add(user);
        _db.Payments.Add(new Payment()
        {
            PubKey = pubkey,
            Type = PaymentType.Credit,
            IsPaid = true,
            Amount = (ulong)user.Balance / 1000,
            PaymentHash = SHA256.HashData(Encoding.UTF8.GetBytes($"{pubkey}-init-credit")).ToHex()
        });

        await _db.SaveChangesAsync();
        return user;
    }

    /// <summary>
    /// Create topup for a user
    /// </summary>
    /// <param name="pubkey"></param>
    /// <param name="amount">milli-sats amount</param>
    /// <param name="descHash"></param>
    /// <param name="nostr"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public async Task<string> CreateTopup(string pubkey, ulong amount, string? descHash, string? nostr)
    {
        var user = await GetUser(pubkey);
        if (user == default) throw new Exception("No user found");

        var invoice = await _lnd.AddInvoice(amount, TimeSpan.FromMinutes(10), $"Top up for {pubkey}", descHash);
        _db.Payments.Add(new()
        {
            PubKey = pubkey,
            Amount = amount / 1000,
            Invoice = invoice.PaymentRequest,
            PaymentHash = invoice.RHash.ToByteArray().ToHex(),
            Nostr = nostr,
            Type = string.IsNullOrEmpty(nostr) ? PaymentType.Topup : PaymentType.Zap
        });

        await _db.SaveChangesAsync();

        return invoice.PaymentRequest;
    }

    public async Task<User?> GetUser(string pubkey)
    {
        return await _db.Users.AsNoTracking()
            .Include(a => a.Forwards)
            .SingleOrDefaultAsync(a => a.PubKey.Equals(pubkey));
    }

    public async Task AcceptTos(string pubkey)
    {
        var change = await _db.Users.Where(a => a.PubKey.Equals(pubkey))
            .ExecuteUpdateAsync(o => o.SetProperty(v => v.TosAccepted, DateTime.UtcNow));

        if (change != 1) throw new Exception($"Failed to accept TOS, {change} rows updated.");
    }

    public async Task AddForward(string pubkey, string name, string dest)
    {
        var protector = _dataProtector.CreateProtector("forward-targets");
        _db.Forwards.Add(new()
        {
            UserPubkey = pubkey,
            Name = name,
            Target = protector.Protect(dest)
        });

        await _db.SaveChangesAsync();
    }

    public async Task RemoveForward(string pubkey, Guid id)
    {
        await _db.Forwards.Where(a => a.UserPubkey.Equals(pubkey) && a.Id == id)
            .ExecuteDeleteAsync();
    }

    public async Task UpdateStreamInfo(string pubkey, PatchEvent req)
    {
        await _db.Users
            .Where(a => a.PubKey == pubkey)
            .ExecuteUpdateAsync(o => o.SetProperty(v => v.Title, req.Title)
                .SetProperty(v => v.Summary, req.Summary)
                .SetProperty(v => v.Image, req.Image)
                .SetProperty(v => v.Tags, req.Tags.Length > 0 ? string.Join(",", req.Tags) : null)
                .SetProperty(v => v.ContentWarning, req.ContentWarning)
                .SetProperty(v => v.Goal, req.Goal));
    }
}
