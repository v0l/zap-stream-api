namespace NostrStreamer.Database;

public class UserStreamGuest
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid StreamId { get; init; }
    public UserStream Stream { get; init; } = null!;
    
    public string PubKey { get; init; } = null!;
    
    public string? Relay { get; init; }
    
    public string? Role { get; init; }
    
    public string? Sig { get; init; }
    
    public decimal ZapSplit { get; init; }
}
