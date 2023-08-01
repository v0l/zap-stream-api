using Newtonsoft.Json;

namespace NostrStreamer.ApiModel;

public class PatchAccount
{
    [JsonProperty("accept_tos")]
    public bool AcceptTos { get; init; }
}
