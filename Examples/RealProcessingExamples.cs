using wwwmmm.PublicAPI;
using wwwmmm.Configuration;
using wwwmmm.Services;
using wwwmmm.Services.Implementation;
using wwwmmm.Services.Imaging;
using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace wwwmmm.Examples;

/// <summary>
/// Validation of the real processing paths. Every output here is produced by a real
/// engine writing real bytes; nothing is simulated and no compatibility alias exists.
/// </summary>
public static class RealProcessingExamples
{
    public static async Task VerifyProductionPipeline()
    {
        using var input = new ValidationImage();
        Console.WriteLine("╔═══════════════════════════════════════════════════╗");
        Console.WriteLine("║  Production pipeline verification                 ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════╝\n");

        LightningAPI.UseRealServices();

        var started = Stopwatch.StartNew();
        var result = await LightningAPI.ExecuteLightningPipelineAsync(input.Path);
        started.Stop();

        Require(result.Success, result.Error ?? "Pipeline failed");
        Console.WriteLine($"  Total time: {started.ElapsedMilliseconds}ms");
        Console.WriteLine($"  Steps completed: {result.Steps.Count}");
        Console.WriteLine("  Result: real scan, real output, real artifact\n");
    }

    /// <summary>
    /// Runs the L (Skia) and R (ImageSharp) engines explicitly through the adapter.
    /// No silent fallback: each engine must produce its own bytes or fail loudly.
    /// </summary>
    public static async Task DemonstrateImageEngines()
    {
        using var input = new ValidationImage();
        Console.WriteLine("╔═══════════════════════════════════════════════════╗");
        Console.WriteLine("║  L/R image engines: Skia and ImageSharp           ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════╝\n");

        var adapter = new ImageEngineAdapter(
            new RealImageProcessor(),
            new FastScanService(NullLogger<FastScanService>.Instance));

        var source = await File.ReadAllBytesAsync(input.Path);
        var modes = new[] { ScanMode.Lightning, ScanMode.TideWave, ScanMode.Resonance, ScanMode.NanoDecibel };
        var engines = new[] { ImageEngine.Skia, ImageEngine.ImageSharp };

        foreach (var engine in engines)
        {
            Console.WriteLine($"Engine selection: {engine}");
            foreach (var mode in modes)
            {
                var started = Stopwatch.StartNew();
                var result = await adapter.ProcessAsync(engine, source, mode, quality: 300);
                started.Stop();

                Require(result.Success && result.Data.Length > 0, result.Error ?? $"{engine}/{mode} returned no data");
                Console.WriteLine(
                    $"  {mode,-12} {result.Data.Length,9:N0} bytes  " +
                    $"{started.ElapsedMilliseconds,5} ms  sha256:{ShortHash(result.Data)}");
            }
            Console.WriteLine();
        }

        Console.WriteLine("Both engines were selected explicitly. Neither substituted the other.\n");
    }

    public static async Task DemonstrateLosslessCompression()
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════╗");
        Console.WriteLine("║  Lossless Brotli engines                          ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════╝\n");

        LightningAPI.UseRealServices();

        var testData = GenerateTestData(1024 * 50);
        var engines = new[]
        {
            ("CLX", ProcessingEngine.CLX),
            ("CLX.i", ProcessingEngine.CLX_I),
            ("XCL", ProcessingEngine.XCL),
            ("Hybrid", ProcessingEngine.Hybrid)
        };

        foreach (var (name, engine) in engines)
        {
            var started = Stopwatch.StartNew();
            var options = new ProcessingOptions(
                Engine: engine,
                UseAI: false,
                UseClisonix: false,
                OptimizationLevel: 5,
                UseStigmaMemory: false);

            var result = await ServiceFactory.CreateProcessingService().ProcessAsync(testData, options);
            started.Stop();

            Require(result.Success && result.ProcessedData.Length > 0, $"{name} processing returned no data");
            var ratio = (double)testData.Length / result.ProcessedData.Length;
            Console.WriteLine($"  {name,-8} {result.ProcessedData.Length,9:N0} bytes  {ratio,5:F2}x  {started.ElapsedMilliseconds,5} ms");
        }

        Console.WriteLine("\nThese engines compress and transform bytes. They are not model inference.\n");
    }

    public static async Task DemonstratePrintArtifacts()
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════╗");
        Console.WriteLine("║  Print artifacts (lossless .br, not paper)        ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════╝\n");

        LightningAPI.UseRealServices();

        var testData = GenerateTestData(1024 * 10);
        var printService = ServiceFactory.CreatePrintService() as RealPrintService;
        Require(printService is not null, "Print service is not the real implementation");

        foreach (var quality in new[] { PrintQuality.Draft, PrintQuality.Normal, PrintQuality.High, PrintQuality.Stigma })
        {
            var started = Stopwatch.StartNew();
            var options = new PrintOptions(
                Quality: quality,
                UseStigma: quality == PrintQuality.Stigma,
                Copies: 1);

            var result = await printService!.PrintAsync(testData, options);
            started.Stop();

            Require(
                result.Success && File.Exists(result.Metadata.Destination) && new FileInfo(result.Metadata.Destination).Length > 0,
                $"{quality} produced no artifact");

            var size = new FileInfo(result.Metadata.Destination).Length;
            Console.WriteLine($"  {quality,-8} {size,9:N0} bytes  {started.ElapsedMilliseconds,5} ms  {Path.GetFileName(result.Metadata.Destination)}");
        }

        Console.WriteLine("\nArtifacts exist on disk. Physical printing requires a configured Linux print queue and is not proven here.\n");
    }

    public static async Task FullRealWorkflowDemo()
    {
        using var input = new ValidationImage();
        Console.WriteLine("╔═══════════════════════════════════════════════════╗");
        Console.WriteLine("║  End-to-end workflow                              ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════╝\n");

        LightningAPI.UseRealServices();
        var total = Stopwatch.StartNew();

        Console.WriteLine("Step 1: scan (Resonance, 600)");
        var scanResult = await ServiceFactory.CreateScanService().ScanAsync(
            new ScanRequest(input.Path, ScanMode.Resonance, Quality: 600));
        Require(scanResult.Success && scanResult.Data.Length > 0, scanResult.Error ?? "Scan returned no data");
        Console.WriteLine($"  {scanResult.Data.Length:N0} bytes in {scanResult.Metadata.ProcessingTimeMs}ms");

        Console.WriteLine("\nStep 2: lossless processing");
        var processResult = await ServiceFactory.CreateProcessingService().ProcessAsync(
            scanResult.Data,
            new ProcessingOptions(ProcessingEngine.Hybrid, UseAI: false, UseClisonix: false, OptimizationLevel: 5, UseStigmaMemory: false));
        Require(processResult.Success && processResult.ProcessedData.Length > 0, "Processing returned no data");
        var ratio = (double)scanResult.Data.Length / processResult.ProcessedData.Length;
        Console.WriteLine($"  {ratio:F2}x, {processResult.Metadata.ProcessingTimeMs}ms");

        Console.WriteLine("\nStep 3: artifact");
        var printResult = await LightningAPI.StigmaPrintAsync(processResult.ProcessedData);
        Require(
            printResult.Success && File.Exists(printResult.Metadata.Destination) && new FileInfo(printResult.Metadata.Destination).Length > 0,
            "Artifact was not written");
        Console.WriteLine($"  {printResult.Metadata.Destination}");

        total.Stop();
        Console.WriteLine($"\nTotal: {total.ElapsedMilliseconds}ms. Every byte above came from a real engine.\n");
    }

    public static async Task RunAllComparisons()
    {
        await VerifyProductionPipeline();
        await DemonstrateImageEngines();
        await DemonstrateLosslessCompression();
        await DemonstratePrintArtifacts();
        await FullRealWorkflowDemo();

        Console.WriteLine("╔═══════════════════════════════════════════════════╗");
        Console.WriteLine("║  Real paths validated                             ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════╝");
    }

    private static byte[] GenerateTestData(int size)
    {
        var data = new byte[size];
        Random.Shared.NextBytes(data);
        return data;
    }

    private static string ShortHash(byte[] data) =>
        Convert.ToHexString(SHA256.HashData(data))[..16].ToLowerInvariant();

    private static void Require(bool condition, string error)
    {
        if (!condition) throw new InvalidOperationException(error);
    }

    private sealed class ValidationImage : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"spp-validation-{Guid.NewGuid():N}.png");

        public ValidationImage()
        {
            using var image = new Image<Rgba32>(128, 128);
            for (var y = 0; y < image.Height; y++)
                for (var x = 0; x < image.Width; x++)
                    image[x, y] = new Rgba32((byte)x, (byte)y, (byte)(x ^ y), 255);
            image.SaveAsPng(Path);
            Console.WriteLine("Input: generated 128x128 PNG validation fixture; processing and outputs are real.\n");
        }

        public void Dispose() => File.Delete(Path);
    }
}
