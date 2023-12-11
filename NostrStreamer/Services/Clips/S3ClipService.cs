using Amazon.S3;
using Microsoft.EntityFrameworkCore;
using NostrStreamer.Database;

namespace NostrStreamer.Services.Clips;

public class S3ClipService : IClipService
{
    private readonly ClipGenerator _generator;
    private readonly AmazonS3Client _client;
    private readonly Config _config;
    private readonly StreamerContext _context;

    public S3ClipService(ClipGenerator generator, Config config, StreamerContext context)
    {
        _generator = generator;
        _client = config.S3Store.CreateClient();
        ;
        _config = config;
        _context = context;
    }

    public async Task<List<ClipSegment>?> PrepareClip(Guid streamId)
    {
        var stream = await _context.Streams
            .Include(a => a.User)
            .Include(a => a.Endpoint)
            .FirstOrDefaultAsync(a => a.Id == streamId);

        if (stream == default)
        {
            return default;
        }

        return await _generator.GetClipSegments(stream);
    }

    public async Task<ClipResult?> MakeClip(string takenBy, List<ClipSegment> segments, float start, float length)
    {
        if (segments.Count == 0) return default;

        var streamId = segments.First().Id;
        var clip = await _generator.CreateClipFromSegments(segments, start, length);
        var clipId = Guid.NewGuid();
        var s3Path = $"{streamId}/clips/{clipId}.mp4";

        await using var fs = new FileStream(clip, FileMode.Open, FileAccess.Read);
        await _client.PutObjectAsync(new()
        {
            BucketName = _config.S3Store.BucketName,
            Key = s3Path,
            InputStream = fs,
            AutoCloseStream = false,
            AutoResetStreamPosition = false,
            ContentType = "video/mp4",
            DisablePayloadSigning = _config.S3Store.DisablePayloadSigning
        });

        var uri = _client.GetPreSignedURL(new()
        {
            BucketName = _config.S3Store.BucketName,
            Key = s3Path,
            Expires = DateTime.UtcNow.AddYears(1000)
        });

        var ub = new UriBuilder(uri)
        {
            Scheme = _config.S3Store.PublicHost.Scheme,
            Host = _config.S3Store.PublicHost.Host,
            Port = _config.S3Store.PublicHost.Port
        };


        var clipObj = new UserStreamClip()
        {
            Id = clipId,
            UserStreamId = streamId,
            TakenByPubkey = takenBy,
            Url = ub.Uri.ToString()
        };

        _context.Clips.Add(clipObj);
        await _context.SaveChangesAsync();

        return new(ub.Uri);
    }
}
