using wwwmmm.Services;
using wwwmmm.Services.Implementation;

namespace wwwmmm.Configuration;

/// <summary>
/// Configuration for Lightning SPP 3.14
/// REAL services only configuration
/// </summary>
public class LightningConfiguration
{
    public bool UseRealServices { get; set; } = true;
    public bool UseRealImageProcessing { get; set; } = true;
    public bool UseRealAIProcessing { get; set; } = true;
    public bool UseRealPrinting { get; set; } = true;
    public string? PrintOutputDirectory { get; set; }
    public int MaxConcurrency { get; set; } = 1000;
    public bool EnablePerformanceLogging { get; set; } = true;
    public bool EnableMemoryOptimization { get; set; } = true;

    public static LightningConfiguration Default => new()
    {
        UseRealServices = true,
        UseRealImageProcessing = true,
        UseRealAIProcessing = true,
        UseRealPrinting = true,
        EnablePerformanceLogging = true,
        EnableMemoryOptimization = true
    };

    public static LightningConfiguration FastSimulated => new()
    {
        // Kept for backward compatibility - now routes to real services.
        UseRealServices = true,
        UseRealImageProcessing = true,
        UseRealAIProcessing = true,
        UseRealPrinting = true,
        EnablePerformanceLogging = true,
        EnableMemoryOptimization = true
    };

    public static LightningConfiguration RealProduction => new()
    {
        UseRealServices = true,
        UseRealImageProcessing = true,
        UseRealAIProcessing = true,
        UseRealPrinting = true,
        MaxConcurrency = 10000,
        EnablePerformanceLogging = true,
        EnableMemoryOptimization = true
    };
}

/// <summary>
/// Service Factory - Creates appropriate services based on configuration
/// </summary>
public static class ServiceFactory
{
    private static LightningConfiguration _config = LightningConfiguration.Default;

    public static void Configure(LightningConfiguration configuration)
    {
        _config = configuration;
    }

    public static IScanService CreateScanService()
    {
        return new wwwmmm.Services.Implementation.RealImageProcessor();
    }

    public static IProcessingService CreateProcessingService()
    {
        return new wwwmmm.Services.Implementation.RealAIProcessor();
    }

    public static IPrintService CreatePrintService()
    {
        return new wwwmmm.Services.Implementation.RealPrintService(_config.PrintOutputDirectory);
    }

    public static LightningPipeline CreatePipeline()
    {
        return new LightningPipeline(
            CreateScanService(),
            CreateProcessingService(),
            CreatePrintService()
        );
    }
}
