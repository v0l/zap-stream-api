using NostrStreamer.Database;

namespace NostrStreamer.Services.Thumbnail;

public interface IThumbnailService
{
    Task GenerateThumb(UserStream stream);
}
