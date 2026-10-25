using System.Diagnostics;
using wwwmmm.FluidEngine.Core.Contracts;
using wwwmmm.FluidEngine.Core.Context;

namespace wwwmmm.FluidEngine.Core.Pipelines;

public sealed class SuitabilityPipeline : IFluidStep<string, bool>
{
    private readonly string _suitPattern;
    public SuitabilityPipeline(string suitPattern) => _suitPattern = suitPattern;
    public string Name => "SuitabilityPipeline/Check";

    public Task<bool> ExecuteAsync(string input, PipelineContext context, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var isSuitable = input.Contains(_suitPattern, StringComparison.OrdinalIgnoreCase);
        sw.Stop();
        context.RecordStep(Name, "SuitChecker", sw.Elapsed.TotalMilliseconds, true);
        return Task.FromResult(isSuitable);
    }
}
