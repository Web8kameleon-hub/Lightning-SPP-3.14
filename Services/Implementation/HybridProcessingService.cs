using System.Diagnostics;

namespace wwwmmm.Services.Implementation;

/// <summary>
/// Compatibility wrapper that routes all processing to REAL AI processor.
/// </summary>
public class HybridProcessingService : IProcessingService
{
    private readonly RealAIProcessor _realProcessor = new();

    public Task<ProcessingResult> ProcessAsync(
        byte[] data,
        ProcessingOptions options,
        CancellationToken cancellationToken = default)
    {
        return _realProcessor.ProcessAsync(data, options, cancellationToken);
    }

    public Task<ProcessingResult> ClxProcessAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        return _realProcessor.ClxProcessAsync(data, cancellationToken);
    }

    public Task<ProcessingResult> CliProcessAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        return _realProcessor.CliProcessAsync(data, cancellationToken);
    }

    public Task<ProcessingResult> XclProcessAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        return _realProcessor.XclProcessAsync(data, cancellationToken);
    }
}
