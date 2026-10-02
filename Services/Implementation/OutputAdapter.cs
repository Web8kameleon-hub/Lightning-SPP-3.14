using SkiaSharp;

namespace wwwmmm.Services.Implementation;

/// <summary>
/// REAL output adapters: encodes processed PNG rasters into PNG, JPEG, BMP, PDF and TIFF.
/// PDF and TIFF multi-page output are real containers. This adapter does not drive hardware.
/// </summary>
public sealed class OutputAdapter : IOutputAdapter
{
    private const int DpiX = 96;
    private const int DpiY = 96;

    public byte[] Encode(byte[] rasterImage, OutputFormat format, int quality = 90)
    {
        ValidateRaster(rasterImage);
        ValidateQuality(quality);

        return format switch
        {
            OutputFormat.Png => EncodeWithSkia(rasterImage, SKEncodedImageFormat.Png, 100),
            OutputFormat.Jpeg => EncodeWithSkia(rasterImage, SKEncodedImageFormat.Jpeg, quality),
            OutputFormat.Bmp => EncodeWithSkia(rasterImage, SKEncodedImageFormat.Bmp, 100),
            OutputFormat.Tiff => TiffEncoder.Encode(new[] { DecodeToRgb24(rasterImage) }),
            OutputFormat.Pdf => EncodePdf(new[] { rasterImage }),
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
            OutputFormat.Tiff => TiffEncoder.Encode(
                rasterPages.Select(DecodeToRgb24).ToList()),
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
        using var bitmap = SKBitmap.Decode(rasterImage)
            ?? throw new InvalidDataException("Unsupported or corrupt raster image.");
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(format, quality)
            ?? throw new InvalidOperationException($"SkiaSharp failed to encode {format}.");
        return encoded.ToArray();
    }

    private static byte[] EncodePdf(IReadOnlyList<byte[]> rasterPages)
    {
        using var output = new MemoryStream();
        using (var document = SKDocument.CreatePdf(output))
        {
            foreach (var page in rasterPages)
            {
                using var bitmap = SKBitmap.Decode(page)
                    ?? throw new InvalidDataException("Unsupported or corrupt raster image in PDF page.");
                var widthPoints = bitmap.Width * 72f / DpiX;
                var heightPoints = bitmap.Height * 72f / DpiY;
                using var canvas = document.BeginPage(widthPoints, heightPoints);
                canvas.DrawBitmap(bitmap, new SKRect(0, 0, widthPoints, heightPoints));
                document.EndPage();
            }
            document.Close();
        }
        return output.ToArray();
    }

    /// <summary>
    /// Decode any supported raster into a tightly packed RGB-24 buffer with its dimensions.
    /// </summary>
    internal static Rgb24Image DecodeToRgb24(byte[] rasterImage)
    {
        using var decoded = SKBitmap.Decode(rasterImage)
            ?? throw new InvalidDataException("Unsupported or corrupt raster image.");
        using var rgba = decoded.Copy(SKColorType.Rgba8888);

        var width = rgba.Width;
        var height = rgba.Height;
        var source = rgba.GetPixelSpan();
        var rgb = new byte[checked(width * height * 3)];

        for (var i = 0; i < width * height; i++)
        {
            var s = i * 4;
            var d = i * 3;
            rgb[d] = source[s];
            rgb[d + 1] = source[s + 1];
            rgb[d + 2] = source[s + 2];
        }

        return new Rgb24Image(width, height, rgb);
    }
}

/// <summary>A decoded RGB-24 image: three bytes per pixel, row-major.</summary>
internal sealed record Rgb24Image(int Width, int Height, byte[] PixelBytes);
