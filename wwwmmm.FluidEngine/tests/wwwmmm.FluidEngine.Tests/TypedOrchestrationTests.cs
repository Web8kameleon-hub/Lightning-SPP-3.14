using System.Text;
using Xunit;
using wwwmmm.FluidEngine.Core.Binary;
using wwwmmm.FluidEngine.Core.Context;
using wwwmmm.FluidEngine.Core.Orchestration;
using wwwmmm.FluidEngine.Core.Pipelines;

namespace wwwmmm.FluidEngine.Tests;

public class TypedOrchestrationTests
{
    [Fact]
    public async Task Pipeline_ChangesTypesWithoutRuntimeCast()
    {
        var pipeline = FluidPipeline<string>.Create()
            .AddStep(new SuitabilityPipeline("ready"));
        var (result, context) = await pipeline.ExecuteAsync("READY for production");
        Assert.True(result);
        Assert.Single(context.StepTraces);
    }

    [Fact]
    public async Task Pipeline_CompressesAndDecompressesWithFreshContext()
    {
        var pipeline = FluidPipeline<byte[]>.Create()
            .AddStep(new BitPipeline())
            .AddStep(new BitUnpacker());
        var input = Encoding.UTF8.GetBytes("non-zero data, real round trip");
        var (first, firstContext) = await pipeline.ExecuteAsync(input);
        var (second, secondContext) = await pipeline.ExecuteAsync(input);
        Assert.Equal(input, first);
        Assert.Equal(input, second);
        Assert.NotEqual(firstContext.TraceId, secondContext.TraceId);
        Assert.Equal(4, firstContext.StepTraces.Count); // Steps record their own trace plus orchestration trace.
        Assert.Equal(4, secondContext.StepTraces.Count);
    }

    [Fact]
    public async Task Pipeline_HonorsCancellation()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var pipeline = FluidPipeline<string>.Create()
            .AddStep(new SuitabilityPipeline("ready"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => pipeline.ExecuteAsync("ready", source.Token));
    }
}
