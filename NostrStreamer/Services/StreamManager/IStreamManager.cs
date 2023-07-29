using NostrStreamer.Database;

namespace NostrStreamer.Services.StreamManager;

public interface IStreamManager
{
    /// <summary>
    /// Return the current stream
    /// </summary>
    /// <returns></returns>
    UserStream GetStream();
    
    /// <summary>
    /// Stream ingress check on srs-edge
    /// </summary>
    /// <returns>List of forward URLs</returns>
    Task<List<string>> OnForward();
    
    /// <summary>
    /// Stream started at origin for HLS split
    /// </summary>
    /// <returns></returns>
    Task StreamStarted();
    
    /// <summary>
    /// Stream stopped
    /// </summary>
    /// <returns></returns>
    Task StreamStopped();
    
    /// <summary>
    /// Stream reap HLS
    /// </summary>
    /// <param name="duration"></param>
    /// <returns></returns>
    Task ConsumeQuota(double duration);

    /// <summary>
    /// Update stream details
    /// </summary>
    /// <param name="title"></param>
    /// <param name="summary"></param>
    /// <param name="image"></param>
    /// <param name="tags"></param>
    /// <param name="contentWarning"></param>
    /// <returns></returns>
    Task PatchEvent(string? title, string? summary, string? image, string[]? tags, string? contentWarning);

    /// <summary>
    /// Update viewer count
    /// </summary>
    public Task UpdateViewers();
    
    /// <summary>
    /// Add a guest to the stream
    /// </summary>
    /// <param name="pubkey"></param>
    /// <param name="role"></param>
    /// <param name="zapSplit"></param>
    /// <returns></returns>
    Task AddGuest(string pubkey, string role, decimal zapSplit);

    /// <summary>
    /// Remove guest from the stream
    /// </summary>
    /// <param name="pubkey"></param>
    /// <returns></returns>
    Task RemoveGuest(string pubkey);

    /// <summary>
    /// When a new DVR segment is available
    /// </summary>
    /// <param name="segment"></param>
    /// <returns></returns>
    Task OnDvr(Uri segment);
}