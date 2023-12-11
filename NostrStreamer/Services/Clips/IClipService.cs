namespace NostrStreamer.Services.Clips;

public interface IClipService
{
    Task<List<ClipSegment>?> PrepareClip(Guid streamId);

    Task<ClipResult?> MakeClip(string takenBy, List<ClipSegment> segments, float start, float length);
}

public record ClipResult(Uri Url);

public record ClipSegment(Guid Id, int Index)
{
    public string GetPath()
    {
        return Path.Join(Path.GetTempPath(), Id.ToString(), $"{Index}.ts");
    }
}