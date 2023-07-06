using Newtonsoft.Json;
using Nostr.Client.Json;
using Nostr.Client.Messages;
using NostrStreamer.Database;

namespace NostrStreamer;

public static class Extensions
{
    public static NostrEvent? GetNostrEvent(this User user)
    {
        return user.Event != default ? JsonConvert.DeserializeObject<NostrEvent>(user.Event, NostrSerializer.Settings) : null;
    }
}
