using Google.Protobuf;
using Grpc.Core;
using Lnrpc;
using Microsoft.EntityFrameworkCore;
using Nostr.Client.Utils;
using NostrStreamer.Database;

namespace NostrStreamer.Services.Background;

public class LndInvoicesStream : BackgroundService
{
    private readonly LndNode _lnd;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LndInvoicesStream> _logger;

    public LndInvoicesStream(LndNode lnd, ILogger<LndInvoicesStream> logger, IServiceScopeFactory scopeFactory)
    {
        _lnd = lnd;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var lastIndex = await GetLastSettleIndex();
                _logger.LogInformation("Starting stream from add_index {idx}", lastIndex);
                var stream = _lnd.LightningClient.SubscribeInvoices(new()
                {
                    SettleIndex = lastIndex
                });

                await foreach (var msg in stream.ResponseStream.ReadAllAsync(stoppingToken))
                {
                    if (msg == default) continue;

                    var pHash = msg.RHash.ToByteArray().ToHex();
                    _logger.LogInformation("{hash} changed to {state}", pHash, msg.State);
                    if (msg.State is Invoice.Types.InvoiceState.Settled or Invoice.Types.InvoiceState.Canceled)
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<StreamerContext>();
                        var zapService = scope.ServiceProvider.GetRequiredService<ZapService>();

                        try
                        {
                            var payment = await db.Payments
                                .Include(a => a.User)
                                .SingleOrDefaultAsync(a => a.PaymentHash == pHash,
                                cancellationToken: stoppingToken);

                            if (payment is {IsPaid: false} && msg.State is Invoice.Types.InvoiceState.Settled)
                            {
                                payment.IsPaid = true;
                                payment.User.Balance += (long)payment.Amount;
                                await db.SaveChangesAsync(stoppingToken);
                                if (!string.IsNullOrEmpty(payment.Nostr) && !string.IsNullOrEmpty(payment.Invoice))
                                {
                                    zapService.HandlePaid(payment.Invoice, payment.Nostr);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to process payment {hash}", pHash);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Subscribe invoices failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task<ulong> GetLastSettleIndex()
    {
        using var scope = _scopeFactory.CreateScope();
        await using var ctx = scope.ServiceProvider.GetRequiredService<StreamerContext>();
        var latestUnpaid = await ctx.Payments
            .AsNoTracking()
            .Where(a => a.IsPaid)
            .OrderByDescending(a => a.Created)
            .FirstOrDefaultAsync();

        if (latestUnpaid == default)
        {
            return 0;
        }

        try
        {
            var invoice = await _lnd.LightningClient.LookupInvoiceAsync(new()
            {
                RHash = ByteString.CopyFrom(Convert.FromHexString(latestUnpaid.PaymentHash))
            });

            return invoice?.SettleIndex ?? 0;
        }
        catch
        {
            return 0;
        }
    }
}
