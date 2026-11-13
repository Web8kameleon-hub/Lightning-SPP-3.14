using wwwmmm.Quality;

namespace LightningSPP.Tests.Quality;

internal static class TestImageFactory
{
    public static ImagePixelBuffer Random(int w, int h, int ch, int seed)
    {
        var rng = new Random(seed);
        var buf = new byte[w * h * ch];
        rng.NextBytes(buf);
        return new ImagePixelBuffer(buf, w, h, ch, w * ch);
    }

    public static ImagePixelBuffer AddNoise(ImagePixelBuffer src, int amplitude, int seed)
    {
        var rng = new Random(seed);
        var buf = new byte[src.Data.Length];
        src.Data.Span.CopyTo(buf);
        for (int i = 0; i < buf.Length; i++)
        {
            var delta = rng.Next(-amplitude, amplitude + 1);
            buf[i] = (byte)Math.Clamp(buf[i] + delta, 0, 255);
        }
        return new ImagePixelBuffer(buf, src.Width, src.Height, src.Channels, src.Stride);
    }

    public static ImagePixelBuffer BoxBlur(ImagePixelBuffer src, int radius)
    {
        var w = src.Width; var h = src.Height; var ch = src.Channels;
        var buf = new byte[w * h * ch];
        var s = src.Data.Span;
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        for (int c = 0; c < ch; c++)
        {
            int sum = 0, n = 0;
            for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                int yy = y + dy, xx = x + dx;
                if (yy < 0 || yy >= h || xx < 0 || xx >= w) continue;
                sum += s[yy * src.Stride + xx * ch + c]; n++;
            }
            buf[y * w * ch + x * ch + c] = (byte)(sum / n);
        }
        return new ImagePixelBuffer(buf, w, h, ch, w * ch);
    }
}
