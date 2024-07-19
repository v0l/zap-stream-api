using System.Security.Cryptography;
using System.Text;
using BTCPayServer.Lightning;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using NBitcoin;
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
            Amount = (ulong)user.Balance,
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
            Amount = amount,
            Invoice = invoice.PaymentRequest,
            PaymentHash = invoice.RHash.ToByteArray().ToHex(),
            Nostr = nostr,
            Type = string.IsNullOrEmpty(nostr) ? PaymentType.Topup : PaymentType.Zap
        });

        await _db.SaveChangesAsync();

        return invoice.PaymentRequest;
    }

    public async Task<(long Fee, string Preimage)> WithdrawFunds(string pubkey, string invoice)
    {
        var user = await GetUser(pubkey);
        if (user == default) throw new Exception("No user found");

        var maxOut = await MaxWithdrawalAmount(pubkey);
        var pr = BOLT11PaymentRequest.Parse(invoice, invoice.StartsWith("lnbc1") ? Network.Main : Network.RegTest);
        if (pr.MinimumAmount == 0)
        {
            throw new Exception("0 amount invoice not supported");
        }

        if (maxOut <= pr.MinimumAmount.MilliSatoshi)
        {
            throw new Exception("Not enough balance to pay invoice");
        }

        // start by taking balance
        var rHash = pr.PaymentHash!.ToString();
        await using (var tx = await _db.Database.BeginTransactionAsync())
        {
            await _db.Users
                .Where(a => a.PubKey == pubkey)
                .ExecuteUpdateAsync(p => p.SetProperty(o => o.Balance, b => b.Balance - pr.MinimumAmount.MilliSatoshi));
            _db.Payments.Add(new()
            {
                PubKey = pubkey,
                Invoice = invoice,
                Type = PaymentType.Withdrawal,
                PaymentHash = rHash,
                Amount = (ulong)pr.MinimumAmount.MilliSatoshi
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }

        try
        {
            const double feeMax = 0.005; // 0.5% max fee
            var result = await _lnd.SendPayment(invoice, (long)(pr.MinimumAmount.MilliSatoshi * feeMax));
            if (result?.Status is Lnrpc.Payment.Types.PaymentStatus.Succeeded)
            {
                // update payment amount with fee + mark as completed
                await _db.Payments
                    .Where(a => a.PaymentHash == rHash)
                    .ExecuteUpdateAsync(o => o.SetProperty(v => v.IsPaid, true)
                        .SetProperty(v => v.Fee, (ulong)result.FeeMsat));

                // take fee from balance
                await _db.Users
                    .Where(a => a.PubKey == pubkey)
                    .ExecuteUpdateAsync(p => p.SetProperty(o => o.Balance, b => b.Balance - result.FeeSat));
                return (result.FeeMsat, result.PaymentPreimage);
            }

            throw new Exception("Payment failed");
        }
        catch
        {
            // return balance on error
            await _db.Users
                .Where(a => a.PubKey == pubkey)
                .ExecuteUpdateAsync(p => p.SetProperty(o => o.Balance, b => b.Balance + pr.MinimumAmount.MilliSatoshi));
            throw;
        }
    }

    public async Task<List<Payment>> ListPayments(string pubkey, int offset = 0, int limit = 100)
    {
        return await _db.Payments
            .Where(a => a.PubKey == pubkey && a.IsPaid)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<BalanceHistoryItem>> BalanceHistory(string pubkey, int offset = 0, int limit = 100)
    {
        return await _db.Payments
            .Where(a => a.PubKey == pubkey && a.IsPaid)
            .Select(t => new BalanceHistoryItem
            {
                Created = t.Created,
                Type = t.Type == PaymentType.Withdrawal ? BalanceHistoryItemType.Debit : BalanceHistoryItemType.Credit,
                Description = t.Type == PaymentType.Withdrawal
                    ? "Withdrawal"
                    : (t.Type == PaymentType.Credit ? "Admin Credit" : ""),
                Amount = t.Amount / 1000m
            })
            .Union(_db.Streams
                .Where(a => a.PubKey == pubkey && a.State == UserStreamState.Ended)
                .Select(t => new BalanceHistoryItem
                {
                    Created = t.Starts,
                    Description = t.Event,
                    Type = BalanceHistoryItemType.Debit,
                    Amount = t.MilliSatsCollected / 1000m
                }))
            .OrderByDescending(a => a.Created)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();
    }


    public async Task<long> MaxWithdrawalAmount(string pubkey)
    {
        var credit = await _db.Payments
            .Where(a => a.PubKey == pubkey &&
                        a.IsPaid &&
                        a.Type == PaymentType.Credit)
            .SumAsync(a => (long)a.Amount);

        var balance = await _db.Users
            .Where(a => a.PubKey == pubkey)
            .Select(a => a.Balance)
            .FirstAsync();

        return Math.Max(0, balance - credit);
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