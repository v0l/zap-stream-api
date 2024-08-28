using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Nostr.Client.Json;
using Nostr.Client.Messages;
using Nostr.Client.Utils;
using NostrServices.Client;
using NostrStreamer.Database;
using NostrStreamer.Services.Dvr;

namespace NostrStreamer.Services.StreamManager;

public class NostrStreamManager : IStreamManager
{
    private readonly ILogger<NostrStreamManager> _logger;
    private readonly StreamManagerContext _context;
    private readonly StreamEventBuilder _eventBuilder;
    private readonly IDvrStore _dvrStore;
    private readonly Config _config;
    private readonly DiscordWebhook _webhook;
    private readonly NostrServicesClient _nostrApi;
    private readonly IDataProtectionProvider _dataProtectionProvider;

    public NostrStreamManager(ILogger<NostrStreamManager> logger, StreamManagerContext context,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _context = context;
        _eventBuilder = serviceProvider.GetRequiredService<StreamEventBuilder>();
        _dvrStore = serviceProvider.GetRequiredService<IDvrStore>();
        _config = serviceProvider.GetRequiredService<Config>();
        _dataProtectionProvider = serviceProvider.GetRequiredService<IDataProtectionProvider>();
        _webhook = serviceProvider.GetRequiredService<DiscordWebhook>();
        _nostrApi = serviceProvider.GetRequiredService<NostrServicesClient>();
    }

    public UserStream GetStream()
    {
        return _context.UserStream;
    }

    public void TestCanStream()
    {
        if (_context.User.Balance <= 0)
        {
            throw new LowBalanceException("User balance empty");
        }

        if (_context.User.TosAccepted == null || _context.User.TosAccepted < _config.TosDate)
        {
            throw new Exception("TOS not accepted");
        }
    }

    public Task<List<string>> OnForward()
    {
        TestCanStream();
        var fwds = new List<string>
        {
            $"rtmp://127.0.0.1:1935/{_context.UserStream.Endpoint.App}/{_context.StreamKey}?vhost={_context.UserStream.Endpoint.Forward}"
        };

        var dataProtector = _dataProtectionProvider.CreateProtector("forward-targets");
        foreach (var f in _context.User.Forwards)
        {
            try
            {
                var target = dataProtector.Unprotect(f.Target);
                fwds.Add(target);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrypt forward target {id} {msg}", f.Id, ex.Message);
            }
        }

        return Task.FromResult(fwds);
    }

    public async Task StreamStarted()
    {
        _logger.LogInformation("Stream started for: {pubkey}", _context.User.PubKey);
        TestCanStream();

        var ev = await UpdateStreamState(UserStreamState.Live);

        _ = Task.Factory.StartNew(async () =>
        {
            if (_config.DiscordLiveWebhook != default)
            {
                try
                {
                    var npub = NostrConverter.ToBech32(_context.User.PubKey, "npub")!;
                    var profile = await _nostrApi.Profile(npub);
                    var name = profile?.Name ?? npub;
                    var id = ev.ToIdentifier();
                    await _webhook.SendMessage(_config.DiscordLiveWebhook,
                        $"{name} went live!\nhttps://zap.stream/{id.ToBech32()}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to send notification {msg}", ex.Message);
                }
            }
        });
    }

    public async Task StreamStopped()
    {
        _logger.LogInformation("Stream stopped for: {pubkey}", _context.User.PubKey);

        await UpdateStreamState(UserStreamState.Ended);

        // send DM with link to summary
        var msg =
            (_context.UserStream.Thumbnail != default ? $"{_context.UserStream.Thumbnail}\n" : "") +
            $"Your stream summary is available here: https://zap.stream/summary/{_context.UserStream.ToIdentifier().ToBech32()}\n\n" +
            $"You paid {_context.UserStream.MilliSatsCollected / 1000:#,##0.###} sats for this stream!\n\n" +
            $"You streamed for {_context.UserStream.Length / 60:#,##0} mins!";

        var chat = _eventBuilder.CreateDm(_context.UserStream, msg);
        _eventBuilder.BroadcastEvent(chat);
    }

    public async Task ConsumeQuota(double duration)
    {
        const long balanceAlertThreshold = 500_000;
        var cost = (long)Math.Ceiling(_context.UserStream.Endpoint.Cost * (duration / 60d));
        if (cost > 0)
        {
            await using var tx = await _context.Db.Database.BeginTransactionAsync();
            try
            {
                await _context.Db.Users
                    .Where(a => a.PubKey == _context.User.PubKey)
                    .ExecuteUpdateAsync(o =>
                        o.SetProperty(v => v.Balance, v => v.Balance - cost));
                await _context.Db.Streams
                    .Where(a => a.Id == _context.UserStream.Id)
                    .ExecuteUpdateAsync(o =>
                        o.SetProperty(v => v.MilliSatsCollected, v => v.MilliSatsCollected + cost)
                            .SetProperty(v => v.Length, v => v.Length + (decimal)duration));
                await tx.CommitAsync();
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Balance update failed {msg}", ex.Message);
            }
        }

        _logger.LogInformation("Stream produced {n} seconds for {pubkey} costing {cost:#,##0} milli-sats", duration,
            _context.User.PubKey,
            cost);

        if (_context.User.Balance >= balanceAlertThreshold && _context.User.Balance - cost < balanceAlertThreshold)
        {
            var chat = _eventBuilder.CreateStreamChat(_context.UserStream,
                $"Your balance is below {(int)(balanceAlertThreshold / 1000m)} sats, please topup, " +
                $"or use this link: lnurlp://zap.stream/.well-known/lnurlp/{_context.User.PubKey}");

            _eventBuilder.BroadcastEvent(chat);
        }

        if (_context.User.Balance <= 0)
        {
            _logger.LogInformation("Kicking stream due to low balance");
            await _context.EdgeApi.KickClient(_context.UserStream.ForwardClientId);
        }
    }

    public async Task AddGuest(string pubkey, string role, decimal zapSplit)
    {
        _context.Db.Guests.Add(new()
        {
            StreamId = _context.UserStream.Id,
            PubKey = pubkey,
            Role = role,
            ZapSplit = zapSplit
        });

        await _context.Db.SaveChangesAsync();
    }

    public async Task RemoveGuest(string pubkey)
    {
        await _context.Db.Guests
            .Where(a => a.PubKey == pubkey && a.StreamId == _context.UserStream.Id)
            .ExecuteDeleteAsync();
    }

    public async Task OnDvr(Uri segment)
    {
        //var matches = new Regex("\\.(\\d+)\\.[\\w]{2,4}$").Match(segment.AbsolutePath);

        try
        {
            if (_context.UserStream.Endpoint.Capabilities.Contains("dvr:source"))
            {
                var result = await _dvrStore.UploadRecording(_context.UserStream, segment);
                _context.Db.Recordings.Add(new()
                {
                    Id = result.Id,
                    UserStreamId = _context.UserStream.Id,
                    Url = result.Result.ToString(),
                    Duration = result.Duration,
                    Timestamp = DateTime
                        .UtcNow //DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(matches.Groups[1].Value)).UtcDateTime
                });

                await _context.Db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to save recording segment {}, {}", segment, ex.Message);
        }

        await _context.Db.Streams
            .Where(a => a.Id == _context.UserStream.Id)
            .ExecuteUpdateAsync(a => a.SetProperty(b => b.LastSegment, DateTime.UtcNow));
    }

    public async Task UpdateEvent()
    {
        await UpdateStreamState(_context.UserStream.State);
    }

    public async Task<List<UserStreamRecording>> GetRecordings()
    {
        return await _context.Db.Recordings.AsNoTracking()
            .Where(a => a.UserStreamId == _context.UserStream.Id)
            .ToListAsync();
    }

    public async Task<UserStreamRecording?> GetLatestRecordingSegment()
    {
        return await _context.Db.Recordings.AsNoTracking()
            .Where(a => a.UserStreamId == _context.UserStream.Id)
            .OrderByDescending(a => a.Timestamp)
            .FirstOrDefaultAsync();
    }

    public async Task UpdateViewers()
    {
        if (_context.UserStream.State is not UserStreamState.Live) return;

        var existingEvent = _context.UserStream.GetEvent();
        var oldViewers = existingEvent?.Tags?.FindFirstTagValue("current_participants");

        var newEvent = _eventBuilder.CreateStreamEvent(_context.UserStream);
        var newViewers = newEvent?.Tags?.FindFirstTagValue("current_participants");

        if (newEvent != default && int.TryParse(oldViewers, out var a) && int.TryParse(newViewers, out var b) && a != b)
        {
            await _context.Db.Streams.Where(a => a.Id == _context.UserStream.Id)
                .ExecuteUpdateAsync(o =>
                    o.SetProperty(v => v.Event, JsonConvert.SerializeObject(newEvent, NostrSerializer.Settings)));

            _eventBuilder.BroadcastEvent(newEvent);
        }
    }

    private async Task<NostrEvent> UpdateStreamState(UserStreamState state)
    {
        DateTime? ends = state == UserStreamState.Ended ? DateTime.UtcNow : null;
        _context.UserStream.State = state;
        _context.UserStream.Ends = ends;
        var ev = _eventBuilder.CreateStreamEvent(_context.UserStream);

        await _context.Db.Streams.Where(a => a.Id == _context.UserStream.Id)
            .ExecuteUpdateAsync(o => o.SetProperty(v => v.State, state)
                .SetProperty(v => v.Event, JsonConvert.SerializeObject(ev, NostrSerializer.Settings))
                .SetProperty(v => v.Ends, ends));

        _eventBuilder.BroadcastEvent(ev);
        return ev;
    }
}