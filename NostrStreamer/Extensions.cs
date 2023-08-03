using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using MaxMind.GeoIP2;
using Newtonsoft.Json;
using Nostr.Client.Json;
using Nostr.Client.Keys;
using Nostr.Client.Messages;
using NostrStreamer.Database;

namespace NostrStreamer;

public static class Extensions
{
    public static NostrEvent? GetEvent(this UserStream us)
    {
        return JsonConvert.DeserializeObject<NostrEvent>(us.Event, NostrSerializer.Settings);
    }

    public static string GetPubKey(this Config cfg)
    {
        return NostrPrivateKey.FromBech32(cfg.PrivateKey).DerivePublicKey().Hex;
    }

    public static NostrPrivateKey GetPrivateKey(this Config cfg)
    {
        return NostrPrivateKey.FromBech32(cfg.PrivateKey);
    }

    public static List<Variant> GetVariants(this IngestEndpoint ep)
    {
        return ep.Capabilities
            .Where(a => a.StartsWith("variant"))
            .Select(Variant.FromString).ToList();
    }

    public static AmazonS3Client CreateClient(this S3BlobConfig c)
    {
        return new AmazonS3Client(new BasicAWSCredentials(c.AccessKey, c.SecretKey),
            new AmazonS3Config
            {
                RegionEndpoint = !string.IsNullOrEmpty(c.Region) ? RegionEndpoint.GetBySystemName(c.Region) : null,
                ServiceURL = c.ServiceUrl.ToString(),
                UseHttp = c.ServiceUrl.Scheme == "http",
                ForcePathStyle = true
            });
    }

    public static string[] SplitTags(this User user)
    {
        return !string.IsNullOrEmpty(user.Tags) ?
            user.Tags.Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) : Array.Empty<string>();
    }

    public static (double lat, double lon)? GetLocation(this HttpContext ctx, IGeoIP2DatabaseReader db)
    {
        var ip = ctx.GetRealIp();
        var loc = db.TryCity(ip, out var city) ? city?.Location : default;
        if ((loc?.Latitude.HasValue ?? false) && loc.Longitude.HasValue)
        {
            return (loc.Latitude.Value, loc.Longitude.Value);
        }

        return default;
    }

    public static string GetRealIp(this HttpContext ctx)
    {
        var cci = ctx.Request.Headers.TryGetValue("CF-Connecting-IP", out var xx) ? xx.ToString() : null;
        if (!string.IsNullOrEmpty(cci))
        {
            return cci;
        }

        var xff = ctx.Request.Headers.TryGetValue("X-Forwarded-For", out var x) ? x.ToString() : null;
        if (!string.IsNullOrEmpty(xff))
        {
            return xff.Split(",", StringSplitOptions.RemoveEmptyEntries).First();
        }

        return ctx.Connection.RemoteIpAddress!.ToString();
    }

    public static double GetDistance(double longitude, double latitude, double otherLongitude, double otherLatitude)
    {
        var d1 = latitude * (Math.PI / 180.0);
        var num1 = longitude * (Math.PI / 180.0);
        var d2 = otherLatitude * (Math.PI / 180.0);
        var num2 = otherLongitude * (Math.PI / 180.0) - num1;
        var d3 = Math.Pow(Math.Sin((d2 - d1) / 2.0), 2.0) + Math.Cos(d1) * Math.Cos(d2) * Math.Pow(Math.Sin(num2 / 2.0), 2.0);

        return 6376500.0 * (2.0 * Math.Atan2(Math.Sqrt(d3), Math.Sqrt(1.0 - d3)));
    }
}

public class Variant
{
    public int Width { get; init; }
    public int Height { get; init; }
    public int Bandwidth { get; init; }

    public string SourceName => Bandwidth == int.MaxValue ? "source" : $"{Height}h";

    /// <summary>
    /// variant:{px}h:{bandwidth}
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    /// <exception cref="FormatException"></exception>
    public static Variant FromString(string str)
    {
        if (str.Equals("variant:source", StringComparison.InvariantCultureIgnoreCase))
        {
            return new()
            {
                Width = 0,
                Height = 0,
                Bandwidth = int.MaxValue
            };
        }

        var strSplit = str.Split(":");
        if (strSplit.Length != 3 || !int.TryParse(strSplit[1][..^1], out var h) || !int.TryParse(strSplit[2], out var b))
        {
            throw new FormatException();
        }

        return new()
        {
            Height = h,
            Width = (int)Math.Ceiling(h / 9m * 16m),
            Bandwidth = b
        };
    }

    public override string ToString()
    {
        if (Bandwidth == int.MaxValue)
        {
            return "variant:source";
        }

        return $"variant:{SourceName}:{Bandwidth}";
    }

    public string ToResolutionArg()
    {
        if (Bandwidth == int.MaxValue)
        {
            return string.Empty;
        }

        return $"RESOLUTION={Width}x{Height}";
    }

    public string ToBandwidthArg()
    {
        if (Bandwidth == int.MaxValue)
        {
            return $"BANDWIDTH={20_000_000}";
        }

        return $"BANDWIDTH={Bandwidth * 1000}";
    }
}
