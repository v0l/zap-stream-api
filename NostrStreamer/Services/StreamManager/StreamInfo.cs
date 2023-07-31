namespace NostrStreamer.Services.StreamManager;

public class StreamInfo
{
    public string App { get; init; } = null!;

    public string Variant { get; init; } = null!;

    public string StreamKey { get; init; } = null!;

    public string ClientId { get; init; } = null!;

    public string StreamId { get; init; } = null!;

    public string EdgeIp { get; init; } = null!;
}
