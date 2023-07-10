using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Nostr.Client.Client;
using Nostr.Client.Json;
using Nostr.Client.Keys;
using Nostr.Client.Messages;
using Nostr.Client.Requests;
using NostrStreamer.Database;

namespace NostrStreamer.Services;

public class StreamManager
{
    private const NostrKind StreamEventKind = (NostrKind)30_311;
    private const NostrKind StreamChatKind = (NostrKind)1311;

    private readonly ILogger<StreamManager> _logger;
    private readonly StreamerContext _db;
    private readonly Config _config;
    private readonly INostrClient _nostr;
    private readonly SrsApi _srsApi;

    public StreamManager(ILogger<StreamManager> logger, StreamerContext db, Config config, INostrClient nostr, SrsApi srsApi)
    {
        _logger = logger;
        _db = db;
        _config = config;
        _nostr = nostr;
        _srsApi = srsApi;
    }

    public async Task StreamStarted(string streamKey)
    {
        var user = await GetUserFromStreamKey(streamKey);
        if (user == default) throw new Exception("No stream key found");

        _logger.LogInformation("Stream started for: {pubkey}", user.PubKey);

        if (user.Balance <= 0)
        {
            throw new Exception("User balance empty");
        }

        var ev = CreateStreamEvent(user, "live");
        await PublishEvent(user, ev);
    }

    public async Task StreamStopped(string streamKey)
    {
        var user = await GetUserFromStreamKey(streamKey);
        if (user == default) throw new Exception("No stream key found");

        _logger.LogInformation("Stream stopped for: {pubkey}", user.PubKey);

        var ev = CreateStreamEvent(user, "ended");
        await PublishEvent(user, ev);
    }

    public async Task ConsumeQuota(string streamKey, double duration, string clientId)
    {
        var user = await GetUserFromStreamKey(streamKey);
        if (user == default) throw new Exception("No stream key found");

        const long balanceAlertThreshold = 500;
        var cost = (int)Math.Ceiling(_config.Cost * (duration / 60d));
        if (cost > 0)
        {
            await _db.Users
                .Where(a => a.PubKey == user.PubKey)
                .ExecuteUpdateAsync(o => o.SetProperty(v => v.Balance, v => v.Balance - cost));
        }

        _logger.LogInformation("Stream consumed {n} seconds for {pubkey} costing {cost} sats", duration, user.PubKey, cost);
        if (user.Balance >= balanceAlertThreshold && user.Balance - cost < balanceAlertThreshold)
        {
            _nostr.Send(new NostrEventRequest(CreateStreamChat(user,
                $"Your balance is below {balanceAlertThreshold} sats, please topup")));
        }

        if (user.Balance <= 0)
        {
            _logger.LogInformation("Kicking stream due to low balance");
            await _srsApi.KickClient(clientId);
        }
    }

    public async Task PatchEvent(string pubkey, string? title, string? summary, string? image, string[]? tags, string? contentWarning)
    {
        var user = await _db.Users.SingleOrDefaultAsync(a => a.PubKey == pubkey);
        if (user == default) throw new Exception("User not found");

        user.Title = title;
        user.Summary = summary;
        user.Image = image;
        user.Tags = tags != null ? string.Join(",", tags) : null;
        user.ContentWarning = contentWarning;

        var ev = CreateStreamEvent(user);
        user.Event = JsonConvert.SerializeObject(ev, NostrSerializer.Settings);

        await _db.SaveChangesAsync();
        _nostr.Send(new NostrEventRequest(ev));
    }

    public async Task UpdateViewers(string streamKey, int viewers)
    {
        var user = await GetUserFromStreamKey(streamKey);
        if (user == default) throw new Exception("No stream key found");

        var existingEvent = user.GetNostrEvent();
        var oldViewers = existingEvent?.Tags?.FindFirstTagValue("viewers");
        if (string.IsNullOrEmpty(oldViewers) || int.Parse(oldViewers) != viewers)
        {
            var ev = CreateStreamEvent(user, viewers: viewers);
            await PublishEvent(user, ev);
        }
    }

    private async Task PublishEvent(User user, NostrEvent ev)
    {
        await _db.Users
            .Where(a => a.PubKey == user.PubKey)
            .ExecuteUpdateAsync(o => o.SetProperty(v => v.Event, JsonConvert.SerializeObject(ev, NostrSerializer.Settings)));

        _nostr.Send(new NostrEventRequest(ev));
    }

    private NostrEvent CreateStreamChat(User user, string message)
    {
        var pk = NostrPrivateKey.FromBech32(_config.PrivateKey);
        var ev = new NostrEvent
        {
            Kind = StreamChatKind,
            Content = message,
            CreatedAt = DateTime.Now,
            Tags = new NostrEventTags(
                new NostrEventTag("a", $"{StreamEventKind}:{pk.DerivePublicKey().Hex}:{user.PubKey}")
            )
        };

        return ev.Sign(pk);
    }

    private NostrEvent CreateStreamEvent(User user, string? state = null, int? viewers = null)
    {
        var existingEvent = user.GetNostrEvent();
        var status = state ?? existingEvent?.Tags?.FindFirstTagValue("status") ?? "ended";

        var tags = new List<NostrEventTag>
        {
            new("d", user.PubKey),
            new("title", user.Title ?? ""),
            new("summary", user.Summary ?? ""),
            new("streaming", GetStreamUrl(user)),
            new("image", user.Image ?? ""),
            new("status", status),
            new("p", user.PubKey, "", "host"),
            new("relays", _config.Relays),
        };

        if (status == "live")
        {
            var starts = existingEvent?.Tags?.FindFirstTagValue("starts") ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            tags.Add(new ("starts", starts));
            tags.Add(
                new("current_participants",
                    (viewers.HasValue ? viewers.ToString() : null) ??
                    existingEvent?.Tags?.FindFirstTagValue("current_participants") ?? "0"));

            if (!string.IsNullOrEmpty(user.ContentWarning))
            {
                tags.Add(new("content-warning", user.ContentWarning));
            }
        }

        foreach (var tag in !string.IsNullOrEmpty(user.Tags) ?
                     user.Tags.Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) : Array.Empty<string>())
        {
            tags.Add(new("t", tag));
        }

        var ev = new NostrEvent
        {
            Kind = StreamEventKind,
            Content = "",
            CreatedAt = DateTime.Now,
            Tags = new NostrEventTags(tags)
        };

        return ev.Sign(NostrPrivateKey.FromBech32(_config.PrivateKey));
    }

    private string GetStreamUrl(User u)
    {
        var ub = new Uri(_config.DataHost, $"{u.PubKey}.m3u8");
        return ub.ToString();
    }

    private async Task<User?> GetUserFromStreamKey(string streamKey)
    {
        return await _db.Users.SingleOrDefaultAsync(a => a.StreamKey == streamKey);
    }
}
