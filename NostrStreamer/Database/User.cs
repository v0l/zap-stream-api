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
    public string? Event { get; set; }
    
    /// <summary>
    /// Sats balance
    /// </summary>
    public long Balance { get; set; }
    
    /// <summary>
    /// Stream title
    /// </summary>
    public string? Title { get; set; }
    
    /// <summary>
    /// Stream summary
    /// </summary>
    public string? Summary { get; set; }
    
    /// <summary>
    /// Stream cover image
    /// </summary>
    public string? Image { get; set; }
    
    /// <summary>
    /// Comma seperated tags
    /// </summary>
    public string? Tags { get; set; }
}
