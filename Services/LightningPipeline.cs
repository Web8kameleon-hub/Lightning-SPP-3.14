using System.Diagnostics;
using LightningSPP.Licensing;

namespace wwwmmm.Services;

/// <summary>
/// Ultra-fast pipeline combining scan, process, and print in lightning speed
/// </summary>
public class LightningPipeline
{
    private readonly IScanService _scanService;
    private readonly IProcessingService _processingService;
    private readonly IPrintService _printService;

    public LightningPipeline(
        IScanService scanService,
        IProcessingService processingService,
        IPrintService printService)
    {
        _scanService = scanService;
        _processingService = processingService;
        _printService = printService;
    }

    public async Task<PipelineResult> ExecuteAsync(
        PipelineRequest request,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var results = new List<string>();

        try
        {
            // License gate — enforce usage limits before any work is done
            if (!LicenseManager.CheckAndIncrementUsage())
            {
                return new PipelineResult(false, 0, results,
                    "Operation limit reached. Please upgrade your Lightning SPP license or wait for the next billing cycle.");
            }

            // Phase 1: Lightning Scan
            results.Add("⚡ Starting Lightning Scan...");
            var scanResult = await _scanService.ScanAsync(
                new ScanRequest(
                    request.Source,
                    request.ScanMode,
                    request.Quality,
                    request.UseResonance,
                    request.UseNanoDecibel
                ),
                cancellationToken
            );

            if (!scanResult.Success)
            {
                return new PipelineResult(false, sw.ElapsedMilliseconds, results, scanResult.Error);
            }

            results.Add($"✓ Scan completed in {scanResult.Metadata.ProcessingTimeMs}ms using {scanResult.Metadata.Technology}");

            // Phase 2: Advanced Processing
            results.Add("🔄 Processing with AI engines...");
            var processResult = await _processingService.ProcessAsync(
                scanResult.Data,
                new ProcessingOptions(
                    request.ProcessingEngine,
                    request.UseAI,
                    request.UseClisonic,
                    request.OptimizationLevel,
                    request.UseStigmaMemory
                ),
                cancellationToken
            );

            if (!processResult.Success)
            {
                return new PipelineResult(false, sw.ElapsedMilliseconds, results, processResult.Error);
            }

            results.Add($"✓ Processing completed in {processResult.Metadata.ProcessingTimeMs}ms using {processResult.Metadata.Engine}");

            // Phase 3: Stigma Print
            if (request.ShouldPrint)
            {
                results.Add("🖨️  Printing with Stigma technology...");
                var printResult = await _printService.PrintAsync(
                    processResult.ProcessedData,
                    new PrintOptions(
                        request.PrintDestination,
                        request.PrintQuality,
                        request.UseStigma,
                        request.UseResonance,
                        request.Copies
                    ),
                    cancellationToken
                );

                if (!printResult.Success)
                {
                    return new PipelineResult(false, sw.ElapsedMilliseconds, results, printResult.Error);
                }

                results.Add($"✓ Print completed in {printResult.Metadata.ProcessingTimeMs}ms");
            }

            sw.Stop();
            results.Add($"\n⚡ Total pipeline time: {sw.ElapsedMilliseconds}ms");

            return new PipelineResult(true, sw.ElapsedMilliseconds, results);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new PipelineResult(false, sw.ElapsedMilliseconds, results, ex.Message);
        }
    }
}

public record PipelineRequest(
    string Source,
    ScanMode ScanMode = ScanMode.Lightning,
    int Quality = 300,
    bool UseResonance = true,
    bool UseNanoDecibel = true,
    ProcessingEngine ProcessingEngine = ProcessingEngine.Auto,
    bool UseAI = true,
    bool UseClisonic = true,
    int OptimizationLevel = 3,
    bool UseStigmaMemory = true,
    bool ShouldPrint = true,
    string PrintDestination = "default",
    PrintQuality PrintQuality = PrintQuality.Stigma,
    bool UseStigma = true,
    int Copies = 1
);

public record PipelineResult(
    bool Success,
    long TotalTimeMs,
    List<string> Steps,
    string? Error = null
);
