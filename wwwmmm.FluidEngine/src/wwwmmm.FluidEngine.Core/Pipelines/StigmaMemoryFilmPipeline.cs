using System.Diagnostics;
using wwwmmm.FluidEngine.Core.Contracts;
using wwwmmm.FluidEngine.Core.Context;

namespace wwwmmm.FluidEngine.Core.Pipelines;

public sealed class StigmaMemoryFilmPipeline : IFluidStep<byte[], Guid>
{
    public string Name => "StigmaMemoryFilm/Store";

    public Task<Guid> ExecuteAsync(byte[] input, PipelineContext context, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var id = Guid.NewGuid();
        context.Fluid.Set(context.TraceId, id.ToString(), input);
        sw.Stop();
        context.RecordStep(Name, "MemoryFilmStore", sw.Elapsed.TotalMilliseconds, true);
        return Task.FromResult(id);
    }
}
