using Newtonsoft.Json;

namespace NostrStreamer.ApiModel;

public enum PushMessageType
{
    StreamStarted = 1
}

public class PushMessage
{
    [JsonProperty("type")]
    public PushMessageType Type { get; init; }

    [JsonProperty("pubkey")]
    public string Pubkey { get; init; } = null!;

    [JsonProperty("name")]
    public string? Name { get; init; }
    
    [JsonProperty("avatar")]
    public string? Avatar { get; init; }
    
}
