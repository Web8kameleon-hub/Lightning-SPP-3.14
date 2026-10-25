using System.Runtime.InteropServices;
using SkiaSharp;

namespace wwwmmm.Quality;

public sealed class SkiaPngCodec : IImageCodec
{
    public string Name => "PNG/Skia";

    public byte[] Encode(ImagePixelBuffer image)
    {
        image.Validate();
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bmp = new SKBitmap(info);
        var dest = bmp.GetPixels();
        var destStride = bmp.RowBytes;
        var src = image.Data.Span;
        var rowBytes = image.Width * image.Channels;
        var rowBuf = new byte[rowBytes];
        for (int y = 0; y < image.Height; y++)
        {
            src.Slice(y * image.Stride, rowBytes).CopyTo(rowBuf);
            var rgba = new byte[image.Width * 4];
            for (int x = 0; x < image.Width; x++)
            {
                var si = x * image.Channels;
                var di = x * 4;
                if (image.Channels >= 3)
                {
                    rgba[di] = rowBuf[si]; rgba[di + 1] = rowBuf[si + 1]; rgba[di + 2] = rowBuf[si + 2];
                    rgba[di + 3] = image.Channels == 4 ? rowBuf[si + 3] : (byte)255;
                }
                else { rgba[di] = rgba[di + 1] = rgba[di + 2] = rowBuf[si]; rgba[di + 3] = 255; }
            }
            Marshal.Copy(rgba, 0, IntPtr.Add(dest, y * destStride), rgba.Length);
        }
        using var skImage = SKImage.FromBitmap(bmp);
        using var data = skImage.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    public ImagePixelBuffer Decode(byte[] encoded)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        using var data = SKData.CreateCopy(encoded);
        using var decoded = SKBitmap.Decode(data)
            ?? throw new InvalidOperationException($"Skia failed to decode {encoded.Length} bytes.");
        using var normalized = new SKBitmap(
            new SKImageInfo(decoded.Width, decoded.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        if (!decoded.CopyTo(normalized, SKColorType.Rgba8888))
            throw new InvalidOperationException("Skia color conversion failed.");
        var stride = normalized.RowBytes;
        var pixels = new byte[stride * normalized.Height];
        Marshal.Copy(normalized.GetPixels(), pixels, 0, pixels.Length);
        return new ImagePixelBuffer(pixels, normalized.Width, normalized.Height, 4, stride);
    }
}
