using wwwmmm.Services;
using wwwmmm.Services.Implementation;
using wwwmmm.Configuration;

namespace wwwmmm.PublicAPI;

/// <summary>
/// Public API for Lightning Scan-Process-Print system
/// Ready for wide public usage
/// NOW with REAL services (not simulated!)
/// </summary>
public static class LightningAPI
{
    private static IScanService _scanService = ServiceFactory.CreateScanService();
    private static IProcessingService _processingService = ServiceFactory.CreateProcessingService();
    private static IPrintService _printService = ServiceFactory.CreatePrintService();
    private static LightningPipeline _pipeline = ServiceFactory.CreatePipeline();

    /// <summary>
    /// Configure Lightning SPP to use REAL or SIMULATED services
    /// </summary>
    public static void Configure(LightningConfiguration configuration)
    {
        ServiceFactory.Configure(configuration);

        // Recreate services with new configuration
        _scanService = ServiceFactory.CreateScanService();
        _processingService = ServiceFactory.CreateProcessingService();
        _printService = ServiceFactory.CreatePrintService();
        _pipeline = ServiceFactory.CreatePipeline();
    }

    /// <summary>
    /// Use REAL services (default)
    /// </summary>
    public static void UseRealServices()
    {
        Configure(LightningConfiguration.RealProduction);
    }

    /// <summary>
    /// Compatibility method. Simulated mode has been removed and this now uses real services.
    /// </summary>
    public static void UseSimulatedServices()
    {
        Configure(LightningConfiguration.RealProduction);
    }

    /// <summary>
    /// Execute complete Lightning pipeline: Scan -> Process -> Print
    /// </summary>
    public static async Task<PipelineResult> ExecuteLightningPipelineAsync(
        string source,
        CancellationToken cancellationToken = default)
    {
        var request = new PipelineRequest(source);
        return await _pipeline.ExecuteAsync(request, cancellationToken);
    }

    /// <summary>
    /// Execute with custom configuration
    /// </summary>
    public static async Task<PipelineResult> ExecuteCustomPipelineAsync(
        PipelineRequest request,
        CancellationToken cancellationToken = default)
    {
        return await _pipeline.ExecuteAsync(request, cancellationToken);
    }

    /// <summary>
    /// Quick scan using TideWave + Resonance
    /// </summary>
    public static async Task<ScanResult> QuickScanAsync(
        string source,
        CancellationToken cancellationToken = default)
    {
        var request = new ScanRequest(
            source,
            ScanMode.TideWave,
            300,
            UseResonance: true,
            UseNanoDecibel: false
        );
        return await _scanService.ScanAsync(request, cancellationToken);
    }

    /// <summary>
    /// High-quality scan using NanoDecibel
    /// </summary>
    public static async Task<ScanResult> HighQualityScanAsync(
        string source,
        CancellationToken cancellationToken = default)
    {
        var request = new ScanRequest(
            source,
            ScanMode.NanoDecibel,
            600,
            UseResonance: true,
            UseNanoDecibel: true
        );
        return await _scanService.ScanAsync(request, cancellationToken);
    }

    /// <summary>
    /// Process using Clisonix Global AI
    /// </summary>
    public static async Task<ProcessingResult> AIProcessAsync(
        byte[] data,
        CancellationToken cancellationToken = default)
    {
        var options = new ProcessingOptions(
            ProcessingEngine.Clisonix,
            UseAI: true,
            UseClisonix: true,
            OptimizationLevel: 5,
            UseStigmaMemory: true
        );
        return await _processingService.ProcessAsync(data, options, cancellationToken);
    }

    /// <summary>
    /// Hybrid processing using all engines
    /// </summary>
    public static async Task<ProcessingResult> HybridProcessAsync(
        byte[] data,
        CancellationToken cancellationToken = default)
    {
        var options = new ProcessingOptions(
            ProcessingEngine.Hybrid,
            UseAI: false,
            UseClisonix: false,
            OptimizationLevel: 5,
            UseStigmaMemory: false
        );
        return await _processingService.ProcessAsync(data, options, cancellationToken);
    }

    /// <summary>
    /// Stigma premium print
    /// </summary>
    public static async Task<PrintResult> StigmaPrintAsync(
        byte[] data,
        CancellationToken cancellationToken = default)
    {
        return await _printService.StigmaPrintAsync(data, cancellationToken);
    }

    /// <summary>
    /// Batch processing for multiple items
    /// </summary>
    public static async Task<List<PipelineResult>> BatchProcessAsync(
        List<string> sources,
        CancellationToken cancellationToken = default)
    {
        var tasks = sources.Select(source => ExecuteLightningPipelineAsync(source, cancellationToken));
        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }
}
