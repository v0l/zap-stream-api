using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Nostr.Client.Json;
using NostrStreamer.ApiModel;
using NostrStreamer.Database;
using NostrStreamer.Services;
using NostrStreamer.Services.Clips;
using NostrStreamer.Services.StreamManager;

namespace NostrStreamer.Controllers;

[Authorize(AuthenticationSchemes = NostrAuth.Scheme)]
[EnableCors]
[Route("/api/nostr")]
public class NostrController : Controller
{
    private readonly StreamerContext _db;
    private readonly Config _config;
    private readonly StreamManagerFactory _streamManagerFactory;
    private readonly UserService _userService;
    private readonly IClipService _clipService;

    public NostrController(StreamerContext db, Config config, StreamManagerFactory streamManager, UserService userService,
        IClipService clipService)
    {
        _db = db;
        _config = config;
        _streamManagerFactory = streamManager;
        _userService = userService;
        _clipService = clipService;
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

        var account = new Account
        {
            Event = new PatchEvent()
            {
                Title = user.Title ?? "",
                Summary = user.Summary ?? "",
                Image = user.Image ?? "",
                ContentWarning = user.ContentWarning,
                Tags = user.SplitTags(),
                Goal = user.Goal
            },
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
            Balance = (long)Math.Floor(user.Balance / 1000m),
            Tos = new()
            {
                Accepted = user.TosAccepted >= _config.TosDate,
                Link = new Uri(_config.ApiHost, "/tos")
            },
            Forwards = user.Forwards.Select(a => new ForwardDest()
            {
                Id = a.Id,
                Name = a.Name
            }).ToList()
        };

        return Content(JsonConvert.SerializeObject(account, NostrSerializer.Settings), "application/json");
    }

    [HttpPatch("event")]
    public async Task<IActionResult> UpdateStreamInfo([FromBody] PatchEvent req)
    {
        var pubkey = GetPubKey();
        if (string.IsNullOrEmpty(pubkey)) return Unauthorized();

        await _userService.UpdateStreamInfo(pubkey, req);
        try
        {
            var streamManager = await _streamManagerFactory.ForCurrentStream(pubkey);
            await streamManager.UpdateEvent();
        }
        catch
        {
            //ignore
        }

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

    [HttpPatch("account")]
    public async Task<IActionResult> PatchAccount([FromBody] PatchAccount patch)
    {
        var user = await GetUser();
        if (user == default)
        {
            return NotFound();
        }

        if (patch.AcceptTos)
        {
            await _userService.AcceptTos(user.PubKey);
        }

        return Accepted();
    }

    [HttpPost("account/forward")]
    public async Task<IActionResult> AddForward([FromBody] NewForwardRequest req)
    {
        var user = await GetUser();
        if (user == default)
        {
            return NotFound();
        }

        await _userService.AddForward(user.PubKey, req.Name, req.Target);

        return Accepted();
    }

    [HttpDelete("account/forward/{id:guid}")]
    public async Task<IActionResult> DeleteForward([FromRoute] Guid id)
    {
        var user = await GetUser();
        if (user == default)
        {
            return NotFound();
        }

        await _userService.RemoveForward(user.PubKey, id);

        return Ok();
    }

    [HttpGet("clip/{id:guid}")]
    public async Task<IActionResult> GetClipSegments([FromRoute] Guid id)
    {
        var clip = await _clipService.PrepareClip(id);
        if (clip == default) return StatusCode(500);

        return Json(new
        {
            id = clip.Id,
            length = clip.Length
        });
    }

    [HttpPost("clip/{streamId:guid}/{tempClipId:guid}")]
    public async Task<IActionResult> MakeClip([FromRoute] Guid streamId, [FromRoute] Guid tempClipId, [FromQuery] float start,
        [FromQuery] float length)
    {
        var pk = GetPubKey();
        var clip = await _clipService.MakeClip(pk, streamId, tempClipId, start, length);
        if (clip == default) return StatusCode(500);

        return Json(new
        {
            url = clip.Url
        });
    }

    [AllowAnonymous]
    [HttpGet("clip/{streamId:guid}/{clipId:guid}")]
    public IActionResult GetClipSegment([FromRoute] Guid streamId, [FromRoute] Guid clipId)
    {
        var seg = new TempClip(streamId, clipId, 0);
        if (!System.IO.File.Exists(seg.GetPath()))
        {
            return NotFound();
        }

        var fs = new FileStream(seg.GetPath(), FileMode.Open, FileAccess.Read);
        return File(fs, "video/mp4", enableRangeProcessing: true);
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
