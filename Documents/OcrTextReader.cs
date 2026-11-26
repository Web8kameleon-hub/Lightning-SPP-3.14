using System.ComponentModel;
using System.Security.Cryptography;

namespace wwwmmm.Documents;

/// <summary>
/// Reads a document as text, choosing the honest path: the PDF's own text layer first, and
/// OCR only when that layer is empty and OCR has been enabled explicitly. A digital PDF is
/// never degraded by OCR, and a scanned PDF never silently produces nothing while OCR is off.
/// </summary>
public static class OcrTextReader
{
    public const string EnabledVariable = "LSPP_OCR_ENABLED";
    private const int MaxOcrPages = 32;

    /// <summary>OCR is opt-in: absent, empty or any value other than "true" leaves it off.</summary>
    public static bool IsEnabled =>
        string.Equals(Environment.GetEnvironmentVariable(EnabledVariable), "true", StringComparison.OrdinalIgnoreCase);
    public static async Task<DocumentText> ReadAsync(
            string pdfPath,
            CancellationToken cancellationToken = default,
            IOcrEngine? engine = null)
    {
        if (!File.Exists(pdfPath))
            throw new FileNotFoundException($"PDF not found: {pdfPath}", pdfPath);

        if (!string.Equals(Path.GetExtension(pdfPath), ".pdf", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Text extraction requires a .pdf input. Render images first or supply a PDF.");

        DocumentText? layer = null;
        Exception? layerFailure = null;
        try
        {
            layer = await TextLayerExtractor.ExtractAsync(pdfPath, cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            // pdftotext may be missing. OCR does not depend on it, so keep the reason and
            // decide below instead of failing before the fallback is considered.
            layerFailure = ex;
        }

        if (layer is not null && !IsBlank(layer))
            return layer;

        if (!IsEnabled)
        {
            if (layer is not null)
                return layer;
            throw layerFailure!;
        }

        var recognizer = engine ?? new TesseractOcrEngine();
        var pdf = await File.ReadAllBytesAsync(pdfPath, cancellationToken);
        var service = new PdfDocumentService();
        var pages = new List<PageText>();

        var first = await service.RenderPageAsync(pdf, 1, cancellationToken);
        var lastPage = Math.Min(first.TotalPages, MaxOcrPages);
        pages.Add(new PageText(1, await recognizer.ReadAsync(first.Png, cancellationToken)));

        for (var page = 2; page <= lastPage; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rendered = await service.RenderPageAsync(pdf, page, cancellationToken);
            pages.Add(new PageText(page, await recognizer.ReadAsync(rendered.Png, cancellationToken)));
        }

        var sha256 = layer?.Sha256 ?? await ComputeSha256Async(pdfPath, cancellationToken);
        return new DocumentText(pdfPath, sha256, pages) { TextSource = DocumentText.OcrSource, TotalPageCount = first.TotalPages };
    }
    private static bool IsBlank(DocumentText document) =>
            document.Pages.Count == 0 || document.Pages.All(page => string.IsNullOrWhiteSpace(page.Text));

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
