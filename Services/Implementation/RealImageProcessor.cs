using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using System.Diagnostics;
using System.IO;

namespace wwwmmm.Services.Implementation;

/// <summary>
/// REAL Image Processing Service - Using ImageSharp with real source and in-memory data
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
        using var image = LoadImageOrCreateFromBinary(sourceData, quality);

        image.Mutate(x =>
        {
            x.AutoOrient();
            ApplyQualityResize(x, image.Width, image.Height, quality, mode);

            switch (mode)
            {
                case ScanMode.Lightning:
                    x.Grayscale().Contrast(1.05f);
                    break;
                case ScanMode.TideWave:
                    x.Grayscale().GaussianSharpen(1.2f).Contrast(1.12f);
                    break;
                case ScanMode.Resonance:
                    x.DetectEdges().GaussianSharpen(2.0f).Contrast(1.25f);
                    break;
                case ScanMode.NanoDecibel:
                    x.DetectEdges()
                     .GaussianSharpen(2.8f)
                     .Contrast(1.35f)
                     .Brightness(1.08f)
                     .Saturate(1.15f);
                    break;
            }
        });

        await using var ms = new MemoryStream();
        await image.SaveAsPngAsync(ms, cancellationToken);
        return ms.ToArray();
    }

    private static void ApplyQualityResize(
        IImageProcessingContext context,
        int sourceWidth,
        int sourceHeight,
        int quality,
        ScanMode mode)
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
        var width = Math.Max(64, (int)Math.Round(sourceWidth * qualityScale));
        var height = Math.Max(64, (int)Math.Round(sourceHeight * qualityScale));
        context.Resize(width, height);
    }

    private static Image<Rgba32> LoadImageOrCreateFromBinary(byte[] sourceData, int quality)
    {
        try
        {
            return Image.Load<Rgba32>(sourceData);
        }
        catch
        {
            var width = 512;
            var targetHeight = Math.Max(128, Math.Min(4096, quality * 2));
            var height = Math.Max(targetHeight, (int)Math.Ceiling(sourceData.Length / (double)width));
            var image = new Image<Rgba32>(width, height);

            for (var y = 0; y < height; y++)
            {
                var row = image.DangerousGetPixelRowMemory(y).Span;
                for (var x = 0; x < width; x++)
                {
                    var index = y * width + x;
                    var value = index < sourceData.Length ? sourceData[index] : (byte)0;
                    row[x] = new Rgba32(value, value, value, 255);
                }
            }

            return image;
        }
    }
}
