using Newtonsoft.Json;

namespace NostrStreamer.ApiModel;

public class Account
{
    [JsonProperty("event")]
    public PatchEvent? Event { get; init; }

    [JsonProperty("endpoints")]
    public List<AccountEndpoint> Endpoints { get; init; } = new();
    
    [JsonProperty("balance")]
    public long Balance { get; init; }

    [JsonProperty("tos")]
    public AccountTos Tos { get; init; } = null!;

    [JsonProperty("forwards")]
    public List<ForwardDest> Forwards { get; init; } = new();
}

public class AccountEndpoint
{
    [JsonProperty("name")]
    public string Name { get; init; } = null!;
    
    [JsonProperty("url")]
    public string Url { get; init; } = null!;
    
    [JsonProperty("key")]
    public string Key { get; init; } = null!;

    [JsonProperty("cost")]
    public EndpointCost Cost { get; init; } = null!;

    [JsonProperty("capabilities")]
    public List<string> Capabilities { get; init; } = new();
}

public class EndpointCost
{
    [JsonProperty("rate")]
    public double Rate { get; init; }

    [JsonProperty("unit")]
    public string Unit { get; init; } = null!;
}

public class AccountTos
{
    [JsonProperty("accepted")]
    public bool Accepted { get; init; }

    [JsonProperty("link")]
    public Uri Link { get; init; } = null!;
}

public class ForwardDest
{
    [JsonProperty("id")]
    public Guid Id { get;init; }
    
    [JsonProperty("name")]
    public string Name { get; init; } = null!;
}