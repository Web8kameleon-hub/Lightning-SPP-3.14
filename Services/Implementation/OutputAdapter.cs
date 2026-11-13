using System.Runtime.InteropServices;
using SkiaSharp;
using wwwmmm.Documents;

namespace wwwmmm.Services.Implementation;

/// <summary>
/// REAL output adapters: PNG, JPEG, BMP (SkiaSharp), PDF (Skia PDF writer) and TIFF (baseline writer).
/// These produce downloadable files; they do not drive printing hardware.
/// </summary>
public sealed class OutputAdapter : IOutputAdapter
{
    private const int MaxPdfPages = 32;

    public byte[] Encode(byte[] rasterImage, OutputFormat format, int quality = 90)
    {
        ValidateRaster(rasterImage);
        ValidateQuality(quality);

        return format switch
        {
            OutputFormat.Png => EncodeWithSkia(rasterImage, SKEncodedImageFormat.Png, 100),
            OutputFormat.Jpeg => EncodeWithSkia(rasterImage, SKEncodedImageFormat.Jpeg, quality),
            OutputFormat.Bmp => BmpEncoder.Encode(ToRgb24(rasterImage)),
            OutputFormat.Tiff => TiffEncoder.Encode([ToRgb24(rasterImage)]),
            OutputFormat.Pdf => EncodePdf([rasterImage]),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported output format.")
        };
    }

    public byte[] EncodeMultiPage(IReadOnlyList<byte[]> rasterPages, OutputFormat format, int quality = 90)
    {
        ArgumentNullException.ThrowIfNull(rasterPages);
        if (rasterPages.Count == 0)
            throw new ArgumentException("At least one raster page is required.", nameof(rasterPages));
        foreach (var page in rasterPages)
            ValidateRaster(page);
        ValidateQuality(quality);

        return format switch
        {
            OutputFormat.Pdf => EncodePdf(rasterPages),
            OutputFormat.Tiff => TiffEncoder.Encode([.. rasterPages.Select(ToRgb24)]),
            _ => throw new NotSupportedException(
                $"{format} is a single-image format; use Encode() for one page.")
        };
    }

    private static void ValidateRaster(byte[] rasterImage)
    {
        if (rasterImage is null || rasterImage.Length == 0)
            throw new ArgumentException("Raster image data is empty.", nameof(rasterImage));
    }

    private static void ValidateQuality(int quality)
    {
        if (quality is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(quality), "Quality must be between 1 and 100.");
    }

    private static byte[] EncodeWithSkia(byte[] rasterImage, SKEncodedImageFormat format, int quality)
    {
        using var bitmap = PdfDocumentService.DecodeImage(rasterImage);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(format, quality)
            ?? throw new DocumentException($"The image could not be encoded as {format}.");
        return encoded.ToArray();
    }

    private static byte[] EncodePdf(IReadOnlyList<byte[]> rasterPages)
    {
        if (rasterPages.Count > MaxPdfPages)
            throw new ArgumentException($"At most {MaxPdfPages} pages can be written to one PDF.", nameof(rasterPages));
        return new PdfDocumentService().CreateFromImages(rasterPages);
    }

    /// <summary>
    /// Decode a validated raster into packed RGB-24. Uses the codec-validated decoder so
    /// corrupt input is rejected here rather than producing a malformed TIFF.
    /// </summary>
    internal static Rgb24Image ToRgb24(byte[] rasterImage)
    {
        using var bitmap = PdfDocumentService.DecodeImage(rasterImage);
        using var rgba = bitmap.Copy(SKColorType.Rgba8888)
            ?? throw new DocumentException("Image could not be converted to RGB for TIFF output.");

        var width = rgba.Width;
        var height = rgba.Height;
        var rowBytes = rgba.RowBytes;
        var raw = new byte[rowBytes * height];
        Marshal.Copy(rgba.GetPixels(), raw, 0, raw.Length);

        var rgb = new byte[checked(width * height * 3)];
        for (var y = 0; y < height; y++)
        {
            var source = y * rowBytes;
            var target = y * width * 3;
            for (var x = 0; x < width; x++)
            {
                var pixel = source + (x * 4);
                var destination = target + (x * 3);
                rgb[destination] = raw[pixel];
                rgb[destination + 1] = raw[pixel + 1];
                rgb[destination + 2] = raw[pixel + 2];
            }
        }

        return new Rgb24Image(width, height, rgb);
    }
}
