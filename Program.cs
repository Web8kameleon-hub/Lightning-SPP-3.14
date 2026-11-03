using wwwmmm.PublicAPI;
using wwwmmm.Services;
using wwwmmm.SDK;
using LightningSPP.Licensing;
using wwwmmm.Web;

namespace wwwmmm;

/// <summary>
/// Lightning SPP 3.14 - Universal Scan-Process-Print System
/// Integrating: TideWave, CLX, CLX.i, XCL, Resonance, NanoDecibel, Clisonix Global AI, Stigma
/// Runtime and device support require validation on the target deployment.
/// </summary>
internal class Program
{
    static async Task Main(string[] args)
    {
        if (args.FirstOrDefault() == "benchmark-documents")
        {
            using var cancellation = new CancellationTokenSource();
            ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
            Console.CancelKeyPress += cancel;
            try { Environment.ExitCode = await Documents.DocumentBenchmark.RunAsync(args, cancellation.Token); }
            finally { Console.CancelKeyPress -= cancel; }
            return;
        }
        if (args.FirstOrDefault() == "workflow")
        {
            using var cancellation = new CancellationTokenSource();
            ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
            Console.CancelKeyPress += cancel;
            try { Environment.ExitCode = await Workflow.WorkflowCommands.RunAsync(args, cancellation.Token); }
            finally { Console.CancelKeyPress -= cancel; }
            return;
        }
        if (args.Length > 0 && args[0].Equals("flow", StringComparison.OrdinalIgnoreCase))
        {
            using var cancellation = new CancellationTokenSource();
            ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
            Console.CancelKeyPress += cancel;
            try { Environment.ExitCode = await Flow.FlowCommands.RunAsync(args, cancellation.Token); }
            finally { Console.CancelKeyPress -= cancel; }
            return;
        }
        if (args.Length > 0 && (args[0].Equals("bridge", StringComparison.OrdinalIgnoreCase) ||
            args[0] is "document" or "device"))
        {
            using var cancellation = new CancellationTokenSource();
            ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
            Console.CancelKeyPress += cancel;
            try
            {
                Environment.ExitCode = args[0].Equals("bridge", StringComparison.OrdinalIgnoreCase)
                    ? await Integrations.BridgeCommands.RunAsync(args, cancellation.Token)
                    : await Documents.DocumentCommands.RunAsync(args, cancellation.Token);
            }
            finally { Console.CancelKeyPress -= cancel; }
            return;
        }
        Console.WriteLine("⚡ Lightning Scan-Process-Print System ⚡");
        Console.WriteLine("Technologies: TideWave • CLX • CLX.i • XCL • Resonance • NanoDecibel • Clisonix AI • Stigma\n");

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
            case "generate-license":
                GenerateSampleLicense(args);
                break;
            case "issue-key":
                IssueLicenseKey(args);
                break;
            case "serve":
            case "api":
                await StartApiServer(args);
                break;
            case "audit-imagesharp":
                Environment.ExitCode = await wwwmmm.Tools.ImageSharpAuditCommand.RunAsync(args);
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
            case "demo":
                await RunDemo();
                break;
            case "smoke-test":
            case "test":
                Console.WriteLine("⚠ 'test' runs the smoke test suite only — this is NOT the full suite.");
                Console.WriteLine("  For the full test suite, run: dotnet test");
                await wwwmmm.Tests.SimpleTests.RunAllTests();
                break;
            case "examples":
                await wwwmmm.Examples.UsageExamples.RunAllExamples();
                break;
            case "engines":
                await wwwmmm.Examples.RealProcessingExamples.DemonstrateImageEngines();
                break;
            case "universal":
                await wwwmmm.Examples.UniversalExamples.RunAllUniversalExamples();
                break;
            case "speed":
                await SpeedTest();
                break;
            case "convert":
                await ConvertFile(args);
                break;
            case "real":
                await wwwmmm.Examples.RealProcessingExamples.RunAllComparisons();
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
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: wwwmmm process <source>");
            return;
        }

        var source = args[1];

        if (!File.Exists(source))
        {
            Console.WriteLine($"✗ Source file not found: {source}");
            return;
        }

        Console.WriteLine("🔄 Processing with Hybrid Multi-Engine...");

        var sourceData = await File.ReadAllBytesAsync(source);
        var result = await LightningAPI.HybridProcessAsync(sourceData);

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
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: wwwmmm print <source>");
            return;
        }

        var source = args[1];

        if (!File.Exists(source))
        {
            Console.WriteLine($"✗ Source file not found: {source}");
            return;
        }

        Console.WriteLine("🖨️  Creating Stigma lossless print artifact...");

        var sourceData = await File.ReadAllBytesAsync(source);
        var result = await LightningAPI.StigmaPrintAsync(sourceData);

        if (result.Success)
        {
            Console.WriteLine($"✓ Print artifact completed in {result.Metadata.ProcessingTimeMs}ms");
            Console.WriteLine($"  Technology: {result.Metadata.Technology}");
            // NOTE: this command produces a .br artifact, not physical pages.
            // The metadata field name is historical; display it as the metadata it is.
            Console.WriteLine($"  Artifact metadata (PagesPrinted): {result.Metadata.PagesPrinted}");
            Console.WriteLine($"  Output: {result.Metadata.Destination}");
        }
        else
        {
            Console.WriteLine($"✗ Print artifact failed: {result.Error}");
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

        // Realistic, non-degenerate input. Zero-filled buffers compress trivially and do not
        // represent production behavior; 64 KiB mixed pattern + entropy is more representative.
        const int inputSize = 64 * 1024;
        var testData = new byte[inputSize];
        Random.Shared.NextBytes(testData);
        for (var i = 0; i < inputSize; i += 64) testData[i] = (byte)(i & 0xFF);

        const int warmupRuns = 5;
        const int measuredRuns = 20;

        var (instantMedian, instantP95) = await MeasureAsync(
            async () => { await Lightning314.Instant(testData); }, warmupRuns, measuredRuns);
        var (ultraMedian, ultraP95) = await MeasureAsync(
            async () => { await Lightning314.UltraFast(testData); }, warmupRuns, measuredRuns);
        var (lightningMedian, lightningP95) = await MeasureAsync(
            async () => { await Lightning314.Lightning(testData); }, warmupRuns, measuredRuns);

        Console.WriteLine($"Input: {inputSize} bytes (mixed entropy), {measuredRuns} runs after {warmupRuns} warmup");
        Console.WriteLine($"Instant:    median {instantMedian:F3} ms, p95 {instantP95:F3} ms");
        Console.WriteLine($"UltraFast:  median {ultraMedian:F3} ms, p95 {ultraP95:F3} ms");
        Console.WriteLine($"Lightning:  median {lightningMedian:F3} ms, p95 {lightningP95:F3} ms");
        Console.WriteLine("\nThese are informational measurements and are environment-dependent.");
        Console.WriteLine("They are not acceptance criteria on their own; regression gates are enforced by PerformanceGateTests in CI.");
    }

    private static async Task ConvertFile(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Usage: wwwmmm convert <input-image> <png|jpeg|bmp|tiff|pdf> [quality 1-100]");
            return;
        }

        if (!TryParseOutputFormat(args[2], out var format))
        {
            Console.Error.WriteLine($"✗ Unknown format '{args[2]}'. Use png, jpeg, bmp, tiff or pdf.");
            Environment.ExitCode = 2;
            return;
        }

        if (!int.TryParse(args.Length > 3 ? args[3] : "90", out var quality) || quality is < 1 or > 100)
        {
            Console.Error.WriteLine("✗ Quality must be an integer between 1 and 100.");
            Environment.ExitCode = 2;
            return;
        }

        var input = args[1];
        if (!File.Exists(input))
        {
            Console.Error.WriteLine($"✗ Source file not found: {input}");
            Environment.ExitCode = 1;
            return;
        }

        var scan = await LightningAPI.QuickScanAsync(input);
        if (!scan.Success)
        {
            Console.Error.WriteLine($"✗ Convert failed: {scan.Error}");
            Environment.ExitCode = 1;
            return;
        }

        var extension = format switch
        {
            OutputFormat.Jpeg => ".jpg",
            OutputFormat.Tiff => ".tiff",
            _ => "." + format.ToString().ToLowerInvariant()
        };
        var output = LightningAPI.EncodeOutput(scan.Data, format, quality);
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
            default: format = default; return false;
        }
    }

    private static async Task<(double median, double p95)> MeasureAsync(Func<Task> action, int warmupRuns, int measuredRuns)
    {
        for (var i = 0; i < warmupRuns; i++) await action();

        var times = new double[measuredRuns];
        for (var i = 0; i < measuredRuns; i++)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await action();
            sw.Stop();
            times[i] = sw.Elapsed.TotalMilliseconds;
        }
        Array.Sort(times);
        return (times[measuredRuns / 2], times[(int)(measuredRuns * 0.95)]);
    }

    private static async Task RunDemo()
    {
        Console.WriteLine("Running the real image-processing validation demo. Generated fixtures are explicitly identified.");
        await wwwmmm.Examples.RealProcessingExamples.RunAllComparisons();
    }

    private static void ShowHelp()
    {
        Console.WriteLine("Lightning Scan-Process-Print System");
        Console.WriteLine("\nLicense Commands:");
        Console.WriteLine("  activate <key>          - Activate a license key");
        Console.WriteLine("  license / status        - Show license status and usage");
        Console.WriteLine("  deactivate              - Deactivate current license");
        Console.WriteLine("  generate-license <tier> - Generate sample license (dev/testing)");
        Console.WriteLine("  issue-key <tier> <email> <company> [devices] [days] [stripe|internal]");
        Console.WriteLine("                         - Issue production key with audit log");
        Console.WriteLine("\nProcessing Commands:");
        Console.WriteLine("  scan <source>           - Quick scan using TideWave + Resonance");
        Console.WriteLine("  process <source>        - Process a source file using Hybrid Multi-Engine");
        Console.WriteLine("  print <source>          - Create a Stigma lossless print artifact (.br)");
        Console.WriteLine("  pipeline <source>       - Run complete Lightning Pipeline");
        Console.WriteLine("  batch <sources...>      - Batch process multiple items");
        Console.WriteLine("  demo                    - Run demonstration");
        Console.WriteLine("  smoke-test / test       - Run SMOKE tests only (not the full suite; use dotnet test)");
        Console.WriteLine("  examples                - Run usage examples");
        Console.WriteLine("  engines                 - Verify both L/R image engines (Skia, ImageSharp)");
        Console.WriteLine("  serve [port]            - Start REST API + Web Dashboard");
        Console.WriteLine("  bridge <operation>      - Excel Core / Excel service / OS-CLX NodeDB adapters (see Docs/BRIDGES.md)");
        Console.WriteLine("  audit-imagesharp [root] [report.md]");
        Console.WriteLine("                         - Scan the repo for real SixLabors.ImageSharp usage");
        Console.WriteLine("  benchmark-documents <input-directory> <new-report.json> [runs:1-10]");
        Console.WriteLine("                         - Run the real document benchmark and write a report");
        Console.WriteLine("  flow <command>          - Reference flow: intake, rules, fields with references, review, batch, package");
        Console.WriteLine("  convert <in> <fmt> [q]  - Encode a processed image to png/jpeg/bmp/tiff/pdf");
        Console.WriteLine("  speed                   - Informational timing (median/p95 over 20 runs)");
        Console.WriteLine("\nTechnologies:");
        Console.WriteLine("  • TideWave - Balanced scanning");
        Console.WriteLine("  • CLX - Core processing");
        Console.WriteLine("  • CLX.i - Interactive processing");
        Console.WriteLine("  • XCL - Extended processing");
        Console.WriteLine("  • Resonance - High precision");
        Console.WriteLine("  • NanoDecibel - Maximum quality");
        Console.WriteLine("  • Clisonix Global AI - AI-powered enhancement");
        Console.WriteLine("  • Stigma - Premium print quality");
        Console.WriteLine("  • Stigma Memory - Optimization");
        Console.WriteLine("\nPricing:");
        Console.WriteLine("  Current plans and terms: https://scanprocessprint.com/pricing");
        Console.WriteLine("  Stripe Checkout confirms the configured price and taxes before payment.");
        Console.WriteLine("\nAccount and existing licenses: https://scanprocessprint.com/account");
        Console.WriteLine("Support:  clisonix@pm.me");
    }

    private static void IssueLicenseKey(string[] args)
    {
        if (args.Length < 5)
        {
            Console.WriteLine("Usage: wwwmmm issue-key <tier> <email> <company> [devices] [days] [stripe|internal]");
            Console.WriteLine("Example: wwwmmm issue-key unlimited admin@aba-gmbh.de \"ABA GmbH\" 10 365 stripe");
            return;
        }

        var tier = args[1];
        var email = args[2];
        var company = args[3];
        var devices = args.Length > 4 && int.TryParse(args[4], out var d) ? d : 1;
        var days = args.Length > 5 && int.TryParse(args[5], out var v) ? v : 365;
        var paymentRaw = args.Length > 6 ? args[6].ToLowerInvariant() : "stripe";
        var mode = paymentRaw == "internal"
            ? LicenseManager.PaymentMode.InternalQuantum
            : LicenseManager.PaymentMode.StripeOnly;

        var license = LicenseManager.CreateLicenseKey(
            email,
            company,
            tier,
            devices,
            days,
            useInternalQuantumSecurity: mode == LicenseManager.PaymentMode.InternalQuantum,
            paymentMode: mode);

        Console.WriteLine("✅ License issued successfully");
        Console.WriteLine($"Key: {license.Key}");
        Console.WriteLine($"Tier: {license.Tier}");
        Console.WriteLine($"Expires: {license.ExpirationDate:yyyy-MM-dd}");
        Console.WriteLine($"Payment mode: {mode}");
    }

    private static async Task StartApiServer(string[] args)
    {
        var portText = args.Length > 1 ? args[1] : Environment.GetEnvironmentVariable("PORT") ?? "13080";
        if (!int.TryParse(portText, out var port) || port < 1 || port > 65535)
            throw new ArgumentException("Port must be an integer between 1 and 65535.");

        await LicenseApiServer.RunAsync(port);
    }

    private static void ActivateLicense(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: wwwmmm activate <LICENSE-KEY>");
            Console.WriteLine("Example: wwwmmm activate LSPP-UNLTD-A1B2-C3D4-E5F6-1234");
            return;
        }

        var licenseKey = args[1];
        LicenseManager.ActivateLicense(licenseKey);
    }

    private static void GenerateSampleLicense(string[] args)
    {
        Console.WriteLine("🔑 Lightning SPP License Generator\n");

        if (args.Length < 2)
        {
            Console.WriteLine("Usage: wwwmmm generate-license <tier> [email] [company]");
            Console.WriteLine("\nTiers:");
            Console.WriteLine("  free       - Free tier (1,000 ops/month, 1 device)");
            Console.WriteLine("  unlimited  - Unlimited development/test key; does not confirm a purchase");
            Console.WriteLine("  enterprise - Enterprise tier (custom, unlimited devices)");
            Console.WriteLine("\nExample:");
            Console.WriteLine("  wwwmmm generate-license unlimited user@company.com \"Acme Corp\"");
            return;
        }

        var tier = args[1].ToLower();
        var email = args.Length > 2 ? args[2] : "demo@example.com";
        var company = args.Length > 3 ? args[3] : "Demo Company";

        LicenseKey license = tier switch
        {
            "free" => LicenseManager.SampleLicenses.GenerateFreeLicense(email),
            "unlimited" => LicenseManager.SampleLicenses.GenerateUnlimitedLicense(email, company),
            "enterprise" => LicenseManager.SampleLicenses.GenerateEnterpriseLicense(email, company, -1),
            _ => throw new ArgumentException($"Unknown tier: {tier}")
        };

        Console.WriteLine("✅ License generated successfully!\n");
        Console.WriteLine(license.ToString());
        Console.WriteLine($"\n💾 License Storage Format:\n{license.ToStorageString()}");
        Console.WriteLine($"\n🔑 To activate, run:");
        Console.WriteLine($"   wwwmmm activate {license.Key}");
    }
}
