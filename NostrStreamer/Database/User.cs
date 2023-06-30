namespace NostrStreamer.Database;

public class User
{
    public string PubKey { get; init; } = null!;
    
    /// <summary>
    /// Stream key
    /// </summary>
    public string StreamKey { get; init; } = null!;

    /// <summary>
    /// Most recent nostr event published 
    /// </summary>
    public string? Event { get; init; }
    
    /// <summary>
    /// Sats balance
    /// </summary>
    public long Balance { get; init; }
    
    /// <summary>
    /// Stream title
    /// </summary>
    public string? Title { get; init; }
    
    /// <summary>
    /// Stream summary
    /// </summary>
    public string? Summary { get; init; }
    
    /// <summary>
    /// Stream cover image
    /// </summary>
    public string? Image { get; init; }
    
    /// <summary>
    /// Comma seperated tags
    /// </summary>
    public string? Tags { get; init; }
}
