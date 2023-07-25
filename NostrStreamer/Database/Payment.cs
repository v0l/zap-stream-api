namespace NostrStreamer.Database;

public class Payment
{
    public string PaymentHash { get; init; } = null!;
    
    public string PubKey { get; init; } = null!;
    public User User { get; init; } = null!;

    public string Invoice { get; init; } = null!;
    
    public bool IsPaid { get; set; }
    
    public ulong Amount { get; init; }
    
    public DateTime Created { get; init; } = DateTime.UtcNow;
    
    public string? Nostr { get; init; }
    
    public PaymentType Type { get; init; }
}

public enum PaymentType
{
    Topup = 0,
    Zap = 1
}