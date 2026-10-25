using System.Diagnostics;
using wwwmmm.FluidEngine.Core.Contracts;
using wwwmmm.FluidEngine.Core.Context;

namespace wwwmmm.FluidEngine.Core.Orchestration;

public sealed class FluidPipeline<TStart>
{
    private readonly PipelineContext _context;
    private readonly Func<TStart, PipelineContext, CancellationToken, Task<TStart>> _executionFlow;

    internal FluidPipeline(PipelineContext context, Func<TStart, PipelineContext, CancellationToken, Task<TStart>> executionFlow)
    {
        _context = context;
        _executionFlow = executionFlow;
    }

    public static FluidPipeline<TStart> Create() => new(new PipelineContext(), (input, _, _) => Task.FromResult(input));

    public FluidPipeline<TStart> AddStep<TNext>(IFluidStep<TStart, TNext> step)
    {
        var previousFlow = _executionFlow;
        var newFlow = async (TStart input, PipelineContext ctx, CancellationToken ct) =>
        {
            var intermediate = await previousFlow(input, ctx, ct);
            var sw = Stopwatch.StartNew();
            try
            {
                var output = await step.ExecuteAsync(intermediate, ctx, ct);
                sw.Stop();
                ctx.RecordStep(step.Name, "RealEngine", sw.Elapsed.TotalMilliseconds, true);
                
                // Për të mbajtur tipizimin e thjeshtë, kthejmë output si TStart (kjo kërkon casting në runtime)
                return (TStart)(object)output!; 
            }
            catch (Exception ex)
            {
                sw.Stop();
                ctx.RecordStep(step.Name, "RealEngine", sw.Elapsed.TotalMilliseconds, false);
                throw new InvalidOperationException($"Pipeline step '{step.Name}' failed.", ex);
            }
        };

        return new FluidPipeline<TStart>(_context, newFlow);
    }

    public async Task<(TStart Result, PipelineContext Context)> ExecuteAsync(TStart input, CancellationToken ct = default)
    {
        var result = await _executionFlow(input, _context, ct);
        _context.GlobalTimer.Stop();
        return (result, _context);
    }
}

