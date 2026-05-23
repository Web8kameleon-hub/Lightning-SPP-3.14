using wwwmmm.PublicAPI;
using Xunit;

namespace LightningSPP.Tests;

public class ApiSmokeTests
{
    [Fact]
    public async Task QuickScanAsync_UsingSimulatedServices_ReturnsSuccess()
    {
        LightningAPI.UseSimulatedServices();

        var result = await LightningAPI.QuickScanAsync("unit-test.pdf");

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task HybridProcessAsync_UsingSimulatedServices_ReturnsSuccess()
    {
        LightningAPI.UseSimulatedServices();
        var input = new byte[512];

        var result = await LightningAPI.HybridProcessAsync(input);

        Assert.True(result.Success);
        Assert.NotNull(result.ProcessedData);
    }

    [Fact]
    public async Task ExecuteLightningPipelineAsync_UsingSimulatedServices_ReturnsSteps()
    {
        LightningAPI.UseSimulatedServices();

        var result = await LightningAPI.ExecuteLightningPipelineAsync("pipeline-test.pdf");

        Assert.True(result.Success);
        Assert.NotEmpty(result.Steps);
    }

    [Fact]
    public async Task BatchProcessAsync_UsingSimulatedServices_ReturnsSameCountAsSources()
    {
        LightningAPI.UseSimulatedServices();
        var sources = new List<string> { "a.pdf", "b.pdf", "c.pdf" };

        var results = await LightningAPI.BatchProcessAsync(sources);

        Assert.Equal(sources.Count, results.Count);
        Assert.All(results, r => Assert.True(r.Success));
    }
}
