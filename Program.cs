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
                Console.WriteLine("✓ Compatibility mode requested. Simulated services are removed; running REAL services.");
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
            Console.WriteLine($"  Artifacts: {result.Metadata.PagesPrinted}");
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

        Console.WriteLine(instantMs < 1 && ultraMs < 5 && lightningMs < 10
            ? "\nAll speed targets met in this run."
            : "\nSome speed targets were not met in this run. CI regression gates are defined in PerformanceGateTests.");
    }

    private static async Task RunDemo()
    {
        Console.WriteLine("Running the real image-processing validation demo. Generated fixtures are explicitly identified.");
        await wwwmmm.Examples.RealVsSimulatedExamples.RunAllComparisons();
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
        Console.WriteLine("  print <source>          - Create a Stigma lossless print artifact");
        Console.WriteLine("  pipeline <source>       - Run complete Lightning Pipeline");
        Console.WriteLine("  batch <sources...>      - Batch process multiple items");
        Console.WriteLine("  demo                    - Run demonstration");
        Console.WriteLine("  test                    - Run system tests");
        Console.WriteLine("  examples                - Run usage examples");
        Console.WriteLine("  serve [port]            - Start REST API + Web Dashboard");
        Console.WriteLine("  bridge <operation>      - Excel Core / Excel service / OS-CLX NodeDB adapters (see Docs/BRIDGES.md)");
        Console.WriteLine("  audit-imagesharp [root] [report.md]");
        Console.WriteLine("  benchmark-documents <input-directory> <new-report.json> [runs:1-10]");
        Console.WriteLine("                         - Scan the repo for real SixLabors.ImageSharp usage");
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
