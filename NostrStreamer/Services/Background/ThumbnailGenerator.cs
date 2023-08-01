using Microsoft.EntityFrameworkCore;
using NostrStreamer.Database;
using NostrStreamer.Services.Thumbnail;

namespace NostrStreamer.Services.Background;

public class ThumbnailGenerator : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ThumbnailGenerator> _logger;
    
    public ThumbnailGenerator(IServiceScopeFactory scopeFactory, ILogger<ThumbnailGenerator> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<StreamerContext>();
                var gen = scope.ServiceProvider.GetRequiredService<IThumbnailService>();

                var streams = await db.Streams
                    .AsNoTracking()
                    .Include(a => a.Endpoint)
                    .Include(a => a.User)
                    .Where(a => a.State == UserStreamState.Live)
                    .ToListAsync(cancellationToken: stoppingToken);

                foreach (var stream in streams)
                {
                    await gen.GenerateThumb(stream);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to generate thumbnail {msg}", ex.Message);
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
