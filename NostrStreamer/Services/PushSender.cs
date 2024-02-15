using System.Net;
using System.Threading.Tasks.Dataflow;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Nostr.Client.Json;
using Nostr.Client.Messages;
using NostrServices.Client;
using NostrStreamer.ApiModel;
using NostrStreamer.Database;
using StackExchange.Redis;
using WebPush;
using PushSubscription = NostrStreamer.Database.PushSubscription;

namespace NostrStreamer.Services;

public record PushNotificationQueue(PushMessage Notification, PushSubscription Subscription);

public class PushSender
{
    private readonly BufferBlock<NostrEvent> _queue = new();

    public void Add(NostrEvent ev)
    {
        _queue.Post(ev);
    }

    public Task<NostrEvent> Next()
    {
        return _queue.ReceiveAsync();
    }
}

public class PushSenderService : BackgroundService
{
    private readonly PushSender _sender;
    private readonly HttpClient _client;
    private readonly Config _config;
    private readonly ILogger<PushSenderService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDatabase _redis;
    private readonly NostrServicesClient _nostrApi;

    public PushSenderService(PushSender sender, HttpClient client, Config config, IServiceScopeFactory scopeFactory,
        ILogger<PushSenderService> logger, NostrServicesClient snort, IDatabase redis)
    {
        _sender = sender;
        _client = client;
        _config = config;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _nostrApi = snort;
        _redis = redis;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<StreamerContext>();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var ev = await _sender.Next();
                foreach (var (msg, sub) in await ComputeNotifications(db, ev))
                {
                    var vapid = new VapidDetails(sub.Scope, _config.VapidKey.PublicKey, _config.VapidKey.PrivateKey);
                    using var webPush = new WebPushClient(_client);
                    try
                    {
                        var pushMsg = JsonConvert.SerializeObject(msg, NostrSerializer.Settings);
                        _logger.LogInformation("Sending notification {msg}", pushMsg);
                        var webSub = new WebPush.PushSubscription(sub.Endpoint, sub.Key, sub.Auth);
                        await webPush.SendNotificationAsync(webSub, pushMsg, vapid, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send push for {pubkey} {error}", sub.Pubkey, ex.Message);
                        if (ex is WebPushException {StatusCode: HttpStatusCode.Gone})
                        {
                            await db.PushSubscriptions.Where(a => a.Id == sub.Id)
                                .ExecuteDeleteAsync(cancellationToken: stoppingToken);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PushSender {message}", ex.Message);
            }
        }
    }

    private async Task<IEnumerable<PushNotificationQueue>> ComputeNotifications(StreamerContext db, NostrEvent ev)
    {
        var ret = new List<PushNotificationQueue>();
        var notification = await MakeNotificationFromEvent(ev);
        if (notification != null)
        {
            foreach (var sub in await db.PushSubscriptions
                         .AsNoTracking()
                         .Join(db.PushSubscriptionTargets, a => a.Pubkey, b => b.SubscriberPubkey,
                             (a, b) => new {Subscription = a, Target = b})
                         .Where(a => a.Target.TargetPubkey == notification.Pubkey)
                         .ToListAsync())
            {
                ret.Add(new(notification, sub.Subscription));
            }
        }

        return ret;
    }

    private async Task<PushMessage?> MakeNotificationFromEvent(NostrEvent ev)
    {
        if (ev.Kind != NostrKind.LiveEvent) return default;

        var dTag = ev.Tags!.FindFirstTagValue("d");
        var key = $"live-event-seen:{ev.Pubkey}:{dTag}";
        if (await _redis.KeyExistsAsync(key)) return default;

        await _redis.StringSetAsync(key, ev.Id!, TimeSpan.FromDays(7));

        var host = ev.GetHost();
        var profile = await _nostrApi.Profile(host);
        return new PushMessage
        {
            Type = PushMessageType.StreamStarted,
            Pubkey = host,
            Name = profile?.Name,
            Avatar = profile?.Picture
        };
    }
}
