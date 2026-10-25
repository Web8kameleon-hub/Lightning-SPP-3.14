using System.Diagnostics;
using wwwmmm.FluidEngine.Core.Contracts;
using wwwmmm.FluidEngine.Core.Context;

namespace wwwmmm.FluidEngine.Core.Orchestration;

// Entry point: each execution receives a fresh context.
public static class FluidPipeline<TStart>
{
    public static FluidPipeline<TStart, TStart> Create() =>
        new((input, _, _) => Task.FromResult(input));
}

// The current output type changes as steps are appended. No runtime type casts.
public sealed class FluidPipeline<TStart, TCurrent>
{
    private readonly Func<TStart, PipelineContext, CancellationToken, Task<TCurrent>> _executionFlow;

    internal FluidPipeline(Func<TStart, PipelineContext, CancellationToken, Task<TCurrent>> executionFlow)
        => _executionFlow = executionFlow;

    public FluidPipeline<TStart, TNext> AddStep<TNext>(IFluidStep<TCurrent, TNext> step)
    {
        ArgumentNullException.ThrowIfNull(step);
        var previous = _executionFlow;
        return new FluidPipeline<TStart, TNext>(async (input, context, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            var intermediate = await previous(input, context, ct);
            ct.ThrowIfCancellationRequested();
            var timer = Stopwatch.StartNew();
            try
            {
                var output = await step.ExecuteAsync(intermediate, context, ct);
                context.RecordStep(step.Name, "FluidPipeline", timer.Elapsed.TotalMilliseconds, true);
                return output;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                context.RecordStep(step.Name, "FluidPipeline", timer.Elapsed.TotalMilliseconds, false);
                throw;
            }
            catch (Exception ex)
            {
                context.RecordStep(step.Name, "FluidPipeline", timer.Elapsed.TotalMilliseconds, false);
                throw new InvalidOperationException($"Pipeline step '{step.Name}' failed.", ex);
            }
        });
    }

    public async Task<(TCurrent Result, PipelineContext Context)> ExecuteAsync(
        TStart input, CancellationToken ct = default)
    {
        var context = new PipelineContext();
        try
        {
            ct.ThrowIfCancellationRequested();
            var result = await _executionFlow(input, context, ct);
            return (result, context);
        }
        finally
        {
            context.GlobalTimer.Stop();
        }
    }
}
