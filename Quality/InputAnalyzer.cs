namespace wwwmmm.Quality;

public enum ComplexityTier { Low, Medium, High }

public sealed record InputCharacteristics(
    int Width, int Height,
    double Entropy, double EdgeDensity, double NoiseEstimate,
    ComplexityTier Tier);

public sealed class InputAnalyzer
{
    public InputCharacteristics Analyze(ImagePixelBuffer image)
    {
        var luma = LumaHelper.Compute(image);
        var entropy = ShannonEntropy(luma);
        var edge = SobelEdgeDensity(luma, image.Width, image.Height);
        var noise = LaplacianNoise(luma, image.Width, image.Height);
        var tier = (entropy, edge) switch
        {
            ( < 4.0, < 0.02) => ComplexityTier.Low,
            ( > 6.5, > 0.10) => ComplexityTier.High,
            _ => ComplexityTier.Medium
        };
        return new InputCharacteristics(image.Width, image.Height, entropy, edge, noise, tier);
    }

    private static double ShannonEntropy(double[] luma)
    {
        var hist = new int[256];
        foreach (var v in luma) hist[Math.Clamp((int)v, 0, 255)]++;
        var n = luma.Length; double h = 0;
        foreach (var c in hist)
        {
            if (c == 0) continue;
            var p = (double)c / n;
            h -= p * Math.Log2(p);
        }
        return h;
    }

    private static double SobelEdgeDensity(double[] luma, int w, int h)
    {
        int strong = 0, total = 0;
        for (int y = 1; y < h - 1; y++)
        for (int x = 1; x < w - 1; x++)
        {
            double gx = -luma[(y-1)*w+x-1] + luma[(y-1)*w+x+1]
                      - 2*luma[y*w+x-1]     + 2*luma[y*w+x+1]
                      - luma[(y+1)*w+x-1]   + luma[(y+1)*w+x+1];
            double gy = -luma[(y-1)*w+x-1] - 2*luma[(y-1)*w+x] - luma[(y-1)*w+x+1]
                      + luma[(y+1)*w+x-1] + 2*luma[(y+1)*w+x] + luma[(y+1)*w+x+1];
            if (Math.Sqrt(gx * gx + gy * gy) > 64) strong++;
            total++;
        }
        return total == 0 ? 0 : (double)strong / total;
    }

    private static double LaplacianNoise(double[] luma, int w, int h)
    {
        double sum = 0; int n = 0;
        for (int y = 1; y < h - 1; y++)
        for (int x = 1; x < w - 1; x++)
        {
            var c = luma[y*w+x];
            var l = luma[y*w+x-1] + luma[y*w+x+1] + luma[(y-1)*w+x] + luma[(y+1)*w+x];
            sum += Math.Abs(4 * c - l); n++;
        }
        return n == 0 ? 0 : sum / n;
    }
}
