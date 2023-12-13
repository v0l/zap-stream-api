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

    public async Task<string> CreateClipFromSegments(List<ClipSegment> segments)
    {
        var path = Path.ChangeExtension(Path.GetTempFileName(), ".mp4");
        var cmd = FFMpegArguments
            .FromConcatInput(segments.Select(a => a.GetPath()))
            .OutputToFile(path)
            .CancellableThrough(new CancellationTokenSource(TimeSpan.FromSeconds(60)).Token);

        _logger.LogInformation("Running command {cmd}", cmd.Arguments);
        await cmd.ProcessAsynchronously();
        return path;
    }

    public async Task<string> SliceTempClip(TempClip tempClip, float start, float length)
    {
        var path = Path.ChangeExtension(Path.GetTempFileName(), ".mp4");
        var cmd = FFMpegArguments
            .FromFileInput(tempClip.GetPath(), true,
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
        var ctx = await GetHlsCtx(stream);
        var path = $"/{stream.Endpoint.App}/source/{stream.User.StreamKey}.m3u8?hls_ctx={ctx}";
        var ub = new Uri(_config.SrsHttpHost, path);

        var rsp = await _client.GetStreamAsync(ub);
        using var sr = new StreamReader(rsp);
        while (await sr.ReadLineAsync() is { } line)
        {
            if (line.StartsWith("#EXTINF"))
            {
                var trackPath = await sr.ReadLineAsync();
                var segLen = Regex.Match(line, @"#EXTINF:([\d\.]+)");
                var seg = Regex.Match(trackPath!, @"-(\d+)\.ts");
                var idx = int.Parse(seg.Groups[1].Value);
                var len = float.Parse(segLen.Groups[1].Value);
                var clipSeg = new ClipSegment(stream.Id, idx, len);
                var outPath = clipSeg.GetPath();
                var outDir = Path.GetDirectoryName(outPath);
                if (!Directory.Exists(outDir))
                {
                    Directory.CreateDirectory(outDir!);
                }

                if (!File.Exists(outPath))
                {
                    var segStream = await _client.GetStreamAsync(new Uri(_config.SrsHttpHost,
                        $"/{stream.Endpoint.App}/source/{stream.User.StreamKey}-{idx}.ts"));

                    await using var fsOut = new FileStream(outPath, FileMode.Create, FileAccess.ReadWrite);

                    await segStream.CopyToAsync(fsOut);
                }

                ret.Add(clipSeg);
            }
        }

        return ret;
    }

    private async Task<string?> GetHlsCtx(UserStream stream)
    {
        var path = $"/{stream.Endpoint.App}/source/{stream.User.StreamKey}.m3u8";
        var ub = new Uri(_config.SrsHttpHost, path);
        var req = new HttpRequestMessage(HttpMethod.Get, ub);
        using var rsp = await _client.SendAsync(req);
        if (!rsp.IsSuccessStatusCode)
        {
            return default;
        }

        using var sr = new StreamReader(await rsp.Content.ReadAsStreamAsync());
        while (await sr.ReadLineAsync() is { } line)
        {
            if (line.StartsWith("#EXT-X-STREAM-INF"))
            {
                var trackLine = await sr.ReadLineAsync();
                var rx = new Regex("\\?hls_ctx=(\\w+)$");
                var match = rx.Match(trackLine!);
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }
        }

        return default;
    }
}
