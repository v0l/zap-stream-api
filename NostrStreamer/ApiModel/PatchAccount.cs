using Newtonsoft.Json;

namespace NostrStreamer.ApiModel;

public class PatchAccount
{
    [JsonProperty("accept_tos")]
    public bool? AcceptTos { get; init; }

    [JsonProperty("blocked")]
    public bool? Blocked { get; init; }

    [JsonProperty("admin")]
    public bool? Admin { get; init; }
}
