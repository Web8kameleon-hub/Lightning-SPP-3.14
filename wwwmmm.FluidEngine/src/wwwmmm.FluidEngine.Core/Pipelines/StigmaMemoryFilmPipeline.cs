using System.Diagnostics;
using wwwmmm.FluidEngine.Core.Contracts;
using wwwmmm.FluidEngine.Core.Context;

namespace wwwmmm.FluidEngine.Core.Pipelines;

public sealed record StigmaFilmEntry(string JobId, string StepId, string State, string RuntimeHash,
    string? ResultHash, string? SourceFingerprint, string Verification, DateTimeOffset RecordedAt);

public sealed class StigmaMemoryFilmPipeline : IFluidStep<byte[], Guid>
{
    public string Name => "StigmaMemoryFilm/Store";

    /// <summary>Persist execution metadata through the caller's private storage boundary.
    /// No document bytes, prompt, customer details or credentials enter this record.</summary>
    public async Task RecordAsync(StigmaFilmEntry entry, PipelineContext context,
        Func<StigmaFilmEntry, CancellationToken, Task> persist, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entry); ArgumentNullException.ThrowIfNull(persist);
        ct.ThrowIfCancellationRequested();
        var sw = Stopwatch.StartNew();
        await persist(entry, ct).ConfigureAwait(false);
        context.Fluid.Set(context.TraceId, entry.JobId + ":" + entry.StepId, entry);
        sw.Stop(); context.RecordStep(Name, "FilmMetadataPersist", sw.Elapsed.TotalMilliseconds, true);
    }

    public Task<Guid> ExecuteAsync(byte[] input, PipelineContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var sw = Stopwatch.StartNew();
        var id = Guid.NewGuid();
        context.Fluid.Set(context.TraceId, id.ToString(), input);
        sw.Stop();
        context.RecordStep(Name, "MemoryFilmStore", sw.Elapsed.TotalMilliseconds, true);
        return Task.FromResult(id);
    }
}
