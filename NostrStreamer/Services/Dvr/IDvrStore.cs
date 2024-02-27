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

    /// <summary>
    /// Delete all recordings from the storage by stream
    /// </summary>
    /// <param name="stream"></param>
    /// <returns>List of deleted recordings</returns>
    Task<List<Guid>> DeleteRecordings(UserStream stream);
}

public record UploadResult(Uri Result, double Duration);
