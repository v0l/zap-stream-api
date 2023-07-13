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

                var streamManager = scope.ServiceProvider.GetRequiredService<StreamManager>();
                var srsApi = scope.ServiceProvider.GetRequiredService<SrsApi>();
                var viewCounter = scope.ServiceProvider.GetRequiredService<ViewCounter>();

                var streams = await srsApi.ListStreams();
                foreach (var stream in streams.GroupBy(a => a.Name))
                {
                    var viewers = viewCounter.Current(stream.Key);
                    await streamManager.UpdateViewers(stream.Key, viewers);
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
