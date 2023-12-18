using Newtonsoft.Json;
using Nostr.Client.Json;
using Nostr.Client.Messages;
using StackExchange.Redis;

namespace NostrStreamer.Services;

public class EventStream : BackgroundService
{
    private readonly ILogger<EventStream> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public EventStream(ILogger<EventStream> logger, IServiceScopeFactory scopeFactory)
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
                var redis = scope.ServiceProvider.GetRequiredService<ConnectionMultiplexer>();
                var push = scope.ServiceProvider.GetRequiredService<PushSender>();
                var queue = await redis.GetSubscriber().SubscribeAsync("event-stream");

                while (!stoppingToken.IsCancellationRequested)
                {
                    var msg = await queue.ReadAsync(stoppingToken);

                    var ev = JsonConvert.DeserializeObject<NostrEvent>(msg.Message!, NostrSerializer.Settings);
                    if (ev is {Kind: NostrKind.LiveEvent})
                    {
                        push.Add(ev);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "failed {msg}", ex.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}
