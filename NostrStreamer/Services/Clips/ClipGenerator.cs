using System.Text.RegularExpressions;
using FFMpegCore;
using NostrStreamer.Database;

namespace NostrStreamer.Services.Clips;

public class ClipGenerator
{
    private readonly ILogger<ClipGenerator> _logger;
    private readonly Config _config;
    private readonly HttpClient _client;

    public ClipGenerator(ILogger<ClipGenerator> logger, Config config, HttpClient client)
    {
        _logger = logger;
        _config = config;
        _client = client;
    }

    public async Task<string> CreateClipFromSegments(List<ClipSegment> segments, float start, float length)
    {
        var path = Path.ChangeExtension(Path.GetTempFileName(), ".mp4");
        var cmd = FFMpegArguments
            .FromConcatInput(segments.Select(a => a.GetPath()),
                inOpt => { inOpt.Seek(TimeSpan.FromSeconds(start)); })
            .OutputToFile(path, true, o => { o.WithDuration(TimeSpan.FromSeconds(length)); })
            .CancellableThrough(new CancellationTokenSource(TimeSpan.FromSeconds(60)).Token);

        _logger.LogInformation("Running command {cmd}", cmd.Arguments);
        await cmd.ProcessAsynchronously();
        return path;
    }

    public async Task<List<ClipSegment>> GetClipSegments(UserStream stream)
    {
        var ret = new List<ClipSegment>();
        var path = $"/{stream.Endpoint.App}/source/{stream.User.StreamKey}.m3u8";
        var ub = new Uri(_config.SrsHttpHost, path);

        var rsp = await _client.GetStreamAsync(ub);
        using var sr = new StreamReader(rsp);
        while (await sr.ReadLineAsync() is { } line)
        {
            if (line.StartsWith("#EXTINF"))
            {
                var trackPath = await sr.ReadLineAsync();
                var seg = Regex.Match(trackPath!, @"-(\d+)\.ts");
                var idx = int.Parse(seg.Groups[1].Value);
                var clipSeg = new ClipSegment(stream.Id, idx);
                var outPath = clipSeg.GetPath();
                if (!File.Exists(outPath))
                {
                    var segStream = await _client.GetStreamAsync(new Uri(_config.DataHost, trackPath));
                    await using var fsOut = new FileStream(outPath, FileMode.Create, FileAccess.ReadWrite);
                    await segStream.CopyToAsync(fsOut);
                }

                ret.Add(clipSeg);
            }
        }

        return ret;
    }
}
