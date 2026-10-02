using wwwmmm.PublicAPI;
using Xunit;

namespace LightningSPP.Tests;

public class ApiSmokeTests
{
    [Fact]
    public async Task QuickScanAsync_ReturnsSuccess()
    {
        LightningAPI.UseRealServices();
        var source = CreateTempSourceFile("quick", 2048);

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
        var source = CreateTempSourceFile("pipeline", 4096);

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
            CreateTempSourceFile("a", 1536),
            CreateTempSourceFile("b", 1792),
            CreateTempSourceFile("c", 2048)
        };

        var results = await LightningAPI.BatchProcessAsync(sources);

        Assert.Equal(sources.Count, results.Count);
        Assert.All(results, r => Assert.True(r.Success));
    }

    private static string CreateTempSourceFile(string prefix, int size)
    {
        var bytes = new byte[size];
        Random.Shared.NextBytes(bytes);
        var filePath = Path.Combine(Path.GetTempPath(), $"lightning-spp-{prefix}-{Guid.NewGuid():N}.bin");
        File.WriteAllBytes(filePath, bytes);
        return filePath;
    }
}
