namespace NostrStreamer.Database;

/// <summary>
/// Single use stream keys
/// </summary>
public class UserStreamKey
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string UserPubkey { get; init; } = null!;
    public User User { get; init; } = null!;

    public string Key { get; init; } = null!;

    public DateTime Created { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Expiry of the key when it can no longer be used
    /// </summary>
    public DateTime? Expires { get; init; }


    public Guid StreamId { get; init; }
    public UserStream UserStream { get; init; } = null!;
}