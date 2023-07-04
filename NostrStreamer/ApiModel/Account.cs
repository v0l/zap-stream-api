using Newtonsoft.Json;
using Nostr.Client.Messages;

namespace NostrStreamer.ApiModel;

public class Account
{
    [JsonProperty("url")]
    public string Url { get; init; } = null!;

    [JsonProperty("key")]
    public string Key { get; init; } = null!;

    [JsonProperty("event")]
    public NostrEvent? Event { get; init; }

    [JsonProperty("quota")]
    public AccountQuota Quota { get; init; } = null!;
}


public class AccountQuota
{
    [JsonProperty("rate")]
    public double Rate { get; init; }

    [JsonProperty("unit")]
    public string Unit { get; init; } = null!;

    [JsonProperty("remaining")]
    public long Remaining { get; init; }
}