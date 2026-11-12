using Xunit;
using wwwmmm.FluidEngine.Core.Data;
using wwwmmm.FluidEngine.Core.Context;

namespace wwwmmm.FluidEngine.Tests;

public class DataPipelineTests
{
    [Fact]
    public async Task JsonValidation_FormatsCorrectly()
    {
        var step = new JsonDataStep(prettyPrint: true);
        var ctx = new PipelineContext();
        
        var result = await step.ExecuteAsync("{\"name\":\"Lightning\",\"version\":3.19}", ctx, default);
        
        Assert.Contains("\n", result); // Pretty-print ka linja të reja
        Assert.Contains("\"name\": \"Lightning\"", result);
    }

    [Fact]
    public async Task InvalidJson_Throws()
    {
        var step = new JsonDataStep();
        var ctx = new PipelineContext();
        
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            step.ExecuteAsync("{invalid-json}", ctx, default));
    }

    [Fact]
    public async Task CsvParser_ParsesCorrectly()
    {
        var step = new CsvDataStep();
        var ctx = new PipelineContext();
        var csv = "id,name,score\n1,Ledjan,100\n2,ABA,95";
        
        var result = await step.ExecuteAsync(csv, ctx, default);
        
        Assert.Equal(3, result.Length);
        Assert.Equal(new[] { "id", "name", "score" }, result[0]);
        Assert.Equal(new[] { "1", "Ledjan", "100" }, result[1]);
    }
}
