namespace ReLiveWP.Services.Devices.Models;

public record DownloadDeviceModel(
    string Id,
    string Name,
    string Manufacturer,
    IReadOnlyList<string> ModelCodes,
    string ImageModel,
    string Support,
    string? Notes,
    int RomCount);

public record DownloadDeviceDetailModel(
    DownloadDeviceModel Device,
    IReadOnlyList<DownloadRomModel> Roms);

public record DownloadRomModel(
    string Id,
    string Name,
    string Version,
    string? OsVersion,
    DateOnly? Released,
    long SizeBytes,
    string? Sha256,
    string FileName,
    string? Notes);

public record DownloadLinkModel(string Url, DateTimeOffset ExpiresAt);
