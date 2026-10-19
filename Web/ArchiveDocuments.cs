using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using wwwmmm.Configuration;
using wwwmmm.Documents;
using wwwmmm.Services;

namespace wwwmmm.Web;

internal static class ArchiveDocuments
{
    internal static async Task<byte[]> ProcessAsync(byte[] input, bool analyze, CancellationToken token = default)
    {
        // Validate the complete archive before invoking any document processor.
        var validated = ArchivePackage.Create(input, analyze);
        using var buffer = new MemoryStream();
        buffer.Write(validated);
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Update, true))
        {
            var reportEntry = zip.GetEntry("report.json")!;
            JsonNode report;
            using (var read = reportEntry.Open()) report = JsonNode.Parse(read)!;
            reportEntry.Delete();
            var processor = new TryDocumentProcessor(ServiceFactory.CreateScanService(), ServiceFactory.CreateProcessingService());
            var attempted = 0; var pagesUsed = 0; long generatedBytes = 0;
            foreach (var file in report["files"]!.AsArray())
            {
                token.ThrowIfCancellationRequested();
                var path = file!["path"]!.GetValue<string>();
                if (!path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) continue;
                file["processing"] = "FAILED";
                if (attempted++ >= 4) { file["processing"] = "SKIPPED_DOCUMENT_LIMIT"; continue; }
                try
                {
                    using var data = new MemoryStream();
                    using (var read = zip.GetEntry(file["outputPath"]!.GetValue<string>())!.Open()) await read.CopyToAsync(data, token);
                    if (data.Length > PdfDocumentService.MaxInputBytes) { file["processing"] = "SKIPPED_SIZE_LIMIT"; continue; }
                    var pdf = new PdfDocumentService();
                    var first = await pdf.RenderPageAsync(data.ToArray(), 1, token);
                    // Failed jobs also consume the page budget to bound total work.
                    if (pagesUsed + first.TotalPages > 32) { file["processing"] = "SKIPPED_PAGE_BUDGET"; continue; }
                    pagesUsed += first.TotalPages;
                    var result = await processor.ProcessAsync(data.ToArray(), true, ScanMode.Lightning, token);
                    if (generatedBytes + result.Output.Length > ArchivePackage.MaxExpandedBytes)
                    { file["processing"] = "SKIPPED_OUTPUT_BUDGET"; continue; }
                    generatedBytes += result.Output.Length;
                    var destination = "processed/" + path;
                    using (var write = zip.CreateEntry(destination, CompressionLevel.Optimal).Open()) await write.WriteAsync(result.Output, token);
                    file["processing"] = "PROCESSED";
                    file["processedOutput"] = new JsonObject {
                        ["path"] = destination, ["sha256"] = Convert.ToHexStringLower(SHA256.HashData(result.Output)),
                        ["bytes"] = result.Output.Length, ["pages"] = result.TotalPages,
                        ["rasterized"] = true, ["preservesPdfStructure"] = false, ["totalTimeMs"] = result.TotalTimeMs };
                }
                catch (Exception ex) when (ex is ArgumentException or DocumentException or IOException or InvalidOperationException)
                { file["processing"] = "FAILED"; file["processingError"] = "PDF could not be validated, rendered or processed. Original preserved; no processed output created."; }
            }
            report["operation"] = "archive-pdf-processing";
            report["scope"] = "Originals preserved. Up to 4 PDF attempts and 32 aggregate pages; generated document bytes limited to 32 MiB. Successful PDFs are rasterized; text, vectors and forms are not preserved. No supplier classification, OCR or optimization is claimed. Inspect every file processing status for failures and skipped files.";
            report["generatedBytes"] = generatedBytes;
            using var output = zip.CreateEntry("report.json").Open();
            await System.Text.Json.JsonSerializer.SerializeAsync(output, report, cancellationToken: token);
        }
        return buffer.ToArray();
    }
}
