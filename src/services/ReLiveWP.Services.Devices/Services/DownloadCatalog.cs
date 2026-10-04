using Microsoft.Extensions.Options;
using ReLiveWP.Services.Devices.Models;

namespace ReLiveWP.Services.Devices.Services;

public class DownloadCatalog(IOptionsMonitor<DownloadCatalogOptions> options)
{
    public IReadOnlyList<CatalogDevice> ListDevices()
    {
        var devices = options.CurrentValue.Devices;
        return devices
            .Select(pair => CreateDevice(pair.Key, pair.Value))
            .OrderBy(d => RankSupport(d.Support))
            .ThenBy(d => d.Manufacturer, StringComparer.OrdinalIgnoreCase)
            .ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public CatalogDevice? FindDevice(string deviceId)
    {
        var devices = options.CurrentValue.Devices;
        var match = devices.FirstOrDefault(pair => string.Equals(pair.Key, deviceId, StringComparison.OrdinalIgnoreCase));
        return match.Value == null ? null : CreateDevice(match.Key, match.Value);
    }

    public CatalogRom? FindRom(string romId)
    {
        var roms = options.CurrentValue.Roms;
        var match = roms.FirstOrDefault(pair => string.Equals(pair.Key, romId, StringComparison.OrdinalIgnoreCase));
        return match.Value == null ? null : CreateRom(match.Key, match.Value);
    }

    public IReadOnlyList<CatalogRom> ListRomsForDevice(string deviceId)
    {
        var roms = options.CurrentValue.Roms;
        return roms
            .Where(pair => string.Equals(pair.Value.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
            .Select(pair => CreateRom(pair.Key, pair.Value))
            .OrderByDescending(r => r.Released ?? DateOnly.MinValue)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyDictionary<string, int> CountRomsByDevice()
    {
        var roms = options.CurrentValue.Roms;
        return roms.Values
            .CountBy(rom => rom.DeviceId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(StringComparer.OrdinalIgnoreCase);
    }

    private static CatalogDevice CreateDevice(string id, CatalogDeviceOptions device)
    {
        var modelCodes = (device.ModelCodes ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new CatalogDevice(id, device.Name, device.Manufacturer, modelCodes, device.ImageModel, device.Support, device.Notes);
    }

    private static CatalogRom CreateRom(string id, CatalogRomOptions rom) => new(
        id,
        rom.DeviceId,
        rom.Name,
        rom.Version,
        rom.OsVersion,
        rom.Released,
        rom.SizeBytes,
        rom.Sha256,
        rom.ObjectKey,
        rom.FileName,
        rom.Notes);

    private static int RankSupport(DeviceSupport support) => support switch
    {
        DeviceSupport.Supported => 0,
        DeviceSupport.Partial => 1,
        DeviceSupport.Untested => 2,
        _ => 3,
    };
}
