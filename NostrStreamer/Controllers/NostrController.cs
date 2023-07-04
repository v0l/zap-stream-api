using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Nostr.Client.Json;
using Nostr.Client.Messages;
using Nostr.Client.Utils;
using NostrStreamer.ApiModel;
using NostrStreamer.Database;
using NostrStreamer.Services;

namespace NostrStreamer.Controllers;

[Authorize]
[Route("/api/nostr")]
public class NostrController : Controller
{
    private readonly StreamerContext _db;
    private readonly Config _config;
    private readonly StreamManager _streamManager;
    private readonly LndNode _lnd;

    public NostrController(StreamerContext db, Config config, StreamManager streamManager, LndNode lnd)
    {
        _db = db;
        _config = config;
        _streamManager = streamManager;
        _lnd = lnd;
    }

    [HttpGet("account")]
    public async Task<ActionResult> GetAccount()
    {
        var user = await GetUser();
        if (user == default)
        {
            var pk = GetPubKey();
            user = new()
            {
                PubKey = pk,
                Balance = 0,
                StreamKey = Guid.NewGuid().ToString()
            };

            _db.Users.Add(user);

            await _db.SaveChangesAsync();
        }

        var account = new Account
        {
            Url = new Uri(_config.RtmpHost, _config.App).ToString(),
            Key = user.StreamKey,
            Event = !string.IsNullOrEmpty(user.Event) ? JsonConvert.DeserializeObject<NostrEvent>(user.Event, NostrSerializer.Settings) :
                null,
            Quota = new()
            {
                Unit = "min",
                Rate = 21,
                Remaining = user.Balance
            }
        };

        return Content(JsonConvert.SerializeObject(account, NostrSerializer.Settings), "application/json");
    }

    [HttpPatch("event")]
    public async Task<IActionResult> UpdateStreamInfo([FromBody] PatchEvent req)
    {
        var pubkey = GetPubKey();
        if (string.IsNullOrEmpty(pubkey)) return Unauthorized();

        await _streamManager.PatchEvent(pubkey, req.Title, req.Summary, req.Image);
        return Accepted();
    }

    [HttpGet("topup")]
    public async Task<IActionResult> TopUp([FromQuery] ulong amount)
    {
        var pubkey = GetPubKey();
        if (string.IsNullOrEmpty(pubkey)) return Unauthorized();

        var invoice = await _lnd.AddInvoice(amount * 1000, TimeSpan.FromMinutes(10), $"Top up for {pubkey}");
        _db.Payments.Add(new()
        {
            PubKey = pubkey,
            Amount = amount,
            Invoice = invoice.PaymentRequest,
            PaymentHash = invoice.RHash.ToByteArray().ToHex()
        });

        await _db.SaveChangesAsync();

        return Json(new
        {
            pr = invoice.PaymentRequest
        });
    }

    private async Task<User?> GetUser()
    {
        var pk = GetPubKey();
        return await _db.Users.FirstOrDefaultAsync(a => a.PubKey == pk);
    }

    private string GetPubKey()
    {
        var claim = HttpContext.User.Claims.FirstOrDefault(a => a.Type == ClaimTypes.Name);
        return claim!.Value;
    }
}
