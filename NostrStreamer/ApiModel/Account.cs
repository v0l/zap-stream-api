using Newtonsoft.Json;
using Nostr.Client.Messages;

namespace NostrStreamer.ApiModel;

public class Account
{
    [JsonProperty("event")]
    public NostrEvent? Event { get; init; }

    [JsonProperty("endpoints")]
    public List<AccountEndpoint> Endpoints { get; init; } = new();
    
    [JsonProperty("balance")]
    public long Balance { get; init; }
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

[Obsolete("Use EndpointCost")]
public class AccountQuota
{
    [JsonProperty("rate")]
    public double Rate { get; init; }

    [JsonProperty("unit")]
    public string Unit { get; init; } = null!;

    [JsonProperty("remaining")]
    public long Remaining { get; init; }
}