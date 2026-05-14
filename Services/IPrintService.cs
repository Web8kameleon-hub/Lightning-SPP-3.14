namespace wwwmmm.Services;

/// <summary>
/// Lightning-fast print service with Stigma technology
/// </summary>
public interface IPrintService
{
    Task<PrintResult> PrintAsync(byte[] data, PrintOptions options, CancellationToken cancellationToken = default);
    Task<PrintResult> StigmaPrintAsync(byte[] data, CancellationToken cancellationToken = default);
}

public record PrintOptions(
    string Destination = "default",
    PrintQuality Quality = PrintQuality.High,
    bool UseStigma = true,
    bool UseResonance = true,
    int Copies = 1
);

public record PrintResult(
    bool Success,
    PrintMetadata Metadata,
    string? Error = null
);

public record PrintMetadata(
    DateTime Timestamp,
    long ProcessingTimeMs,
    string Technology,
    int PagesPrinted,
    string Destination
);

public enum PrintQuality
{
    Draft,
    Normal,
    High,
    Stigma      // Maximum quality with Stigma technology
}
