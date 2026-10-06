using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkiaSharp;

namespace wwwmmm.Documents;

public sealed record PdfPage(byte[] Png, int Page, int TotalPages);

/// <summary>Real Skia PDF output and bounded Poppler page rasterization.</summary>
public sealed class PdfDocumentService
{
    public const int MaxInputBytes = 8 * 1024 * 1024;
    private const int MaxPixels = 8_000_000;

    public byte[] CreateFromImages(IReadOnlyList<byte[]> images, int dpi = 300,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(images);
        if (images.Count is < 1 or > 32 || dpi is < 72 or > 600)
            throw new ArgumentException("Supply 1 to 32 images and a resolution from 72 to 600 DPI.");
        if (images.Sum(image => (long)(image?.Length ?? 0)) > 32 * 1024 * 1024)
            throw new ArgumentException("Total image input exceeds 32 MiB.");
        using var output = new MemoryStream();
        using var document = SKDocument.CreatePdf(output)
            ?? throw new DocumentException("PDF writer could not be initialized.");
        foreach (var bytes in images)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var bitmap = DecodeImage(bytes);
            var width = bitmap.Width * 72f / dpi;
            var height = bitmap.Height * 72f / dpi;
            var canvas = document.BeginPage(width, height);
            canvas.Clear(SKColors.White);
            canvas.DrawBitmap(bitmap, new SKRect(0, 0, width, height));
            document.EndPage();
        }
        document.Close();
        cancellationToken.ThrowIfCancellationRequested();
        return output.ToArray();
    }

    public async Task<PdfPage> RenderPageAsync(byte[] pdf, int page = 1,
        CancellationToken cancellationToken = default)
    {
        ValidatePdf(pdf);
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        var directory = Path.Combine(Path.GetTempPath(), "spp-pdf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var input = Path.Combine(directory, "input.pdf");
            await File.WriteAllBytesAsync(input, pdf, cancellationToken);
            var info = await NativeCommand.RunAsync(Tool("SPP_PDFINFO_PATH", "pdfinfo"), [input],
                TimeSpan.FromSeconds(30), cancellationToken);
            var match = Regex.Match(info, @"^Pages:\s+(\d+)\s*$", RegexOptions.Multiline);
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out var pages) || pages < 1)
                throw new DocumentException("PDF page count could not be verified.");
            if (Regex.IsMatch(info, @"^Encrypted:\s+yes\b", RegexOptions.Multiline))
                throw new DocumentException("Encrypted PDFs are not supported.");
            if (page > pages) throw new ArgumentOutOfRangeException(nameof(page), "Requested PDF page does not exist.");
            var prefix = Path.Combine(directory, "page");
            var number = page.ToString(CultureInfo.InvariantCulture);
            await NativeCommand.RunAsync(Tool("SPP_PDFTOPPM_PATH", "pdftoppm"),
                ["-f", number, "-l", number, "-singlefile", "-scale-to", "2400", "-png", input, prefix],
                TimeSpan.FromSeconds(30), cancellationToken);
            var png = await DocumentFiles.ReadAsync(prefix + ".png", 32 * 1024 * 1024, cancellationToken);
            using var bitmap = DecodeImage(png, 32 * 1024 * 1024);
            return new(png, page, pages);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    internal static void ValidatePdf(byte[] pdf)
    {
        if (pdf is null || pdf.Length is < 8 or > MaxInputBytes ||
            !pdf.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
            throw new ArgumentException("Supply a PDF file of at most 8 MiB.");
    }

    internal static SKBitmap DecodeImage(byte[] bytes, int maxBytes = MaxInputBytes)
    {
        if (bytes is null || bytes.Length == 0 || bytes.Length > maxBytes)
            throw new ArgumentException("Image is empty or exceeds the input size limit.");
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        if (codec is null || codec.Info.Width <= 0 || codec.Info.Height <= 0 ||
            (long)codec.Info.Width * codec.Info.Height > MaxPixels)
            throw new DocumentException("Input is not a supported image or exceeds eight million pixels.");
        var bitmap = new SKBitmap(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        var result = codec.GetPixels(bitmap.Info, bitmap.GetPixels());
        if (result != SKCodecResult.Success)
        {
            bitmap.Dispose();
            throw new DocumentException("Image data is incomplete or invalid.");
        }
        return bitmap;
    }

    private static string Tool(string variable, string fallback)
    {
        var configured = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(configured)) return fallback;
        if (!Path.IsPathFullyQualified(configured))
            throw new ArgumentException($"{variable} must be an absolute executable path.");
        return configured;
    }
}
