using System.Diagnostics;
using LightningSPP.Licensing;

namespace wwwmmm.Services;

/// <summary>
/// Orchestrates the real Scan-Process-Print services under one explicit Stigma profile.
/// </summary>
public class LightningPipeline
{
    private readonly IScanService _scanService;
    private readonly IProcessingService _processingService;
    private readonly IPrintService _printService;
    private readonly StigmaExecutionProfile _profile;

    public LightningPipeline(
        IScanService scanService,
        IProcessingService processingService,
        IPrintService printService,
        StigmaExecutionProfile? profile = null)
    {
        _scanService = scanService;
        _processingService = processingService;
        _printService = printService;
        _profile = profile ?? StigmaProfiles.FromEnvironment();
    }

    public async Task<PipelineResult> ExecuteAsync(
        PipelineRequest request,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var results = new List<string>();
        request = StigmaProfiles.Apply(request, _profile);
        results.Add($"Stigma profile: {(int)_profile} ({_profile.ToString().ToLowerInvariant()})");

        try
        {
            if (!LicenseManager.CheckAndIncrementUsage())
            {
                LightningMetrics.RecordPipeline(false);
                return new PipelineResult(false, 0, results,
                    "Operation limit reached. Please upgrade your Lightning SPP license or wait for the next billing cycle.");
            }

            results.Add("Starting scan...");
            var scanResult = await _scanService.ScanAsync(
                new ScanRequest(
                    request.Source,
                    request.ScanMode,
                    request.Quality,
                    request.UseResonance,
                    request.UseNanoDecibel),
                cancellationToken);

            if (!scanResult.Success)
            {
                LightningMetrics.RecordPipeline(false);
                return new PipelineResult(false, sw.ElapsedMilliseconds, results, scanResult.Error);
            }

            LightningMetrics.RecordScan(scanResult.Metadata.ProcessingTimeMs);
            results.Add($"Scan completed in {scanResult.Metadata.ProcessingTimeMs}ms using {scanResult.Metadata.Technology}");

            results.Add("Starting processing...");
            var processResult = await _processingService.ProcessAsync(
                scanResult.Data,
                new ProcessingOptions(
                    request.ProcessingEngine,
                    request.UseAI,
                    request.UseClisonix,
                    request.OptimizationLevel,
                    request.UseStigmaMemory),
                cancellationToken);

            if (!processResult.Success)
            {
                LightningMetrics.RecordPipeline(false);
                return new PipelineResult(false, sw.ElapsedMilliseconds, results, processResult.Error);
            }

            LightningMetrics.RecordProcess(processResult.Metadata.ProcessingTimeMs);
            results.Add($"Processing completed in {processResult.Metadata.ProcessingTimeMs}ms using {processResult.Metadata.Engine}");

            if (request.ShouldPrint)
            {
                results.Add("Creating output artifact...");
                var printResult = await _printService.PrintAsync(
                    processResult.ProcessedData,
                    new PrintOptions(
                        request.PrintDestination,
                        request.PrintQuality,
                        request.UseStigma,
                        request.UseResonance,
                        request.Copies),
                    cancellationToken);

                if (!printResult.Success)
                {
                    LightningMetrics.RecordPipeline(false);
                    return new PipelineResult(false, sw.ElapsedMilliseconds, results, printResult.Error);
                }

                LightningMetrics.RecordPrint(printResult.Metadata.ProcessingTimeMs);
                results.Add($"Output completed in {printResult.Metadata.ProcessingTimeMs}ms at {printResult.Metadata.Destination}");
            }

            sw.Stop();
            results.Add($"Total pipeline time: {sw.ElapsedMilliseconds}ms");
            LightningMetrics.RecordPipeline(true);
            return new PipelineResult(true, sw.ElapsedMilliseconds, results);
        }
        catch (Exception ex)
        {
            sw.Stop();
            LightningMetrics.RecordPipeline(false);
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
    bool UseClisonix = false,
    int OptimizationLevel = 3,
    bool UseStigmaMemory = false,
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
