namespace NostrStreamer.Database;

public class UserStream
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string PubKey { get; init; } = null!;
    public User User { get; init; } = null!;

    public string StreamId { get; init; } = null!;
    
    public DateTime Starts { get; init; } = DateTime.UtcNow;
    
    public DateTime? Ends { get; set; }
    
    public UserStreamState State { get; set; }
    
    /// <summary>
    /// Nostr Event for this stream
    /// </summary>
    public string Event { get; set; } = null!;
    
    /// <summary>
    /// Recording URL of ended stream
    /// </summary>
    public string? Recording { get; set; }

    public Guid EndpointId { get; init; }
    public IngestEndpoint Endpoint { get; init; } = null!;
    
    public List<UserStreamGuest> Guests { get; init; } = new();
    public List<UserStreamRecording> Recordings { get; init; } = new();
}

public enum UserStreamState
{
    Planned = 1,
    Live = 2,
    Ended = 3
}