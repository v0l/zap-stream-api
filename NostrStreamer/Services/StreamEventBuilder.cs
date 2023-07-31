using System.ComponentModel;
using Nostr.Client.Client;
using Nostr.Client.Keys;
using Nostr.Client.Messages;
using Nostr.Client.Requests;
using NostrStreamer.Database;

namespace NostrStreamer.Services;

public class StreamEventBuilder
{
    private const NostrKind StreamEventKind = (NostrKind)30_311;
    private const NostrKind StreamChatKind = (NostrKind)1311;
    private readonly Config _config;
    private readonly ViewCounter _viewCounter;
    private readonly INostrClient _nostrClient;

    public StreamEventBuilder(Config config, ViewCounter viewCounter, INostrClient nostrClient)
    {
        _config = config;
        _viewCounter = viewCounter;
        _nostrClient = nostrClient;
    }

    public NostrEvent CreateStreamEvent(User user, UserStream stream)
    {
        var status = stream.State switch
        {
            UserStreamState.Planned => "planned",
            UserStreamState.Live => "live",
            UserStreamState.Ended => "ended",
            _ => throw new InvalidEnumArgumentException()
        };

        var tags = new List<NostrEventTag>
        {
            new("d", stream.Id.ToString()),
            new("title", user.Title ?? ""),
            new("summary", user.Summary ?? ""),
            new("image", string.IsNullOrEmpty(user.Image) ? new Uri(_config.DataHost, $"{stream.Id}.jpg").ToString() : user.Image),
            new("status", status),
            new("p", user.PubKey, "", "host"),
            new("relays", _config.Relays),
        };

        if (status == "live")
        {
            var viewers = _viewCounter.Current(stream.Id);
            var starts = new DateTimeOffset(stream.Starts).ToUnixTimeSeconds();
            tags.Add(new("streaming", new Uri(_config.DataHost, $"{stream.Id}.m3u8").ToString()));
            tags.Add(new("starts", starts.ToString()));
            tags.Add(new("current_participants", viewers.ToString()));

            if (!string.IsNullOrEmpty(user.ContentWarning))
            {
                tags.Add(new("content-warning", user.ContentWarning));
            }
        }
        else if (status == "ended")
        {
            tags.Add(new("recording", new Uri(_config.DataHost, $"recording/{stream.Id}.m3u8").ToString()));
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

    public NostrEvent CreateStreamChat(UserStream stream, string message)
    {
        var pk = NostrPrivateKey.FromBech32(_config.PrivateKey);
        var ev = new NostrEvent
        {
            Kind = StreamChatKind,
            Content = message,
            CreatedAt = DateTime.Now,
            Tags = new NostrEventTags(
                new NostrEventTag("a", $"{StreamEventKind}:{pk.DerivePublicKey().Hex}:{stream.Id}")
            )
        };

        return ev.Sign(pk);
    }

    public void BroadcastEvent(NostrEvent ev)
    {
        _nostrClient.Send(new NostrEventRequest(ev));
    }
}
