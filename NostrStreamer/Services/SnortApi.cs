using Newtonsoft.Json;

namespace NostrStreamer.Services;

public class SnortApi
{
    private readonly HttpClient _client;

    public SnortApi(HttpClient client, Config config)
    {
        _client = client;
        _client.BaseAddress = config.SnortApi;
        _client.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<SnortProfile?> Profile(string pubkey)
    {
        var json = await _client.GetStringAsync($"/api/v1/raw/p/{pubkey}");
        if (!string.IsNullOrEmpty(json))
        {
            return JsonConvert.DeserializeObject<SnortProfile>(json);
        }

        return default;
    }
}

public class SnortProfile
{
    [JsonProperty("pubKey")]
    public string PubKey { get; init; } = null!;

    [JsonProperty("name")]
    public string? Name { get; init; }

    [JsonProperty("about")]
    public string? About { get; init; }

    [JsonProperty("picture")]
    public string? Picture { get; init; }

    [JsonProperty("nip05")]
    public string? Nip05 { get; init; }

    [JsonProperty("lud16")]
    public string? Lud16 { get; init; }

    [JsonProperty("banner")]
    public string? Banner { get; init; }
}
