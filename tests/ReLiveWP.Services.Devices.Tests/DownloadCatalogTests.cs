using Microsoft.Extensions.Configuration;
using ReLiveWP.Services.Devices.Models;

namespace ReLiveWP.Services.Devices.Tests;

public class DownloadCatalogTests
{
    [Fact]
    public void BindsDevicesAndRomsFromIni()
    {
        var catalog = TestCatalogs.CreateCatalog();

        var lumia800 = catalog.FindDevice("lumia-800");
        Assert.NotNull(lumia800);
        Assert.Equal("Lumia 800", lumia800.Name);
        Assert.Equal(["RM-801", "RM-802"], lumia800.ModelCodes);
        Assert.Equal(DeviceSupport.Supported, lumia800.Support);
        Assert.Equal(DeviceSupport.Partial, catalog.FindDevice("lumia-710")?.Support);
        Assert.Equal(DeviceSupport.Untested, catalog.FindDevice("samsung-omnia-7")?.Support);

        var rom = catalog.FindRom("relive-78-800");
        Assert.NotNull(rom);
        Assert.Equal("relive-78-800", rom.Id);
        Assert.Equal("lumia-800", rom.DeviceId);
        Assert.Equal(new DateOnly(2026, 9, 1), rom.Released);
        Assert.Equal(524288000, rom.SizeBytes);
        Assert.Equal("lumia-800/relive-78.zip", rom.ObjectKey);
    }

    [Fact]
    public void DevicesSortBySupportThenName()
    {
        var catalog = TestCatalogs.CreateCatalog();

        var devices = catalog.ListDevices();

        Assert.Equal(["lumia-800", "lumia-710", "samsung-omnia-7"], devices.Select(d => d.Id));
    }

    [Fact]
    public void RomsListOnlyForTheirDeviceNewestFirst()
    {
        var catalog = TestCatalogs.CreateCatalog();

        var lumia800 = catalog.ListRomsForDevice("lumia-800").Select(r => r.Id).ToList();
        var lumia710 = catalog.ListRomsForDevice("lumia-710").Select(r => r.Id).ToList();

        Assert.Equal(["relive-78-800", "relive-75-800"], lumia800);
        Assert.Equal(["relive-78-710"], lumia710);
    }

    [Fact]
    public void LookupsIgnoreCase()
    {
        var catalog = TestCatalogs.CreateCatalog();

        Assert.Equal("lumia-800", catalog.FindDevice("Lumia-800")?.Id);
        Assert.NotNull(catalog.FindRom("RELIVE-78-800"));
        Assert.Equal(2, catalog.ListRomsForDevice("LUMIA-800").Count);
    }

    [Fact]
    public void UnknownIdsReturnNothing()
    {
        var catalog = TestCatalogs.CreateCatalog();

        Assert.Null(catalog.FindDevice("lumia-1020"));
        Assert.Null(catalog.FindRom("nope"));
        Assert.Empty(catalog.ListRomsForDevice("lumia-1020"));
    }

    [Fact]
    public void DeviceWithNoRomsListsEmpty()
    {
        var catalog = TestCatalogs.CreateCatalog();

        Assert.NotNull(catalog.FindDevice("samsung-omnia-7"));
        Assert.Empty(catalog.ListRomsForDevice("samsung-omnia-7"));
    }

    [Fact]
    public void MissingCatalogIsEmpty()
    {
        var catalog = TestCatalogs.CreateCatalog("");

        Assert.Empty(catalog.ListDevices());
        Assert.Null(catalog.FindRom("relive-78-800"));
    }

    [Fact]
    public void ExampleCatalogBindsAndRomsPointAtKnownDevices()
    {
        var examplePath = Path.Combine(FindRepoRoot(), "deploy", "example", "data", "downloads", "catalog.ini");
        var configuration = new ConfigurationBuilder()
            .AddIniFile(examplePath, optional: false)
            .Build();
        var catalog = TestCatalogs.CreateCatalog(configuration);

        var devices = catalog.ListDevices();
        Assert.NotEmpty(devices);
        Assert.All(devices, d =>
        {
            Assert.False(string.IsNullOrWhiteSpace(d.Name));
            Assert.NotEmpty(d.ModelCodes);
        });

        var romIds = configuration.GetSection("DownloadCatalog:Roms").GetChildren().Select(s => s.Key);
        Assert.All(romIds, id =>
        {
            var rom = catalog.FindRom(id);
            Assert.NotNull(rom);
            Assert.NotNull(catalog.FindDevice(rom.DeviceId));
        });
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ReLiveWP.slnx")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
