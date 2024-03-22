namespace NostrStreamer.Database;

public class Payment
{
    public string PaymentHash { get; init; } = null!;
    
    public string PubKey { get; init; } = null!;
    public User User { get; init; } = null!;

    public string? Invoice { get; init; }
    
    public bool IsPaid { get; set; }
    
    /// <summary>
    /// Payment amount in sats!!
    /// </summary>
    public ulong Amount { get; init; }
    
    public DateTime Created { get; init; } = DateTime.UtcNow;
    
    public string? Nostr { get; init; }
    
    public PaymentType Type { get; init; }
}

public enum PaymentType
{
    Topup = 0,
    Zap = 1,
    Credit = 2,
    Withdrawal = 3,
}