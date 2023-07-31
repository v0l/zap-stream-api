using System.Diagnostics;
using Amazon.S3;
using Amazon.S3.Model;
using FFMpegCore;
using NostrStreamer.Database;

namespace NostrStreamer.Services.Dvr;

public class S3DvrStore : IDvrStore
{
    private readonly AmazonS3Client _client;
    private readonly S3BlobConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<S3DvrStore> _logger;

    public S3DvrStore(Config config, HttpClient httpClient, ILogger<S3DvrStore> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _config = config.DvrStore;
        _client = config.DvrStore.CreateClient();
    }

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
        var dl = await _httpClient.GetStreamAsync(source);
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
            Expires = new DateTime(3000, 1, 1)
        });

        var ub = new UriBuilder(url)
        {
            Scheme = _config.PublicHost.Scheme,
            Host = _config.PublicHost.Host
        };

        var tsUpload = sw.Elapsed;

        _logger.LogInformation("download={tc:#,##0}ms, probe={pc:#,##0}ms, upload={uc:#,##0}ms", tsDownload.TotalMilliseconds,
            tsProbe.TotalMilliseconds, tsUpload.TotalMilliseconds);

        return new(ub.Uri, probe.Duration.TotalSeconds);
    }
}
