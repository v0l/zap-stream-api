using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Nostr.Client.Json;
using Nostr.Client.Utils;
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
    private readonly LndNode _lnd;

    public NostrController(StreamerContext db, Config config, StreamManagerFactory streamManager, LndNode lnd)
    {
        _db = db;
        _config = config;
        _streamManagerFactory = streamManager;
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
                Balance = 1000_000,
                StreamKey = Guid.NewGuid().ToString()
            };

            _db.Users.Add(user);

            await _db.SaveChangesAsync();
        }

        var endpoints = await _db.Endpoints.ToListAsync();
        var account = new Account
        {
            Event = null,
            Endpoints = endpoints.Select(a => new AccountEndpoint()
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
