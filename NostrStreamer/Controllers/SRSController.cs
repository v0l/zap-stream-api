using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NostrStreamer.Services.StreamManager;

namespace NostrStreamer.Controllers;

[Route("/api/srs")]
public class SrsController : Controller
{
    private readonly ILogger<SrsController> _logger;
    private readonly StreamManagerFactory _streamManagerFactory;
    private readonly Config _config;

    public SrsController(ILogger<SrsController> logger, StreamManagerFactory streamManager, Config config)
    {
        _logger = logger;
        _streamManagerFactory = streamManager;
        _config = config;
    }

    [HttpPost]
    public async Task<SrsHookReply> OnStream([FromBody] SrsHook req)
    {
        _logger.LogInformation("OnStream: {obj}", JsonConvert.SerializeObject(req));
        try
        {
            if (string.IsNullOrEmpty(req.Stream) || string.IsNullOrEmpty(req.App))
            {
                return new()
                {
                    Code = 2 // invalid request
                };
            }

            var appSplit = req.App.Split("/");
            var info = new StreamInfo
            {
                App = appSplit[0],
                Variant = appSplit.Length > 1 ? appSplit[1] : "",
                ClientId = req.ClientId!,
                StreamKey = req.Stream,
                EdgeIp = req.Ip!
            };
            
            if (req.Action == "on_forward")
            {
                var newStream = await _streamManagerFactory.CreateStream(info);
                var urls = await newStream.OnForward();
                if (urls.Count > 0)
                {
                    return new SrsForwardHookReply
                    {
                        Data = new()
                        {
                            Urls = urls
                        }
                    };
                }

                return new()
                {
                    Code = 2 // invalid request
                };
            }

            var streamManager = await _streamManagerFactory.ForStream(info);
            if (req.App.EndsWith("/source"))
            {
                if (req.Action == "on_publish")
                {
                    await streamManager.StreamStarted();
                    return new();
                }

                if (req.Action == "on_unpublish")
                {
                    await streamManager.StreamStopped();
                    return new();
                }

                if (req.Action == "on_hls" && req.Duration.HasValue && !string.IsNullOrEmpty(req.ClientId))
                {
                    await streamManager.ConsumeQuota(req.Duration.Value);
                    await streamManager.OnDvr(new Uri(_config.SrsHttpHost, $"{req.App}/{Path.GetFileName(req.File)}"));
                    return new();
                }

                /*if (req.Action == "on_dvr" && !string.IsNullOrEmpty(req.File))
                {
                    await streamManager.OnDvr(new Uri(_config.SrsHttpHost, $"{req.App}/{Path.GetFileName(req.File)}"));
                    return new();
                }*/
            }
            else
            {
                return new();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to start stream: {message}", ex.Message);
        }

        return new()
        {
            Code = 1 // generic error
        };
    }
}

public class SrsHookReply
{
    [JsonProperty("code")]
    public int Code { get; init; }
}

public class SrsForwardHookReply : SrsHookReply
{
    [JsonProperty("data")]
    public SrsUrlList Data { get; init; } = null!;
}

public class SrsUrlList
{
    [JsonProperty("urls")]
    public List<string> Urls { get; init; } = new();
}

public class SrsHook
{
    [JsonProperty("action")]
    public string? Action { get; set; }

    [JsonProperty("client_id")]
    public string? ClientId { get; set; }

    [JsonProperty("stream_id")]
    public string? StreamId { get; set; }

    [JsonProperty("ip")]
    public string? Ip { get; set; }

    [JsonProperty("vhost")]
    public string? Vhost { get; set; }

    [JsonProperty("app")]
    public string? App { get; set; }

    [JsonProperty("stream")]
    public string? Stream { get; set; }

    [JsonProperty("param")]
    public string? Param { get; init; }

    [JsonProperty("duration")]
    public double? Duration { get; init; }

    [JsonProperty("file")]
    public string? File { get; init; }
}
