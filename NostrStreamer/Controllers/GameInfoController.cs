using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NostrStreamer.ApiModel;
using NostrStreamer.Services;

namespace NostrStreamer.Controllers;

[Route("/api/v1/games")]
public class GameInfoController : Controller
{
    private readonly GameDb _gameDb;

    public GameInfoController(GameDb gameDb)
    {
        _gameDb = gameDb;
    }

    [HttpGet]
    public async Task<IActionResult> GetGames([FromQuery] string q, [FromQuery] int limit = 10)
    {
        var data = await _gameDb.SearchGames(q, limit);

        var mapped = data?.Select(a => new GameInfo()
        {
            Id = $"igdb:{a.Id}",
            Name = a.Name,
            Cover = $"https://images.igdb.com/igdb/image/upload/t_cover_big_2x/{a.Cover?.ImageId}.jpg",
            Genres = a.Genres.Select(b => b.Name).ToList()
        });

        return Json(mapped, new JsonSerializerSettings()
        {
            NullValueHandling = NullValueHandling.Ignore
        });
    }
}
