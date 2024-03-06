using Newtonsoft.Json;

namespace NostrStreamer.ApiModel;

public class GameInfo
{
    [JsonProperty("id")]
    public string Id { get; init; } = null!;
    
    [JsonProperty("name")]
    public string Name { get; init; } = null!;
    
    [JsonProperty("cover")]
    public string Cover { get; init; } = null!;

    [JsonProperty("genres")]
    public List<string> Genres { get; init; } = new();
}
