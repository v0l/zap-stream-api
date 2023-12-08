namespace NostrStreamer.Database;

public class UserStreamClip
{
    public Guid Id { get; init; } = Guid.NewGuid();
    
    public Guid UserStreamId { get; init; }
    public UserStream UserStream { get; init; } = null!;
    
    public DateTime Created { get; init; } = DateTime.UtcNow;

    public string TakenByPubkey { get; init; } = null!;

    public string Url { get; init; } = null!;
}
