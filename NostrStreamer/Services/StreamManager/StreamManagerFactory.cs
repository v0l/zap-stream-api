using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Nostr.Client.Json;
using NostrStreamer.Database;

namespace NostrStreamer.Services.StreamManager;

public class StreamManagerFactory
{
    private readonly StreamerContext _db;
    private readonly ILoggerFactory _loggerFactory;
    private readonly StreamEventBuilder _eventBuilder;
    private readonly IServiceProvider _serviceProvider;
    private readonly Config _config;

    public StreamManagerFactory(StreamerContext db, ILoggerFactory loggerFactory, StreamEventBuilder eventBuilder,
        IServiceProvider serviceProvider, Config config)
    {
        _db = db;
        _loggerFactory = loggerFactory;
        _eventBuilder = eventBuilder;
        _serviceProvider = serviceProvider;
        _config = config;
    }

    public async Task<IStreamManager> CreateStream(StreamInfo info)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Include(a => a.Forwards)
            .Include(user => user.StreamKeys)
            .SingleOrDefaultAsync(a =>
                a.StreamKey.Equals(info.StreamKey) || a.StreamKeys.Any(b => b.Key == info.StreamKey));

        if (user == default) throw new Exception("No user found");

        var ep = await _db.Endpoints
            .AsNoTracking()
            .SingleOrDefaultAsync(a => a.App.Equals(info.App));

        if (ep == default) throw new Exception("No endpoint found");

        if (user.Balance <= 0)
        {
            throw new LowBalanceException("Cannot start stream with empty balance");
        }

        if (user.TosAccepted == null || user.TosAccepted < _config.TosDate)
        {
            throw new Exception("TOS not accepted");
        }

        if (user.IsBlocked)
        {
            throw new Exception("User account blocked");
        }

        var singleUseKey = user.StreamKeys.FirstOrDefault(a => a.Key == info.StreamKey);

        var existingLive = singleUseKey != default
            ? await _db.Streams.SingleOrDefaultAsync(a => a.Id == singleUseKey.StreamId)
            : await _db.Streams
                .SingleOrDefaultAsync(a => a.State == UserStreamState.Live && a.PubKey == user.PubKey);

        var stream = existingLive ?? new UserStream
        {
            EndpointId = ep.Id,
            PubKey = user.PubKey,
            State = UserStreamState.Live,
            EdgeIp = info.EdgeIp,
            ForwardClientId = info.ClientId,
        };

        // add new stream
        if (existingLive == default)
        {
            await stream.CopyLastStreamDetails(_db);
            var ev = _eventBuilder.CreateStreamEvent(stream);
            stream.Event = NostrJson.Serialize(ev) ?? "";
            _db.Streams.Add(stream);
            await _db.SaveChangesAsync();
        }
        else
        {
            // resume stream, update edge forward info
            existingLive.EdgeIp = info.EdgeIp;
            existingLive.ForwardClientId = info.ClientId;
            await _db.SaveChangesAsync();
        }

        var ctx = new StreamManagerContext
        {
            Db = _db,
            StreamKey = info.StreamKey,
            UserStream = new()
            {
                Id = stream.Id,
                PubKey = stream.PubKey,
                State = stream.State,
                EdgeIp = stream.EdgeIp,
                ForwardClientId = stream.ForwardClientId,
                Endpoint = ep,
                User = user
            },
            EdgeApi = new SrsApi(_serviceProvider.GetRequiredService<HttpClient>(),
                new Uri($"http://{stream.EdgeIp}:1985"))
        };

        return new NostrStreamManager(_loggerFactory.CreateLogger<NostrStreamManager>(), ctx, _serviceProvider);
    }

    public async Task<IStreamManager> ForStream(Guid id)
    {
        var stream = await _db.Streams
            .AsNoTracking()
            .Include(a => a.User)
            .Include(a => a.Endpoint)
            .Include(a => a.StreamKey)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (stream == default) throw new Exception("No live stream");

        var ctx = new StreamManagerContext
        {
            Db = _db,
            StreamKey = stream.StreamKey?.Key ?? stream.User.StreamKey,
            UserStream = stream,
            EdgeApi = new SrsApi(_serviceProvider.GetRequiredService<HttpClient>(),
                new Uri($"http://{stream.EdgeIp}:1985"))
        };

        return new NostrStreamManager(_loggerFactory.CreateLogger<NostrStreamManager>(), ctx, _serviceProvider);
    }

    public async Task<IStreamManager> ForCurrentStream(string pubkey)
    {
        var stream = await _db.Streams
            .AsNoTracking()
            .Include(a => a.User)
            .Include(a => a.Endpoint)
            .Include(a => a.StreamKey)
            .FirstOrDefaultAsync(a => a.PubKey.Equals(pubkey) && a.State == UserStreamState.Live);

        if (stream == default) throw new Exception("No live stream");

        var ctx = new StreamManagerContext
        {
            Db = _db,
            StreamKey = stream.StreamKey?.Key ?? stream.User.StreamKey,
            UserStream = stream,
            EdgeApi = new SrsApi(_serviceProvider.GetRequiredService<HttpClient>(),
                new Uri($"http://{stream.EdgeIp}:1985"))
        };

        return new NostrStreamManager(_loggerFactory.CreateLogger<NostrStreamManager>(), ctx, _serviceProvider);
    }

    public async Task<IStreamManager> ForStream(StreamInfo info)
    {
        var stream = await _db.Streams
            .AsNoTracking()
            .Include(a => a.User)
            .Include(a => a.Endpoint)
            .Include(a => a.StreamKey)
            .OrderByDescending(a => a.Starts)
            .FirstOrDefaultAsync(a =>
                (a.StreamKey != default && a.StreamKey.Key == info.StreamKey) ||
                (a.User.StreamKey.Equals(info.StreamKey) &&
                 a.Endpoint.App.Equals(info.App) &&
                 a.State == UserStreamState.Live));

        if (stream == default)
        {
            throw new Exception("No stream found");
        }

        var ctx = new StreamManagerContext
        {
            Db = _db,
            StreamKey = info.StreamKey,
            UserStream = stream,
            StreamInfo = info,
            EdgeApi = new SrsApi(_serviceProvider.GetRequiredService<HttpClient>(),
                new Uri($"http://{stream.EdgeIp}:1985"))
        };

        return new NostrStreamManager(_loggerFactory.CreateLogger<NostrStreamManager>(), ctx, _serviceProvider);
    }
}