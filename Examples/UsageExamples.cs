using wwwmmm.PublicAPI;
using wwwmmm.Services;
using wwwmmm.Services.Implementation;

namespace wwwmmm.Examples;

/// <summary>
/// Shembuj praktikë për përdorim të Lightning Scan-Process-Print System
/// </summary>
public static class UsageExamples
{
    /// <summary>
    /// Shembull 1: Skanim dhe printim i shpejtë
    /// </summary>
    public static async Task QuickScanAndPrint()
    {
        Console.WriteLine("Shembull 1: Skanim dhe Printim i Shpejtë\n");

        // Skanim i shpejtë
        var scanResult = await LightningAPI.QuickScanAsync("document.pdf");
        
        if (scanResult.Success)
        {
            Console.WriteLine($"✓ Dokumenti u skanua në {scanResult.Metadata.ProcessingTimeMs}ms");
            
            // Printim me Stigma
            var printResult = await LightningAPI.StigmaPrintAsync(scanResult.Data);
            
            if (printResult.Success)
            {
                Console.WriteLine($"✓ Dokumenti u printua në {printResult.Metadata.ProcessingTimeMs}ms");
            }
        }
    }

    /// <summary>
    /// Shembull 2: Pipeline i plotë me konfigurim custom
    /// </summary>
    public static async Task CustomPipeline()
    {
        Console.WriteLine("Shembull 2: Pipeline i Plotë me Konfigurim Custom\n");

        var request = new PipelineRequest(
            Source: "photo.jpg",
            ScanMode: ScanMode.NanoDecibel,      // Cilësi maksimale
            Quality: 600,
            UseResonance: true,
            UseNanoDecibel: true,
            ProcessingEngine: ProcessingEngine.Hybrid, // Të gjithë motorët
            UseAI: true,
            UseClisonic: true,
            OptimizationLevel: 5,
            UseStigmaMemory: true,
            ShouldPrint: true,
            PrintQuality: PrintQuality.Stigma,
            Copies: 2
        );

        var result = await LightningAPI.ExecuteCustomPipelineAsync(request);
        
        if (result.Success)
        {
            Console.WriteLine("✓ Pipeline përfundoi me sukses!");
            Console.WriteLine($"  Kohë totale: {result.TotalTimeMs}ms");
        }
    }

    /// <summary>
    /// Shembull 3: Përpunim në grup (batch)
    /// </summary>
    public static async Task BatchProcessing()
    {
        Console.WriteLine("Shembull 3: Përpunim në Grup\n");

        var documents = new List<string>
        {
            "invoice-001.pdf",
            "invoice-002.pdf",
            "invoice-003.pdf",
            "invoice-004.pdf",
            "invoice-005.pdf"
        };

        var results = await LightningAPI.BatchProcessAsync(documents);
        
        var successful = results.Count(r => r.Success);
        var totalTime = results.Sum(r => r.TotalTimeMs);
        var avgTime = results.Average(r => r.TotalTimeMs);

        Console.WriteLine($"✓ Përpunuar: {successful}/{documents.Count} dokumente");
        Console.WriteLine($"  Kohë totale: {totalTime}ms");
        Console.WriteLine($"  Mesatare: {avgTime:F2}ms për dokument");
    }

    /// <summary>
    /// Shembull 4: Skanim me cilësi të ndryshme
    /// </summary>
    public static async Task DifferentQualityScans()
    {
        Console.WriteLine("Shembull 4: Skanim me Cilësi të Ndryshme\n");

        // Lightning scan - më i shpejti
        var lightningResult = await LightningAPI.QuickScanAsync("doc1.pdf");
        Console.WriteLine($"Lightning Scan: {lightningResult.Metadata.ProcessingTimeMs}ms");

        // High-quality scan - cilësi e lartë
        var hqResult = await LightningAPI.HighQualityScanAsync("photo.jpg");
        Console.WriteLine($"High Quality Scan: {hqResult.Metadata.ProcessingTimeMs}ms");
    }

    /// <summary>
    /// Shembull 5: Përpunim me AI
    /// </summary>
    public static async Task AIProcessing()
    {
        Console.WriteLine("Shembull 5: Përpunim me Clisonic Global AI\n");

        // Skanim fillimisht
        var scanResult = await LightningAPI.QuickScanAsync("complex-document.pdf");
        
        if (scanResult.Success)
        {
            // Përpunim me AI
            var processResult = await LightningAPI.AIProcessAsync(scanResult.Data);
            
            if (processResult.Success)
            {
                Console.WriteLine($"✓ AI Processing përfundoi në {processResult.Metadata.ProcessingTimeMs}ms");
                Console.WriteLine($"  Motor: {processResult.Metadata.Engine}");
                Console.WriteLine($"  Optimizim: Level {processResult.Metadata.OptimizationLevel}");
            }
        }
    }

    /// <summary>
    /// Shembull 6: Krahasim i motorëve të ndryshëm
    /// </summary>
    public static async Task EngineComparison()
    {
        Console.WriteLine("Shembull 6: Krahasim i Motorëve të Ndryshëm\n");

        var testData = new byte[5000];

        // CLX
        var clxOptions = new ProcessingOptions(ProcessingEngine.CLX);
        var clxResult = await new HybridProcessingService().ProcessAsync(testData, clxOptions);
        Console.WriteLine($"CLX: {clxResult.Metadata.ProcessingTimeMs}ms");

        // CLI.I
        var cliOptions = new ProcessingOptions(ProcessingEngine.CLI_I);
        var cliResult = await new HybridProcessingService().ProcessAsync(testData, cliOptions);
        Console.WriteLine($"CLI.I: {cliResult.Metadata.ProcessingTimeMs}ms");

        // XCL
        var xclOptions = new ProcessingOptions(ProcessingEngine.XCL);
        var xclResult = await new HybridProcessingService().ProcessAsync(testData, xclOptions);
        Console.WriteLine($"XCL: {xclResult.Metadata.ProcessingTimeMs}ms");

        // Clisonic AI
        var aiOptions = new ProcessingOptions(ProcessingEngine.Clisonic);
        var aiResult = await new HybridProcessingService().ProcessAsync(testData, aiOptions);
        Console.WriteLine($"Clisonic AI: {aiResult.Metadata.ProcessingTimeMs}ms");

        // Hybrid
        var hybridOptions = new ProcessingOptions(ProcessingEngine.Hybrid);
        var hybridResult = await new HybridProcessingService().ProcessAsync(testData, hybridOptions);
        Console.WriteLine($"Hybrid: {hybridResult.Metadata.ProcessingTimeMs}ms");
    }

    /// <summary>
    /// Shembull 7: Pipeline minimal për test
    /// </summary>
    public static async Task MinimalPipeline()
    {
        Console.WriteLine("Shembull 7: Pipeline Minimal\n");

        // Pipeline me parametra default
        var result = await LightningAPI.ExecuteLightningPipelineAsync("test.pdf");
        
        foreach (var step in result.Steps)
        {
            Console.WriteLine(step);
        }
    }

    /// <summary>
    /// Shembull 8: Error handling
    /// </summary>
    public static async Task ErrorHandling()
    {
        Console.WriteLine("Shembull 8: Error Handling\n");

        try
        {
            var result = await LightningAPI.ExecuteLightningPipelineAsync("nonexistent.pdf");
            
            if (!result.Success)
            {
                Console.WriteLine($"⚠️  Pipeline failed: {result.Error}");
                Console.WriteLine($"   Completed steps:");
                foreach (var step in result.Steps)
                {
                    Console.WriteLine($"   {step}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Ekzekuto të gjithë shembujt
    /// </summary>
    public static async Task RunAllExamples()
    {
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("        LIGHTNING SPP - EXAMPLES       ");
        Console.WriteLine("═══════════════════════════════════════\n");

        await QuickScanAndPrint();
        Console.WriteLine();

        await CustomPipeline();
        Console.WriteLine();

        await BatchProcessing();
        Console.WriteLine();

        await DifferentQualityScans();
        Console.WriteLine();

        await AIProcessing();
        Console.WriteLine();

        await EngineComparison();
        Console.WriteLine();

        await MinimalPipeline();
        Console.WriteLine();

        await ErrorHandling();

        Console.WriteLine("\n═══════════════════════════════════════");
        Console.WriteLine("     TË GJITHË SHEMBUJT PËRFUNDUAN     ");
        Console.WriteLine("═══════════════════════════════════════");
    }
}
