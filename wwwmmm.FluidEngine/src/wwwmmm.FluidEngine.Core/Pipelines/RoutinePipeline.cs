using System.Diagnostics;
using wwwmmm.FluidEngine.Core.Contracts;
using wwwmmm.FluidEngine.Core.Context;

namespace wwwmmm.FluidEngine.Core.Pipelines;

public sealed class RoutinePipeline : IFluidStep<int, string>
{
    private readonly int _iterations;
    public RoutinePipeline(int iterations) => _iterations = iterations;
    public string Name => "RoutinePipeline/Iterate";

    public Task<string> ExecuteAsync(int input, PipelineContext context, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        int sum = 0;
        for (int i = 0; i < _iterations; i++) sum += input;
        sw.Stop();
        context.RecordStep(Name, "RoutineIter", sw.Elapsed.TotalMilliseconds, true);
        return Task.FromResult($"Routine done in {_iterations} iterations. Sum: {sum}");
    }
}
