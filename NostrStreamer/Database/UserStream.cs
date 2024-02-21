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
    /// URL of auto-generated thumbnail
    /// </summary>
    public string? Thumbnail { get; set; }

    public Guid EndpointId { get; init; }
    public IngestEndpoint Endpoint { get; init; } = null!;

    /// <summary>
    /// Publisher edge IP
    /// </summary>
    public string EdgeIp { get; set; } = null!;
    
    /// <summary>
    /// Publisher edge client id
    /// </summary>
    public string ForwardClientId { get; set; } = null!;

    public DateTime LastSegment { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Total sats charged during this stream
    /// </summary>
    public decimal MilliSatsCollected { get; set; }
    
    /// <summary>
    /// Total seconds produced in HLS segments
    /// </summary>
    public decimal Length { get; set; }
    
    public List<UserStreamGuest> Guests { get; init; } = new();
}

public enum UserStreamState
{
    Planned = 1,
    Live = 2,
    Ended = 3
}