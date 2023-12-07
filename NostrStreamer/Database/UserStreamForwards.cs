namespace NostrStreamer.Database;

public class UserStreamForwards
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string UserPubkey { get; init; } = null!;
    public User User { get; init; } = null!;

    public string Name { get; init; } = null!;
    
    public string Target { get; init; } = null!;
}
