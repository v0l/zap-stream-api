using Microsoft.EntityFrameworkCore;
using NostrStreamer.Database;
using NostrStreamer.Services.StreamManager;

namespace NostrStreamer.Services;

public class BackgroundStreamManager : BackgroundService
{
    private readonly ILogger<BackgroundStreamManager> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public BackgroundStreamManager(ILogger<BackgroundStreamManager> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                var streamManager = scope.ServiceProvider.GetRequiredService<StreamManagerFactory>();
                var db = scope.ServiceProvider.GetRequiredService<StreamerContext>();
                var srs = scope.ServiceProvider.GetRequiredService<SrsApi>();

                var liveStreams = await db.Streams
                    .AsNoTracking()
                    .Where(a => a.State == UserStreamState.Live)
                    .Select(a => a.Id)
                    .ToListAsync(cancellationToken: stoppingToken);

                foreach (var id in liveStreams)
                {
                    var manager = await streamManager.ForStream(id);
                    var client = await srs.GetStream(manager.GetStream().StreamId);
                    if (client != default)
                    {
                        await manager.UpdateViewers();
                    }
                    else
                    {
                        await manager.StreamStopped();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to run");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
