using Amazon;
using Amazon.Runtime;
using Amazon.S3;
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
