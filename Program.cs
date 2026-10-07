using wwwmmm.PublicAPI;
using wwwmmm.Services;
using wwwmmm.SDK;
using LightningSPP.Licensing;
using wwwmmm.Web;

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
            ShowHelp();
        }
    }

    private static async Task ProcessCommandLineArgs(string[] args)
    {
        var command = args[0].ToLower();

        switch (command)
        {
            case "activate":
                ActivateLicense(args);
                break;
            case "license":
            case "status":
                LicenseManager.ShowLicenseStatus();
                break;
            case "deactivate":
                LicenseManager.DeactivateLicense();
                break;
            case "issue-key":
                IssueLicenseKey(args);
                break;
            case "serve":
            case "api":
                await StartApiServer(args);
                break;
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
            case "speed":
                await SpeedTest(args);
                break;
            case "convert":
                await ConvertFile(args);
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

        if (args.Length < 2) throw new ArgumentException("Usage: process <source-file>");
        var input = await File.ReadAllBytesAsync(args[1]);
        var result = await LightningAPI.HybridProcessAsync(input);

        if (result.Success)
        {
            Console.WriteLine($"✓ Processing completed in {result.Metadata.ProcessingTimeMs}ms");
            Console.WriteLine($"  Engine: {result.Metadata.Engine}");
            Console.WriteLine($"  Optimization: Level {result.Metadata.OptimizationLevel}");
        }
        else
        {
            Environment.ExitCode = 1;
            Console.WriteLine($"✗ Processing failed: {result.Error}");
        }
    }

    private static async Task PrintFile(string[] args)
    {
        Console.WriteLine("🖨️  Printing with Stigma Premium...");

        if (args.Length < 2) throw new ArgumentException("Usage: print <source-file>");
        var input = await File.ReadAllBytesAsync(args[1]);
        var result = await LightningAPI.StigmaPrintAsync(input);

        if (result.Success)
        {
            Console.WriteLine($"✓ Print completed in {result.Metadata.ProcessingTimeMs}ms");
            Console.WriteLine($"  Technology: {result.Metadata.Technology}");
            Console.WriteLine($"  Pages: {result.Metadata.PagesPrinted}");
        }
        else
        {
            Environment.ExitCode = 1;
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

    private static async Task SpeedTest(string[] args)
    {
        Console.WriteLine("⚡ Lightning 3.14 - Speed Test\n");

        if (args.Length < 2) throw new ArgumentException("Usage: speed <source-file>");
        var testData = await File.ReadAllBytesAsync(args[1]);

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

        Console.WriteLine($"\nMeasurements completed for the supplied file.");
    }

    private static async Task ConvertFile(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Usage: wwwmmm convert <input-image> <png|jpeg|bmp|tiff|pdf|xlsx|docx> [quality 1-100]");
            return;
        }

        if (!TryParseOutputFormat(args[2], out var format))
        {
            Console.WriteLine($"Unknown format '{args[2]}'. Use png, jpeg, bmp, tiff, pdf, xlsx or docx.");
            Environment.ExitCode = 1;
            return;
        }

        var quality = args.Length > 3 && int.TryParse(args[3], out var parsedQuality) ? parsedQuality : 90;
        var input = args[1];

        var scan = await LightningAPI.QuickScanAsync(input);
        if (!scan.Success)
        {
            Environment.ExitCode = 1;
            Console.WriteLine($"✗ Convert failed: {scan.Error}");
            return;
        }

        var output = LightningAPI.EncodeOutput(scan.Data, format, quality);
        var extension = format switch
        {
            OutputFormat.Jpeg => ".jpg",
            OutputFormat.Tiff => ".tiff",
            OutputFormat.Xlsx => ".xlsx",
            OutputFormat.Docx => ".docx",
            _ => "." + format.ToString().ToLowerInvariant()
        };
        var outputPath = Path.ChangeExtension(input, extension);
        await File.WriteAllBytesAsync(outputPath, output);

        Console.WriteLine($"✓ Converted {input} → {outputPath}");
        Console.WriteLine($"  Format: {format} (quality {quality})");
        Console.WriteLine($"  Output size: {output.Length} bytes");
    }

    private static bool TryParseOutputFormat(string value, out OutputFormat format)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "png": format = OutputFormat.Png; return true;
            case "jpg":
            case "jpeg": format = OutputFormat.Jpeg; return true;
            case "bmp": format = OutputFormat.Bmp; return true;
            case "tif":
            case "tiff": format = OutputFormat.Tiff; return true;
            case "pdf": format = OutputFormat.Pdf; return true;
            case "xlsx":
            case "excel": format = OutputFormat.Xlsx; return true;
            case "docx":
            case "word": format = OutputFormat.Docx; return true;
            default: format = default; return false;
        }
    }

    private static void ShowHelp()
    {
        Console.WriteLine("Lightning Scan-Process-Print System");
        Console.WriteLine("\nLicense Commands:");
        Console.WriteLine("  activate <key>          - Activate a license key");
        Console.WriteLine("  license / status        - Show license status and usage");
        Console.WriteLine("  deactivate              - Deactivate current license");
        Console.WriteLine("  issue-key <tier> <email> <company> [devices] [days] [manual]");
        Console.WriteLine("                         - Issue production key with audit log");
        Console.WriteLine("\nProcessing Commands:");
        Console.WriteLine("  scan <source>           - Quick scan using TideWave + Resonance");
        Console.WriteLine("  process <source>        - Process using Hybrid Multi-Engine");
        Console.WriteLine("  print <source>          - Print with Stigma Premium");
        Console.WriteLine("  pipeline <source>       - Run complete Lightning Pipeline");
        Console.WriteLine("  batch <sources...>      - Batch process multiple items");
        Console.WriteLine("  convert <in> <fmt> [q]  - Encode processed image to png/jpeg/bmp/tiff/pdf/xlsx/docx");
        Console.WriteLine("  serve [port]            - Start REST API + Web Dashboard");
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
        Console.WriteLine("\nPricing:");
        Console.WriteLine("  • FREE Tier: 1,000 operations/month");
        Console.WriteLine("  • PAY-AS-YOU-GO: €0.001/operation, min €9.99/month");
        Console.WriteLine("  • UNLIMITED: €99.99/year, unlimited operations");
        Console.WriteLine("  • ENTERPRISE: Custom pricing and SLA");
        Console.WriteLine("\nPurchase: https://aba-gmbh.de/lightning-spp");
        Console.WriteLine("Support:  clisonix@pm.me");
    }

    private static void IssueLicenseKey(string[] args)
    {
        if (args.Length < 4)
        {
            Console.WriteLine("Usage: wwwmmm issue-key <tier> <email> <company> [devices] [days] [manual]");
            Console.WriteLine("Example: wwwmmm issue-key unlimited admin@aba-gmbh.de \"ABA GmbH\" 10 365 manual");
            return;
        }

        var tier = args[1];
        var email = args[2];
        var company = args[3];
        var devices = args.Length > 4 && int.TryParse(args[4], out var d) ? d : 1;
        var days = args.Length > 5 && int.TryParse(args[5], out var v) ? v : 365;
        var mode = LicenseManager.PaymentMode.Manual;

        var license = LicenseManager.CreateLicenseKey(
            email,
            company,
            tier,
            devices,
            days,
            useInternalQuantumSecurity: false,
            paymentMode: mode);

        Console.WriteLine("✅ License issued successfully");
        Console.WriteLine($"Key: {license.Key}");
        Console.WriteLine($"Tier: {license.Tier}");
        Console.WriteLine($"Expires: {license.ExpirationDate:yyyy-MM-dd}");
        Console.WriteLine($"Payment mode: {mode}");
    }

    private static async Task StartApiServer(string[] args)
    {
        var port = 13080;
        if (args.Length > 1 && int.TryParse(args[1], out var parsedPort))
        {
            port = parsedPort;
        }

        await LicenseApiServer.RunAsync(port);
    }

    private static void ActivateLicense(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: wwwmmm activate <LICENSE-KEY>");
            return;
        }

        var licenseKey = args[1];
        LicenseManager.ActivateLicense(licenseKey);
    }

}
