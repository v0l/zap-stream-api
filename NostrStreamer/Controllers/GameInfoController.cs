using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NostrStreamer.Services;

namespace NostrStreamer.Controllers;

[Route("/api/v1/games")]
public class GameInfoController(GameDb gameDb) : Controller
{
    [HttpGet("search")]
    public async Task<IActionResult> GetGames([FromQuery] string q, [FromQuery] int limit = 10)
    {
        var data = await gameDb.SearchGames(q, limit);

        var mapped = data?.Select(a => a.ToGameInfo());

        return Json(mapped, new JsonSerializerSettings()
        {
            NullValueHandling = NullValueHandling.Ignore
        });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetGame([FromRoute] string id)
    {
        var data = await gameDb.GetGame(id.Split(":")[1]);

        return Json(data?.ToGameInfo(), new JsonSerializerSettings()
        {
            NullValueHandling = NullValueHandling.Ignore
        });
    }
}