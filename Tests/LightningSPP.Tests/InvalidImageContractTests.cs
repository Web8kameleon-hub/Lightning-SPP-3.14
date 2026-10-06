using Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using wwwmmm.Services;
using wwwmmm.Services.Implementation;
using wwwmmm.Services.Imaging;

namespace LightningSPP.Tests;

public sealed class InvalidImageContractTests
{
    private static readonly byte[] InvalidImage =
    [
        0x4E, 0x4F, 0x54, 0x2D, 0x41, 0x4E, 0x2D, 0x49,
        0x4D, 0x41, 0x47, 0x45
    ];

    [Fact]
    public async Task Skia_MustReject_NonImageBytes()
    {
        var scanner = new RealImageProcessor();

        var result = await scanner.ScanAsync(
            InvalidImage,
            ScanMode.Lightning,
            300);

        Assert.False(
            result.Success,
            "ZERO-FAKE violation: Skia accepted arbitrary bytes as an image.");
    }

    [Fact]
    public async Task ImageSharp_MustReject_NonImageBytes()
    {
        var scanner = new FastScanService(
            NullLogger<FastScanService>.Instance);

        var result = await scanner.ScanAsync(
            InvalidImage,
            ScanMode.Lightning,
            300);

        Assert.False(
            result.Success,
            "ZERO-FAKE violation: ImageSharp accepted arbitrary bytes as an image.");
    }
}


