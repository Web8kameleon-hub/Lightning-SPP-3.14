using System.Diagnostics;
using Microsoft.Extensions.Logging;
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
    private const int MaxNanoDecibelQuality = 2400;

    private readonly ILogger<FastScanService> _logger;

    public FastScanService(ILogger<FastScanService> logger)
    {
        _logger = logger;
    }
    public async Task<ScanResult> ScanAsync(ScanRequest request, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogDebug("ScanAsync started. Mode={Mode} Quality={Quality} Source={Source}",
            request.Mode, request.Quality, request.Source);

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

            _logger.LogDebug("ScanAsync completed in {ElapsedMs}ms", sw.ElapsedMilliseconds);
            return new ScanResult(data, metadata, true);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "ScanAsync failed after {ElapsedMs}ms", sw.ElapsedMilliseconds);
            return new ScanResult(
                Array.Empty<byte>(),
                new ScanMetadata(DateTime.UtcNow, sw.ElapsedMilliseconds, "Error", 0, "None"),
                false,
                ex.Message
            );
        }
    }

    public async Task<ScanResult> ScanAsync(byte[] data, ScanMode mode = ScanMode.Lightning, int quality = 300, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogDebug("ScanAsync(byte[]) started. Mode={Mode} Quality={Quality} DataLength={Length}",
            mode, quality, data.Length);

        try
        {
            var request = new ScanRequest(Source: string.Empty, Mode: mode, Quality: quality);
            var processed = mode switch
            {
                ScanMode.Lightning => await ProcessInMemoryAsync(data, request, cancellationToken),
                ScanMode.TideWave => await ProcessInMemoryAsync(data, request, cancellationToken),
                ScanMode.Resonance => await ProcessInMemoryAsync(data, request, cancellationToken),
                ScanMode.NanoDecibel => await ProcessInMemoryAsync(data, request, cancellationToken),
                _ => await ProcessInMemoryAsync(data, request, cancellationToken)
            };

            sw.Stop();
            _logger.LogDebug("ScanAsync(byte[]) completed in {ElapsedMs}ms", sw.ElapsedMilliseconds);
            return new ScanResult(processed, new ScanMetadata(DateTime.UtcNow, sw.ElapsedMilliseconds, mode.ToString(), quality, "PNG"), true);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "ScanAsync(byte[]) failed after {ElapsedMs}ms", sw.ElapsedMilliseconds);
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
        var image = LoadSupportedImage(sourceData, request.Quality);

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
        var image = LoadSupportedImage(sourceData, request.Quality);

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
        var image = LoadSupportedImage(sourceData, request.Quality * 2);

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

    private async Task<byte[]> ProcessInMemoryAsync(byte[] sourceData, ScanRequest request, CancellationToken cancellationToken)
    {
        var image = LoadSupportedImage(sourceData, request.Quality);
        using (image)
        {
            image.Mutate(ctx =>
            {
                ctx.AutoOrient();
                ResizeForQuality(ctx, image.Size, request.Quality);
                ApplyModeFilters(ctx, request.Mode);
            });
            var encoded = await EncodePngAsync(image, cancellationToken);
            return request.Mode switch
            {
                ScanMode.TideWave => await ApplyTideWaveProcessing(encoded, cancellationToken),
                ScanMode.Resonance => await ApplyResonanceAlgorithm(encoded, cancellationToken),
                ScanMode.NanoDecibel => await ApplyNanoDecibelEnhancement(encoded, cancellationToken),
                _ => encoded
            };
        }
    }

    private static void ApplyModeFilters(IImageProcessingContext ctx, ScanMode mode)
    {
        switch (mode)
        {
            case ScanMode.Lightning:
                ctx.Grayscale().Contrast(1.05f);
                break;
            case ScanMode.TideWave:
                ctx.Grayscale().GaussianSharpen(1.25f).Contrast(1.1f);
                break;
            case ScanMode.Resonance:
                ctx.Grayscale().GaussianSharpen(1.8f).Contrast(1.2f);
                break;
            case ScanMode.NanoDecibel:
                ctx.Grayscale().GaussianSharpen(2.2f).Contrast(1.3f);
                break;
        }
    }

    private async Task<byte[]> NanoDecibelScanAsync(ScanRequest request, CancellationToken cancellationToken)
    {
        var effectiveQuality = Math.Min(request.Quality * 4, MaxNanoDecibelQuality);
        var sourceData = await ReadSourceBytesAsync(request.Source, cancellationToken);
        var image = LoadSupportedImage(sourceData, effectiveQuality);

        using (image)
        {
            image.Mutate(ctx =>
            {
                ctx.AutoOrient();
                ResizeForQuality(ctx, image.Size, effectiveQuality);
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

    private static Image<Rgba32> LoadSupportedImage(byte[] sourceData, int quality)
    {
        ArgumentNullException.ThrowIfNull(sourceData);

        if (sourceData.Length == 0)
            throw new InvalidDataException("Image input is empty.");

        try
        {
            return Image.Load<Rgba32>(sourceData);
        }
        catch (UnknownImageFormatException ex)
        {
            throw new InvalidDataException(
                "Input is not a supported image. Arbitrary binary payloads are not accepted.",
                ex);
        }
    }
    private static async Task<byte[]> ApplyImageTransformAsync(
        byte[] data,
        CancellationToken cancellationToken,
        Action<IImageProcessingContext> transform)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var image = LoadSupportedImage(data, 300);
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
