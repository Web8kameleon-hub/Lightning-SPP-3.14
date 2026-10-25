using wwwmmm.FluidEngine.Core.Context;

namespace wwwmmm.FluidEngine.Core.Contracts;

public interface IFluidStep<in TInput, TOutput>
{
    string Name { get; }
    Task<TOutput> ExecuteAsync(TInput input, PipelineContext context, CancellationToken ct);
}

