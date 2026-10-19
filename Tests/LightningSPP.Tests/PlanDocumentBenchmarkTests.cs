using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using LightningSPP.Licensing;
using SkiaSharp;
using wwwmmm.Configuration;
using wwwmmm.Documents;
using wwwmmm.Services;
using wwwmmm.Services.Implementation;
using wwwmmm.Web;
using Xunit;

namespace LightningSPP.Tests;

public sealed class PlanBenchmarkFactAttribute : FactAttribute
{
    public PlanBenchmarkFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("SPP_RUN_PLAN_BENCHMARKS") != "1")
            Skip = "Opt-in isolated plan benchmark; run the document-plan-benchmarks workflow.";
    }
}

public class PlanDocumentBenchmarkTests
{
    [PlanBenchmarkFact]
    public async Task ComparePaidPlansWithIdenticalDocuments()
    {
        var root = Environment.GetEnvironmentVariable("SPP_PLAN_BENCHMARK_OUTPUT")
            ?? throw new InvalidOperationException("An isolated evidence directory is required.");
        Directory.CreateDirectory(root);
        Assert.Null(LicenseManager.GetActiveLicense());
        var keys = new[] { "LSPP_LICENSE_SIGNING_KEYS", "LSPP_LICENSE_SIGNING_ACTIVE_KID", "LSPP_LICENSE_SIGNING_KEY" };
        var old = keys.ToDictionary(k => k, Environment.GetEnvironmentVariable);
        var rows = new List<object>();
        try
        {
            Environment.SetEnvironmentVariable(keys[0], "bench=" + Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
            Environment.SetEnvironmentVariable(keys[1], "bench");
            Environment.SetEnvironmentVariable(keys[2], null);
            var png = ReportPage();
            var pdfService = new PdfDocumentService();
            var pdf = pdfService.CreateFromImages(new[] { png, png });
            await File.WriteAllBytesAsync(Path.Combine(root, "input-report.png"), png);
            await File.WriteAllBytesAsync(Path.Combine(root, "input-two-page.pdf"), pdf);
            var source = Path.Combine(root, "input-report.png");
            foreach (var tier in new[] { LicenseKey.LicenseTier.PayAsYouGo, LicenseKey.LicenseTier.Unlimited, LicenseKey.LicenseTier.Enterprise })
            {
                var directory = Path.Combine(root, tier.ToString());
                Directory.CreateDirectory(directory);
                var license = LicenseKey.Generate(tier, "benchmark@example.invalid", "ISOLATED CI TEST ONLY", 1,
                    tier == LicenseKey.LicenseTier.Enterprise ? -1 : tier == LicenseKey.LicenseTier.Unlimited ? 10 : 5);
                Assert.True(LicenseManager.ActivateLicense(license.Key));
                Assert.True(LicenseManager.HasValidLicense());
                Assert.Equal(tier, LicenseManager.GetLicenseTier());
                var pipeline = new LightningPipeline(ServiceFactory.CreateScanService(),
                    ServiceFactory.CreateProcessingService(), new RealPrintService(directory), StigmaExecutionProfile.Lightning);
                var artifact = Path.Combine(directory, "pipeline.br");
                async Task<byte[]> Pipeline()
                {
                    var result = await pipeline.ExecuteAsync(new PipelineRequest(source, PrintDestination: artifact));
                    Assert.True(result.Success, result.Error);
                    Assert.True(File.Exists(artifact));
                    return await File.ReadAllBytesAsync(artifact);
                }
                var usage = LicenseManager.GetUsageThisMonth();
                var core = await Measure(Pipeline);
                Assert.Equal(11, LicenseManager.GetUsageThisMonth() - usage);
                using (var input = File.OpenRead(artifact))
                using (var decompressor = new BrotliStream(input, CompressionMode.Decompress))
                using (var decoded = new MemoryStream())
                {
                    await decompressor.CopyToAsync(decoded);
                    Assert.NotEmpty(decoded.ToArray());
                }
                // The browser document processor is shared and does not enforce commercial tiers.
                // Measure it separately, never describe this as paid HTTP entitlement coverage.
                var processor = new TryDocumentProcessor(ServiceFactory.CreateScanService(), ServiceFactory.CreateProcessingService());
                async Task<byte[]> Pdf()
                {
                    var result = await processor.ProcessAsync(pdf, true, ScanMode.Lightning);
                    Assert.Equal(2, result.TotalPages);
                    return result.Output;
                }
                var document = await Measure(Pdf);
                var final = await Pdf();
                await File.WriteAllBytesAsync(Path.Combine(directory, "processed.pdf"), final);
                for (var page = 1; page <= 2; page++)
                {
                    var rendered = await pdfService.RenderPageAsync(final, page);
                    Assert.Equal(2, rendered.TotalPages);
                    Assert.NotEmpty(rendered.Png);
                }
                var row = new
                {
                    plan = tier.ToString(), profile = "lightning", aiEnabled = false,
                    licenseActivated = true, testLicenseOnly = true,
                    limits = new { license.MaxDevices, license.MaxOperationsPerMonth },
                    coreImagePipeline = new { inputSha256 = Hash(png), inputBytes = png.Length, stats = core,
                        outputSha256 = Hash(await File.ReadAllBytesAsync(artifact)), outputBytes = new FileInfo(artifact).Length,
                        usageIncrementVerified = true },
                    sharedTwoPagePdf = new { inputSha256 = Hash(pdf), inputBytes = pdf.Length, stats = document,
                        outputBytes = final.Length, outputSha256 = Hash(final), pagesRerendered = 2, tierEnforced = false }
                };
                rows.Add(row);
                await File.WriteAllTextAsync(Path.Combine(directory, "measurements.json"), JsonSerializer.Serialize(row, new JsonSerializerOptions { WriteIndented = true }));
                LicenseManager.DeactivateLicense();
            }
            await File.WriteAllTextAsync(Path.Combine(root, "comparison.json"), JsonSerializer.Serialize(new
            {
                schemaVersion = 1, commit = Environment.GetEnvironmentVariable("GITHUB_SHA"), createdUtc = DateTimeOffset.UtcNow,
                os = RuntimeInformation.OSDescription, runtime = RuntimeInformation.FrameworkDescription, processors = Environment.ProcessorCount,
                warmupRuns = 1, measuredRuns = 10, executionOrder = "PayAsYouGo, Unlimited, Enterprise; sequential on one runner",
                corpus = "Generated technical document fixture with explicit benchmark text and table; not customer documents or a production workload.",
                scope = "Actual licensed core image pipeline and shared native PDF processor. Same engine/profile for all plans. PDF path has no tier gate. No Stripe, paid account, HTTP load, device-limit enforcement, SLA or real-model AI validation. Peak memory is process-wide, not incremental per document. Output validation is not OCR or perceptual-quality validation. Fixed order and small sample do not establish a plan speed advantage.",
                rows
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            LicenseManager.DeactivateLicense();
            foreach (var key in keys) Environment.SetEnvironmentVariable(key, old[key]);
        }
    }

    private static async Task<object> Measure(Func<Task<byte[]>> action)
    {
        await action();
        var times = new List<double>();
        var watch = Stopwatch.StartNew();
        for (var i = 0; i < 10; i++)
        {
            var sample = Stopwatch.StartNew();
            Assert.NotEmpty(await action());
            times.Add(sample.Elapsed.TotalMilliseconds);
        }
        watch.Stop();
        times.Sort();
        return new { elapsedMs = times, medianMs = (times[4] + times[5]) / 2,
            p95Ms = times[9], maxMs = times[9], throughputDocumentsPerSecond = 10 / watch.Elapsed.TotalSeconds,
            peakProcessWorkingSetBytes = Process.GetCurrentProcess().PeakWorkingSet64 };
    }

    private static string Hash(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));

    private static byte[] ReportPage()
    {
        using var bitmap = new SKBitmap(1240, 1754);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        using var font = new SKFont(SKTypeface.Default, 24);
        canvas.DrawText("LIGHTNING SPP — TECHNICAL BENCHMARK FIXTURE", 45, 70, font, paint);
        canvas.DrawText("NO CUSTOMER DATA — generated document for repeatable processing", 45, 115, font, paint);
        for (var row = 0; row < 35; row++)
        {
            var y = 180 + row * 40;
            canvas.DrawText($"Record {row + 1:D2}    Document pipeline verification    Reference {row + 1000}", 45, y, font, paint);
            canvas.DrawLine(45, y + 10, 1190, y + 10, paint);
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }
}
