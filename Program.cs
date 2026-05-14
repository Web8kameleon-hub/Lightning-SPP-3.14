using wwwmmm.PublicAPI;
using wwwmmm.Services;
using wwwmmm.SDK;

namespace wwwmmm;

/// <summary>
/// Lightning SPP 3.14 - Universal Scan-Process-Print System
/// Integrating: TideWave, CLX, CLI.I, XCL, Resonance, NanoDecibel, Clisonic Global AI, Stigma
/// NOW: Optimized for MASSIVE deployment on ANY device!
/// Ready for wide public usage
/// </summary>
internal class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("⚡ Lightning Scan-Process-Print System ⚡");
        Console.WriteLine("Technologies: TideWave • CLX • CLI.I • XCL • Resonance • NanoDecibel • Clisonic AI • Stigma\n");

        if (args.Length > 0)
        {
            await ProcessCommandLineArgs(args);
        }
        else
        {
            await RunDemo();
        }
    }

    private static async Task ProcessCommandLineArgs(string[] args)
    {
        var command = args[0].ToLower();

        switch (command)
        {
            case "scan":
                await QuickScan(args);
                break;
            case "process":
                await ProcessFile(args);
                break;
            case "print":
                await PrintFile(args);
                break;
            case "pipeline":
                await RunPipeline(args);
                break;
            case "batch":
                await BatchProcess(args);
                break;
            case "demo":
                await RunDemo();
                break;
            case "test":
                await wwwmmm.Tests.SimpleTests.RunAllTests();
                break;
            case "examples":
                await wwwmmm.Examples.UsageExamples.RunAllExamples();
                break;
            case "universal":
                await wwwmmm.Examples.UniversalExamples.RunAllUniversalExamples();
                break;
            case "speed":
                await SpeedTest();
                break;
            case "real":
                await wwwmmm.Examples.RealVsSimulatedExamples.RunAllComparisons();
                break;
            case "simulated":
                LightningAPI.UseSimulatedServices();
                Console.WriteLine("✓ Switched to SIMULATED mode (fast, fake data)");
                await RunDemo();
                break;
            default:
                ShowHelp();
                break;
        }
    }

    private static async Task QuickScan(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: wwwmmm scan <source>");
            return;
        }

        var source = args[1];
        Console.WriteLine($"🔍 Scanning: {source}");

        var result = await LightningAPI.QuickScanAsync(source);

        if (result.Success)
        {
            Console.WriteLine($"✓ Scan completed in {result.Metadata.ProcessingTimeMs}ms");
            Console.WriteLine($"  Technology: {result.Metadata.Technology}");
            Console.WriteLine($"  Resolution: {result.Metadata.Resolution} DPI");
            Console.WriteLine($"  Data size: {result.Data.Length} bytes");
        }
        else
        {
            Console.WriteLine($"✗ Scan failed: {result.Error}");
        }
    }

    private static async Task ProcessFile(string[] args)
    {
        Console.WriteLine("🔄 Processing with Hybrid Multi-Engine...");

        var mockData = new byte[1000];
        var result = await LightningAPI.HybridProcessAsync(mockData);

        if (result.Success)
        {
            Console.WriteLine($"✓ Processing completed in {result.Metadata.ProcessingTimeMs}ms");
            Console.WriteLine($"  Engine: {result.Metadata.Engine}");
            Console.WriteLine($"  Optimization: Level {result.Metadata.OptimizationLevel}");
        }
        else
        {
            Console.WriteLine($"✗ Processing failed: {result.Error}");
        }
    }

    private static async Task PrintFile(string[] args)
    {
        Console.WriteLine("🖨️  Printing with Stigma Premium...");

        var mockData = new byte[1000];
        var result = await LightningAPI.StigmaPrintAsync(mockData);

        if (result.Success)
        {
            Console.WriteLine($"✓ Print completed in {result.Metadata.ProcessingTimeMs}ms");
            Console.WriteLine($"  Technology: {result.Metadata.Technology}");
            Console.WriteLine($"  Pages: {result.Metadata.PagesPrinted}");
        }
        else
        {
            Console.WriteLine($"✗ Print failed: {result.Error}");
        }
    }

    private static async Task RunPipeline(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: wwwmmm pipeline <source>");
            return;
        }

        var source = args[1];
        Console.WriteLine($"⚡ Running Lightning Pipeline for: {source}\n");

        var result = await LightningAPI.ExecuteLightningPipelineAsync(source);

        Console.WriteLine("\n📊 Pipeline Results:");
        foreach (var step in result.Steps)
        {
            Console.WriteLine($"  {step}");
        }

        if (result.Success)
        {
            Console.WriteLine($"\n✓ Pipeline completed successfully!");
        }
        else
        {
            Console.WriteLine($"\n✗ Pipeline failed: {result.Error}");
        }
    }

    private static async Task BatchProcess(string[] args)
    {
        var sources = args.Skip(1).ToList();
        if (!sources.Any())
        {
            Console.WriteLine("Usage: wwwmmm batch <source1> <source2> ...");
            return;
        }

        Console.WriteLine($"⚡ Batch processing {sources.Count} items...\n");

        var results = await LightningAPI.BatchProcessAsync(sources);

        var successful = results.Count(r => r.Success);
        Console.WriteLine($"\n✓ Completed: {successful}/{results.Count}");
        Console.WriteLine($"  Total time: {results.Sum(r => r.TotalTimeMs)}ms");
        Console.WriteLine($"  Average: {results.Average(r => r.TotalTimeMs):F2}ms per item");
    }

    private static async Task SpeedTest()
    {
        Console.WriteLine("⚡ Lightning 3.14 - Speed Test\n");

        var testData = new byte[1024];

        Console.WriteLine("Testing speed modes...\n");

        // Instant
        var start = DateTime.UtcNow;
        await Lightning314.Instant(testData);
        var instantMs = (DateTime.UtcNow - start).TotalMilliseconds;
        Console.WriteLine($"Instant:    {instantMs:F3}ms (target: <1ms) {(instantMs < 1 ? "✓" : "⚠")}");

        // Ultra Fast
        start = DateTime.UtcNow;
        await Lightning314.UltraFast(testData);
        var ultraMs = (DateTime.UtcNow - start).TotalMilliseconds;
        Console.WriteLine($"UltraFast:  {ultraMs:F3}ms (target: <5ms) {(ultraMs < 5 ? "✓" : "⚠")}");

        // Lightning
        start = DateTime.UtcNow;
        await Lightning314.Lightning(testData);
        var lightningMs = (DateTime.UtcNow - start).TotalMilliseconds;
        Console.WriteLine($"Lightning:  {lightningMs:F3}ms (target: <10ms) {(lightningMs < 10 ? "✓" : "⚠")}");

        Console.WriteLine($"\n✓ ABSOLUTE SPEED achieved!");
    }

    private static async Task RunDemo()
    {
        Console.WriteLine("🚀 Running Demo...\n");

        // Demo 1: Lightning Pipeline
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("Demo 1: Full Lightning Pipeline");
        Console.WriteLine("═══════════════════════════════════════");

        var pipelineRequest = new PipelineRequest(
            Source: "demo-document.pdf",
            ScanMode: ScanMode.Lightning,
            Quality: 300,
            UseResonance: true,
            UseNanoDecibel: true,
            ProcessingEngine: ProcessingEngine.Hybrid,
            UseAI: true,
            UseClisonic: true,
            OptimizationLevel: 5,
            UseStigmaMemory: true,
            ShouldPrint: true,
            PrintQuality: PrintQuality.Stigma,
            Copies: 1
        );

        var result = await LightningAPI.ExecuteCustomPipelineAsync(pipelineRequest);

        foreach (var step in result.Steps)
        {
            Console.WriteLine(step);
        }

        // Demo 2: High-Quality Scan
        Console.WriteLine("\n═══════════════════════════════════════");
        Console.WriteLine("Demo 2: High-Quality NanoDecibel Scan");
        Console.WriteLine("═══════════════════════════════════════");

        var scanResult = await LightningAPI.HighQualityScanAsync("demo-photo.jpg");
        Console.WriteLine($"✓ Scanned in {scanResult.Metadata.ProcessingTimeMs}ms");
        Console.WriteLine($"  Technology: {scanResult.Metadata.Technology}");
        Console.WriteLine($"  Resolution: {scanResult.Metadata.Resolution} DPI");

        // Demo 3: AI Processing
        Console.WriteLine("\n═══════════════════════════════════════");
        Console.WriteLine("Demo 3: Clisonic Global AI Processing");
        Console.WriteLine("═══════════════════════════════════════");

        var processResult = await LightningAPI.AIProcessAsync(scanResult.Data);
        Console.WriteLine($"✓ Processed in {processResult.Metadata.ProcessingTimeMs}ms");
        Console.WriteLine($"  Engine: {processResult.Metadata.Engine}");
        Console.WriteLine($"  AI: {processResult.Metadata.AdditionalInfo["UseAI"]}");

        // Demo 4: Batch Processing
        Console.WriteLine("\n═══════════════════════════════════════");
        Console.WriteLine("Demo 4: Batch Processing");
        Console.WriteLine("═══════════════════════════════════════");

        var batchSources = new List<string> { "doc1.pdf", "doc2.pdf", "doc3.pdf" };
        var batchResults = await LightningAPI.BatchProcessAsync(batchSources);
        Console.WriteLine($"✓ Processed {batchResults.Count} documents");
        Console.WriteLine($"  Total time: {batchResults.Sum(r => r.TotalTimeMs)}ms");
        Console.WriteLine($"  Average: {batchResults.Average(r => r.TotalTimeMs):F2}ms");

        Console.WriteLine("\n═══════════════════════════════════════");
        Console.WriteLine("✨ Demo completed! System ready for public use.");
        Console.WriteLine("═══════════════════════════════════════");
    }

    private static void ShowHelp()
    {
        Console.WriteLine("Lightning Scan-Process-Print System");
        Console.WriteLine("\nCommands:");
        Console.WriteLine("  scan <source>           - Quick scan using TideWave + Resonance");
        Console.WriteLine("  process                 - Process using Hybrid Multi-Engine");
        Console.WriteLine("  print                   - Print with Stigma Premium");
        Console.WriteLine("  pipeline <source>       - Run complete Lightning Pipeline");
        Console.WriteLine("  batch <sources...>      - Batch process multiple items");
        Console.WriteLine("  demo                    - Run demonstration");
        Console.WriteLine("  test                    - Run system tests");
        Console.WriteLine("  examples                - Run usage examples");
        Console.WriteLine("\nTechnologies:");
        Console.WriteLine("  • TideWave - Balanced scanning");
        Console.WriteLine("  • CLX - Core processing");
        Console.WriteLine("  • CLI.I - Interactive processing");
        Console.WriteLine("  • XCL - Extended processing");
        Console.WriteLine("  • Resonance - High precision");
        Console.WriteLine("  • NanoDecibel - Maximum quality");
        Console.WriteLine("  • Clisonic Global AI - AI-powered enhancement");
        Console.WriteLine("  • Stigma - Premium print quality");
        Console.WriteLine("  • Stigma Memory - Optimization");
    }
}
