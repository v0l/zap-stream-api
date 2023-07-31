using NostrStreamer.Database;

namespace NostrStreamer.Services.Dvr;

public interface IDvrStore
{
    /// <summary>
    /// Upload a DVR recording to storage and return the URL
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="source"></param>
    /// <returns></returns>
    Task<UploadResult> UploadRecording(UserStream stream, Uri source);
}

public record UploadResult(Uri Result, double Duration);
