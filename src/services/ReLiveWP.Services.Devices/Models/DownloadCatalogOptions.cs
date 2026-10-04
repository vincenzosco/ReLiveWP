namespace ReLiveWP.Services.Devices.Models;

public class DownloadCatalogOptions
{
    public const string SectionName = "DownloadCatalog";

    public Dictionary<string, CatalogDeviceOptions> Devices { get; set; } = [];
    public Dictionary<string, CatalogRomOptions> Roms { get; set; } = [];
}

public class CatalogDeviceOptions
{
    public string Name { get; set; } = "";
    public string Manufacturer { get; set; } = "";
    public string? ModelCodes { get; set; }
    public string ImageModel { get; set; } = "";
    public DeviceSupport Support { get; set; } = DeviceSupport.Untested;
    public string? Notes { get; set; }
}

public class CatalogRomOptions
{
    public string DeviceId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string? OsVersion { get; set; }
    public DateOnly? Released { get; set; }
    public long SizeBytes { get; set; }
    public string? Sha256 { get; set; }
    public string ObjectKey { get; set; } = "";
    public string FileName { get; set; } = "";
    public string? Notes { get; set; }
}

public enum DeviceSupport
{
    Untested,
    Supported,
    Partial,
    Unsupported,
}

public record CatalogDevice(
    string Id,
    string Name,
    string Manufacturer,
    IReadOnlyList<string> ModelCodes,
    string ImageModel,
    DeviceSupport Support,
    string? Notes);

public record CatalogRom(
    string Id,
    string DeviceId,
    string Name,
    string Version,
    string? OsVersion,
    DateOnly? Released,
    long SizeBytes,
    string? Sha256,
    string ObjectKey,
    string FileName,
    string? Notes);
