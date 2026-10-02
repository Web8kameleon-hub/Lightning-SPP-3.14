using System.Diagnostics;
using wwwmmm.PublicAPI;
using wwwmmm.SDK;
using Xunit;

namespace LightningSPP.Tests;

public class PerformanceGateTests
{
    [Fact]
    public async Task Gate_Instant_MedianUnder50ms()
    {
        var input = ImageFixture.Png();
        var median = await MedianMs(async () => await Lightning314.Instant(input), 5);
        Assert.True(median <= 50, $"Instant median={median:F2}ms exceeds gate 50ms");
    }

    [Fact]
    public async Task Gate_UltraFast_MedianUnder100ms()
    {
        var input = ImageFixture.Png();
        var median = await MedianMs(async () => await Lightning314.UltraFast(input), 5);
        Assert.True(median <= 100, $"UltraFast median={median:F2}ms exceeds gate 100ms");
    }

    [Fact]
    public async Task Gate_Lightning_MedianUnder150ms()
    {
        var input = ImageFixture.Png();
        var median = await MedianMs(async () => await Lightning314.Lightning(input), 5);
        Assert.True(median <= 150, $"Lightning median={median:F2}ms exceeds gate 150ms");
    }

    [Fact]
    public async Task Gate_Pipeline_SingleRunUnder1500ms()
    {
        LightningAPI.UseRealServices();

        var filePath = Path.Combine(Path.GetTempPath(), $"lightning-perf-{Guid.NewGuid():N}.bin");
        await File.WriteAllBytesAsync(filePath, ImageFixture.Png());

        var sw = Stopwatch.StartNew();
        var result = await LightningAPI.ExecuteLightningPipelineAsync(filePath);
        sw.Stop();

        Assert.True(result.Success, result.Error ?? "Pipeline failed");
        Assert.True(sw.ElapsedMilliseconds <= 1500,
            $"Pipeline elapsed={sw.ElapsedMilliseconds}ms exceeds gate 1500ms");
    }

    private static async Task<double> MedianMs(Func<Task> action, int runs)
    {
        var timings = new List<double>(runs);
        for (var i = 0; i < runs; i++)
        {
            var sw = Stopwatch.StartNew();
            await action();
            sw.Stop();
            timings.Add(sw.Elapsed.TotalMilliseconds);
        }

        timings.Sort();
        return timings[timings.Count / 2];
    }
}
