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

    public S3BlobConfig S3Store { get; init; } = null!;
    
    public DateTime TosDate { get; init; }

    public string GeoIpDatabase { get; init; } = null!;
    
    public List<EdgeLocation> Edges { get; init; } = new();

    public TwitchApi Twitch { get; init; } = null!;

    public string DataProtectionKeyPath { get; init; } = null!;

    public VapidKeyDetails VapidKey { get; init; } = null!;

    public string Redis { get; init; } = null!;

    public Uri SnortApi { get; init; } = null!;
    
    public Uri? DiscordLiveWebhook { get; init; }
}

public class VapidKeyDetails
{
    public string PublicKey { get; init; } = null!;
    public string PrivateKey { get; init; } = null!;
}

public class TwitchApi
{
    public string ClientId { get; init; } = null!;
    public string ClientSecret { get; init; } = null!;
}

public class LndConfig
{
    public Uri Endpoint { get; init; } = null!;

    public string CertPath { get; init; } = null!;

    public string MacaroonPath { get; init; } = null!;
}

public sealed class S3BlobConfig
{
    public string Name { get; init; } = null!;
    public string AccessKey { get; init; } = null!;
    public string SecretKey { get; init; } = null!;
    public Uri ServiceUrl { get; init; } = null!;
    public string? Region { get; init; }
    public string BucketName { get; init; } = "zap-stream-dvr";
    public bool DisablePayloadSigning { get; init; }
    public Uri PublicHost { get; init; } = null!;
}

public sealed class EdgeLocation
{
    public string Name { get; init; } = null!;
    public Uri Url { get; init; } = null!;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
}