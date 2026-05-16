using System.Diagnostics;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace wwwmmm.Services.Implementation;

/// <summary>
/// Lightning-fast scan implementation using TideWave and Resonance
/// </summary>
public class FastScanService : IScanService
{
    public async Task<ScanResult> ScanAsync(ScanRequest request, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            if (string.IsNullOrWhiteSpace(request.Source))
            {
                throw new ArgumentException("Source path must not be empty.", nameof(request));
            }

            byte[] data = request.Mode switch
            {
                ScanMode.Lightning => await LightningScanAsync(request, cancellationToken),
                ScanMode.TideWave => await TideWaveScanAsync(request, cancellationToken),
                ScanMode.Resonance => await ResonanceScanInternalAsync(request, cancellationToken),
                ScanMode.NanoDecibel => await NanoDecibelScanAsync(request, cancellationToken),
                _ => await LightningScanAsync(request, cancellationToken)
            };

            sw.Stop();

            var metadata = new ScanMetadata(
                DateTime.UtcNow,
                sw.ElapsedMilliseconds,
                request.Mode.ToString(),
                request.Quality,
                "PNG"
            );

            return new ScanResult(data, metadata, true);
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

        var processedData = await ApplyResonanceAlgorithm(data, cancellationToken);

        sw.Stop();

        var metadata = new ScanMetadata(
            DateTime.UtcNow,
            sw.ElapsedMilliseconds,
            "Resonance",
            600,
            "PNG"
        );

        return new ScanResult(processedData, metadata, true);
    }

    private async Task<byte[]> LightningScanAsync(ScanRequest request, CancellationToken cancellationToken)
    {
        var sourceData = await ReadSourceBytesAsync(request.Source, cancellationToken);
        var image = LoadImageOrCreateFromBinary(sourceData, request.Quality);

        using (image)
        {
            image.Mutate(ctx =>
            {
                ctx.AutoOrient();
                ResizeForQuality(ctx, image.Size, request.Quality);
                ctx.Grayscale().Contrast(1.05f);
            });

            return await EncodePngAsync(image, cancellationToken);
        }
    }

    private async Task<byte[]> TideWaveScanAsync(ScanRequest request, CancellationToken cancellationToken)
    {
        var sourceData = await ReadSourceBytesAsync(request.Source, cancellationToken);
        var image = LoadImageOrCreateFromBinary(sourceData, request.Quality);

        using (image)
        {
            image.Mutate(ctx =>
            {
                ctx.AutoOrient();
                ResizeForQuality(ctx, image.Size, request.Quality);
                ctx.Grayscale().GaussianSharpen(1.25f).Contrast(1.1f);
            });

            var encoded = await EncodePngAsync(image, cancellationToken);
            return await ApplyTideWaveProcessing(encoded, cancellationToken);
        }
    }

    private async Task<byte[]> ResonanceScanInternalAsync(ScanRequest request, CancellationToken cancellationToken)
    {
        var sourceData = await ReadSourceBytesAsync(request.Source, cancellationToken);
        var image = LoadImageOrCreateFromBinary(sourceData, request.Quality * 2);

        using (image)
        {
            image.Mutate(ctx =>
            {
                ctx.AutoOrient();
                ResizeForQuality(ctx, image.Size, request.Quality * 2);
                ctx.Grayscale().GaussianSharpen(1.8f).Contrast(1.2f);
            });

            var encoded = await EncodePngAsync(image, cancellationToken);
            return await ApplyResonanceAlgorithm(encoded, cancellationToken);
        }
    }

    private async Task<byte[]> NanoDecibelScanAsync(ScanRequest request, CancellationToken cancellationToken)
    {
        var sourceData = await ReadSourceBytesAsync(request.Source, cancellationToken);
        var image = LoadImageOrCreateFromBinary(sourceData, request.Quality * 4);

        using (image)
        {
            image.Mutate(ctx =>
            {
                ctx.AutoOrient();
                ResizeForQuality(ctx, image.Size, request.Quality * 4);
                ctx.Grayscale().GaussianSharpen(2.2f).Contrast(1.3f);
            });

            var encoded = await EncodePngAsync(image, cancellationToken);
            return await ApplyNanoDecibelEnhancement(encoded, cancellationToken);
        }
    }

    private async Task<byte[]> ApplyTideWaveProcessing(byte[] data, CancellationToken cancellationToken)
    {
        return await ApplyImageTransformAsync(data, cancellationToken, ctx =>
        {
            ctx.GaussianBlur(1.1f).Contrast(1.05f);
        });
    }

    private async Task<byte[]> ApplyResonanceAlgorithm(byte[] data, CancellationToken cancellationToken)
    {
        return await ApplyImageTransformAsync(data, cancellationToken, ctx =>
        {
            ctx.GaussianSharpen(1.4f).Contrast(1.12f);
        });
    }

    private async Task<byte[]> ApplyNanoDecibelEnhancement(byte[] data, CancellationToken cancellationToken)
    {
        return await ApplyImageTransformAsync(data, cancellationToken, ctx =>
        {
            ctx.GaussianSharpen(2.0f).Contrast(1.18f);
        });
    }

    private static async Task<byte[]> ReadSourceBytesAsync(string source, CancellationToken cancellationToken)
    {
        if (!File.Exists(source))
        {
            throw new FileNotFoundException($"Source file not found: {source}", source);
        }

        return await File.ReadAllBytesAsync(source, cancellationToken);
    }

    private static Image<Rgba32> LoadImageOrCreateFromBinary(byte[] sourceData, int quality)
    {
        try
        {
            return Image.Load<Rgba32>(sourceData);
        }
        catch (UnknownImageFormatException)
        {
            return BuildImageFromBinaryPayload(sourceData, quality);
        }
    }

    private static Image<Rgba32> BuildImageFromBinaryPayload(byte[] sourceData, int quality)
    {
        var width = 512;
        var targetHeight = Math.Max(128, Math.Min(2048, quality * 2));
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

    private static async Task<byte[]> ApplyImageTransformAsync(
        byte[] data,
        CancellationToken cancellationToken,
        Action<IImageProcessingContext> transform)
    {
        using var image = LoadImageOrCreateFromBinary(data, 300);
        image.Mutate(transform);
        return await EncodePngAsync(image, cancellationToken);
    }

    private static void ResizeForQuality(IImageProcessingContext ctx, Size sourceSize, int quality)
    {
        var scale = Math.Clamp(quality / 300f, 0.5f, 4f);
        var width = Math.Max(64, (int)Math.Round(sourceSize.Width * scale));
        var height = Math.Max(64, (int)Math.Round(sourceSize.Height * scale));
        ctx.Resize(width, height);
    }

    private static async Task<byte[]> EncodePngAsync(Image<Rgba32> image, CancellationToken cancellationToken)
    {
        await using var ms = new MemoryStream();
        var encoder = new PngEncoder
        {
            CompressionLevel = PngCompressionLevel.BestSpeed
        };

        await image.SaveAsPngAsync(ms, encoder, cancellationToken);
        return ms.ToArray();
    }
}
