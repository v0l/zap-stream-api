using System.Diagnostics;
using Amazon.S3;
using Amazon.S3.Model;
using FFMpegCore;
using NostrStreamer.Database;

namespace NostrStreamer.Services.Dvr;

public class S3DvrStore(Config config, HttpClient httpClient, ILogger<S3DvrStore> logger)
    : IDvrStore
{
    private readonly AmazonS3Client _client = config.S3Store.CreateClient();
    private readonly S3BlobConfig _config = config.S3Store;

    public async Task<UploadResult> UploadRecording(UserStream stream, Uri source)
    {
        var sw = Stopwatch.StartNew();

        var tmpFile = Path.GetTempFileName();
        var recordingId = Guid.NewGuid();

        /*
        var cmdTranscode = FFMpegArguments.FromUrlInput(source)
            .OutputToFile(tmpFile, true, o =>
            {
                o.WithCustomArgument("-movflags frag_keyframe+empty_moov");
                o.CopyChannel(Channel.All);
                o.ForceFormat("mp4");
            });
        _logger.LogInformation("Transcoding with: {cmd}", cmdTranscode.Arguments);
        await cmdTranscode.ProcessAsynchronously();

        var tsTranscode = sw.Elapsed;*/

        sw.Restart();
        await using var fs = new FileStream(tmpFile, FileMode.Create, FileAccess.ReadWrite);
        var dl = await httpClient.GetStreamAsync(source);
        await dl.CopyToAsync(fs);
        await fs.FlushAsync();
        fs.Seek(0, SeekOrigin.Begin);
        var tsDownload = sw.Elapsed;

        sw.Restart();
        var probe = await FFProbe.AnalyseAsync(tmpFile);
        var tsProbe = sw.Elapsed;

        sw.Restart();
        var ext = Path.GetExtension(source.AbsolutePath);
        var key = $"{stream.Id}/{recordingId}{ext}";
        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _config.BucketName,
            Key = key,
            InputStream = fs,
            AutoCloseStream = false,
            AutoResetStreamPosition = false,
            ContentType = ext == ".ts" ? "video/mp2t" : "video/mp4",
            DisablePayloadSigning = _config.DisablePayloadSigning
        });

        var url = _client.GetPreSignedURL(new()
        {
            BucketName = _config.BucketName,
            Key = key,
            Expires = DateTime.Now.AddSeconds(604800)
        });

        var ub = new UriBuilder(url)
        {
            Scheme = _config.PublicHost.Scheme,
            Host = _config.PublicHost.Host,
            Port = _config.PublicHost.Port
        };

        var tsUpload = sw.Elapsed;

        // cleanup temp file
        fs.Close();
        File.Delete(tmpFile);

        logger.LogInformation("download={tc:#,##0}ms, probe={pc:#,##0}ms, upload={uc:#,##0}ms",
            tsDownload.TotalMilliseconds,
            tsProbe.TotalMilliseconds, tsUpload.TotalMilliseconds);

        return new(recordingId, ub.Uri, probe.Duration.TotalSeconds);
    }

    public async Task<List<Guid>> DeleteRecordings(UserStream stream)
    {
        var deleted = new HashSet<Guid>();
        foreach (var batch in stream.Recordings.Select((a, i) => (Batch: i / 100, Item: a)).GroupBy(a => a.Batch))
        {
            var res = await _client.DeleteObjectsAsync(new()
            {
                BucketName = _config.BucketName,
                Objects = batch.Select(a => new KeyVersion()
                {
                    Key = $"{stream.Id}/{a.Item.Id}.ts"
                }).Concat(batch.Select(a =>
                {
                    var url = new Uri(a.Item.Url);
                    return new KeyVersion
                    {
                        Key = url.AbsolutePath.Replace($"/${_config.BucketName}", string.Empty)
                    };
                })).ToList()
            });
            foreach (var d in res.DeletedObjects)
            {
                deleted.Add(Guid.Parse(Path.GetFileNameWithoutExtension(d.Key)));
            }
        }

        if (deleted.Count == stream.Recordings.Count)
        {
            await _client.DeleteObjectAsync(new()
            {
                BucketName = _config.BucketName,
                Key = $"{stream.Id}/"
            });
        }

        return deleted.ToList();
    }
}