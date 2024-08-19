namespace NostrStreamer.Database;

public class Payment
{
    public string PaymentHash { get; init; } = null!;
    
    public string PubKey { get; init; } = null!;
    public User User { get; init; } = null!;

    public string? Invoice { get; init; }
    
    public bool IsPaid { get; set; }
    
    /// <summary>
    /// Payment amount in milli-sats!!
    /// </summary>
    public ulong Amount { get; init; }
    
    public DateTime Created { get; init; } = DateTime.UtcNow;
    
    public string? Nostr { get; init; }
    
    public PaymentType Type { get; init; }
    
    /// <summary>
    /// Fee paid for withdrawal in milli-sats
    /// </summary>
    public ulong Fee { get; init; }
}

public enum PaymentType
{
    TopUp = 0,
    Zap = 1,
    Credit = 2,
    Withdrawal = 3,
    AdmissionFee = 4,
}