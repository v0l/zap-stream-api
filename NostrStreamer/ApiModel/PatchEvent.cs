using Newtonsoft.Json;

namespace NostrStreamer.ApiModel;

public class PatchEvent
{
    [JsonProperty("id")]
    public Guid? Id { get; init; }
    
    [JsonProperty("title")]
    public string? Title { get; init; }
    
    [JsonProperty("summary")]
    public string? Summary { get; init; }
    
    [JsonProperty("image")]
    public string? Image { get; init; }

    [JsonProperty("tags")]
    public string[]? Tags { get; init; } = [];
    
    [JsonProperty("content_warning")]
    public string? ContentWarning { get; init; }
    
    [JsonProperty("goal")]
    public string? Goal { get; init; }
}
