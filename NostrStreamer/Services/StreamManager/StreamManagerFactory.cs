using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Nostr.Client.Json;
using NostrStreamer.Database;
using NostrStreamer.Services.Dvr;

namespace NostrStreamer.Services.StreamManager;

public class StreamManagerFactory
{
    private readonly StreamerContext _db;
    private readonly ILoggerFactory _loggerFactory;
    private readonly StreamEventBuilder _eventBuilder;
    private readonly SrsApi _srsApi;
    private readonly IDvrStore _dvrStore;

    public StreamManagerFactory(StreamerContext db, ILoggerFactory loggerFactory, StreamEventBuilder eventBuilder,
        SrsApi srsApi, IDvrStore dvrStore)
    {
        _db = db;
        _loggerFactory = loggerFactory;
        _eventBuilder = eventBuilder;
        _srsApi = srsApi;
        _dvrStore = dvrStore;
    }

    public async Task<IStreamManager> ForStream(Guid id)
    {
        var currentStream = await _db.Streams
            .AsNoTracking()
            .Include(a => a.User)
            .Include(a => a.Endpoint)
            .Include(a => a.Recordings)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (currentStream == default) throw new Exception("No live stream");

        var ctx = new StreamManagerContext
        {
            Db = _db,
            UserStream = currentStream
        };

        return new NostrStreamManager(_loggerFactory.CreateLogger<NostrStreamManager>(), ctx, _eventBuilder, _srsApi, _dvrStore);
    }

    public async Task<IStreamManager> ForCurrentStream(string pubkey)
    {
        var currentStream = await _db.Streams
            .AsNoTracking()
            .Include(a => a.User)
            .Include(a => a.Endpoint)
            .Include(a => a.Recordings)
            .FirstOrDefaultAsync(a => a.PubKey.Equals(pubkey) && a.State == UserStreamState.Live);

        if (currentStream == default) throw new Exception("No live stream");

        var ctx = new StreamManagerContext
        {
            Db = _db,
            UserStream = currentStream
        };

        return new NostrStreamManager(_loggerFactory.CreateLogger<NostrStreamManager>(), ctx, _eventBuilder, _srsApi, _dvrStore);
    }

    public async Task<IStreamManager> ForStream(StreamInfo info)
    {
        var stream = await _db.Streams
            .AsNoTracking()
            .Include(a => a.User)
            .Include(a => a.Endpoint)
            .Include(a => a.Recordings)
            .FirstOrDefaultAsync(a =>
                a.StreamId.Equals(info.StreamId) &&
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

            // create new stream entry for source only
            if (info.Variant == "source")
            {
                stream = new()
                {
                    EndpointId = ep.Id,
                    PubKey = user.PubKey,
                    StreamId = info.StreamId,
                    State = UserStreamState.Planned
                };

                var ev = _eventBuilder.CreateStreamEvent(user, stream);
                stream.Event = JsonConvert.SerializeObject(ev, NostrSerializer.Settings);
                _db.Streams.Add(stream);
                await _db.SaveChangesAsync();
            }

            // replace again with new values
            stream = new()
            {
                Id = stream?.Id ?? Guid.NewGuid(),
                User = user,
                Endpoint = ep,
                StreamId = info.StreamId,
                State = UserStreamState.Planned,
            };
        }

        var ctx = new StreamManagerContext
        {
            Db = _db,
            UserStream = stream,
            StreamInfo = info
        };

        return new NostrStreamManager(_loggerFactory.CreateLogger<NostrStreamManager>(), ctx, _eventBuilder, _srsApi, _dvrStore);
    }
}
