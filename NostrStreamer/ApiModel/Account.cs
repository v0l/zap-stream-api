using Newtonsoft.Json;

namespace NostrStreamer.ApiModel;

public class Account
{
    [JsonProperty("url")]
    public string Url { get; init; } = null!;

    [JsonProperty("key")]
    public string Key { get; init; } = null!;
}
