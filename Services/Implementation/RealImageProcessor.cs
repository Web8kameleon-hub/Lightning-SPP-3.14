using SkiaSharp;
using System.Diagnostics;
using System.IO;

namespace wwwmmm.Services.Implementation;

/// <summary>
/// REAL Image Processing Service - Using SkiaSharp with real source and in-memory data
/// </summary>
public class RealImageProcessor : IScanService
{
    public async Task<ScanResult> ScanAsync(ScanRequest request, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var sourceBytes = await ReadSourceBytesAsync(request.Source, cancellationToken);
            var processedData = await ProcessBytesByModeAsync(sourceBytes, request.Mode, request.Quality, cancellationToken);

            sw.Stop();

            var metadata = new ScanMetadata(
                DateTime.UtcNow,
                sw.ElapsedMilliseconds,
                request.Mode.ToString(),
                request.Quality,
                "PNG"
            );

            return new ScanResult(processedData, metadata, true);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ScanResult(
                Array.Empty<byte>(),
                new ScanMetadata(DateTime.UtcNow, sw.ElapsedMilliseconds, "Error", 0, "None"),
                false,
                ex.Message
            );
        }
    }

    public async Task<ScanResult> ScanAsync(
        byte[] data,
        ScanMode mode = ScanMode.Lightning,
        int quality = 300,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var processedData = await ProcessBytesByModeAsync(data, mode, quality, cancellationToken);
            sw.Stop();

            var metadata = new ScanMetadata(
                DateTime.UtcNow,
                sw.ElapsedMilliseconds,
                mode.ToString(),
                quality,
                "PNG"
            );

            return new ScanResult(processedData, metadata, true);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ScanResult(
                Array.Empty<byte>(),
                new ScanMetadata(DateTime.UtcNow, sw.ElapsedMilliseconds, "Error", 0, "None"),
                false,
                ex.Message
            );
        }
    }

    public async Task<ScanResult> ResonanceScanAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var processedData = await ProcessBytesByModeAsync(data, ScanMode.Resonance, 600, cancellationToken);
            sw.Stop();

            var metadata = new ScanMetadata(
                DateTime.UtcNow,
                sw.ElapsedMilliseconds,
                "Resonance-Real",
                600,
                "PNG"
            );

            return new ScanResult(processedData, metadata, true);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ScanResult(
                Array.Empty<byte>(),
                new ScanMetadata(DateTime.UtcNow, sw.ElapsedMilliseconds, "Error", 0, "None"),
                false,
                ex.Message
            );
        }
    }

    private static async Task<byte[]> ReadSourceBytesAsync(string source, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException("Source path must not be empty.", nameof(source));
        }

        if (!File.Exists(source))
        {
            throw new FileNotFoundException($"Source file not found: {source}", source);
        }

        return await File.ReadAllBytesAsync(source, cancellationToken);
    }

    private static async Task<byte[]> ProcessBytesByModeAsync(
        byte[] sourceData,
        ScanMode mode,
        int quality,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var sourceBitmap = DecodeImage(sourceData, quality);
        using var resizedBitmap = ResizeByQuality(sourceBitmap, quality, mode);
        using var processedBitmap = ApplyModeProcessing(resizedBitmap, mode);
        using var image = SKImage.FromBitmap(processedBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

        await using var ms = new MemoryStream();
        data.SaveTo(ms);
        return ms.ToArray();
    }

    private static SKBitmap ResizeByQuality(SKBitmap source, int quality, ScanMode mode)
    {
        var modeMultiplier = mode switch
        {
            ScanMode.Lightning => 1.0f,
            ScanMode.TideWave => 1.2f,
            ScanMode.Resonance => 1.8f,
            ScanMode.NanoDecibel => 2.5f,
            _ => 1.0f
        };

        var qualityScale = Math.Clamp(quality / 300f, 0.5f, 4f) * modeMultiplier;
        var width = Math.Max(64, (int)Math.Round(source.Width * qualityScale));
        var height = Math.Max(64, (int)Math.Round(source.Height * qualityScale));

        var resized = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(resized);
        canvas.Clear(SKColors.Black);
        using var paint = new SKPaint
        {
            IsAntialias = true,
            FilterQuality = SKFilterQuality.High,
            IsDither = true
        };
        canvas.DrawBitmap(source, new SKRect(0, 0, width, height), paint);
        return resized;
    }

    private static SKBitmap ApplyModeProcessing(SKBitmap source, ScanMode mode)
    {
        // Keep processing explicit and deterministic per scan mode.
        return mode switch
        {
            ScanMode.Lightning => ApplyColorTransform(source, grayscale: true, contrast: 1.05f),
            ScanMode.TideWave => ApplyColorTransform(source, grayscale: true, contrast: 1.12f, saturation: 1.03f),
            ScanMode.Resonance => ApplyEdgeEnhanced(source, contrast: 1.25f, brightness: 1.02f),
            ScanMode.NanoDecibel => ApplyEdgeEnhanced(source, contrast: 1.35f, brightness: 1.08f, saturation: 1.15f),
            _ => source.Copy(SKColorType.Rgba8888)
        };
    }

    private static SKBitmap ApplyColorTransform(SKBitmap source, bool grayscale = false, float contrast = 1.0f, float brightness = 1.0f, float saturation = 1.0f)
    {
        var output = new SKBitmap(source.Width, source.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(output);

        var matrix = BuildColorMatrix(grayscale, contrast, brightness, saturation);
        using var paint = new SKPaint
        {
            IsAntialias = true,
            FilterQuality = SKFilterQuality.High,
            ColorFilter = SKColorFilter.CreateColorMatrix(matrix)
        };
        canvas.DrawBitmap(source, 0, 0, paint);
        return output;
    }

    private static SKBitmap ApplyEdgeEnhanced(SKBitmap source, float contrast, float brightness, float saturation = 1.0f)
    {
        using var gray = ApplyColorTransform(source, grayscale: true, contrast: 1.0f);
        var output = new SKBitmap(gray.Width, gray.Height, SKColorType.Rgba8888, SKAlphaType.Premul);

        for (var y = 1; y < gray.Height - 1; y++)
        {
            for (var x = 1; x < gray.Width - 1; x++)
            {
                var gx =
                    -GetGray(gray, x - 1, y - 1) + GetGray(gray, x + 1, y - 1) +
                    -2 * GetGray(gray, x - 1, y) + 2 * GetGray(gray, x + 1, y) +
                    -GetGray(gray, x - 1, y + 1) + GetGray(gray, x + 1, y + 1);

                var gy =
                    GetGray(gray, x - 1, y - 1) + 2 * GetGray(gray, x, y - 1) + GetGray(gray, x + 1, y - 1) -
                    GetGray(gray, x - 1, y + 1) - 2 * GetGray(gray, x, y + 1) - GetGray(gray, x + 1, y + 1);

                var magnitude = Math.Clamp((int)Math.Sqrt((gx * gx) + (gy * gy)), 0, 255);
                output.SetPixel(x, y, new SKColor((byte)magnitude, (byte)magnitude, (byte)magnitude, 255));
            }
        }

        return ApplyColorTransform(output, grayscale: false, contrast: contrast, brightness: brightness, saturation: saturation);
    }

    private static int GetGray(SKBitmap bitmap, int x, int y)
    {
        var p = bitmap.GetPixel(x, y);
        return (int)(0.299f * p.Red + 0.587f * p.Green + 0.114f * p.Blue);
    }

    private static float[] BuildColorMatrix(bool grayscale, float contrast, float brightness, float saturation)
    {
        var c = contrast;
        var b = (brightness - 1f) * 255f;
        var s = saturation;

        if (grayscale)
        {
            return
            [
                0.2126f * c, 0.7152f * c, 0.0722f * c, 0, b,
                0.2126f * c, 0.7152f * c, 0.0722f * c, 0, b,
                0.2126f * c, 0.7152f * c, 0.0722f * c, 0, b,
                0,           0,           0,           1, 0
            ];
        }

        var rw = 0.3086f;
        var gw = 0.6094f;
        var bw = 0.0820f;

        var sr = (1 - s) * rw;
        var sg = (1 - s) * gw;
        var sb = (1 - s) * bw;

        return
        [
            (sr + s) * c, sg * c,       sb * c,       0, b,
            sr * c,       (sg + s) * c, sb * c,       0, b,
            sr * c,       sg * c,       (sb + s) * c, 0, b,
            0,            0,            0,            1, 0
        ];
    }

    private static SKBitmap DecodeImage(byte[] sourceData, int quality)
    {
        if (sourceData == null || sourceData.Length == 0) throw new ArgumentException("Image data is empty.");
        using var encoded = SKData.CreateCopy(sourceData);
        return SKBitmap.Decode(encoded) ?? throw new InvalidDataException("Unsupported or corrupt image; supply a valid PNG, JPEG or supported image file.");
    }
}
