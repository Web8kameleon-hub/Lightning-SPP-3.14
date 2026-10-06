using wwwmmm.PublicAPI;
using Xunit;

namespace LightningSPP.Tests;

public class ApiSmokeTests
{
    [Fact]
    public async Task QuickScanAsync_ReturnsSuccess()
    {
        LightningAPI.UseRealServices();
        var source = CreateTempImageFile("quick");

        var result = await LightningAPI.QuickScanAsync(source);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task HybridProcessAsync_ReturnsSuccess()
    {
        LightningAPI.UseRealServices();
        var input = new byte[512];

        var result = await LightningAPI.HybridProcessAsync(input);

        Assert.True(result.Success);
        Assert.NotNull(result.ProcessedData);
    }

    [Fact]
    public async Task ExecuteLightningPipelineAsync_ReturnsSteps()
    {
        LightningAPI.UseRealServices();
        var source = CreateTempImageFile("pipeline");

        var result = await LightningAPI.ExecuteLightningPipelineAsync(source);

        Assert.True(result.Success);
        Assert.NotEmpty(result.Steps);
    }

    [Fact]
    public async Task BatchProcessAsync_ReturnsSameCountAsSources()
    {
        LightningAPI.UseRealServices();
        var sources = new List<string>
        {
            CreateTempImageFile("a"),
            CreateTempImageFile("b"),
            CreateTempImageFile("c")
        };

        var results = await LightningAPI.BatchProcessAsync(sources);

        Assert.Equal(sources.Count, results.Count);
        Assert.All(results, r => Assert.True(r.Success));
    }

    private static string CreateTempImageFile(string prefix)
    {
        var filePath = Path.Combine(
            Path.GetTempPath(),
            $"lightning-spp-{prefix}-{Guid.NewGuid():N}.png");

        File.WriteAllBytes(filePath, RealTestImage.Png());
        return filePath;
    }
}
