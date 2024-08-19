namespace NostrStreamer.Database;

public class StreamTickets
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid UserStreamId { get; init; }
    public UserStream UserStream { get; init; } = null!;

    public DateTime Created { get; init; } = DateTime.UtcNow;

    public Guid Token { get; init; } = Guid.NewGuid();
}