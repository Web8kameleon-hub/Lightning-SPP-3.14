namespace wwwmmm.Services;

/// <summary>
/// Encodes processed raster images (PNG bytes) into real customer-facing output formats.
/// This is an output-format adapter, not a physical printer driver.
/// </summary>
public interface IOutputAdapter
{
    /// <summary>
    /// Encode a single raster image into the requested format.
    /// </summary>
    byte[] Encode(byte[] rasterImage, OutputFormat format, int quality = 90);

    /// <summary>
    /// Encode multiple raster pages into a multi-page container (TIFF or PDF).
    /// Throws <see cref="NotSupportedException"/> for single-image formats.
    /// </summary>
    byte[] EncodeMultiPage(IReadOnlyList<byte[]> rasterPages, OutputFormat format, int quality = 90);
}

/// <summary>
/// Output formats the adapter can produce.
/// </summary>
public enum OutputFormat
{
    Png,
    Jpeg,
    Bmp,
    Tiff,
    Pdf
}
