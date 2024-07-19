using Newtonsoft.Json;

namespace NostrStreamer.ApiModel;

public enum BalanceHistoryItemType
{
    Credit,
    Debit
}

public class BalanceHistoryItem
{
    [JsonProperty("created")]
    public DateTime Created { get; init; }

    [JsonProperty("type")]
    public BalanceHistoryItemType Type { get; init; }

    [JsonProperty("amount")]
    public decimal Amount { get; init; }

    [JsonProperty("desc")]
    public string? Description { get; init; }
}