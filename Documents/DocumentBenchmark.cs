using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using SkiaSharp;
using wwwmmm.Configuration;
using wwwmmm.Services;
using wwwmmm.Web;

namespace wwwmmm.Documents;

/// <summary>Measures the same real document pipeline as the public demo, using local files.</summary>
internal static class DocumentBenchmark
{
    internal static async Task<int> RunAsync(string[] args, CancellationToken token)
    {
        try
        {
            if (args.Length is < 3 or > 4 || !Directory.Exists(args[1]) ||
                (args.Length == 4 && (!int.TryParse(args[3], out var count) || count is < 1 or > 10)))
                throw new ArgumentException("Usage: benchmark-documents <input-directory> <new-report.json> [runs:1-10]");
            var runs = args.Length == 4 ? int.Parse(args[3]) : 3;
            var destination = Path.GetFullPath(args[2]);
            if (File.Exists(destination) || !destination.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Report must be a new .json file; existing files are never overwritten.");
            var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { ".pdf", ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff", ".gif" };
            // Deliberately non-recursive: operators choose the corpus, never scan unrelated folders.
            var files = Directory.EnumerateFiles(args[1]).Where(p => extensions.Contains(Path.GetExtension(p)))
                .Order(StringComparer.Ordinal).Take(101).ToArray();
            if (files.Length is < 1 or > 100) throw new ArgumentException("Choose a directory containing 1 to 100 supported documents.");
            var processor = new TryDocumentProcessor(ServiceFactory.CreateScanService(), ServiceFactory.CreateProcessingService());
            var measurements = new List<object>();
            var failures = 0;
            foreach (var file in files)
            {
                token.ThrowIfCancellationRequested();
                string? sha256 = null;
                try
                {
                    var bytes = await DocumentFiles.ReadAsync(file, PdfDocumentService.MaxInputBytes, token);
                    sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
                    var isPdf = Path.GetExtension(file).Equals(".pdf", StringComparison.OrdinalIgnoreCase);
                    // Warm-up exercises validation and output as well as JIT; its duration is excluded.
                    await processor.ProcessAsync(bytes, isPdf, ScanMode.Lightning, token);
                    var durations = new List<double>();
                    TryDocumentResult result = null!;
                    for (var run = 0; run < runs; run++)
                    {
                        var timer = Stopwatch.StartNew();
                        result = await processor.ProcessAsync(bytes, isPdf, ScanMode.Lightning, token);
                        durations.Add(timer.Elapsed.TotalMilliseconds);
                    }
                    var pdf = new PdfDocumentService();
                    var reference = isPdf ? (await pdf.RenderPageAsync(bytes, 1, token)).Png : bytes;
                    if (isPdf)
                    {
                        // Inspect every output page outside the timed pipeline, not only its header.
                        for (var page = 1; page <= result.TotalPages; page++)
                        {
                            var rendered = await pdf.RenderPageAsync(result.Output, page, token);
                            if (rendered.TotalPages != result.TotalPages) throw new DocumentException("Output page count mismatch.");
                        }
                    }
                    using var original = PdfDocumentService.DecodeImage(reference, 32 * 1024 * 1024);
                    using var preview = PdfDocumentService.DecodeImage(result.Preview);
                    double? meanAbsoluteChannelDifference = null;
                    if (original.Width == preview.Width && original.Height == preview.Height)
                    {
                        double difference = 0;
                        for (var y = 0; y < original.Height; y++)
                        {
                            token.ThrowIfCancellationRequested();
                            for (var x = 0; x < original.Width; x++)
                            {
                                var a = original.GetPixel(x, y); var b = preview.GetPixel(x, y);
                                difference += Math.Abs(a.Red - b.Red) + Math.Abs(a.Green - b.Green) + Math.Abs(a.Blue - b.Blue);
                            }
                        }
                        meanAbsoluteChannelDifference = difference / (original.Width * (double)original.Height * 3);
                    }
                    durations.Sort();
                    measurements.Add(new
                    {
                        sha256, success = true, inputBytes = bytes.Length, outputBytes = result.Output.Length,
                        outputSha256 = Convert.ToHexStringLower(SHA256.HashData(result.Output)),
                        sizeReductionPercent = 100d * (1 - result.Output.Length / (double)bytes.Length),
                        pages = isPdf ? result.TotalPages : 1, pdfRasterized = isPdf,
                        inputWidth = original.Width, inputHeight = original.Height,
                        previewWidth = preview.Width, previewHeight = preview.Height,
                        firstPageMeanAbsoluteRgbDifference = meanAbsoluteChannelDifference,
                        elapsedMs = durations, medianMs = durations.Count % 2 == 1 ? durations[durations.Count / 2] :
                            (durations[durations.Count / 2 - 1] + durations[durations.Count / 2]) / 2,
                        maxMs = durations[^1]
                    });
                }
                catch (Exception ex) when (ex is ArgumentException or DocumentException or IOException or UnauthorizedAccessException)
                {
                    failures++;
                    // Reports omit file names, contents and internal exception messages.
                    measurements.Add(new { sha256, success = false, errorType = ex.GetType().Name });
                }
            }
            var report = new
            {
                schemaVersion = 1, createdUtc = DateTimeOffset.UtcNow,
                version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                os = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                runtime = RuntimeInformation.FrameworkDescription, processors = Environment.ProcessorCount,
                gcMemoryAvailableBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
                profile = "lightning", aiEnabled = false, warmupRuns = 1, measuredRuns = runs,
                qualityScope = "First-page RGB difference measures change, not perceptual quality, OCR accuracy or customer acceptance. PDF output is rasterized. No latency or size reduction guarantee.",
                documentCount = files.Length, failures, measurements
            };
            await DocumentFiles.SaveNewAsync(destination, JsonSerializer.SerializeToUtf8Bytes(report,
                new JsonSerializerOptions { WriteIndented = true }), ".json", token);
            Console.WriteLine($"Document report saved: {files.Length} files, {failures} failures. Inputs were not modified.");
            return failures == 0 ? 0 : 1;
        }
        catch (OperationCanceledException) { Console.Error.WriteLine("Document benchmark cancelled."); return 1; }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        { Console.Error.WriteLine(ex is ArgumentException ? ex.Message : "Benchmark inputs or report storage are unavailable."); return 1; }
    }
}
