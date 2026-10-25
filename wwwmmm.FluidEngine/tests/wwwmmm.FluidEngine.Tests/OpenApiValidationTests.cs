using System.Text.Json;
using wwwmmm.FluidEngine.Core.Registry;
using Xunit;

namespace wwwmmm.FluidEngine.Tests;

public class OpenApiValidationTests
{
    [Fact]
    public void GeneratedSpec_OnlyExposesImplementedHttpPipelines()
    {
        var json = OpenApiGenerator.Generate();
        using var doc = JsonDocument.Parse(json);
        
        var paths = doc.RootElement.GetProperty("paths");
        var pathCount = paths.EnumerateObject().Count();
        var implementedHttpCount = 2;
        
        Assert.Equal(implementedHttpCount, pathCount);
        Assert.True(paths.TryGetProperty("/api/fluid/BitPipeline", out _));
        Assert.True(paths.TryGetProperty("/api/fluid/BitUnpacker", out _));
        Assert.False(paths.TryGetProperty("/api/fluid/VisionPipeline", out _));
    }

    [Fact]
    public void GeneratedSpec_DeclaresOpenApi310()
    {
        var json = OpenApiGenerator.Generate();
        using var doc = JsonDocument.Parse(json);
        
        Assert.Equal("3.1.0", doc.RootElement.GetProperty("openapi").GetString());
    }
}
