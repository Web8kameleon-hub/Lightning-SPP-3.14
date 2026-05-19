namespace wwwmmm.Services;

/// <summary>
/// Ultra-fast scanning service using Resonance and TideWave technologies
/// </summary>
public interface IScanService
{
    Task<ScanResult> ScanAsync(ScanRequest request, CancellationToken cancellationToken = default);
    Task<ScanResult> ScanAsync(byte[] data, ScanMode mode = ScanMode.Lightning, int quality = 300, CancellationToken cancellationToken = default);
    Task<ScanResult> ResonanceScanAsync(byte[] data, CancellationToken cancellationToken = default);
}

public record ScanRequest(
    string Source,
    ScanMode Mode = ScanMode.Lightning,
    int Quality = 300,
    bool UseResonance = true,
    bool UseNanoDecibel = true
);

public record ScanResult(
    byte[] Data,
    ScanMetadata Metadata,
    bool Success,
    string? Error = null
);

public record ScanMetadata(
    DateTime Timestamp,
    long ProcessingTimeMs,
    string Technology,
    int Resolution,
    string Format
);

public enum ScanMode
{
    Lightning,      // Fastest mode
    TideWave,       // Balanced speed and quality
    Resonance,      // High precision
    NanoDecibel     // Maximum quality
}
