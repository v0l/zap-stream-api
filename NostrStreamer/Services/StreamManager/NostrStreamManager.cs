using System.Text.RegularExpressions;
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
    private readonly SrsApi _srsApi;
    private readonly IDvrStore _dvrStore;

    public NostrStreamManager(ILogger<NostrStreamManager> logger, StreamManagerContext context,
        StreamEventBuilder eventBuilder, SrsApi srsApi, IDvrStore dvrStore)
    {
        _logger = logger;
        _context = context;
        _eventBuilder = eventBuilder;
        _srsApi = srsApi;
        _dvrStore = dvrStore;
    }

    public UserStream GetStream()
    {
        return _context.UserStream;
    }

    public Task<List<string>> OnForward()
    {
        if (_context.User.Balance <= 0)
        {
            throw new LowBalanceException("User balance empty");
        }

        return Task.FromResult(new List<string>
        {
            $"rtmp://127.0.0.1:1935/{_context.UserStream.Endpoint.App}/{_context.User.StreamKey}?vhost={_context.UserStream.Endpoint.Forward}"
        });
    }

    public async Task StreamStarted()
    {
        _logger.LogInformation("Stream started for: {pubkey}", _context.User.PubKey);

        if (_context.User.Balance <= 0)
        {
            throw new Exception("User balance empty");
        }

        await UpdateStreamState(UserStreamState.Live);
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
            await _srsApi.KickClient(_context.UserStream.ClientId);
        }
    }

    public async Task PatchEvent(string? title, string? summary, string? image, string[]? tags, string? contentWarning)
    {
        var user = _context.User;

        await _context.Db.Users
            .Where(a => a.PubKey == _context.User.PubKey)
            .ExecuteUpdateAsync(o => o.SetProperty(v => v.Title, title)
                .SetProperty(v => v.Summary, summary)
                .SetProperty(v => v.Image, image)
                .SetProperty(v => v.Tags, tags != null ? string.Join(",", tags) : null)
                .SetProperty(v => v.ContentWarning, contentWarning));

        user.Title = title;
        user.Summary = summary;
        user.Image = image;
        user.Tags = tags != null ? string.Join(",", tags) : null;
        user.ContentWarning = contentWarning;

        var ev = _eventBuilder.CreateStreamEvent(user, _context.UserStream);
        await _context.Db.Streams.Where(a => a.Id == _context.UserStream.Id)
            .ExecuteUpdateAsync(o => o.SetProperty(v => v.Event, JsonConvert.SerializeObject(ev, NostrSerializer.Settings)));

        _eventBuilder.BroadcastEvent(ev);
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
        var matches = new Regex("\\.(\\d+)\\.[\\w]{2,4}$").Match(segment.AbsolutePath);

        var result = await _dvrStore.UploadRecording(segment);
        _context.Db.Recordings.Add(new()
        {
            UserStreamId = _context.UserStream.Id,
            Url = result.Result.ToString(),
            Duration = result.Duration,
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(matches.Groups[1].Value)).UtcDateTime
        });

        await _context.Db.SaveChangesAsync();
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
