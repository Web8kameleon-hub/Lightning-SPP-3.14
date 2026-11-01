namespace wwwmmm.Services;

/// <summary>
/// Converts a processed raster image (PNG bytes) into a real downloadable format.
/// This is a format adapter; it does not drive printing hardware.
/// </summary>
public interface IOutputAdapter
{
    /// <summary>Encode a single raster image into the requested format.</summary>
    byte[] Encode(byte[] rasterImage, OutputFormat format, int quality = 90);

    /// <summary>
    /// Encode several raster pages into a multi-page container (PDF or TIFF).
    /// Single-image formats throw <see cref="NotSupportedException"/>.
    /// </summary>
    byte[] EncodeMultiPage(IReadOnlyList<byte[]> rasterPages, OutputFormat format, int quality = 90);
}

/// <summary>Download formats the adapter can produce.</summary>
public enum OutputFormat
{
    Png,
    Jpeg,
    Bmp,
    Tiff,
    Pdf
}
