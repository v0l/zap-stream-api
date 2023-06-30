namespace NostrStreamer.Database;

public class Payment
{
    public string PubKey { get; init; } = null!;

    public string Invoice { get; init; } = null!;
    
    public bool IsPaid { get; init; }
}
