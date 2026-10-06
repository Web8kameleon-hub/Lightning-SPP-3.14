using System.Diagnostics;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using wwwmmm.Services;
using wwwmmm.Services.Imaging;
using wwwmmm.Services.Implementation;

namespace LightningSPP.Tests;

public class ImageEngineAdapterTests
{
    [Theory]
    [InlineData(ImageEngine.Skia, ScanMode.Lightning)]
    [InlineData(ImageEngine.ImageSharp, ScanMode.Lightning)]
    [InlineData(ImageEngine.Skia, ScanMode.TideWave)]
    [InlineData(ImageEngine.ImageSharp, ScanMode.TideWave)]
    [InlineData(ImageEngine.Skia, ScanMode.Resonance)]
    [InlineData(ImageEngine.ImageSharp, ScanMode.Resonance)]
    [InlineData(ImageEngine.Skia, ScanMode.NanoDecibel)]
    [InlineData(ImageEngine.ImageSharp, ScanMode.NanoDecibel)]
    public async Task BothRealEngines_ProcessSameRealImage(
        ImageEngine engine,
        ScanMode mode)
    {
        // Mandatory CI contract: deterministic, valid PNG decoded by both engines.
        // Real-world image verification remains a separate release/integration gate.
        var source = RealTestImage.Png();

        var skia = new RealImageProcessor();
        var imageSharp = new FastScanService(
            NullLogger<FastScanService>.Instance);

        var adapter = new ImageEngineAdapter(skia, imageSharp);

        var started = Stopwatch.GetTimestamp();

        var result = await adapter.ProcessAsync(
            engine,
            source,
            mode,
            300);

        var elapsed = Stopwatch.GetElapsedTime(started);

        Assert.True(
            result.Success,
            $"{engine}/{mode} failed: {result.Error}");

        Assert.NotEmpty(result.Data);

        // PNG signature: 89 50 4E 47 0D 0A 1A 0A
        Assert.True(result.Data.Length >= 8);
        Assert.Equal(0x89, result.Data[0]);
        Assert.Equal(0x50, result.Data[1]);
        Assert.Equal(0x4E, result.Data[2]);
        Assert.Equal(0x47, result.Data[3]);
        Assert.Equal(0x0D, result.Data[4]);
        Assert.Equal(0x0A, result.Data[5]);
        Assert.Equal(0x1A, result.Data[6]);
        Assert.Equal(0x0A, result.Data[7]);

        Console.WriteLine(
            $"LR_RESULT|{engine}|{mode}|SUCCESS|{elapsed.TotalMilliseconds:F2}|{result.Data.Length}");
    }
}


