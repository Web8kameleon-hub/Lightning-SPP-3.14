using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using wwwmmm.Documents;
using wwwmmm.Services;
using wwwmmm.Configuration;

namespace wwwmmm.Web;

internal static class ArchiveDocumentPackage
{
    internal const int MaxPdfFiles = 4;
    private const int MaxGeneratedBytes = 32 * 1024 * 1024;

    internal static async Task<byte[]> CreateAsync(byte[] input, bool analyze, ScanMode mode,
        CancellationToken cancellationToken = default)
    {
        // Validate the entire archive before any native document processing starts.
        var inventory = ArchivePackage.Create(input, analyze);
        using var source = new ZipArchive(new MemoryStream(inventory));
        using var reportInput = source.GetEntry("report.json")!.Open();
        var report = JsonNode.Parse(reportInput)!.AsObject();
        report["schemaVersion"] = 2;
        report["operation"] = "archive-pdf-processing";
        report["profile"] = mode.ToString();
        report["aiEnabled"] = false;
        report["scope"] = "Original bytes preserved. Optional PDF processing rasterizes pages and does not preserve PDF text, signatures or interactive structure. No code execution, OCR, supplier classification or malware scan.";
        report["documentLimits"] = new JsonObject { ["maxPdfFiles"] = MaxPdfFiles,
            ["maxPagesPerPdf"] = 32, ["maxBytesPerPdf"] = PdfDocumentService.MaxInputBytes,
            ["maxGeneratedBytes"] = MaxGeneratedBytes, ["secondsPerPdf"] = 20 };
        using var output = new MemoryStream();
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            foreach (var entry in source.Entries.Where(e => e.FullName != "report.json"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var read = entry.Open();
                using var write = target.CreateEntry(entry.FullName, CompressionLevel.Optimal).Open();
                await read.CopyToAsync(write, cancellationToken);
            }
            var processor = new TryDocumentProcessor(ServiceFactory.CreateScanService(), ServiceFactory.CreateProcessingService());
            int attempted = 0, generatedBytes = 0;
            foreach (var node in report["files"]!.AsArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var file = node!.AsObject();
                var path = file["path"]!.GetValue<string>();
                var status = new JsonObject();
                file["documentProcessing"] = status;
                if (!Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    status["status"] = "NOT_REQUESTED_FOR_TYPE";
                    continue;
                }
                if (file["bytes"]!.GetValue<int>() > PdfDocumentService.MaxInputBytes)
                {
                    status["status"] = "SKIPPED_SIZE_LIMIT";
                    continue;
                }
                if (attempted >= MaxPdfFiles)
                {
                    status["status"] = "SKIPPED_DOCUMENT_LIMIT";
                    continue;
                }
                attempted++;
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                deadline.CancelAfter(TimeSpan.FromSeconds(20));
                try
                {
                    using var bytes = new MemoryStream();
                    using (var read = source.GetEntry(file["outputPath"]!.GetValue<string>())!.Open())
                        await read.CopyToAsync(bytes, deadline.Token);
                    var result = await processor.ProcessAsync(bytes.ToArray(), true, mode, deadline.Token);
                    if ((long)generatedBytes + result.Output.Length + result.Preview.Length > MaxGeneratedBytes)
                    {
                        status["status"] = "SKIPPED_OUTPUT_LIMIT";
                        continue;
                    }
                    var prefix = $"processed/{attempted:D4}/";
                    Write(target, prefix + "processed.pdf", result.Output);
                    Write(target, prefix + "preview.png", result.Preview);
                    generatedBytes += result.Output.Length + result.Preview.Length;
                    status["status"] = "PROCESSED";
                    status["engine"] = result.Engine;
                    status["totalPages"] = result.TotalPages;
                    status["pdfRasterized"] = true;
                    status["preservesPdfStructure"] = false;
                    status["outputPath"] = prefix + "processed.pdf";
                    status["outputSha256"] = Hash(result.Output);
                    status["previewPath"] = prefix + "preview.png";
                    status["previewSha256"] = Hash(result.Preview);
                    status["pageMapping"] = new JsonArray(Enumerable.Range(1, result.TotalPages)
                        .Select(page => (JsonNode)new JsonObject { ["inputPage"] = page, ["outputPage"] = page }).ToArray());
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    status["status"] = "TIME_LIMIT_EXCEEDED";
                }
                catch (Exception ex) when (ex is DocumentException or ArgumentException or InvalidDataException)
                {
                    status["status"] = "FAILED";
                    status["reason"] = "PDF invalid, unsupported or processing unavailable; original preserved.";
                }
            }
            Write(target, "report.json", System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(report));
        }
        return output.ToArray();
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Write(ZipArchive zip, string path, byte[] bytes)
    {
        using var stream = zip.CreateEntry(path, CompressionLevel.Optimal).Open();
        stream.Write(bytes);
    }
}
