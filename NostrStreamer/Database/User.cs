namespace NostrStreamer.Database;

public class User
{
    public string PubKey { get; init; } = null!;

    /// <summary>
    /// Stream key
    /// </summary>
    public string StreamKey { get; init; } = null!;

    /// <summary>
    /// Milli sats balance
    /// </summary>
    public long Balance { get; set; }

    /// <summary>
    /// Date when user accepted TOS
    /// </summary>
    public DateTime? TosAccepted { get; init; }

    /// <summary>
    /// Concurrency token
    /// </summary>
    public uint Version { get; set; }

    /// <summary>
    /// User is service admin
    /// </summary>
    public bool IsAdmin { get; set; }

    /// <summary>
    /// User is blocked and cannot stream
    /// </summary>
    public bool IsBlocked { get; set; }

    public List<Payment> Payments { get; init; } = new();
    public List<UserStream> Streams { get; init; } = new();
    public List<UserStreamForwards> Forwards { get; init; } = new();
    public List<UserStreamKey> StreamKeys { get; init; } = new();
}