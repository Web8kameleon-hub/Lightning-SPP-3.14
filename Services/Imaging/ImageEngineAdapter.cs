using wwwmmm.Services.Implementation;

namespace wwwmmm.Services.Imaging;

/// <summary>
/// Explicit L/R image-engine adapter.
/// L = SkiaSharp / RealImageProcessor
/// R = ImageSharp / FastScanService
/// No silent fallback between engines.
/// </summary>
public sealed class ImageEngineAdapter
{
    private readonly IScanService _skia;
    private readonly IScanService _imageSharp;

    public ImageEngineAdapter(
        RealImageProcessor skia,
        FastScanService imageSharp)
    {
        _skia = skia ?? throw new ArgumentNullException(nameof(skia));
        _imageSharp = imageSharp ?? throw new ArgumentNullException(nameof(imageSharp));
    }

    public IScanService Get(ImageEngine engine) =>
        engine switch
        {
            ImageEngine.Skia => _skia,
            ImageEngine.ImageSharp => _imageSharp,
            _ => throw new ArgumentOutOfRangeException(
                nameof(engine),
                engine,
                "Unsupported image engine.")
        };

    public Task<ScanResult> ProcessAsync(
        ImageEngine engine,
        byte[] source,
        ScanMode mode = ScanMode.Lightning,
        int quality = 300,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        return Get(engine).ScanAsync(
            source,
            mode,
            quality,
            cancellationToken);
    }

    public Task<ScanResult> ResonanceAsync(
        ImageEngine engine,
        byte[] source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        return Get(engine).ResonanceScanAsync(
            source,
            cancellationToken);
    }
}
