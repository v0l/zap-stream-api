using System.Diagnostics;
using Amazon.S3;
using Microsoft.EntityFrameworkCore;
using NostrStreamer.Database;

namespace NostrStreamer.Services.Thumbnail;

public class S3ThumbnailService(Config config, ILogger<S3ThumbnailService> logger, StreamerContext context)
    : BaseThumbnailService(config, logger), IThumbnailService
{
    private readonly AmazonS3Client _client = config.S3Store.CreateClient();

    public async Task GenerateThumb(UserStream stream)
    {
        try
        {
            var sw = Stopwatch.StartNew();
            var path = await GenerateThumbnail(stream);
            var tGen = sw.Elapsed;
            var s3Path = MapPath(stream.Id);

            sw.Restart();
            await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            await _client.PutObjectAsync(new()
            {
                BucketName = Config.S3Store.BucketName,
                Key = s3Path,
                InputStream = fs,
                AutoCloseStream = false,
                AutoResetStreamPosition = false,
                ContentType = "image/jpeg",
                DisablePayloadSigning = Config.S3Store.DisablePayloadSigning
            });

            var uri = _client.GetPreSignedURL(new()
            {
                BucketName = Config.S3Store.BucketName,
                Key = s3Path,
                Expires = DateTime.UtcNow.AddYears(1000)
            });

            var ub = new UriBuilder(uri)
            {
                Scheme = Config.S3Store.PublicHost.Scheme,
                Host = Config.S3Store.PublicHost.Host,
                Port = Config.S3Store.PublicHost.Port
            };

            var tUpload = sw.Elapsed;
            sw.Restart();
            await context.Streams.Where(a => a.Id == stream.Id)
                .ExecuteUpdateAsync(o => o.SetProperty(v => v.Thumbnail, ub.Uri.ToString()));

            var tDbUpdate = sw.Elapsed;
            
            stream.Thumbnail = ub.Uri.ToString();
            
            fs.Close();
            File.Delete(path);

            Logger.LogInformation("{id} generated={tg:#,##0}ms, uploaded={tu:#,##0}ms, db={td:#,##0}ms", stream.Id, tGen.TotalMilliseconds,
                tUpload.TotalMilliseconds, tDbUpdate.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Failed to generate {id} thumbnail {msg}", stream.Id, ex.Message);
        }
    }

    private string MapPath(Guid id)
    {
        return $"{id}/thumb.jpg";
    }
}
