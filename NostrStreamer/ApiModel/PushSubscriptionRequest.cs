using Newtonsoft.Json;

namespace NostrStreamer.ApiModel;

public class PushSubscriptionRequest
{
    [JsonProperty("endpoint")]
    public string Endpoint { get; init; } = null!;
    
    [JsonProperty("auth")]
    public string Auth { get; init; } = null!;
    
    [JsonProperty("key")]
    public string Key { get; init; } = null!;
    
    [JsonProperty("scope")]
    public string Scope { get; init; } = null!;
}
