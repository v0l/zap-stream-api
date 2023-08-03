using System.Diagnostics;
using MaxMind.GeoIP2;

namespace NostrStreamer.Services;

public class EdgeSteering
{
    private readonly Config _config;
    private readonly IGeoIP2DatabaseReader _db;
    private readonly ILogger<EdgeSteering> _logger;

    public EdgeSteering(Config config, IGeoIP2DatabaseReader db, ILogger<EdgeSteering> logger)
    {
        _config = config;
        _db = db;
        _logger = logger;
    }

    public EdgeLocation? GetEdge(HttpContext ctx)
    {
        var sw = Stopwatch.StartNew();
        var loc = ctx.GetLocation(_db);
        if (loc != default)
        {
            var ret = _config.Edges.MinBy(a => Extensions.GetDistance(a.Longitude, a.Latitude, loc.Value.lon, loc.Value.lat));
            sw.Stop();
            _logger.LogTrace("Found edge in {n:#,##0.#}ms", sw.Elapsed.TotalMilliseconds);
            return ret;
        }
        
        sw.Stop();
        _logger.LogTrace("Found no edge in {n:#,##0.#}ms", sw.Elapsed.TotalMilliseconds);
        return default;
    }
}
