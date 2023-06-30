using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NostrStreamer.ApiModel;
using NostrStreamer.Database;

namespace NostrStreamer.Controllers;

[Authorize]
[Route("/api/account")]
public class AccountController : Controller
{
    private readonly StreamerContext _db;
    private readonly Config _config;

    public AccountController(StreamerContext db, Config config)
    {
        _db = db;
        _config = config;
    }

    [HttpGet]
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
            Url = $"rtmp://{_config.SrsPublicHost.Host}/${_config.App}",
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
