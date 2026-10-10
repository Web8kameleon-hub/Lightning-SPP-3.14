using wwwmmm.Services;
using wwwmmm.Services.Implementation;
using wwwmmm.Services.Imaging;

namespace wwwmmm.Configuration;

/// <summary>
/// Runtime configuration for Lightning SPP 3.14.
/// </summary>
public class LightningConfiguration
{
    public bool UseRealServices { get; set; } = true;
    public bool UseRealImageProcessing { get; set; } = true;
    public bool UseRealAIProcessing { get; set; } = true;
    public bool UseRealPrinting { get; set; } = true;
    public string? PrintOutputDirectory { get; set; }
    public int MaxConcurrency { get; set; } = Environment.ProcessorCount;
    public bool EnablePerformanceLogging { get; set; } = true;
    public bool EnableMemoryOptimization { get; set; } = true;
    public StigmaExecutionProfile StigmaProfile { get; set; } = StigmaProfiles.FromEnvironment();

    /// <summary>
    /// Hybrid image engine selection (L=Skia, R=ImageSharp). Defaults to Skia so
    /// existing behavior is unchanged; set WWWMMM_IMAGE_ENGINE=ImageSharp to switch
    /// explicitly. No silent fallback between engines.
    /// </summary>
    public ImageEngine ImageEngine { get; set; } = ImageEngineSelection.FromEnvironment();

    public static LightningConfiguration Default => new();

    public static LightningConfiguration FastSimulated => new()
    {
        // Backward-compatible name. Simulated services have been removed.
        StigmaProfile = StigmaExecutionProfile.Lightning
    };

    public static LightningConfiguration RealProduction => new()
    {
        MaxConcurrency = Math.Max(1, Environment.ProcessorCount * 2),
        StigmaProfile = StigmaProfiles.FromEnvironment()
    };
}

/// <summary>
/// Creates the concrete services used by the public API.
/// </summary>
public static class ServiceFactory
{
    private static LightningConfiguration _config = LightningConfiguration.Default;

    public static void Configure(LightningConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (configuration.MaxConcurrency < 1)
            throw new ArgumentOutOfRangeException(nameof(configuration.MaxConcurrency));

        _config = configuration;
    }

    public static IScanService CreateScanService() =>
        new ImageEngineAdapter(
            new RealImageProcessor(),
            new FastScanService(Microsoft.Extensions.Logging.Abstractions.NullLogger<FastScanService>.Instance))
        .Get(_config.ImageEngine);

    public static IProcessingService CreateProcessingService() => new RealAIProcessor();

    public static IPrintService CreatePrintService() =>
        new RealPrintService(_config.PrintOutputDirectory);

    public static LightningPipeline CreatePipeline() =>
        new(
            CreateScanService(),
            CreateProcessingService(),
            CreatePrintService(),
            _config.StigmaProfile);
}
