using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Nostr.Client.Utils;
using NostrStreamer.Database;

namespace NostrStreamer.Services;

public class UserService
{
    private readonly StreamerContext _db;
    private readonly LndNode _lnd;

    public UserService(StreamerContext db, LndNode lnd)
    {
        _db = db;
        _lnd = lnd;
    }

    public async Task<string> CreateTopup(string pubkey, ulong amount, string? nostr)
    {
        var user = await GetUser(pubkey);
        if (user == default) throw new Exception("No user found");

        var descHash = string.IsNullOrEmpty(nostr) ? null : SHA256.HashData(Encoding.UTF8.GetBytes(nostr)).ToHex();
        var invoice = await _lnd.AddInvoice(amount * 1000, TimeSpan.FromMinutes(10), $"Top up for {pubkey}", descHash);
        _db.Payments.Add(new()
        {
            PubKey = pubkey,
            Amount = amount,
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
            .SingleOrDefaultAsync(a => a.PubKey.Equals(pubkey));
    }
}
