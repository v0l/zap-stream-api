namespace NostrStreamer;

public class Config
{
    /// <summary>
    /// Bitcoin network
    /// </summary>
    public string Network { get; init; } = "mainnet";

    /// <summary>
    /// Ingest URL
    /// </summary>
    public Uri RtmpHost { get; init; } = null!;

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

    /// <summary>
    /// Public URL for the api
    /// </summary>
    public Uri ApiHost { get; init; } = null!;

    public string PrivateKey { get; init; } = null!;
    public string[] Relays { get; init; } = Array.Empty<string>();

    public LndConfig Lnd { get; init; } = null!;
}

public class LndConfig
{
    public Uri Endpoint { get; init; } = null!;

    public string CertPath { get; init; } = null!;

    public string MacaroonPath { get; init; } = null!;
}
