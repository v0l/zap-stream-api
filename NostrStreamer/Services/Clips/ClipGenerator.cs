using FFMpegCore;
using NostrStreamer.Database;

namespace NostrStreamer.Services.Clips;

public class ClipGenerator
{
    private readonly ILogger<ClipGenerator> _logger;
    private readonly Config _config;

    public ClipGenerator(ILogger<ClipGenerator> logger, Config config)
    {
        _logger = logger;
        _config = config;
    }

    public async Task<string> GenerateClip(UserStream stream)
    {
        const int clipLength = 20;
        var path = Path.ChangeExtension(Path.GetTempFileName(), ".mp4");
        var cmd = FFMpegArguments
            .FromUrlInput(new Uri(_config.DataHost, $"stream/{stream.Id}.m3u8"),
                inOpt =>
                {
                    inOpt.WithCustomArgument($"-ss -{clipLength}");
                })
            .OutputToFile(path, true, o => { o.WithDuration(TimeSpan.FromSeconds(clipLength)); })
            .CancellableThrough(new CancellationTokenSource(TimeSpan.FromSeconds(60)).Token);
        
        _logger.LogInformation("Running command {cmd}", cmd.Arguments);
        await cmd.ProcessAsynchronously();
        return path;
    }
}
