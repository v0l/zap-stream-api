using Amazon.S3;
using Amazon.S3.Model;
using FFMpegCore;

namespace NostrStreamer.Services.Dvr;

public class S3DvrStore : IDvrStore
{
    private readonly AmazonS3Client _client;
    private readonly S3BlobConfig _config;
    private readonly HttpClient _httpClient;

    public S3DvrStore(Config config, HttpClient httpClient)
    {
        _httpClient = httpClient;
        _config = config.DvrStore;
        _client = config.DvrStore.CreateClient();
    }

    public async Task<UploadResult> UploadRecording(Uri source)
    {
        var tmpFile = Path.GetTempFileName();
        var recordingId = Guid.NewGuid();
        var dvrSeg = await _httpClient.GetStreamAsync(source);
        
        await using var fs = new FileStream(tmpFile, FileMode.Create, FileAccess.ReadWrite);
        await dvrSeg.CopyToAsync(fs);
        fs.Seek(0, SeekOrigin.Begin);
        var probe = await FFProbe.AnalyseAsync(tmpFile);
        fs.Seek(0, SeekOrigin.Begin);
        
        var key = $"{recordingId}.mp4";
        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _config.BucketName,
            Key = key,
            InputStream = fs,
            AutoCloseStream = false,
            AutoResetStreamPosition = false,
            ContentType = "video/mp4",
            DisablePayloadSigning = _config.DisablePayloadSigning
        });

        var url = _client.GetPreSignedURL(new()
        {
            BucketName = _config.BucketName,
            Key = key,
            Expires = new DateTime(3000, 1, 1)
        });

        var ret = new UriBuilder(url)
        {
            Scheme = _config.ServiceUrl.Scheme
        };

        return new(ret.Uri, probe.Duration.TotalSeconds);
    }
}
