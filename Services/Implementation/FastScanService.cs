using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace wwwmmm.Services.Implementation;

/// <summary>
/// Lightning-fast scan implementation using TideWave and Resonance.
/// SkiaSharp-backed pipeline for image and binary payload processing.
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
        _logger.LogDebug("ScanAsync started. Mode={Mode} Quality={Quality} Source={Source}", request.Mode, request.Quality, request.Source);

        try
        {
            if (string.IsNullOrWhiteSpace(request.Source))
                throw new ArgumentException("Source path must not be empty.", nameof(request));

            byte[] data = request.Mode switch
            {
                ScanMode.Lightning => await LightningScanAsync(request, cancellationToken),
                ScanMode.TideWave => await TideWaveScanAsync(request, cancellationToken),
                ScanMode.Resonance => await ResonanceScanInternalAsync(request, cancellationToken),
                ScanMode.NanoDecibel => await NanoDecibelScanAsync(request, cancellationToken),
                _ => await LightningScanAsync(request, cancellationToken)
            };

            sw.Stop();
            var metadata = new ScanMetadata(DateTime.UtcNow, sw.ElapsedMilliseconds, request.Mode.ToString(), request.Quality, "PNG");
            _logger.LogDebug("ScanAsync completed in {ElapsedMs}ms", sw.ElapsedMilliseconds);
            return new ScanResult(data, metadata, true);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "ScanAsync failed after {ElapsedMs}ms", sw.ElapsedMilliseconds);
            return new ScanResult(Array.Empty<byte>(), new ScanMetadata(DateTime.UtcNow, sw.ElapsedMilliseconds, "Error", 0, "None"), false, ex.Message);
        }
    }

    public async Task<ScanResult> ScanAsync(byte[] data, ScanMode mode = ScanMode.Lightning, int quality = 300, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogDebug("ScanAsync(byte[]) started. Mode={Mode} Quality={Quality} DataLength={Length}", mode, quality, data.Length);

        try
        {
            var request = new ScanRequest(Source: string.Empty, Mode: mode, Quality: quality);
            var processed = await ProcessInMemoryAsync(data, request, cancellationToken);

            sw.Stop();
            _logger.LogDebug("ScanAsync(byte[]) completed in {ElapsedMs}ms", sw.ElapsedMilliseconds);
            return new ScanResult(processed, new ScanMetadata(DateTime.UtcNow, sw.ElapsedMilliseconds, mode.ToString(), quality, "PNG"), true);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "ScanAsync(byte[]) failed after {ElapsedMs}ms", sw.ElapsedMilliseconds);
            return new ScanResult(Array.Empty<byte>(), new ScanMetadata(DateTime.UtcNow, sw.ElapsedMilliseconds, "Error", 0, "None"), false, ex.Message);
        }
    }

    public async Task<ScanResult> ResonanceScanAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var processedData = await ApplyResonanceAlgorithm(data, cancellationToken);
        sw.Stop();

        var metadata = new ScanMetadata(DateTime.UtcNow, sw.ElapsedMilliseconds, "Resonance", 600, "PNG");
        return new ScanResult(processedData, metadata, true);
    }

    private async Task<byte[]> LightningScanAsync(ScanRequest request, CancellationToken cancellationToken)
    {
        var sourceData = await ReadSourceBytesAsync(request.Source, cancellationToken);
        return await ProcessBytesByModeAsync(sourceData, ScanMode.Lightning, request.Quality, cancellationToken);
    }

    private async Task<byte[]> TideWaveScanAsync(ScanRequest request, CancellationToken cancellationToken)
    {
        var sourceData = await ReadSourceBytesAsync(request.Source, cancellationToken);
        var encoded = await ProcessBytesByModeAsync(sourceData, ScanMode.TideWave, request.Quality, cancellationToken);
        return await ApplyTideWaveProcessing(encoded, cancellationToken);
    }

    private async Task<byte[]> ResonanceScanInternalAsync(ScanRequest request, CancellationToken cancellationToken)
    {
        var sourceData = await ReadSourceBytesAsync(request.Source, cancellationToken);
        var encoded = await ProcessBytesByModeAsync(sourceData, ScanMode.Resonance, request.Quality * 2, cancellationToken);
        return await ApplyResonanceAlgorithm(encoded, cancellationToken);
    }

    private async Task<byte[]> ProcessInMemoryAsync(byte[] sourceData, ScanRequest request, CancellationToken cancellationToken)
    {
        var encoded = await ProcessBytesByModeAsync(sourceData, request.Mode, request.Quality, cancellationToken);
        return request.Mode switch
        {
            ScanMode.TideWave => await ApplyTideWaveProcessing(encoded, cancellationToken),
            ScanMode.Resonance => await ApplyResonanceAlgorithm(encoded, cancellationToken),
            ScanMode.NanoDecibel => await ApplyNanoDecibelEnhancement(encoded, cancellationToken),
            _ => encoded
        };
    }

    private async Task<byte[]> NanoDecibelScanAsync(ScanRequest request, CancellationToken cancellationToken)
    {
        var effectiveQuality = Math.Min(request.Quality * 4, MaxNanoDecibelQuality);
        var sourceData = await ReadSourceBytesAsync(request.Source, cancellationToken);
        var encoded = await ProcessBytesByModeAsync(sourceData, ScanMode.NanoDecibel, effectiveQuality, cancellationToken);
        return await ApplyNanoDecibelEnhancement(encoded, cancellationToken);
    }

    private async Task<byte[]> ApplyTideWaveProcessing(byte[] data, CancellationToken cancellationToken)
    {
        return await ApplyImageTransformAsync(data, cancellationToken, bitmap => ApplyColorTransform(bitmap, grayscale: true, contrast: 1.05f, saturation: 1.03f));
    }

    private async Task<byte[]> ApplyResonanceAlgorithm(byte[] data, CancellationToken cancellationToken)
    {
        return await ApplyImageTransformAsync(data, cancellationToken, bitmap => ApplyEdgeEnhanced(bitmap, contrast: 1.12f, brightness: 1.02f));
    }

    private async Task<byte[]> ApplyNanoDecibelEnhancement(byte[] data, CancellationToken cancellationToken)
    {
        return await ApplyImageTransformAsync(data, cancellationToken, bitmap => ApplyEdgeEnhanced(bitmap, contrast: 1.18f, brightness: 1.05f, saturation: 1.10f));
    }

    private static async Task<byte[]> ReadSourceBytesAsync(string source, CancellationToken cancellationToken)
    {
        if (!File.Exists(source))
            throw new FileNotFoundException($"Source file not found: {source}", source);

        return await File.ReadAllBytesAsync(source, cancellationToken);
    }

    private static async Task<byte[]> ProcessBytesByModeAsync(byte[] sourceData, ScanMode mode, int quality, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var sourceBitmap = LoadBitmapOrCreateFromBinary(sourceData, quality);
        using var resizedBitmap = ResizeByQuality(sourceBitmap, quality, mode);
        using var processedBitmap = ApplyModeProcessing(resizedBitmap, mode);
        using var image = SKImage.FromBitmap(processedBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

        await using var ms = new MemoryStream();
        data.SaveTo(ms);
        return ms.ToArray();
    }

    private static async Task<byte[]> ApplyImageTransformAsync(byte[] data, CancellationToken cancellationToken, Func<SKBitmap, SKBitmap> transform)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var input = LoadBitmapOrCreateFromBinary(data, 300);
        using var transformed = transform(input);
        using var image = SKImage.FromBitmap(transformed);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);

        await using var ms = new MemoryStream();
        encoded.SaveTo(ms);
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

        var scale = Math.Clamp(quality / 300f, 0.5f, 4f) * modeMultiplier;
        var width = Math.Max(64, (int)Math.Round(source.Width * scale));
        var height = Math.Max(64, (int)Math.Round(source.Height * scale));

        var resized = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(resized);
        using var paint = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.High, IsDither = true };
        canvas.Clear(SKColors.Black);
        canvas.DrawBitmap(source, new SKRect(0, 0, width, height), paint);
        return resized;
    }

    private static SKBitmap ApplyModeProcessing(SKBitmap source, ScanMode mode)
    {
        return mode switch
        {
            ScanMode.Lightning => ApplyColorTransform(source, grayscale: true, contrast: 1.05f),
            ScanMode.TideWave => ApplyColorTransform(source, grayscale: true, contrast: 1.10f, saturation: 1.03f),
            ScanMode.Resonance => ApplyEdgeEnhanced(source, contrast: 1.20f, brightness: 1.02f),
            ScanMode.NanoDecibel => ApplyEdgeEnhanced(source, contrast: 1.30f, brightness: 1.08f, saturation: 1.12f),
            _ => source.Copy(SKColorType.Rgba8888)
        };
    }

    private static SKBitmap ApplyColorTransform(SKBitmap source, bool grayscale = false, float contrast = 1.0f, float brightness = 1.0f, float saturation = 1.0f)
    {
        var output = new SKBitmap(source.Width, source.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(output);
        using var paint = new SKPaint
        {
            IsAntialias = true,
            FilterQuality = SKFilterQuality.High,
            ColorFilter = SKColorFilter.CreateColorMatrix(BuildColorMatrix(grayscale, contrast, brightness, saturation))
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

        return ApplyColorTransform(output, contrast: contrast, brightness: brightness, saturation: saturation);
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

        const float rw = 0.3086f;
        const float gw = 0.6094f;
        const float bw = 0.0820f;

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

    private static SKBitmap LoadBitmapOrCreateFromBinary(byte[] sourceData, int quality)
    {
        try
        {
            using var managed = SKData.CreateCopy(sourceData);
            var decoded = SKBitmap.Decode(managed);
            if (decoded != null)
                return decoded;
        }
        catch
        {
            // Fallback below.
        }

        var width = 512;
        var targetHeight = Math.Max(128, Math.Min(2048, quality * 2));
        var height = Math.Max(targetHeight, (int)Math.Ceiling(sourceData.Length / (double)width));
        var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = y * width + x;
                var value = index < sourceData.Length ? sourceData[index] : (byte)0;
                bitmap.SetPixel(x, y, new SKColor(value, value, value, 255));
            }
        }

        return bitmap;
    }
}