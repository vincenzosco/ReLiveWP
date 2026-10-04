using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using ReLiveWP.Services.Devices.Models;
using ReLiveWP.Services.Devices.Services;

namespace ReLiveWP.Services.Devices.Tests;

public class RomLinkSignerTests
{
    private static readonly CatalogRom Rom = TestCatalogs.CreateRom("lumia-800/relive-78.zip", "relive-78-lumia-800.zip");

    [Theory]
    [InlineData(null, "relivewp-roms", "key", "secret")]
    [InlineData("account", "", "key", "secret")]
    [InlineData("account", "relivewp-roms", " ", "secret")]
    [InlineData("account", "relivewp-roms", "key", null)]
    public async Task NotConfiguredWithoutEverySetting(string? accountId, string? bucket, string? accessKeyId, string? secretAccessKey)
    {
        var options = new DownloadStorageOptions()
        {
            AccountId = accountId,
            Bucket = bucket,
            AccessKeyId = accessKeyId,
            SecretAccessKey = secretAccessKey,
        };
        using var signer = new DownloadLinkSigner(Options.Create(options));

        Assert.False(signer.IsConfigured);
        await Assert.ThrowsAsync<InvalidOperationException>(() => signer.SignRomLinkAsync(Rom));
    }

    [Fact]
    public async Task SignsAgainstTheAccountEndpointPathStyle()
    {
        using var signer = TestCatalogs.CreateSigner();

        var link = await signer.SignRomLinkAsync(Rom);
        var url = new Uri(link.Url);

        Assert.Equal("https", url.Scheme);
        Assert.Equal("0123456789abcdef.r2.cloudflarestorage.com", url.Host);
        Assert.Equal("/relivewp-roms/lumia-800/relive-78.zip", url.AbsolutePath);
    }

    [Fact]
    public async Task SignsWithSigV4ForTheAutoRegion()
    {
        using var signer = TestCatalogs.CreateSigner();

        var link = await signer.SignRomLinkAsync(Rom);
        var query = QueryHelpers.ParseQuery(new Uri(link.Url).Query);

        Assert.Equal("AWS4-HMAC-SHA256", query["X-Amz-Algorithm"].ToString());
        Assert.StartsWith("test-access-key/", query["X-Amz-Credential"].ToString());
        Assert.EndsWith("/auto/s3/aws4_request", query["X-Amz-Credential"].ToString());
        Assert.False(string.IsNullOrEmpty(query["X-Amz-Signature"].ToString()));
        Assert.DoesNotContain("test-secret-key", link.Url);
    }

    [Fact]
    public async Task ExpiryMatchesTheConfiguredLifetime()
    {
        var lifetime = TimeSpan.FromHours(6);
        using var signer = TestCatalogs.CreateSigner(lifetime);

        var before = DateTimeOffset.UtcNow;
        var link = await signer.SignRomLinkAsync(Rom);
        var query = QueryHelpers.ParseQuery(new Uri(link.Url).Query);
        var expiresSeconds = int.Parse(query["X-Amz-Expires"].ToString());

        Assert.InRange(expiresSeconds, lifetime.TotalSeconds - 5, lifetime.TotalSeconds);
        Assert.InRange(link.ExpiresAt, before + lifetime, DateTimeOffset.UtcNow + lifetime);
    }

    [Fact]
    public async Task AsksR2ToServeItAsAnAttachmentWithTheCatalogFileName()
    {
        using var signer = TestCatalogs.CreateSigner();

        var link = await signer.SignRomLinkAsync(Rom);
        var query = QueryHelpers.ParseQuery(new Uri(link.Url).Query);
        var disposition = query["response-content-disposition"].ToString();

        Assert.StartsWith("attachment;", disposition);
        Assert.Contains("filename=relive-78-lumia-800.zip", disposition);
    }

    [Fact]
    public async Task QuotesFileNamesThatNeedIt()
    {
        using var signer = TestCatalogs.CreateSigner();
        var rom = TestCatalogs.CreateRom("a/b.zip", "relive 7.8 \"final\".zip");

        var link = await signer.SignRomLinkAsync(rom);
        var query = QueryHelpers.ParseQuery(new Uri(link.Url).Query);
        var disposition = query["response-content-disposition"].ToString();

        Assert.Contains("filename=\"relive 7.8 \\\"final\\\".zip\"", disposition);
    }
}
