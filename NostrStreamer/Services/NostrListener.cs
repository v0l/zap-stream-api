using System.Net.WebSockets;
using System.Reflection;
using Nostr.Client.Client;
using Nostr.Client.Communicator;
using Nostr.Client.Requests;
using Websocket.Client.Models;

namespace NostrStreamer.Services;

public class NostrListener : IDisposable
{
    private readonly Config _config;
    private readonly NostrMultiWebsocketClient _client;
    private readonly INostrCommunicator[] _communicators;
    private readonly ILogger<NostrListener> _logger;

    private readonly Dictionary<string, NostrFilter> _subscriptionToFilter = new();

    public NostrListener(Config config, NostrMultiWebsocketClient client, ILogger<NostrListener> logger)
    {
        _config = config;
        _client = client;
        _logger = logger;

        _communicators = CreateCommunicators();
        foreach (var communicator in _communicators)
            _client.RegisterCommunicator(communicator);
    }

    public NostrClientStreams Streams => _client.Streams;

    public void Dispose()
    {
        _client.Dispose();

        foreach (var comm in _communicators)
        {
            comm.Dispose();
        }
    }

    public void RegisterFilter(string subscription, NostrFilter filter)
    {
        _subscriptionToFilter[subscription] = filter;
    }

    public void Start()
    {
        foreach (var comm in _communicators)
        {
            // fire and forget
            _ = comm.Start();
        }
    }

    public void Stop()
    {
        foreach (var comm in _communicators)
        {
            // fire and forget
            _ = comm.Stop(WebSocketCloseStatus.NormalClosure, string.Empty);
        }
    }

    private INostrCommunicator[] CreateCommunicators() =>
        _config.Relays
            .Select(x => CreateCommunicator(new Uri(x)))
            .ToArray();

    private INostrCommunicator CreateCommunicator(Uri uri)
    {
        var comm = new NostrWebsocketCommunicator(uri, () =>
        {
            var client = new ClientWebSocket();
            client.Options.SetRequestHeader("Origin", "http://localhost");
            client.Options.SetRequestHeader("User-Agent", $"NostrStreamer ({Assembly.GetExecutingAssembly().GetName().Version})");
            return client;
        });

        comm.Name = uri.Host;
        comm.ReconnectTimeout = null; //TimeSpan.FromSeconds(30);
        comm.ErrorReconnectTimeout = TimeSpan.FromSeconds(2);

        comm.ReconnectionHappened.Subscribe(info => OnCommunicatorReconnection(info, comm.Name));
        comm.DisconnectionHappened.Subscribe(info =>
            _logger.LogWarning("[{relay}] Disconnected, type: {type}, reason: {reason}", comm.Name, info.Type, info.CloseStatus));

        comm.MessageReceived.Subscribe(msg => _logger.LogInformation(msg.Text));
        return comm;
    }

    private void OnCommunicatorReconnection(ReconnectionInfo info, string communicatorName)
    {
        try
        {
            _logger.LogInformation("[{relay}] Reconnected, sending Nostr filters ({filterCount})", communicatorName,
                _subscriptionToFilter.Count);

            var client = _client.FindClient(communicatorName);
            if (client == null)
            {
                _logger.LogWarning("[{relay}] Cannot find client", communicatorName);
                return;
            }

            foreach (var (sub, filter) in _subscriptionToFilter)
            {
                client.Send(new NostrRequest(sub, filter));
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[{relay}] Failed to process reconnection, error: {error}", communicatorName, e.Message);
        }
    }
}

public class NostrListenerLifetime : IHostedService
{
    private readonly NostrListener _nostrListener;
    
    public NostrListenerLifetime(NostrListener nostrListener)
    {
        _nostrListener = nostrListener;
    }
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _nostrListener.Start();
        return Task.CompletedTask;
    }
    
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _nostrListener.Dispose();
        return Task.CompletedTask;
    }
}
