
using Newtonsoft.Json;

namespace NostrStreamer.ApiModel;

public class MakeClipReq
{
    [JsonProperty("segments")]
    public List<int> Segments { get; init; } = null!;
    
    [JsonProperty("start")]
    public float Start { get; init; }
    
    [JsonProperty("length")]
    public float Length { get; init; }
}
