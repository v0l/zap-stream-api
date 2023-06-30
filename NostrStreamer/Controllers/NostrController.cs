using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NostrStreamer.ApiModel;
using NostrStreamer.Database;

namespace NostrStreamer.Controllers;

[Authorize]
[Route("/api/nostr")]
public class NostrController : Controller
{
    private readonly StreamerContext _db;
    private readonly Config _config;

    public NostrController(StreamerContext db, Config config)
    {
        _db = db;
        _config = config;
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

        return Json(new Account
        {
            Url = new Uri(_config.RtmpHost, _config.App).ToString(),
            Key = user.StreamKey
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
