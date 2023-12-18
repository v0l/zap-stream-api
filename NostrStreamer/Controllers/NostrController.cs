using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Nostr.Client.Json;
using Nostr.Client.Messages;
using NostrStreamer.ApiModel;
using NostrStreamer.Database;
using NostrStreamer.Services;
using NostrStreamer.Services.Clips;
using NostrStreamer.Services.StreamManager;
using WebPush;

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
    private readonly ILogger<NostrController> _logger;
    private readonly PushSender _pushSender;

    public NostrController(StreamerContext db, Config config, StreamManagerFactory streamManager, UserService userService,
        IClipService clipService, ILogger<NostrController> logger, PushSender pushSender)
    {
        _db = db;
        _config = config;
        _streamManagerFactory = streamManager;
        _userService = userService;
        _clipService = clipService;
        _logger = logger;
        _pushSender = pushSender;
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

    [HttpGet("notifications/info")]
    [AllowAnonymous]
    public IActionResult GetInfo()
    {
        return Json(new
        {
            publicKey = _config.VapidKey.PublicKey
        });
    }

#if DEBUG
    [HttpGet("notifications/generate-keys")]
    [AllowAnonymous]
    public IActionResult GenerateKeys()
    {
        var vapidKeys = VapidHelper.GenerateVapidKeys();

        return Json(new
        {
            publicKey = vapidKeys.PublicKey,
            privateKey = vapidKeys.PrivateKey
        });
    }

    [HttpPost("notifications/test")]
    [AllowAnonymous]
    public void TestNotification([FromBody] NostrEvent ev)
    {
        _pushSender.Add(ev);
    }

#endif

    [HttpPost("notifications/register")]
    public async Task<IActionResult> Register([FromBody] PushSubscriptionRequest sub)
    {
        if (string.IsNullOrEmpty(sub.Endpoint) || string.IsNullOrEmpty(sub.Auth) || string.IsNullOrEmpty(sub.Key))
            return BadRequest();

        var pubkey = GetPubKey();
        if (string.IsNullOrEmpty(pubkey))
            return BadRequest();

        var count = await _db.PushSubscriptions.CountAsync(a => a.Pubkey == pubkey);
        if (count >= 5)
            return Json(new
            {
                error = "Too many active subscriptions"
            });

        var existing = await _db.PushSubscriptions.FirstOrDefaultAsync(a => a.Key == sub.Key);
        if (existing != default)
        {
            return Json(new {id = existing.Id});
        }

        var newId = Guid.NewGuid();
        _db.PushSubscriptions.Add(new()
        {
            Id = newId,
            Pubkey = pubkey,
            Endpoint = sub.Endpoint,
            Key = sub.Key,
            Auth = sub.Auth,
            Scope = sub.Scope
        });

        await _db.SaveChangesAsync();
        _logger.LogInformation("{pubkey} registered for notifications", pubkey);
        return Json(new
        {
            id = newId
        });
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> ListNotifications([FromQuery] string auth)
    {
        var userPubkey = GetPubKey();
        if (string.IsNullOrEmpty(userPubkey))
            return BadRequest();

        var sub = await _db.PushSubscriptionTargets
            .Join(_db.PushSubscriptions, a => a.SubscriberPubkey, b => b.Pubkey,
                (a, b) => new {a.SubscriberPubkey, a.TargetPubkey, b.Auth})
            .Where(a => a.SubscriberPubkey == userPubkey && a.Auth == auth)
            .Select(a => a.TargetPubkey)
            .ToListAsync();

        return Json(sub);
    }

    [HttpPatch("notifications")]
    public async Task<IActionResult> RegisterForStreamer([FromQuery] string pubkey)
    {
        if (string.IsNullOrEmpty(pubkey)) return BadRequest();

        var userPubkey = GetPubKey();
        if (string.IsNullOrEmpty(userPubkey))
            return BadRequest();

        var sub = await _db.PushSubscriptionTargets
            .CountAsync(a => a.SubscriberPubkey == userPubkey && a.TargetPubkey == pubkey);

        if (sub > 0) return Ok();

        _db.PushSubscriptionTargets.Add(new()
        {
            SubscriberPubkey = userPubkey,
            TargetPubkey = pubkey
        });

        await _db.SaveChangesAsync();

        return Accepted();
    }

    [HttpDelete("notifications")]
    public async Task<IActionResult> UnregisterForStreamer([FromQuery] string pubkey)
    {
        if (string.IsNullOrEmpty(pubkey)) return BadRequest();

        var userPubkey = GetPubKey();
        if (string.IsNullOrEmpty(userPubkey))
            return BadRequest();

        var sub = await _db.PushSubscriptionTargets
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.SubscriberPubkey == userPubkey && a.TargetPubkey == pubkey);

        if (sub == default) return NotFound();

        await _db.PushSubscriptionTargets
            .Where(a => a.Id == sub.Id)
            .ExecuteDeleteAsync();

        return Accepted();
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
