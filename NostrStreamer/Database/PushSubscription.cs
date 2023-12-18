using System.ComponentModel.DataAnnotations;

namespace NostrStreamer.Database;

public class PushSubscription
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTime Created { get; init; } = DateTime.UtcNow;

    public DateTime LastUsed { get; init; } = DateTime.UtcNow;

    [MaxLength(64)]
    public string Pubkey { get; init; } = null!;

    public string Endpoint { get; init; } = null!;

    public string Key { get; init; } = null!;

    public string Auth { get; init; } = null!;

    public string Scope { get; init; } = null!;
}
