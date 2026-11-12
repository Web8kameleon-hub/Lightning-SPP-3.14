using System.Diagnostics;
using wwwmmm.FluidEngine.Core.Contracts;
using wwwmmm.FluidEngine.Core.Context;

namespace wwwmmm.FluidEngine.Core.Pipelines;

public sealed class SeltenDataPipeline : IFluidStep<int[], int[]>
{
    public string Name => "SeltenDataPipeline/FilterRare";

    public Task<int[]> ExecuteAsync(int[] input, PipelineContext context, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        if (input.Length == 0) return Task.FromResult(input);
        
        var avg = input.Average();
        var stdDev = Math.Sqrt(input.Select(x => Math.Pow(x - avg, 2)).Average());
        var threshold = avg + 2 * stdDev;
        
        var rare = input.Where(x => x > threshold).ToArray();
        sw.Stop();
        context.RecordStep(Name, "RareFilter", sw.Elapsed.TotalMilliseconds, true);
        return Task.FromResult(rare);
    }
}
