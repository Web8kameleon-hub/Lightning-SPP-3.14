using System.Buffers.Binary;

namespace wwwmmm.Services.Implementation;

/// <summary>
/// Uncompressed 24-bit BMP (BITMAPINFOHEADER) writer. SkiaSharp 4.x does not encode BMP,
/// so the container is written directly: bottom-up BGR rows padded to a 4-byte boundary.
/// </summary>
internal static class BmpEncoder
{
    private const int FileHeaderSize = 14;
    private const int InfoHeaderSize = 40;
    private const int PixelOffset = FileHeaderSize + InfoHeaderSize;
    private const int PixelsPerMetre = 2835; // 72 DPI

    public static byte[] Encode(Rgb24Image image)
    {
        ArgumentNullException.ThrowIfNull(image.PixelBytes);

        var stride = ((image.Width * 3) + 3) & ~3;
        var imageSize = checked(stride * image.Height);
        var fileSize = checked(PixelOffset + imageSize);
        var buffer = new byte[fileSize];

        buffer[0] = (byte)'B';
        buffer[1] = (byte)'M';
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(2, 4), fileSize);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(10, 4), PixelOffset);

        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(14, 4), InfoHeaderSize);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(18, 4), image.Width);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(22, 4), image.Height);
        BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(26, 2), 1);
        BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(28, 2), 24);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(30, 4), 0);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(34, 4), imageSize);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(38, 4), PixelsPerMetre);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(42, 4), PixelsPerMetre);

        for (var y = 0; y < image.Height; y++)
        {
            var target = PixelOffset + ((image.Height - 1 - y) * stride);
            var source = y * image.Width * 3;
            for (var x = 0; x < image.Width; x++)
            {
                var from = source + (x * 3);
                var to = target + (x * 3);
                buffer[to] = image.PixelBytes[from + 2];
                buffer[to + 1] = image.PixelBytes[from + 1];
                buffer[to + 2] = image.PixelBytes[from];
            }
        }

        return buffer;
    }
}
