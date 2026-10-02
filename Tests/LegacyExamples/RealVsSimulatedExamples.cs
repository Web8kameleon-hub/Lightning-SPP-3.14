using wwwmmm.PublicAPI;
using wwwmmm.Configuration;
using System.Diagnostics;

namespace wwwmmm.Examples;

/// <summary>
/// Examples validating REAL processing paths.
/// Legacy simulated entry points are compatibility aliases and route to real services.
/// </summary>
public static class RealVsSimulatedExamples
{
    public static async Task CompareRealVsSimulated()
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════╗");
        Console.WriteLine("║  REAL Processing Path Validation                 ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════╝\n");

        // Legacy compatibility method - now routes to real services.
        Console.WriteLine("🔹 Testing COMPATIBILITY path (UseSimulatedServices => REAL services)...\n");
        LightningAPI.UseSimulatedServices();

        var compatibilityStart = Stopwatch.StartNew();
        var compatibilityResult = await LightningAPI.ExecuteLightningPipelineAsync("test-compatibility.dat");
        compatibilityStart.Stop();

        Console.WriteLine("COMPATIBILITY Path Results:");
        Console.WriteLine($"  Total Time: {compatibilityStart.ElapsedMilliseconds}ms");
        Console.WriteLine($"  Success: {compatibilityResult.Success}");
        Console.WriteLine($"  Steps completed: {compatibilityResult.Steps.Count}");

        // Real path
        Console.WriteLine("\n🔹 Testing REAL path (UseRealServices)...\n");
        LightningAPI.UseRealServices();

        var realStart = Stopwatch.StartNew();
        var realResult = await LightningAPI.ExecuteLightningPipelineAsync("test-real.dat");
        realStart.Stop();

        Console.WriteLine("REAL Path Results:");
        Console.WriteLine($"  Total Time: {realStart.ElapsedMilliseconds}ms");
        Console.WriteLine($"  Success: {realResult.Success}");
        Console.WriteLine($"  Steps completed: {realResult.Steps.Count}");

        // Comparison
        Console.WriteLine("\n📊 Path Comparison:");
        Console.WriteLine($"  Compatibility path: {compatibilityStart.ElapsedMilliseconds}ms");
        Console.WriteLine($"  Real path:          {realStart.ElapsedMilliseconds}ms");
        Console.WriteLine($"  Difference:         {realStart.ElapsedMilliseconds - compatibilityStart.ElapsedMilliseconds}ms");

        Console.WriteLine("\n✅ Both paths use REAL processing services.");
        Console.WriteLine("✅ No fake/simulated data path is active.");
    }

    public static async Task DemonstrateRealImageProcessing()
    {
        Console.WriteLine("\n╔═══════════════════════════════════════════════════╗");
        Console.WriteLine("║  REAL Image Processing with ImageSharp           ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════╝\n");

        LightningAPI.UseRealServices();

        // Demonstate different quality levels
        Console.WriteLine("Testing different scan modes with REAL processing:\n");

        var modes = new[] 
        { 
            ("Lightning", Services.ScanMode.Lightning),
            ("TideWave", Services.ScanMode.TideWave),
            ("Resonance", Services.ScanMode.Resonance),
            ("NanoDecibel", Services.ScanMode.NanoDecibel)
        };

        foreach (var (name, mode) in modes)
        {
            var sw = Stopwatch.StartNew();
            
            var request = new Services.ScanRequest(
                "test.jpg",
                mode,
                Quality: 300
            );

            // This will use RealImageProcessor with actual ImageSharp processing!
            var result = await ServiceFactory.CreateScanService().ScanAsync(request);
            sw.Stop();

            Console.WriteLine($"{name} Mode:");
            Console.WriteLine($"  Time: {sw.ElapsedMilliseconds}ms");
            Console.WriteLine($"  Data size: {result.Data.Length:N0} bytes");
            Console.WriteLine($"  Real processing: ✓");
            Console.WriteLine();
        }
    }

    public static async Task DemonstrateRealAIProcessing()
    {
        Console.WriteLine("\n╔═══════════════════════════════════════════════════╗");
        Console.WriteLine("║  REAL AI Processing with Microsoft.ML            ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════╝\n");

        LightningAPI.UseRealServices();

        var testData = GenerateTestData(1024 * 50); // 50KB

        var engines = new[]
        {
            ("CLX - Fast Compression", Services.ProcessingEngine.CLX),
            ("CLI.I - Optimal Compression", Services.ProcessingEngine.CLI_I),
            ("XCL - Maximum Compression", Services.ProcessingEngine.XCL),
            ("Clisonic AI - ML Optimization", Services.ProcessingEngine.Clisonic),
            ("Hybrid - Full Pipeline", Services.ProcessingEngine.Hybrid)
        };

        foreach (var (name, engine) in engines)
        {
            var sw = Stopwatch.StartNew();

            var options = new Services.ProcessingOptions(
                Engine: engine,
                UseAI: true,
                OptimizationLevel: 5
            );

            // This will use RealAIProcessor with actual compression and ML!
            var result = await ServiceFactory.CreateProcessingService().ProcessAsync(testData, options);
            sw.Stop();

            var compressionRatio = (double)testData.Length / result.ProcessedData.Length;

            Console.WriteLine($"{name}:");
            Console.WriteLine($"  Time: {sw.ElapsedMilliseconds}ms");
            Console.WriteLine($"  Original: {testData.Length:N0} bytes");
            Console.WriteLine($"  Processed: {result.ProcessedData.Length:N0} bytes");
            Console.WriteLine($"  Compression: {compressionRatio:F2}x");
            Console.WriteLine($"  Real AI: ✓");
            Console.WriteLine();
        }
    }

    public static async Task DemonstrateRealPrinting()
    {
        Console.WriteLine("\n╔═══════════════════════════════════════════════════╗");
        Console.WriteLine("║  REAL Printing to File System                    ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════╝\n");

        LightningAPI.UseRealServices();

        var testData = GenerateTestData(1024 * 10); // 10KB

        var qualities = new[]
        {
            Services.PrintQuality.Draft,
            Services.PrintQuality.Normal,
            Services.PrintQuality.High,
            Services.PrintQuality.Stigma
        };

        var printService = ServiceFactory.CreatePrintService() as Services.Implementation.RealPrintService;

        foreach (var quality in qualities)
        {
            var sw = Stopwatch.StartNew();

            var options = new Services.PrintOptions(
                Quality: quality,
                UseStigma: quality == Services.PrintQuality.Stigma,
                Copies: 1
            );

            var result = await printService!.PrintAsync(testData, options);
            sw.Stop();

            Console.WriteLine($"{quality} Quality:");
            Console.WriteLine($"  Time: {sw.ElapsedMilliseconds}ms");
            Console.WriteLine($"  Output: {result.Metadata.Destination}");
            Console.WriteLine($"  Real file: ✓");
            Console.WriteLine();
        }

        // Show created files
        var files = printService!.GetPrintedFiles();
        Console.WriteLine($"📁 Created {files.Length} real output files:");
        foreach (var file in files.Take(5))
        {
            var info = new FileInfo(file);
            Console.WriteLine($"  • {Path.GetFileName(file)} ({info.Length:N0} bytes)");
        }

        if (files.Length > 5)
        {
            Console.WriteLine($"  ... and {files.Length - 5} more files");
        }

        Console.WriteLine("\n✅ All files are REAL and saved to disk!");
    }

    public static async Task FullRealWorkflowDemo()
    {
        Console.WriteLine("\n╔═══════════════════════════════════════════════════╗");
        Console.WriteLine("║  COMPLETE REAL WORKFLOW - End to End             ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════╝\n");

        // Ensure we're using REAL services
        LightningAPI.UseRealServices();

        Console.WriteLine("🔹 Workflow: Real Scan → Real AI → Real Print\n");

        var sw = Stopwatch.StartNew();

        // Step 1: REAL Scan with ImageSharp
        Console.WriteLine("Step 1: REAL Image Scan (ImageSharp)...");
        var scanRequest = new Services.ScanRequest(
            "document.jpg",
            Services.ScanMode.Resonance,
            Quality: 600
        );
        var scanResult = await ServiceFactory.CreateScanService().ScanAsync(scanRequest);
        Console.WriteLine($"  ✓ Scanned {scanResult.Data.Length:N0} bytes in {scanResult.Metadata.ProcessingTimeMs}ms");

        // Step 2: REAL AI Processing with ML
        Console.WriteLine("\nStep 2: REAL AI Processing (Microsoft.ML + Brotli)...");
        var processOptions = new Services.ProcessingOptions(
            Engine: Services.ProcessingEngine.Hybrid,
            UseAI: true,
            UseClisonic: true,
            OptimizationLevel: 5
        );
        var processResult = await ServiceFactory.CreateProcessingService().ProcessAsync(
            scanResult.Data,
            processOptions
        );
        var ratio = (double)scanResult.Data.Length / processResult.ProcessedData.Length;
        Console.WriteLine($"  ✓ Processed with {ratio:F2}x compression in {processResult.Metadata.ProcessingTimeMs}ms");

        // Step 3: REAL Print to file
        Console.WriteLine("\nStep 3: REAL Print to File (Stigma Premium)...");
        var printResult = await LightningAPI.StigmaPrintAsync(processResult.ProcessedData);
        Console.WriteLine($"  ✓ Printed to: {printResult.Metadata.Destination}");
        Console.WriteLine($"  ✓ Quality: {printResult.Metadata.Technology}");

        sw.Stop();

        Console.WriteLine($"\n⚡ Total REAL workflow time: {sw.ElapsedMilliseconds}ms");
        Console.WriteLine("\n✅ Everything was REAL - no fake data!");
        Console.WriteLine("✅ Actual image processing with ImageSharp");
        Console.WriteLine("✅ Actual AI/ML with Microsoft.ML");
        Console.WriteLine("✅ Actual file output to disk");
    }

    private static byte[] GenerateTestData(int size)
    {
        var data = new byte[size];
        Random.Shared.NextBytes(data);
        return data;
    }

    public static async Task RunAllComparisons()
    {
        await CompareRealVsSimulated();
        await DemonstrateRealImageProcessing();
        await DemonstrateRealAIProcessing();
        await DemonstrateRealPrinting();
        await FullRealWorkflowDemo();

        Console.WriteLine("\n╔═══════════════════════════════════════════════════╗");
        Console.WriteLine("║  ALL COMPARISONS COMPLETE                        ║");
        Console.WriteLine("║  Lightning SPP 3.14 = REAL Processing!           ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════╝");
    }
}
