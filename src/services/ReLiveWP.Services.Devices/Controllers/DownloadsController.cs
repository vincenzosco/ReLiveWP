using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ReLiveWP.Identity;
using ReLiveWP.Services.Devices.Models;
using ReLiveWP.Services.Devices.Services;

namespace ReLiveWP.Services.Devices.Controllers;

[ApiController]
[Route("downloads")]
public class DownloadsController(
    ILogger<DownloadsController> logger,
    DownloadCatalog catalog,
    DownloadLinkSigner linkSigner) : ControllerBase
{
    public const string RomLinkRateLimit = "RomLink";

    private const string CatalogCacheControl = "public, max-age=300";

    [HttpGet("devices")]
    [AllowAnonymous]
    public IReadOnlyList<DownloadDeviceModel> ListDevices()
    {
        Response.Headers.CacheControl = CatalogCacheControl;

        var devices = catalog.ListDevices();
        var romCounts = catalog.CountRomsByDevice();
        return devices
            .Select(d => CreateDeviceModel(d, romCounts.GetValueOrDefault(d.Id)))
            .ToList();
    }

    [HttpGet("devices/{deviceId}")]
    [AllowAnonymous]
    public ActionResult<DownloadDeviceDetailModel> GetDevice(string deviceId)
    {
        var device = catalog.FindDevice(deviceId);
        if (device == null)
            return NotFound();

        Response.Headers.CacheControl = CatalogCacheControl;

        var roms = catalog.ListRomsForDevice(device.Id);
        var romModels = roms.Select(CreateRomModel).ToList();
        var deviceModel = CreateDeviceModel(device, romModels.Count);
        return new DownloadDeviceDetailModel(deviceModel, romModels);
    }

    [HttpPost("roms/{romId}/link")]
    [Authorize]
    [EnableRateLimiting(RomLinkRateLimit)]
    public async Task<ActionResult<DownloadLinkModel>> CreateRomLinkAsync(string romId)
    {
        var rom = catalog.FindRom(romId);
        if (rom == null)
            return NotFound();

        if (!linkSigner.IsConfigured)
        {
            logger.LogError("ROM link requested for {RomId} but ROM storage is not configured", rom.Id);
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var link = await linkSigner.SignRomLinkAsync(rom);

        logger.LogInformation("Signed ROM link for {RomId} for user {UserId}, expires {ExpiresAt}", rom.Id, User.Id(), link.ExpiresAt);
        DevicesMetrics.RecordDownloadLinkSigned(rom.Id);

        return link;
    }

    private static DownloadDeviceModel CreateDeviceModel(CatalogDevice device, int romCount) => new(
        device.Id,
        device.Name,
        device.Manufacturer,
        device.ModelCodes,
        device.ImageModel,
        device.Support.ToString().ToLowerInvariant(),
        device.Notes,
        romCount);

    private static DownloadRomModel CreateRomModel(CatalogRom rom) => new(
        rom.Id,
        rom.Name,
        rom.Version,
        rom.OsVersion,
        rom.Released,
        rom.SizeBytes,
        rom.Sha256,
        rom.FileName,
        rom.Notes);
}
