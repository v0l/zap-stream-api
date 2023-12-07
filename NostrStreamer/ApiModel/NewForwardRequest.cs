using Newtonsoft.Json;

namespace NostrStreamer.ApiModel;

public class NewForwardRequest
{
    [JsonProperty("name")]
    public string Name { get; init; } = null!;

    [JsonProperty("target")]
    public string Target { get; init; } = null!;
}
