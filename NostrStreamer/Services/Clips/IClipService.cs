namespace NostrStreamer.Services.Clips;

public interface IClipService
{
    Task<ClipResult?> CreateClip(Guid streamId, string takenBy);
}

public record ClipResult(Uri Url);
