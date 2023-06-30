namespace NostrStreamer;

public class Config
{
    /// <summary>
    /// Ingest URL
    /// </summary>
    public Uri RtmpHost { get; init; } = null!;
    
    /// <summary>
    /// SRS app name
    /// </summary>
    public string App { get; init; } = "live";

    /// <summary>
    /// SRS api server host
    /// </summary>
    public Uri SrsApiHost { get; init; } = null!;
    
    /// <summary>
    /// SRS Http server host
    /// </summary>
    public Uri SrsHttpHost { get; init; } = null!;

    /// <summary>
    /// Public host where playlists are located
    /// </summary>
    public Uri DataHost { get; init; } = null!;

    public string PrivateKey { get; init; } = null!;
    public string[] Relays { get; init; } = Array.Empty<string>();
}