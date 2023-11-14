using Microsoft.AspNetCore.Mvc;

namespace NostrStreamer.Controllers;

[Route("/api/time")]
public class TimeController : Controller
{
    [HttpGet]
    public IActionResult GetTime()
    {
        return Json(new
        {
            time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
    }
}
