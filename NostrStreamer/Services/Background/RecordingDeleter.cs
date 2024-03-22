using Microsoft.EntityFrameworkCore;
using NostrStreamer.Database;
using NostrStreamer.Services.Dvr;

namespace NostrStreamer.Services.Background;

public class RecordingDeleter : BackgroundService
{
    private readonly ILogger<RecordingDeleter> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Config _config;

    public RecordingDeleter(ILogger<RecordingDeleter> logger, IServiceScopeFactory scopeFactory, Config config)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await using var db = scope.ServiceProvider.GetRequiredService<StreamerContext>();
                var dvrStore = scope.ServiceProvider.GetRequiredService<IDvrStore>();

                var olderThan = DateTime.UtcNow.Subtract(TimeSpan.FromDays(_config.RetainRecordingsDays));
                var toDelete = await db.Streams
                    .AsNoTracking()
                    .Where(a => a.Starts < olderThan)
                    .Where(a => a.Recordings.Count > 0)
                    .ToListAsync(cancellationToken: stoppingToken);

                _logger.LogInformation("Starting delete of {n:###0} stream recordings", toDelete.Count);

                foreach (var stream in toDelete)
                {
                    try
                    {
                        var streamRecordings = await db.Streams
                            .AsNoTracking()
                            .Include(a => a.Recordings)
                            .SingleAsync(a => a.Id == stream.Id, cancellationToken: stoppingToken);
                        var deleted = await dvrStore.DeleteRecordings(streamRecordings);

                        await db.Recordings
                            .Where(a => deleted.Contains(a.Id))
                            .ExecuteDeleteAsync(cancellationToken: stoppingToken);
                        
                        _logger.LogInformation("Deleted {n}/{m} recordings from stream {id}", deleted.Count,
                            streamRecordings.Recordings.Count, stream.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete stream recordings {id} {msg}", stream.Id, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to run background deleter");
            }

            await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
        }
    }

    private async Task DeleteBroken(StreamerContext db, IDvrStore dvrStore, CancellationToken stoppingToken)
    {
        var olderThan = DateTime.UtcNow.Subtract(TimeSpan.FromDays(_config.RetainRecordingsDays));
        
        var toDelete = await db.Streams
            .AsNoTracking()
            .Where(a => a.Starts < olderThan)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken: stoppingToken);

        _logger.LogInformation("Starting (broken) delete of {n:###0} stream recordings", toDelete.Count);
    }
}