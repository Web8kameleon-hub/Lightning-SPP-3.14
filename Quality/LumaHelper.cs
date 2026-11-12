namespace wwwmmm.Quality;

internal static class LumaHelper
{
    public static double[] Compute(ImagePixelBuffer img)
    {
        img.Validate();
        var w = img.Width; var h = img.Height; var ch = img.Channels;
        var result = new double[w * h];
        var span = img.Data.Span;
        for (int y = 0; y < h; y++)
        {
            var row = y * img.Stride;
            for (int x = 0; x < w; x++)
            {
                int i = row + x * ch;
                result[y * w + x] = ch >= 3
                    ? 0.299 * span[i] + 0.587 * span[i + 1] + 0.114 * span[i + 2]
                    : span[i];
            }
        }
        return result;
    }
}
