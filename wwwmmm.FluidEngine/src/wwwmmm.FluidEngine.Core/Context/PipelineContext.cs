using System.Diagnostics;

namespace wwwmmm.FluidEngine.Core.Context;

public sealed class PipelineContext
{
    public string TraceId { get; } = Guid.NewGuid().ToString("N");
    public Stopwatch GlobalTimer { get; } = Stopwatch.StartNew();
    public FluidLayer Fluid { get; } = new();
    public Dictionary<string, object> Metadata { get; } = new();
    public List<PipelineStepTrace> StepTraces { get; } = new();

    public void RecordStep(string name, string engineUsed, double durationMs, bool success)
    {
        StepTraces.Add(new PipelineStepTrace(name, engineUsed, durationMs, success));
    }
}

public readonly record struct PipelineStepTrace(
    string StepName,
    string EngineUsed,
    double DurationMs,
    bool Success
);
