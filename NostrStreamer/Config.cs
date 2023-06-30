namespace NostrStreamer;

public class Config
{
    public Uri SrsPublicHost { get; init; } = null!;
    public string App { get; init; } = null!;

    public Uri SrsApi { get; init; } = null!;

    public string PrivateKey { get; init; } = null!;
    public string[] Relays { get; init; } = Array.Empty<string>();
}