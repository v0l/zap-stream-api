using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Nostr.Client.Json;
using NostrStreamer.Database;
using NostrStreamer.Services.Dvr;

namespace NostrStreamer.Services.StreamManager;

public class NostrStreamManager : IStreamManager
{
    private readonly ILogger<NostrStreamManager> _logger;
    private readonly StreamManagerContext _context;
    private readonly StreamEventBuilder _eventBuilder;
    private readonly IDvrStore _dvrStore;
    private readonly ThumbnailService _thumbnailService;
    private readonly Config _config;

    public NostrStreamManager(ILogger<NostrStreamManager> logger, StreamManagerContext context, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _context = context;
        _eventBuilder = serviceProvider.GetRequiredService<StreamEventBuilder>();
        _dvrStore = serviceProvider.GetRequiredService<IDvrStore>();
        _thumbnailService = serviceProvider.GetRequiredService<ThumbnailService>();
        _config = serviceProvider.GetRequiredService<Config>();
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
        return Task.FromResult(new List<string>
        {
            $"rtmp://127.0.0.1:1935/{_context.UserStream.Endpoint.App}/{_context.User.StreamKey}?vhost={_context.UserStream.Endpoint.Forward}"
        });
    }

    public async Task StreamStarted()
    {
        _logger.LogInformation("Stream started for: {pubkey}", _context.User.PubKey);
        TestCanStream();
        await UpdateStreamState(UserStreamState.Live);

#pragma warning disable CS4014
        Task.Run(async () => await _thumbnailService.GenerateThumb(_context.UserStream));
#pragma warning restore CS4014
    }

    public async Task StreamStopped()
    {
        _logger.LogInformation("Stream stopped for: {pubkey}", _context.User.PubKey);

        await UpdateStreamState(UserStreamState.Ended);
    }

    public async Task ConsumeQuota(double duration)
    {
        const long balanceAlertThreshold = 500_000;
        var cost = (long)Math.Ceiling(_context.UserStream.Endpoint.Cost * (duration / 60d));
        if (cost > 0)
        {
            await _context.Db.Users
                .Where(a => a.PubKey == _context.User.PubKey)
                .ExecuteUpdateAsync(o => o.SetProperty(v => v.Balance, v => v.Balance - cost));
        }

        _logger.LogInformation("Stream consumed {n} seconds for {pubkey} costing {cost:#,##0} milli-sats", duration, _context.User.PubKey,
            cost);

        if (_context.User.Balance >= balanceAlertThreshold && _context.User.Balance - cost < balanceAlertThreshold)
        {
            var chat = _eventBuilder.CreateStreamChat(_context.UserStream,
                $"Your balance is below {(int)(balanceAlertThreshold / 1000m)} sats, please topup");

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

        var result = await _dvrStore.UploadRecording(_context.UserStream, segment);
        _context.Db.Recordings.Add(new()
        {
            UserStreamId = _context.UserStream.Id,
            Url = result.Result.ToString(),
            Duration = result.Duration,
            Timestamp = DateTime.UtcNow //DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(matches.Groups[1].Value)).UtcDateTime
        });

        await _context.Db.SaveChangesAsync();
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

    public async Task UpdateViewers()
    {
        if (_context.UserStream.State is not UserStreamState.Live) return;

        var existingEvent = _context.UserStream.GetEvent();
        var oldViewers = existingEvent?.Tags?.FindFirstTagValue("current_participants");

        var newEvent = _eventBuilder.CreateStreamEvent(_context.User, _context.UserStream);
        var newViewers = newEvent?.Tags?.FindFirstTagValue("current_participants");

        if (newEvent != default && int.TryParse(oldViewers, out var a) && int.TryParse(newViewers, out var b) && a != b)
        {
            await _context.Db.Streams.Where(a => a.Id == _context.UserStream.Id)
                .ExecuteUpdateAsync(o => o.SetProperty(v => v.Event, JsonConvert.SerializeObject(newEvent, NostrSerializer.Settings)));

            _eventBuilder.BroadcastEvent(newEvent);
        }
    }

    private async Task UpdateStreamState(UserStreamState state)
    {
        _context.UserStream.State = state;
        var ev = _eventBuilder.CreateStreamEvent(_context.User, _context.UserStream);

        DateTime? ends = state == UserStreamState.Ended ? DateTime.UtcNow : null;
        await _context.Db.Streams.Where(a => a.Id == _context.UserStream.Id)
            .ExecuteUpdateAsync(o => o.SetProperty(v => v.State, state)
                .SetProperty(v => v.Event, JsonConvert.SerializeObject(ev, NostrSerializer.Settings))
                .SetProperty(v => v.Ends, ends));

        _eventBuilder.BroadcastEvent(ev);
    }
}
