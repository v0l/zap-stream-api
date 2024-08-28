namespace NostrStreamer.Database;

public class UserStream
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string PubKey { get; init; } = null!;
    public User User { get; init; } = null!;

    public DateTime Starts { get; init; } = DateTime.UtcNow;

    public DateTime? Ends { get; set; }

    public UserStreamState State { get; set; }

    /// <summary>
    /// Stream title
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Stream summary
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Stream cover image
    /// </summary>
    public string? Image { get; set; }

    /// <summary>
    /// Comma seperated tags
    /// </summary>
    public string? Tags { get; set; }

    /// <summary>
    /// Any content warning tag (NIP-36)
    /// </summary>
    public string? ContentWarning { get; set; }

    /// <summary>
    /// Stream goal
    /// </summary>
    public string? Goal { get; set; }
    
    /// <summary>
    /// Nostr Event for this stream
    /// </summary>
    public string Event { get; set; } = null!;

    /// <summary>
    /// URL of auto-generated thumbnail
    /// </summary>
    public string? Thumbnail { get; set; }

    public Guid? EndpointId { get; set; }
    public IngestEndpoint? Endpoint { get; init; } = null!;

    /// <summary>
    /// Publisher edge IP
    /// </summary>
    public string? EdgeIp { get; set; }

    /// <summary>
    /// Publisher edge client id
    /// </summary>
    public string? ForwardClientId { get; set; }

    public DateTime? LastSegment { get; set; }

    /// <summary>
    /// Total sats charged during this stream
    /// </summary>
    public decimal MilliSatsCollected { get; set; }

    /// <summary>
    /// Total seconds produced in HLS segments
    /// </summary>
    public decimal Length { get; set; }

    /// <summary>
    /// Cost to view stream, tickets in <see cref="StreamTickets"/>
    /// </summary>
    public decimal? AdmissionCost { get; set; }

    public List<UserStreamGuest> Guests { get; init; } = new();

    public List<UserStreamRecording> Recordings { get; init; } = new();

    public UserStreamKey? StreamKey { get; init; }

    public string Key => StreamKey?.Key ?? User.StreamKey;
}

public enum UserStreamState
{
    Unknown = 0,
    Planned = 1,
    Live = 2,
    Ended = 3
}