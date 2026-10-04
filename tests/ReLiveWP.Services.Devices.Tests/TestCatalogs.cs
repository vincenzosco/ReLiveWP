using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReLiveWP.Services.Devices.Models;
using ReLiveWP.Services.Devices.Services;

namespace ReLiveWP.Services.Devices.Tests;

internal static class TestCatalogs
{
    public const string SampleIni = """
        [DownloadCatalog:Devices:samsung-omnia-7]
        Name=Omnia 7
        Manufacturer=Samsung
        ModelCodes=GT-I8700
        ImageModel=CETUS

        [DownloadCatalog:Devices:lumia-710]
        Name=Lumia 710
        Manufacturer=Nokia
        ModelCodes=RM-803
        ImageModel=RM-803
        Support=partial

        [DownloadCatalog:Devices:lumia-800]
        Name=Lumia 800
        Manufacturer=Nokia
        ModelCodes=RM-801, RM-802
        ImageModel=RM-801
        Support=Supported

        [DownloadCatalog:Roms:relive-78-800]
        DeviceId=lumia-800
        Name=ReLiveWP 7.8
        Version=1.0
        OsVersion=7.10.8862
        Released=2026-09-01
        SizeBytes=524288000
        Sha256=00
        ObjectKey=lumia-800/relive-78.zip
        FileName=relive-78-lumia-800.zip

        [DownloadCatalog:Roms:relive-75-800]
        DeviceId=lumia-800
        Name=ReLiveWP 7.5
        Version=0.9
        Released=2026-06-01
        SizeBytes=1024
        ObjectKey=lumia-800/relive-75.zip
        FileName=relive-75-lumia-800.zip

        [DownloadCatalog:Roms:relive-78-710]
        DeviceId=lumia-710
        Name=ReLiveWP 7.8
        Version=1.0
        SizeBytes=2048
        ObjectKey=lumia-710/relive-78.zip
        FileName=relive-78-lumia-710.zip
        """;

    public static DownloadCatalog CreateCatalog(string ini = SampleIni)
    {
        var configuration = new ConfigurationBuilder()
            .AddIniStream(new MemoryStream(Encoding.UTF8.GetBytes(ini)))
            .Build();
        return CreateCatalog(configuration);
    }

    public static DownloadCatalog CreateCatalog(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.Configure<DownloadCatalogOptions>(configuration.GetSection(DownloadCatalogOptions.SectionName));
        var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptionsMonitor<DownloadCatalogOptions>>();
        return new DownloadCatalog(options);
    }

    public static DownloadLinkSigner CreateSigner(TimeSpan? linkLifetime = null)
    {
        var options = new DownloadStorageOptions()
        {
            AccountId = "0123456789abcdef",
            Bucket = "relivewp-roms",
            AccessKeyId = "test-access-key",
            SecretAccessKey = "test-secret-key",
            LinkLifetime = linkLifetime ?? TimeSpan.FromHours(6),
        };
        return new DownloadLinkSigner(Options.Create(options));
    }

    public static CatalogRom CreateRom(string objectKey, string fileName) => new(
        "test-rom", "lumia-800", "Test ROM", "1.0", null, null, 0, null, objectKey, fileName, null);
}
