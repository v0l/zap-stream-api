using System.Text.RegularExpressions;
using MediaFormatLibrary.MP4;
using Microsoft.AspNetCore.Mvc;
using NostrStreamer.Database;
using NostrStreamer.Services;
using NostrStreamer.Services.StreamManager;

namespace NostrStreamer.Controllers;

[Route("/api/playlist")]
public class PlaylistController : Controller
{
    private readonly ILogger<PlaylistController> _logger;
    private readonly Config _config;
    private readonly HttpClient _client;
    private readonly SrsApi _srsApi;
    private readonly ViewCounter _viewCounter;
    private readonly StreamManagerFactory _streamManagerFactory;
    private readonly ThumbnailService _thumbnailService;

    public PlaylistController(Config config, ILogger<PlaylistController> logger,
        HttpClient client, SrsApi srsApi, ViewCounter viewCounter, StreamManagerFactory streamManagerFactory,
        ThumbnailService thumbnailService)
    {
        _config = config;
        _logger = logger;
        _client = client;
        _srsApi = srsApi;
        _viewCounter = viewCounter;
        _streamManagerFactory = streamManagerFactory;
        _thumbnailService = thumbnailService;
    }

    [ResponseCache(Duration = 1, Location = ResponseCacheLocation.Any)]
    [HttpGet("{variant}/{id}.m3u8")]
    public async Task RewritePlaylist([FromRoute] Guid id, [FromRoute] string variant, [FromQuery(Name = "hls_ctx")] string hlsCtx)
    {
        try
        {
            var streamManager = await _streamManagerFactory.ForStream(id);
            var userStream = streamManager.GetStream();

            var path = $"/{userStream.Endpoint.App}/{variant}/{userStream.User.StreamKey}.m3u8";
            var ub = new UriBuilder(_config.SrsHttpHost)
            {
                Path = path,
                Query = string.Join("&", Request.Query.Select(a => $"{a.Key}={a.Value}"))
            };

            Response.ContentType = "application/x-mpegurl";
            await using var sw = new StreamWriter(Response.Body);

            var req = CreateProxyRequest(ub.Uri);
            using var rsp = await _client.SendAsync(req);
            if (!rsp.IsSuccessStatusCode)
            {
                Response.StatusCode = (int)rsp.StatusCode;
                return;
            }

            await Response.StartAsync();
            using var sr = new StreamReader(await rsp.Content.ReadAsStreamAsync());
            while (await sr.ReadLineAsync() is { } line)
            {
                if (line.StartsWith("#EXTINF"))
                {
                    await sw.WriteLineAsync(line);
                    var trackPath = await sr.ReadLineAsync();
                    var seg = Regex.Match(trackPath!, @"-(\d+)\.ts");
                    await sw.WriteLineAsync($"{id}/{seg.Groups[1].Value}.ts");
                }
                else
                {
                    await sw.WriteLineAsync(line);
                }
            }

            Response.Body.Close();
            _viewCounter.Activity(userStream.Id, hlsCtx);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to get stream for {stream} {message}", id, ex.Message);
            Response.StatusCode = 404;
        }
    }

    [ResponseCache(Duration = 30, Location = ResponseCacheLocation.Any)]
    [HttpGet("{id}.jpg")]
    public async Task GetPreview([FromRoute] Guid id)
    {
        try
        {
            var stream = _thumbnailService.GetThumbnail(id);
            if (stream != default)
            {
                Response.ContentLength = stream.Length;
                Response.ContentType = "image/jpg";
                Response.Headers.CacheControl = "public, max-age=60";
                await Response.StartAsync();
                await stream.CopyToAsync(Response.Body);
                await Response.CompleteAsync();
            }
            else
            {
                Response.StatusCode = 404;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to get preview image for {id} {message}", id, ex.Message);
            Response.StatusCode = 404;
        }
    }

    [ResponseCache(Duration = 1, Location = ResponseCacheLocation.Any)]
    [HttpGet("{pubkey}.m3u8")]
    public async Task<IActionResult> GetCurrentStreamRedirect([FromRoute] string pubkey)
    {
        try
        {
            var streamManager = await _streamManagerFactory.ForCurrentStream(pubkey);
            var userStream = streamManager.GetStream();
            return Redirect($"stream/{userStream.Id}.m3u8");
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to get stream for {pubkey} {message}", pubkey, ex.Message);
        }

        return NotFound();
    }

    [ResponseCache(Duration = 1, Location = ResponseCacheLocation.Any)]
    [HttpGet("stream/{id:guid}.m3u8")]
    public async Task CreateMultiBitrate([FromRoute] Guid id)
    {
        try
        {
            var streamManager = await _streamManagerFactory.ForStream(id);
            var userStream = streamManager.GetStream();

            var hlsCtx = await GetHlsCtx(userStream);
            if (string.IsNullOrEmpty(hlsCtx))
            {
                Response.StatusCode = 404;
                return;
            }

            Response.ContentType = "application/vnd.apple.mpegurl";
            await using var sw = new StreamWriter(Response.Body);

            var streams = await _srsApi.ListStreams();
            await sw.WriteLineAsync("#EXTM3U");

            foreach (var variant in userStream.Endpoint.GetVariants().OrderBy(a => a.Bandwidth))
            {
                var stream = streams.FirstOrDefault(a =>
                    a.Name == userStream.User.StreamKey && a.App == $"{userStream.Endpoint.App}/{variant.SourceName}");

                var resArg = stream?.Video != default ? $"RESOLUTION={stream.Video?.Width}x{stream.Video?.Height}" :
                    variant.ToResolutionArg();

                var bandwidthArg = variant.ToBandwidthArg();

                var averageBandwidthArg = stream?.Kbps?.Recv30s.HasValue ?? false ? $"AVERAGE-BANDWIDTH={stream.Kbps.Recv30s * 1000}" : "";
                var codecArg = "CODECS=\"avc1.640028,mp4a.40.2\"";
                var allArgs = new[] {bandwidthArg, averageBandwidthArg, resArg, codecArg}.Where(a => !string.IsNullOrEmpty(a));
                await sw.WriteLineAsync(
                    $"#EXT-X-STREAM-INF:{string.Join(",", allArgs)}");

                var u = new Uri(_config.DataHost,
                    $"{variant.SourceName}/{userStream.Id}.m3u8{(!string.IsNullOrEmpty(hlsCtx) ? $"?hls_ctx={hlsCtx}" : "")}");

                await sw.WriteLineAsync(u.ToString());
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to get stream for {id} {message}", id, ex.Message);
            Response.StatusCode = 404;
        }
    }

    [HttpGet("{variant}/{id}/{segment}")]
    public async Task GetSegment([FromRoute] Guid id, [FromRoute] string segment, [FromRoute] string variant)
    {
        try
        {
            var streamManager = await _streamManagerFactory.ForStream(id);
            var userStream = streamManager.GetStream();

            var path = $"/{userStream.Endpoint.App}/{variant}/{userStream.User.StreamKey}-{segment}";
            await ProxyRequest(path);
        }
        catch
        {
            Response.StatusCode = 404;
        }
    }

    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    [HttpGet("recording/{id:guid}.m3u8")]
    public async Task RecordingPlaylist([FromRoute] Guid id)
    {
        try
        {
            var streamManager = await _streamManagerFactory.ForStream(id);
            var recordings = await streamManager.GetRecordings();

            if (recordings.Count == 0)
            {
                Response.StatusCode = 404;
                return;
            }

            // https://developer.apple.com/documentation/http-live-streaming/video-on-demand-playlist-construction
            Response.ContentType = "application/vnd.apple.mpegurl";
            await using var sw = new StreamWriter(Response.Body);
            await sw.WriteLineAsync("#EXTM3U");
            await sw.WriteLineAsync("#EXT-X-PLAYLIST-TYPE:VOD");
            await sw.WriteLineAsync("#EXT-X-TARGETDURATION:4");
            await sw.WriteLineAsync("#EXT-X-VERSION:6");
            await sw.WriteLineAsync("#EXT-X-MEDIA-SEQUENCE:0");
            //await sw.WriteLineAsync($"#EXT-X-MAP:URI=\"{id}_init.mp4\"");

            foreach (var seg in recordings.OrderBy(a => a.Timestamp))
            {
                await sw.WriteLineAsync($"#EXTINF:{seg.Duration},");
                await sw.WriteLineAsync($"#EXT-X-PROGRAM-DATE-TIME:{seg.Timestamp:yyyy-MM-ddTHH:mm:ss.fffzzz}");
                await sw.WriteLineAsync(seg.Url);
            }

            await sw.WriteLineAsync("#EXT-X-ENDLIST");
        }
        catch
        {
            Response.StatusCode = 404;
        }
    }

    [HttpGet("recording/{id:guid}_init.mp4")]
    public async Task GenerateInitTrack([FromRoute] Guid id)
    {
        try
        {
            var streamManager = await _streamManagerFactory.ForStream(id);
            var recordings = await streamManager.GetRecordings();

            var firstFrag = await _client.GetStreamAsync(recordings.First().Url);
            var tmpFrag = Path.GetTempFileName();
            await firstFrag.CopyToAsync(new FileStream(tmpFrag, FileMode.Open, FileAccess.ReadWrite));

            var frag = MP4Stream.Open(tmpFrag, FileMode.Open, FileAccess.Read);
            var boxes = frag.ReadRootBoxes();

            Response.ContentType = "video/mp4";
            using var outStream = new MemoryStream();
            foreach (var box in boxes.Take(2))
            {
                box.WriteBytes(outStream);
            }

            outStream.Seek(0, SeekOrigin.Begin);
            await outStream.CopyToAsync(Response.Body);
        }
        catch
        {
            Response.StatusCode = 404;
        }
    }

    private async Task<string?> GetHlsCtx(UserStream stream)
    {
        var path = $"/{stream.Endpoint.App}/source/{stream.User.StreamKey}.m3u8";
        var ub = new Uri(_config.SrsHttpHost, path);
        var req = CreateProxyRequest(ub);
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

    private async Task ProxyRequest(string path)
    {
        var req = CreateProxyRequest(new Uri(_config.SrsHttpHost, path));
        using var rsp = await _client.SendAsync(req);
        Response.Headers.ContentType = rsp.Content.Headers.ContentType?.ToString();

        await rsp.Content.CopyToAsync(Response.Body);
    }

    private HttpRequestMessage CreateProxyRequest(Uri u)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, u);
        if (Request.Headers.TryGetValue("X-Forwarded-For", out var xff) || HttpContext.Connection.RemoteIpAddress != default)
        {
            req.Headers.Add("X-Forwarded-For", xff.Count > 0 ? xff.ToString() : HttpContext.Connection.RemoteIpAddress!.ToString());
        }

        return req;
    }
}
