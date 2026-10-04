using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ReLiveWP.Services.Devices.Controllers;
using ReLiveWP.Services.Devices.Models;
using ReLiveWP.Services.Devices.Services;

namespace ReLiveWP.Services.Devices.Tests;

public class DownloadsControllerTests
{
    private static DownloadsController CreateController(DownloadLinkSigner signer)
    {
        var catalog = TestCatalogs.CreateCatalog();
        var httpContext = new DefaultHttpContext()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "test")),
        };

        return new DownloadsController(NullLogger<DownloadsController>.Instance, catalog, signer)
        {
            ControllerContext = new ControllerContext() { HttpContext = httpContext },
        };
    }

    [Fact]
    public void ListsEveryDeviceWithItsRomCount()
    {
        using var signer = TestCatalogs.CreateSigner();
        var controller = CreateController(signer);

        var devices = controller.ListDevices();

        Assert.Equal([2, 1, 0], devices.Select(d => d.RomCount));
        Assert.Equal(["supported", "partial", "untested"], devices.Select(d => d.Support));
        Assert.Equal("public, max-age=300", controller.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public void DeviceDetailCarriesRomMetadata()
    {
        using var signer = TestCatalogs.CreateSigner();
        var controller = CreateController(signer);

        var result = controller.GetDevice("lumia-800");

        var detail = Assert.IsType<DownloadDeviceDetailModel>(result.Value);
        Assert.Equal("lumia-800", detail.Device.Id);
        Assert.Equal(["relive-78-800", "relive-75-800"], detail.Roms.Select(r => r.Id));
        Assert.Equal("relive-78-lumia-800.zip", detail.Roms[0].FileName);
    }

    [Fact]
    public void DeviceWithNoRomsIsNotAnError()
    {
        using var signer = TestCatalogs.CreateSigner();
        var controller = CreateController(signer);

        var result = controller.GetDevice("samsung-omnia-7");

        var detail = Assert.IsType<DownloadDeviceDetailModel>(result.Value);
        Assert.Empty(detail.Roms);
        Assert.Equal(0, detail.Device.RomCount);
    }

    [Fact]
    public void UnknownDeviceIsNotFound()
    {
        using var signer = TestCatalogs.CreateSigner();
        var controller = CreateController(signer);

        var result = controller.GetDevice("lumia-1020");

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task UnknownRomIsNotFound()
    {
        using var signer = TestCatalogs.CreateSigner();
        var controller = CreateController(signer);

        var result = await controller.CreateRomLinkAsync("nope");

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task KnownRomGetsASignedLink()
    {
        using var signer = TestCatalogs.CreateSigner();
        var controller = CreateController(signer);

        var result = await controller.CreateRomLinkAsync("relive-78-800");

        var link = Assert.IsType<DownloadLinkModel>(result.Value);
        Assert.Contains("/relivewp-roms/lumia-800/relive-78.zip?", link.Url);
    }

    [Fact]
    public async Task UnconfiguredStorageIsServiceUnavailable()
    {
        using var signer = new DownloadLinkSigner(Options.Create(new DownloadStorageOptions()));
        var controller = CreateController(signer);

        var result = await controller.CreateRomLinkAsync("relive-78-800");

        var status = Assert.IsType<StatusCodeResult>(result.Result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status.StatusCode);
    }

    [Fact]
    public void OnlyTheLinkEndpointNeedsSignInAndIsRateLimited()
    {
        var type = typeof(DownloadsController);
        var createLink = type.GetMethod(nameof(DownloadsController.CreateRomLinkAsync))!;
        var listDevices = type.GetMethod(nameof(DownloadsController.ListDevices))!;
        var getDevice = type.GetMethod(nameof(DownloadsController.GetDevice))!;

        Assert.NotNull(createLink.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Null(createLink.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Equal(DownloadsController.RomLinkRateLimit, createLink.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName);

        Assert.NotNull(listDevices.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.NotNull(getDevice.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Null(type.GetCustomAttribute<AuthorizeAttribute>());
    }
}
