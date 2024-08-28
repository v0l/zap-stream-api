using FFMpegCore;
using NostrStreamer.Database;

namespace NostrStreamer.Services.Thumbnail;

public abstract class BaseThumbnailService
{
    protected readonly ILogger Logger;
    protected readonly Config Config;

    protected BaseThumbnailService(Config config, ILogger logger)
    {
        Config = config;
        Logger = logger;
    }

    protected async Task<string?> GenerateThumbnail(UserStream stream)
    {
        if (stream.Endpoint == default) return default;
        var path = Path.ChangeExtension(Path.GetTempFileName(), ".jpg");
        var cmd = FFMpegArguments
            .FromUrlInput(new Uri(Config.RtmpHost,
                $"{stream.Endpoint.App}/source/{stream.User.StreamKey}?vhost=hls.zap.stream"))
            .OutputToFile(path, true, o => { o.ForceFormat("image2").WithCustomArgument("-vframes 1"); })
            .CancellableThrough(new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token);

        Logger.LogInformation("Running command {cmd}", cmd.Arguments);
        await cmd.ProcessAsynchronously();
        return path;
    }
}