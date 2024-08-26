using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NostrStreamer.ApiModel;
using NostrStreamer.Services;
using NostrStreamer.Services.Dvr;
using NostrStreamer.Services.StreamManager;

namespace NostrStreamer.Controllers;

[Authorize(AuthenticationSchemes = NostrAuth.Scheme, Roles = NostrAuth.RoleAdmin)]
[Route("/api/admin")]
public class AdminController : Controller
{
    private readonly ILogger<AdminController> _logger;
    private readonly StreamManagerFactory _streamManagerFactory;
    private readonly IDvrStore _dvrStore;
    private readonly UserService _userService;

    public AdminController(ILogger<AdminController> logger, StreamManagerFactory streamManagerFactory, IDvrStore dvrStore,
        UserService userService)
    {
        _logger = logger;
        _streamManagerFactory = streamManagerFactory;
        _dvrStore = dvrStore;
        _userService = userService;
    }

    [HttpPatch("stream/{id:guid}")]
    public async Task PublishEvent([FromRoute] Guid id)
    {
        var stream = await _streamManagerFactory.ForStream(id);
        await stream.UpdateEvent();
    }

    [HttpDelete("stream/{id:guid}")]
    public async Task DeleteEvent([FromRoute] Guid id)
    {
        var mgr = await _streamManagerFactory.ForStream(id);
        var stream = mgr.GetStream();
        await _dvrStore.DeleteRecordings(stream);
    }

    [HttpPatch("account/{pubkey}")]
    public async Task UpdateAccount([FromRoute] string pubkey, [FromBody] PatchAccount req)
    {
        if (req.Blocked.HasValue)
        {
            await _userService.SetBlocked(pubkey, req.Blocked.Value);
        }

        if (req.Admin.HasValue)
        {
            await _userService.SetAdmin(pubkey, req.Admin.Value);
        }
    }
}
