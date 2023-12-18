using System.ComponentModel.DataAnnotations;

namespace NostrStreamer.Database;

public class PushSubscriptionTarget
{
    public Guid Id { get; init; } = Guid.NewGuid();

    [MaxLength(64)]
    public string SubscriberPubkey { get; init; } = null!;

    [MaxLength(64)]
    public string TargetPubkey { get; init; } = null!;
}
