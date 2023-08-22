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
    /// Test if streaming is allowed for this user, otherwise throw
    /// </summary>
    /// <exception cref="Exception">Throws if cant stream</exception>
    void TestCanStream();
    
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
    
    /// <summary>
    /// Republish stream event
    /// </summary>
    /// <returns></returns>
    Task UpdateEvent();

    /// <summary>
    /// Return a list of recordings segments
    /// </summary>
    /// <returns></returns>
    Task<List<UserStreamRecording>> GetRecordings();

    /// <summary>
    /// Return the last added recording segment
    /// </summary>
    /// <returns></returns>
    Task<UserStreamRecording?> GetLatestRecordingSegment();
}