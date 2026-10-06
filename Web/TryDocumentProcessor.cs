using System.Diagnostics;
using System.IO.Compression;
using wwwmmm.Documents;
using wwwmmm.Services;

namespace wwwmmm.Web;

internal sealed record TryDocumentResult(byte[] Preview, byte[] Output, byte[]? ImageArtifact,
    int TotalPages, long ScanTimeMs, long ProcessTimeMs, long TotalTimeMs, string Engine);

/// <summary>Demo output contract: explicitly restore real Hybrid artifacts to images for viewing/PDF creation.</summary>
internal sealed class TryDocumentProcessor(IScanService scanner, IProcessingService processor)
{
    private const int MaxPageBytes = 8 * 1024 * 1024;
    private const int MaxDocumentImageBytes = 32 * 1024 * 1024;

    internal async Task<TryDocumentResult> ProcessAsync(byte[] input, bool isPdf, ScanMode mode,
        CancellationToken cancellationToken = default)
    {
        var clock = Stopwatch.StartNew();
        var pdf = new PdfDocumentService();
        var first = isPdf ? await pdf.RenderPageAsync(input, 1, cancellationToken) : null;
        var totalPages = first?.TotalPages ?? 0;
        if (totalPages > 32)
            throw new ArgumentException("PDF documents must contain no more than 32 pages.");

        var pages = new List<byte[]>();
        byte[]? artifact = null;
        long scanMs = 0, processMs = 0, imageBytes = 0;
        var engine = string.Empty;
        for (var page = 1; page <= (isPdf ? totalPages : 1); page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rendered = page == 1 ? first : await pdf.RenderPageAsync(input, page, cancellationToken);
            if (isPdf && rendered!.TotalPages != totalPages)
                throw new DocumentException("PDF page count changed during processing.");
            var scanInput = isPdf ? rendered!.Png : input;
            var scan = await scanner.ScanAsync(scanInput, mode, quality: 300, cancellationToken);
            if (!scan.Success)
                throw new DocumentException("Document scanning failed.");
            var processed = await processor.ProcessAsync(scan.Data,
                new ProcessingOptions(ProcessingEngine.Hybrid, UseAI: false, UseClisonix: false,
                    OptimizationLevel: 3, UseStigmaMemory: false), cancellationToken);
            if (!processed.Success || processed.ProcessedData.Length == 0)
                throw new DocumentException("Document processing failed.");

            // Hybrid's established format is three nested lossless Brotli streams.
            // Decode the actual result; never substitute scan.Data on a decoding failure.
            artifact = processed.ProcessedData;
            try
            {
                processed = processed with { ProcessedData = await RestoreHybridImageAsync(artifact, cancellationToken) };
            }
            catch (Exception ex) when (ex is InvalidDataException ||
                (ex is InvalidOperationException && ex.TargetSite?.DeclaringType == typeof(BrotliStream) &&
                 ex.TargetSite.Name == "TryDecompress" && ex.Message == "Decoder ran into invalid data."))
            {
                // Preserve decoder details for the endpoint's internal log, never its client response.
                throw new DocumentException($"Hybrid image restoration failed: {ex}");
            }
            try
            {
                using (PdfDocumentService.DecodeImage(processed.ProcessedData)) { }
            }
            catch (ArgumentException ex)
            {
                throw new DocumentException($"Restored Hybrid image validation failed: {ex}");
            }
            imageBytes += processed.ProcessedData.Length;
            if (imageBytes > MaxDocumentImageBytes)
                throw new DocumentException("Processed document images exceed the 32 MiB limit.");
            pages.Add(processed.ProcessedData);
            scanMs += scan.Metadata.ProcessingTimeMs;
            processMs += processed.Metadata.ProcessingTimeMs;
            engine = processed.Metadata.Engine;
            LightningMetrics.RecordScan(scan.Metadata.ProcessingTimeMs);
            LightningMetrics.RecordProcess(processed.Metadata.ProcessingTimeMs);
        }

        var output = isPdf ? pdf.CreateFromImages(pages, dpi: 300, cancellationToken) : pages[0];
        if (isPdf && (output.Length < 5 || !output.AsSpan(0, 5).SequenceEqual("%PDF-"u8)))
            throw new DocumentException("PDF output could not be verified.");
        return new(pages[0], output, isPdf ? null : artifact, totalPages,
            scanMs, processMs, clock.ElapsedMilliseconds, engine);
    }

    private static async Task<byte[]> RestoreHybridImageAsync(byte[] artifact, CancellationToken cancellationToken)
    {
        var bytes = artifact;
        for (var stage = 0; stage < 3; stage++)
        {
            using var input = new MemoryStream(bytes, writable: false);
            using var decoder = new BrotliStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            var buffer = new byte[8192];
            int count;
            while ((count = await decoder.ReadAsync(buffer, cancellationToken)) > 0)
            {
                if (output.Length + count > MaxPageBytes)
                    throw new DocumentException("Processed page exceeds the image size limit.");
                output.Write(buffer, 0, count);
            }
            bytes = output.ToArray();
        }
        return bytes;
    }
}
