namespace NostrStreamer.Services.Clips;

public interface IClipService
{
    Task<TempClip?> PrepareClip(Guid streamId);

    Task<ClipResult?> MakeClip(string takenBy, Guid streamId, Guid clipId, float start, float length);
}

public record ClipResult(Uri Url, float Length);

public record ClipSegment(Guid Id, int Index, float Length)
{
    public string GetPath()
    {
        return Path.Join(Path.GetTempPath(), Id.ToString(), $"{Index}.ts");
    }
}

public record TempClip(Guid StreamId, Guid Id, float Length)
{
    public string GetPath()
    {
        return Path.Join(Path.GetTempPath(), $"{Id}.mp4");
    }
}
