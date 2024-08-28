namespace NostrStreamer.ApiModel;

public class CreateStreamKeyRequest
{
    public PatchEvent Event { get; init; } = null!;
    
    public DateTime? Expires { get; init; }
}