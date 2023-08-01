using Microsoft.AspNetCore.Mvc;
using NostrStreamer.Services.StreamManager;

namespace NostrStreamer.Controllers;

[Route("/api/admin")]
public class AdminController : Controller
{
    private readonly ILogger<AdminController> _logger;
    private readonly StreamManagerFactory _streamManagerFactory;
    
    public AdminController(ILogger<AdminController> logger, StreamManagerFactory streamManagerFactory)
    {
        _logger = logger;
        _streamManagerFactory = streamManagerFactory;
    }
    
    [HttpPatch("stream/{id:guid}")]
    public async Task PublishEvent([FromRoute] Guid id)
    {
        var stream = await _streamManagerFactory.ForStream(id);
        await stream.UpdateEvent();
    }
}
