using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NostrStreamer.Database;
using NostrStreamer.Services;

namespace NostrStreamer.Controllers;

[Route("/api/playlist")]
public class PlaylistController : Controller
{
    private readonly ILogger<PlaylistController> _logger;
    private readonly IMemoryCache _cache;
    private readonly Config _config;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly HttpClient _client;
    private readonly SrsApi _srsApi;

    public PlaylistController(Config config, IMemoryCache cache, ILogger<PlaylistController> logger, IServiceScopeFactory scopeFactory,
        HttpClient client, SrsApi srsApi)
    {
        _config = config;
        _cache = cache;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _client = client;
        _srsApi = srsApi;
    }

    [HttpGet("{variant}/{pubkey}.m3u8")]
    public async Task RewritePlaylist([FromRoute] string pubkey, [FromRoute] string variant)
    {
        var key = await GetStreamKey(pubkey);
        if (string.IsNullOrEmpty(key))
        {
            Response.StatusCode = 404;
            return;
        }

        var path = $"/{_config.App}/{variant}/{key}.m3u8";
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
                var seg = Regex.Match(trackPath!, @"-(\d+)\.ts$");
                await sw.WriteLineAsync($"{pubkey}/{seg.Groups[1].Value}.ts");
            }
            else
            {
                await sw.WriteLineAsync(line);
            }
        }

        Response.Body.Close();
    }

    [HttpGet("{pubkey}.m3u8")]
    public async Task CreateMultiBitrate([FromRoute] string pubkey)
    {
        var key = await GetStreamKey(pubkey);
        if (string.IsNullOrEmpty(key))
        {
            Response.StatusCode = 404;
            return;
        }

        var hlsCtx = await GetHlsCtx(key);
        if (string.IsNullOrEmpty(hlsCtx))
        {
            Response.StatusCode = 404;
            return;
        }
        Response.ContentType = "application/x-mpegurl";
        await using var sw = new StreamWriter(Response.Body);

        var streams = await _srsApi.ListStreams();
        await sw.WriteLineAsync("#EXTM3U");

        foreach (var variant in _config.Variants.OrderBy(a => a.Bandwidth))
        {
            var stream = streams.FirstOrDefault(a =>
                a.Name == key && a.App == $"{_config.App}/{variant.Name}");

            var resArg = stream?.Video != default ? $"RESOLUTION={stream.Video?.Width}x{stream.Video?.Height}" :
                $"RESOLUTION={variant.Width}x{variant.Height}";

            var bandwidthArg = $"BANDWIDTH={variant.Bandwidth * 1000}";

            var averageBandwidthArg = stream?.Kbps?.Recv30s.HasValue ?? false ? $"AVERAGE-BANDWIDTH={stream.Kbps.Recv30s * 1000}" : "";
            var allArgs = new[] {bandwidthArg, averageBandwidthArg, resArg}.Where(a => !string.IsNullOrEmpty(a));
            await sw.WriteLineAsync(
                $"#EXT-X-STREAM-INF:{string.Join(",", allArgs)},CODECS=\"avc1.640028,mp4a.40.2\"");

            var u = new Uri(_config.DataHost, $"{variant.Name}/{pubkey}.m3u8{(!string.IsNullOrEmpty(hlsCtx) ? $"?hls_ctx={hlsCtx}" : "")}");
            await sw.WriteLineAsync(u.ToString());
        }
    }

    [HttpGet("{variant}/{pubkey}/{segment}")]
    public async Task GetSegment([FromRoute] string pubkey, [FromRoute] string segment, [FromRoute] string variant)
    {
        var key = await GetStreamKey(pubkey);
        if (string.IsNullOrEmpty(key))
        {
            Response.StatusCode = 404;
            return;
        }

        var path = $"/{_config.App}/{variant}/{key}-{segment}";
        await ProxyRequest(path);
    }

    private async Task<string?> GetHlsCtx(string key)
    {
        var path = $"/{_config.App}/source/{key}.m3u8";
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
        if (Request.Headers.TryGetValue("x-forward-for", out var xff) || HttpContext.Connection.RemoteIpAddress != default)
        {
            req.Headers.Add("x-forward-for", xff.Count > 0 ? xff.ToString() : HttpContext.Connection.RemoteIpAddress!.ToString());
        }

        return req;
    }

    private async Task<string?> GetStreamKey(string pubkey)
    {
        var cacheKey = $"stream-key:{pubkey}";
        var cached = _cache.Get<string>(cacheKey);
        if (cached != default)
        {
            return cached;
        }

        using var scope = _scopeFactory.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<StreamerContext>();
        var user = await db.Users.SingleOrDefaultAsync(a => a.PubKey == pubkey);

        _cache.Set(cacheKey, user?.StreamKey);
        return user?.StreamKey;
    }
}
