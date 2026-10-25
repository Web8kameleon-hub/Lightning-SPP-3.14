using System.Text;
using Xunit;
using wwwmmm.FluidEngine.Core.Binary;
using wwwmmm.FluidEngine.Core.Data;
using wwwmmm.FluidEngine.Core.Pipelines;
using wwwmmm.FluidEngine.Core.Context;

namespace wwwmmm.FluidEngine.Tests;

public class EndToEndProductTest
{
    [Fact]
    public async Task FullProductFlow_Works()
    {
        var ctx = new PipelineContext();
        var input = "{\"sensor\":\"temp\",\"value\":22.5,\"status\":\"suit\"}";

        // 1. DataPipeline (Validim JSON)
        var jsonStep = new JsonDataStep(prettyPrint: false);
        var jsonResult = await jsonStep.ExecuteAsync(input, ctx, default);

        // 2. FlowPipeline (Rrugëtim në bazë të përmbajtjes)
        var flowStep = new FlowPipeline(s => s.Contains("suit") ? "HIGH" : "LOW");
        var flowResult = await flowStep.ExecuteAsync(jsonResult, ctx, default);
        Assert.Equal("HIGH", flowResult);

        // 3. BitPipeline (Kompresim + Framing)
        var bitStep = new BitPipeline();
        var compressed = await bitStep.ExecuteAsync(Encoding.UTF8.GetBytes(flowResult), ctx, default);

        // 4. BitUnpacker (Dekodim dhe verifikim)
        var unpackStep = new BitUnpacker();
        var unpacked = await unpackStep.ExecuteAsync(compressed, ctx, default);

        // 5. StandardDataMetaknowPipeline (Nxjerrje metatëdhënash)
        var metaStep = new StandardDataMetaknowPipeline();
        var metaResult = await metaStep.ExecuteAsync(Encoding.UTF8.GetString(unpacked), ctx, default);

        // Verifikimi Final (Hard Proof)
        Assert.Contains("Length: 4", metaResult);
        Assert.Equal(5, ctx.StepTraces.Count);
        Assert.All(ctx.StepTraces, t => Assert.True(t.DurationMs >= 0));
    }
}
