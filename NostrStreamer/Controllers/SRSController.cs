using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NostrStreamer.Services.StreamManager;

namespace NostrStreamer.Controllers;

[Route("/api/srs")]
public class SrsController : Controller
{
    private readonly ILogger<SrsController> _logger;
    private readonly StreamManagerFactory _streamManagerFactory;

    public SrsController(ILogger<SrsController> logger, StreamManagerFactory streamManager)
    {
        _logger = logger;
        _streamManagerFactory = streamManager;
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
            var streamManager = await _streamManagerFactory.ForStream(new StreamInfo
            {
                App = appSplit[0],
                Variant = appSplit.Length > 1 ? appSplit[1] : "source",
                ClientId = req.ClientId!,
                StreamKey = req.Stream
            });

            if (req.Action == "on_forward")
            {
                var urls = await streamManager.OnForward();
                return new SrsForwardHookReply
                {
                    Data = new()
                    {
                        Urls = urls
                    }
                };
            }
            
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
                    return new();
                }
            }
            else
            {
                return new();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start stream");
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
}
