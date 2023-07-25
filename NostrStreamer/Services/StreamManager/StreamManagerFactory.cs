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
    private readonly SrsApi _srsApi;

    public StreamManagerFactory(StreamerContext db, ILoggerFactory loggerFactory, StreamEventBuilder eventBuilder,
        SrsApi srsApi)
    {
        _db = db;
        _loggerFactory = loggerFactory;
        _eventBuilder = eventBuilder;
        _srsApi = srsApi;
    }

    public async Task<IStreamManager> ForStream(Guid id)
    {
        var currentStream = await _db.Streams
            .AsNoTracking()
            .Include(a => a.User)
            .Include(a => a.Endpoint)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (currentStream == default) throw new Exception("No live stream");

        var ctx = new StreamManagerContext
        {
            Db = _db,
            UserStream = currentStream
        };

        return new NostrStreamManager(_loggerFactory.CreateLogger<NostrStreamManager>(), ctx, _eventBuilder, _srsApi);
    }
    
    public async Task<IStreamManager> ForCurrentStream(string pubkey)
    {
        var currentStream = await _db.Streams
            .AsNoTracking()
            .Include(a => a.User)
            .Include(a => a.Endpoint)
            .FirstOrDefaultAsync(a => a.PubKey.Equals(pubkey) && a.State == UserStreamState.Live);

        if (currentStream == default) throw new Exception("No live stream");

        var ctx = new StreamManagerContext
        {
            Db = _db,
            UserStream = currentStream
        };

        return new NostrStreamManager(_loggerFactory.CreateLogger<NostrStreamManager>(), ctx, _eventBuilder, _srsApi);
    }

    public async Task<IStreamManager> ForStream(StreamInfo info)
    {
        var stream = await _db.Streams
            .AsNoTracking()
            .Include(a => a.User)
            .Include(a => a.Endpoint)
            .FirstOrDefaultAsync(a =>
                a.ClientId.Equals(info.ClientId) &&
                a.User.StreamKey.Equals(info.StreamKey) &&
                a.Endpoint.App.Equals(info.App));

        if (stream == default)
        {
            var user = await _db.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(a => a.StreamKey.Equals(info.StreamKey));

            if (user == default) throw new Exception("No user found");

            var ep = await _db.Endpoints
                .AsNoTracking()
                .SingleOrDefaultAsync(a => a.App.Equals(info.App));

            if (ep == default) throw new Exception("No endpoint found");

            stream = new()
            {
                EndpointId = ep.Id,
                PubKey = user.PubKey,
                ClientId = info.ClientId,
                State = UserStreamState.Planned
            };

            var ev = _eventBuilder.CreateStreamEvent(user, stream);
            stream.Event = JsonConvert.SerializeObject(ev, NostrSerializer.Settings);
            _db.Streams.Add(stream);
            await _db.SaveChangesAsync();
            
            // replace again with new values
            stream = new()
            {
                Id = stream.Id,
                User = user,
                Endpoint = ep,
                ClientId = info.ClientId,
                State = UserStreamState.Planned,
            };
        }

        var ctx = new StreamManagerContext
        {
            Db = _db,
            UserStream = stream
        };

        return new NostrStreamManager(_loggerFactory.CreateLogger<NostrStreamManager>(), ctx, _eventBuilder, _srsApi);
    }
}
