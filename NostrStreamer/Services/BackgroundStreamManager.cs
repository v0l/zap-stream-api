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

                var clients = await srsApi.ListClients();
                var streams = clients.Where(a => !a.Publish).GroupBy(a => a.Url);
                foreach (var stream in streams)
                {
                    var viewers = stream.Select(a => a.Ip).Distinct().Count();
                    var streamKey = stream.Key.Split("/").Last();
                    await streamManager.UpdateViewers(streamKey, viewers);
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
