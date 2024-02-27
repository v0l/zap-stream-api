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
            new("image", stream.Thumbnail ?? user.Image ?? ""),
            new("status", status),
            new("p", user.PubKey, "", "host"),
            new("relays", _config.Relays),
            new("starts", new DateTimeOffset(stream.Starts).ToUnixTimeSeconds().ToString()),
            new("service", new Uri(_config.ApiHost, "/api/nostr").ToString())
        };

        if (status == "live")
        {
            var viewers = _viewCounter.Current(stream.Id);
            tags.Add(new("streaming", new Uri(_config.DataHost, $"stream/{stream.Id}.m3u8").ToString()));
            tags.Add(new("current_participants", viewers.ToString()));

            if (!string.IsNullOrEmpty(user.ContentWarning))
            {
                tags.Add(new("content-warning", user.ContentWarning));
            }
        }
        else if (status == "ended")
        {
            tags.Add(new("recording", new Uri(_config.DataHost, $"recording/{stream.Id}.m3u8").ToString()));
            if (stream.Ends.HasValue)
            {
                tags.Add(new("ends", new DateTimeOffset(stream.Ends.Value).ToUnixTimeSeconds().ToString()));
            }
        }

        foreach (var tag in user.SplitTags())
        {
            tags.Add(new("t", tag));
        }

        if (!string.IsNullOrEmpty(user.Goal))
        {
            tags.Add(new("goal", user.Goal));
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
                new NostrEventTag("a", $"{(int)StreamEventKind}:{pk.DerivePublicKey().Hex}:{stream.Id}")
            )
        };

        return ev.Sign(pk);
    }

    public NostrEvent CreateDm(UserStream stream, string message)
    {
        var pk = NostrPrivateKey.FromBech32(_config.PrivateKey);
        var ev = new NostrEvent
        {
            Kind = NostrKind.EncryptedDm,
            Content = message,
            CreatedAt = DateTime.Now,
            Tags = new NostrEventTags(
                new NostrEventTag("p", stream.PubKey)
            )
        };

        return ev.EncryptDirect(pk, NostrPublicKey.FromHex(stream.PubKey)).Sign(pk);
    }

    public void BroadcastEvent(NostrEvent ev)
    {
        _nostrClient.Send(new NostrEventRequest(ev));
    }
}