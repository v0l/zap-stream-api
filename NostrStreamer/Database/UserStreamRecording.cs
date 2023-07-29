namespace NostrStreamer.Database;

public class UserStreamRecording
{
    public Guid Id { get; init; } = Guid.NewGuid();
    
    public Guid UserStreamId { get; init; }
    public UserStream Stream { get; init; } = null!;

    public string Url { get; init; } = null!;
    
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    
    public double Duration { get; init; }
}
