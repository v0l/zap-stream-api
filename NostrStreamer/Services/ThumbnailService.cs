using System.Diagnostics;
using FFMpegCore;
using NostrStreamer.Database;

namespace NostrStreamer.Services;

public class ThumbnailService
{
    private const string Dir = "thumbs";
    private readonly Config _config;
    private readonly ILogger<ThumbnailService> _logger;

    public ThumbnailService(Config config, ILogger<ThumbnailService> logger)
    {
        _config = config;
        _logger = logger;
        if (!Directory.Exists(Dir))
        {
            Directory.CreateDirectory(Dir);
        }
    }

    public async Task GenerateThumb(UserStream stream)
    {
        var path = MapPath(stream.Id);
        try
        {
            var sw = Stopwatch.StartNew();
            var cmd = FFMpegArguments
                .FromUrlInput(new Uri(_config.RtmpHost, $"{stream.Endpoint.App}/source/{stream.User.StreamKey}?vhost=hls.zap.stream"))
                .OutputToFile(path, true, o => { o.ForceFormat("image2").WithCustomArgument("-vframes 1"); })
                .CancellableThrough(new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token);

            _logger.LogInformation("Running command {cmd}", cmd.Arguments);
            await cmd.ProcessAsynchronously();
            sw.Stop();
            _logger.LogInformation("Generated {id} thumb in {n:#,##0}ms", stream.Id, sw.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to generate {id} thumbnail {msg}", stream.Id, ex.Message);
        }
    }

    public System.IO.Stream? GetThumbnail(Guid id)
    {
        var path = MapPath(id);
        return File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.Read) : null;
    }

    private string MapPath(Guid id)
    {
        return Path.Combine(Dir, $"{id}.jpg");
    }
}
