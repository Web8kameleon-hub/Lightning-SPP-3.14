using System.Text.Json;
using wwwmmm.FluidEngine.Core.Registry;
using Xunit;

namespace wwwmmm.FluidEngine.Tests;

public class OpenApiValidationTests
{
    [Fact]
    public void GeneratedSpec_HasOnePathPerPipeline()
    {
        var json = OpenApiGenerator.Generate();
        using var doc = JsonDocument.Parse(json);
        
        var paths = doc.RootElement.GetProperty("paths");
        var pathCount = paths.EnumerateObject().Count();
        var registryCount = PipelineRegistry.All.Count;
        
        Assert.Equal(registryCount, pathCount);
    }

    [Fact]
    public void GeneratedSpec_DeclaresOpenApi310()
    {
        var json = OpenApiGenerator.Generate();
        using var doc = JsonDocument.Parse(json);
        
        Assert.Equal("3.1.0", doc.RootElement.GetProperty("openapi").GetString());
    }
}
