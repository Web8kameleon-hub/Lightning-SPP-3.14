using System.Diagnostics;
using wwwmmm.FluidEngine.Core.Contracts;
using wwwmmm.FluidEngine.Core.Context;

namespace wwwmmm.FluidEngine.Core.Pipelines;

public sealed class StandardDataMetaknowPipeline : IFluidStep<string, string>
{
    public string Name => "StandardDataMetaknow/Extract";

    public Task<string> ExecuteAsync(string input, PipelineContext context, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var meta = $"Length: {input.Length}, Words: {input.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length}";
        sw.Stop();
        context.RecordStep(Name, "MetaExtractor", sw.Elapsed.TotalMilliseconds, true);
        return Task.FromResult(meta);
    }
}
