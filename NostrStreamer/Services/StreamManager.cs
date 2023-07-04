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
    private readonly ILogger<StreamManager> _logger;
    private readonly StreamerContext _db;
    private readonly Config _config;
    private readonly INostrClient _nostr;

    public StreamManager(ILogger<StreamManager> logger, StreamerContext db, Config config, INostrClient nostr)
    {
        _logger = logger;
        _db = db;
        _config = config;
        _nostr = nostr;
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

    public async Task ConsumeQuota(string streamKey, double duration)
    {
        var user = await GetUserFromStreamKey(streamKey);
        if (user == default) throw new Exception("No stream key found");

        const double rate = 21.0d;
        var cost = Math.Round(duration / 60d * rate);
        await _db.Users
            .Where(a => a.PubKey == user.PubKey)
            .ExecuteUpdateAsync(o => o.SetProperty(v => v.Balance, v => v.Balance - cost));

        _logger.LogInformation("Stream consumed {n} seconds for {pubkey} costing {cost} sats", duration, user.PubKey, cost);
        if (user.Balance <= 0)
        {
            throw new Exception("User balance empty");
        }
    }

    public async Task PatchEvent(string pubkey, string? title, string? summary, string? image)
    {
        var user = await _db.Users.SingleOrDefaultAsync(a => a.PubKey == pubkey);
        if (user == default) throw new Exception("User not found");

        user.Title = title;
        user.Summary = summary;
        user.Image = image;

        var existingEvent = user.Event != default ? JsonConvert.DeserializeObject<NostrEvent>(user.Event, NostrSerializer.Settings) : null;
        var ev = CreateStreamEvent(user, existingEvent?.Tags?.FindFirstTagValue("status") ?? "planned");
        user.Event = JsonConvert.SerializeObject(ev, NostrSerializer.Settings);

        await _db.SaveChangesAsync();

        _nostr.Send(new NostrEventRequest(ev));
    }

    private async Task PublishEvent(User user, NostrEvent ev)
    {
        await _db.Users
            .Where(a => a.PubKey == user.PubKey)
            .ExecuteUpdateAsync(o => o.SetProperty(v => v.Event, JsonConvert.SerializeObject(ev, NostrSerializer.Settings)));

        _nostr.Send(new NostrEventRequest(ev));
    }

    private NostrEvent CreateStreamEvent(User user, string state)
    {
        var tags = new List<NostrEventTag>()
        {
            new("d", user.PubKey),
            new("title", user.Title ?? ""),
            new("summary", user.Summary ?? ""),
            new("streaming", GetStreamUrl(user)),
            new("image", user.Image ?? ""),
            new("status", state),
            new("p", user.PubKey, "", "host")
        };

        foreach (var tag in user.Tags?.Split(",") ?? Array.Empty<string>())
        {
            tags.Add(new("t", tag.Trim()));
        }

        var ev = new NostrEvent
        {
            Kind = (NostrKind)30_311,
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
