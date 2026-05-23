using System.Diagnostics;

namespace wwwmmm.Services.Implementation;

/// <summary>
/// Compatibility wrapper that routes all print operations to REAL print service.
/// </summary>
public class StigmaPrintService : IPrintService
{
    private readonly RealPrintService _realPrintService = new();

    public Task<PrintResult> PrintAsync(
        byte[] data,
        PrintOptions options,
        CancellationToken cancellationToken = default)
    {
        return _realPrintService.PrintAsync(data, options, cancellationToken);
    }

    public Task<PrintResult> StigmaPrintAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        return _realPrintService.StigmaPrintAsync(data, cancellationToken);
    }
}
