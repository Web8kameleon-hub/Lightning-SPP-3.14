using System.Diagnostics;
using wwwmmm.FluidEngine.Core.Contracts;
using wwwmmm.FluidEngine.Core.Context;

namespace wwwmmm.FluidEngine.Core.Pipelines;

public sealed class FlowPipeline : IFluidStep<string, string>
{
    private readonly Func<string, string> _router;
    public FlowPipeline(Func<string, string> router) => _router = router;
    public string Name => "FlowPipeline/Router";

    public Task<string> ExecuteAsync(string input, PipelineContext context, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var result = _router(input);
        sw.Stop();
        context.RecordStep(Name, "FlowRouter", sw.Elapsed.TotalMilliseconds, true);
        return Task.FromResult(result);
    }
}
