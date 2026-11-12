using System.Diagnostics;
using System.Runtime.CompilerServices;
using wwwmmm.FluidEngine.Core.Contracts;
using wwwmmm.FluidEngine.Core.Context;

namespace wwwmmm.FluidEngine.Core.Pipelines;

public sealed class EchtzeitDataPipeline : IFluidStep<IAsyncEnumerable<string>, IAsyncEnumerable<string>>
{
    public string Name => "EchtzeitDataPipeline/Stream";

    public Task<IAsyncEnumerable<string>> ExecuteAsync(IAsyncEnumerable<string> input, PipelineContext context, CancellationToken ct)
    {
        return Task.FromResult(ProcessStream(input, context, ct));
    }

    private async IAsyncEnumerable<string> ProcessStream(IAsyncEnumerable<string> input, PipelineContext context, [EnumeratorCancellation] CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        await foreach (var item in input.WithCancellation(ct))
        {
            yield return $"[{DateTime.UtcNow:O}] {item}";
        }
        sw.Stop();
        context.RecordStep(Name, "StreamProcessor", sw.Elapsed.TotalMilliseconds, true);
    }
}
