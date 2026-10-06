namespace wwwmmm.Services;

/// <summary>
/// Advanced processing service using CLX, CLX.i, and XCL engines
/// </summary>
public interface IProcessingService
{
    Task<ProcessingResult> ProcessAsync(byte[] data, ProcessingOptions options, CancellationToken cancellationToken = default);
    Task<ProcessingResult> ClxProcessAsync(byte[] data, CancellationToken cancellationToken = default);
    Task<ProcessingResult> ClxIProcessAsync(byte[] data, CancellationToken cancellationToken = default);
    Task<ProcessingResult> XclProcessAsync(byte[] data, CancellationToken cancellationToken = default);
}

public record ProcessingOptions(
    ProcessingEngine Engine = ProcessingEngine.Auto,
    bool UseAI = true,
    bool UseClisonix = false,
    int OptimizationLevel = 3,
    bool UseStigmaMemory = false
);

public record ProcessingResult(
    byte[] ProcessedData,
    ProcessingMetadata Metadata,
    bool Success,
    string? Error = null
);

public record ProcessingMetadata(
    DateTime Timestamp,
    long ProcessingTimeMs,
    string Engine,
    int OptimizationLevel,
    Dictionary<string, object> AdditionalInfo
);

public enum ProcessingEngine
{
    Auto,
    CLX,        // Core processing
    CLX_I,      // Interactive processing
    XCL,        // Extended processing
    Clisonix = 4, // Canonical Clisonix integration
    Hybrid      // Combined engines
}
