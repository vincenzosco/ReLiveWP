using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using ReLiveWP.Services.Devices.Models;

namespace ReLiveWP.Services.Devices.Services;

public sealed class DownloadLinkSigner : IDisposable
{
    private readonly DownloadStorageOptions options;
    private readonly AmazonS3Client? client;

    public DownloadLinkSigner(IOptions<DownloadStorageOptions> options)
    {
        this.options = options.Value;
        if (!this.options.IsConfigured)
            return;

        var config = new AmazonS3Config()
        {
            ServiceURL = $"https://{this.options.AccountId}.r2.cloudflarestorage.com",
            AuthenticationRegion = "auto",
            ForcePathStyle = true,
        };
        var credentials = new BasicAWSCredentials(this.options.AccessKeyId, this.options.SecretAccessKey);
        client = new AmazonS3Client(credentials, config);
    }

    public bool IsConfigured => client != null;

    public async Task<DownloadLinkModel> SignRomLinkAsync(CatalogRom rom)
    {
        if (client == null)
            throw new InvalidOperationException("ROM storage is not configured");

        var expiresAt = DateTimeOffset.UtcNow + options.LinkLifetime;
        var request = new GetPreSignedUrlRequest()
        {
            BucketName = options.Bucket,
            Key = rom.ObjectKey,
            Verb = HttpVerb.GET,
            Expires = expiresAt.UtcDateTime,
        };
        request.ResponseHeaderOverrides.ContentDisposition = FormatContentDisposition(rom.FileName);

        var url = await client.GetPreSignedURLAsync(request);
        return new DownloadLinkModel(url, expiresAt);
    }

    private static string FormatContentDisposition(string fileName)
    {
        var disposition = new ContentDispositionHeaderValue("attachment");
        disposition.SetHttpFileName(fileName);
        return disposition.ToString();
    }

    public void Dispose() => client?.Dispose();
}
