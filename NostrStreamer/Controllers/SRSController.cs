using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NostrStreamer.Services;

namespace NostrStreamer.Controllers;

[Route("/api/srs")]
public class SrsController : Controller
{
    private readonly ILogger<SrsController> _logger;
    private readonly Config _config;
    private readonly StreamManager _streamManager;

    public SrsController(ILogger<SrsController> logger, Config config, StreamManager streamManager)
    {
        _logger = logger;
        _config = config;
        _streamManager = streamManager;
    }

    [HttpPost]
    public async Task<SrsHookReply> OnStream([FromBody] SrsHook req)
    {
        _logger.LogInformation("OnStream: {obj}", JsonConvert.SerializeObject(req));
        try
        {
            if (string.IsNullOrEmpty(req.Stream) || string.IsNullOrEmpty(req.App) || string.IsNullOrEmpty(req.Stream) ||
                !req.App.StartsWith(_config.App, StringComparison.InvariantCultureIgnoreCase))
            {
                return new()
                {
                    Code = 2 // invalid request
                };
            }

            if (req.App.EndsWith("/source"))
            {
                if (req.Action == "on_publish")
                {
                    await _streamManager.StreamStarted(req.Stream);
                    return new();
                }

                if (req.Action == "on_unpublish")
                {
                    await _streamManager.StreamStopped(req.Stream);
                    return new();
                }

                if (req.Action == "on_hls" && req.Duration.HasValue && !string.IsNullOrEmpty(req.ClientId))
                {
                    await _streamManager.ConsumeQuota(req.Stream, req.Duration.Value, req.ClientId);
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
