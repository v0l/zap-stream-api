using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Nostr.Client.Json;
using Nostr.Client.Messages;
using NostrStreamer.ApiModel;
using NostrStreamer.Database;
using NostrStreamer.Services;
using NostrStreamer.Services.StreamManager;

namespace NostrStreamer.Controllers;

[Authorize]
[Route("/api/nostr")]
public class NostrController : Controller
{
    private readonly StreamerContext _db;
    private readonly Config _config;
    private readonly StreamManagerFactory _streamManagerFactory;
    private readonly UserService _userService;

    public NostrController(StreamerContext db, Config config, StreamManagerFactory streamManager, UserService userService)
    {
        _db = db;
        _config = config;
        _streamManagerFactory = streamManager;
        _userService = userService;
    }

    [HttpGet("account")]
    public async Task<ActionResult> GetAccount()
    {
        var user = await GetUser();
        if (user == default)
        {
            var pk = GetPubKey();
            user = await _userService.CreateAccount(pk);
        }

        var endpoints = await _db.Endpoints
            .AsNoTracking()
            .ToListAsync();

        var latestEvent = await _db.Streams
            .AsNoTracking()
            .Where(a => a.User.PubKey == user.PubKey)
            .OrderByDescending(a => a.Starts)
            .Select(a => a.Event)
            .FirstOrDefaultAsync();

        var account = new Account
        {
            Event = !string.IsNullOrEmpty(latestEvent) ? JsonConvert.DeserializeObject<NostrEvent>(latestEvent, NostrSerializer.Settings) : null,
            Endpoints = endpoints.Select(a => new AccountEndpoint
            {
                Name = a.Name,
                Url = new Uri(_config.RtmpHost, a.App).ToString(),
                Key = user.StreamKey,
                Capabilities = a.Capabilities,
                Cost = new()
                {
                    Unit = "min",
                    Rate = a.Cost / 1000d
                }
            }).ToList(),
            Balance = (long)Math.Floor(user.Balance / 1000m)
        };

        return Content(JsonConvert.SerializeObject(account, NostrSerializer.Settings), "application/json");
    }

    [HttpPatch("event")]
    public async Task<IActionResult> UpdateStreamInfo([FromBody] PatchEvent req)
    {
        var pubkey = GetPubKey();
        if (string.IsNullOrEmpty(pubkey)) return Unauthorized();

        var streamManager = await _streamManagerFactory.ForCurrentStream(pubkey);
        await streamManager.PatchEvent(req.Title, req.Summary, req.Image, req.Tags, req.ContentWarning);
        return Accepted();
    }

    [HttpGet("topup")]
    public async Task<IActionResult> TopUp([FromQuery] ulong amount)
    {
        var pubkey = GetPubKey();
        if (string.IsNullOrEmpty(pubkey)) return Unauthorized();

        var invoice = await _userService.CreateTopup(pubkey, amount * 1000, null, null);
        return Json(new
        {
            pr = invoice
        });
    }

    private async Task<User?> GetUser()
    {
        var pk = GetPubKey();
        return await _userService.GetUser(pk);
    }

    private string GetPubKey()
    {
        var claim = HttpContext.User.Claims.FirstOrDefault(a => a.Type == ClaimTypes.Name);
        return claim!.Value;
    }
}
