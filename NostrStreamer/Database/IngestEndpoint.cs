namespace NostrStreamer.Database;

public class IngestEndpoint
{
    public Guid Id { get; init; } = Guid.NewGuid();
    
    public string Name { get; init; } = null!;
    
    /// <summary>
    /// Stream app name at ingest
    /// </summary>
    public string App { get; init; } = null!;

    /// <summary>
    /// Forward to VHost
    /// </summary>
    public string Forward { get; init; } = null!;
    
    /// <summary>
    /// Cost/min (milli-sats)
    /// </summary>
    public int Cost { get; init; } = 10_000;

    /// <summary>
    /// Stream capability tags
    /// </summary>
    public List<string> Capabilities { get; init; } = new();
}
