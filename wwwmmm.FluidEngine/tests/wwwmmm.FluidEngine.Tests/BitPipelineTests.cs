using System.Text;
using Xunit;
using wwwmmm.FluidEngine.Core.Binary;
using wwwmmm.FluidEngine.Core.Context;

namespace wwwmmm.FluidEngine.Tests;

public class BitPipelineTests
{
    [Fact]
    public async Task RoundTrip_PreservesData()
    {
        // 1. Përgatit të dhëna reale (jo zero-buffer)
        var input = Encoding.UTF8.GetBytes("Lightning SPP 3.19 - Hard Proof Data - " + Guid.NewGuid());
        var ctx = new PipelineContext();
        var compressor = new BitPipeline();
        var decompressor = new BitUnpacker();

        // 2. Kompreso dhe Dekompreso
        var compressed = await compressor.ExecuteAsync(input, ctx, default);
        var result = await decompressor.ExecuteAsync(compressed, ctx, default);

        // 3. Verifikim i saktësisë (Hard Proof)
        Assert.Equal(input.Length, result.Length);
        Assert.Equal(input, result);
        Assert.Equal(2, ctx.StepTraces.Count); // Kompresor + Dekompresor
    }

    [Fact]
    public async Task InvalidHeader_Throws()
    {
        var compressor = new BitPipeline();
        var decompressor = new BitUnpacker();
        var ctx = new PipelineContext();

        var valid = await compressor.ExecuteAsync(new byte[] { 1, 2, 3 }, ctx, default);
        valid[0] = 0xFF; // Korrupto Magic Byte

        await Assert.ThrowsAsync<InvalidOperationException>(() => decompressor.ExecuteAsync(valid, ctx, default));
    }
}

