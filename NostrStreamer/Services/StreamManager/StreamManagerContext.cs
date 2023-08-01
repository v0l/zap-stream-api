using NostrStreamer.Database;

namespace NostrStreamer.Services.StreamManager;

public class StreamManagerContext
{
    public StreamerContext Db { get; init; } = null!;
    public UserStream UserStream { get; init; } = null!;
    public User User => UserStream.User;
    public StreamInfo? StreamInfo { get; init; }
    public SrsApi EdgeApi { get; init; } = null!;
}
