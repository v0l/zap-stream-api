using Newtonsoft.Json;

namespace NostrStreamer.ApiModel;

public class PatchEvent
{
    [JsonProperty("title")]
    public string Title { get; init; } = null!;
    
    [JsonProperty("summary")]
    public string Summary { get; init; } = null!;
    
    [JsonProperty("image")]
    public string Image { get; init; } = null!;

    [JsonProperty("tags")]
    public string[] Tags { get; init; } = Array.Empty<string>();
}
