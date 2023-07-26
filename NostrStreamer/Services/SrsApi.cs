using Newtonsoft.Json;

namespace NostrStreamer.Services;

public class SrsApi
{
    private readonly HttpClient _client;

    public SrsApi(HttpClient client, Config config)
    {
        _client = client;
        _client.BaseAddress = config.SrsApiHost;
    }

    public async Task<List<Stream>> ListStreams()
    {
        var rsp = await _client.GetFromJsonAsync<StreamsResponse>("/api/v1/streams/?count=10000");
        return rsp!.Streams;
    }

    public async Task<Stream?> GetStream(string id)
    {
        return await _client.GetFromJsonAsync<Stream>($"/api/v1/streams/{id}");
    }

    public async Task<List<Client>> ListClients()
    {
        var rsp = await _client.GetFromJsonAsync<ListClientsResponse>("/api/v1/clients/?count=10000");
        return rsp!.Clients;
    }

    public async Task<Client?> GetClient(string cid)
    {
        var rsp = await _client.GetFromJsonAsync<GetClientResponse>($"/api/v1/clients/{cid}");
        return rsp?.Client;
    }
    
    public async Task KickClient(string clientId)
    {
        await _client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/clients/{clientId}"));
    }
}

public class Audio
{
    [JsonProperty("codec")]
    public string Codec { get; set; }

    [JsonProperty("sample_rate")]
    public int? SampleRate { get; set; }

    [JsonProperty("channel")]
    public int? Channel { get; set; }

    [JsonProperty("profile")]
    public string Profile { get; set; }
}

public class Kbps
{
    [JsonProperty("recv_30s")]
    public int? Recv30s { get; set; }

    [JsonProperty("send_30s")]
    public int? Send30s { get; set; }
}

public class Publish
{
    [JsonProperty("active")]
    public bool? Active { get; set; }

    [JsonProperty("cid")]
    public string Cid { get; set; }
}

public class StreamsResponse
{
    [JsonProperty("code")]
    public int? Code { get; set; }

    [JsonProperty("server")]
    public string Server { get; set; }

    [JsonProperty("streams")]
    public List<Stream> Streams { get; set; }
}

public class Stream
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("vhost")]
    public string Vhost { get; set; }

    [JsonProperty("app")]
    public string App { get; set; }

    [JsonProperty("live_ms")]
    public object LiveMs { get; set; }

    [JsonProperty("clients")]
    public int? Clients { get; set; }

    [JsonProperty("frames")]
    public int? Frames { get; set; }

    [JsonProperty("send_bytes")]
    public int? SendBytes { get; set; }

    [JsonProperty("recv_bytes")]
    public long? RecvBytes { get; set; }

    [JsonProperty("kbps")]
    public Kbps? Kbps { get; set; }

    [JsonProperty("publish")]
    public Publish Publish { get; set; }

    [JsonProperty("video")]
    public Video? Video { get; set; }

    [JsonProperty("audio")]
    public Audio Audio { get; set; }
}

public class Video
{
    [JsonProperty("codec")]
    public string Codec { get; set; }

    [JsonProperty("profile")]
    public string Profile { get; set; }

    [JsonProperty("level")]
    public string Level { get; set; }

    [JsonProperty("width")]
    public int? Width { get; set; }

    [JsonProperty("height")]
    public int? Height { get; set; }
}

public class Client
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("vhost")]
    public string Vhost { get; set; }

    [JsonProperty("stream")]
    public string Stream { get; set; }

    [JsonProperty("ip")]
    public string Ip { get; set; }

    [JsonProperty("pageUrl")]
    public string PageUrl { get; set; }

    [JsonProperty("swfUrl")]
    public string SwfUrl { get; set; }

    [JsonProperty("tcUrl")]
    public string TcUrl { get; set; }

    [JsonProperty("url")]
    public string Url { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; }

    [JsonProperty("publish")]
    public bool Publish { get; set; }

    [JsonProperty("alive")]
    public double? Alive { get; set; }

    [JsonProperty("kbps")]
    public Kbps Kbps { get; set; }
}

public class ListClientsResponse
{
    [JsonProperty("code")]
    public int? Code { get; set; }

    [JsonProperty("server")]
    public string Server { get; set; }

    [JsonProperty("clients")]
    public List<Client> Clients { get; set; }
}

public class GetClientResponse
{
    [JsonProperty("code")]
    public int? Code { get; set; }

    [JsonProperty("server")]
    public string Server { get; set; }

    [JsonProperty("client")]
    public Client Client { get; set; }
}
