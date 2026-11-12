namespace wwwmmm.Quality;

public readonly record struct QualityBreakdown(
    double Ssim,
    double MsSsim,
    double PsnrNormalized,
    double SharpnessRatio,
    double GradientRatio,
    double Composite);

public static class QualityBreakdownCalculator
{
    public static QualityBreakdown Compute(ImagePixelBuffer reference, ImagePixelBuffer candidate)
    {
        var ssim = new SsimQualityMetric().Score(reference, candidate);
        var msSsim = MultiScaleSsimQualityMetric.Compute(reference, candidate);
        var psnr = PsnrQualityMetric.ComputeNormalized(reference, candidate);
        var sharp = SharpnessRatioQualityMetric.Compute(reference, candidate);
        var grad = GradientRatioQualityMetric.Compute(reference, candidate);
        // Weights chosen so SSIM dominates but blur/oversharpen (where SSIM is weak) get 25%.
        var composite =
            0.35 * ssim +
            0.25 * msSsim +
            0.15 * psnr +
            0.15 * sharp +
            0.10 * grad;
        return new QualityBreakdown(ssim, msSsim, psnr, sharp, grad, composite);
    }
}

/// <summary>MS-SSIM (Wang 2003). 5 scales, renormalized weights for small images.</summary>
public sealed class MultiScaleSsimQualityMetric : IImageQualityMetric
{
    public string Name => "MS-SSIM";
    public double Score(ImagePixelBuffer reference, ImagePixelBuffer candidate)
        => Compute(reference, candidate);

    private static readonly double[] StdWeights = { 0.0448, 0.2856, 0.3001, 0.2363, 0.1333 };

    public static double Compute(ImagePixelBuffer reference, ImagePixelBuffer candidate)
    {
        var r = reference; var c = candidate;
        var used = new List<double>();
        var products = new List<double>();

        for (int i = 0; i < StdWeights.Length; i++)
        {
            if (r.Width < 16 || r.Height < 16) break;
            var s = Math.Max(new SsimQualityMetric().Score(r, c), 1e-9);
            products.Add(Math.Log(s)); // accumulate in log space to avoid underflow
            used.Add(StdWeights[i]);
            if (i < StdWeights.Length - 1)
            {
                r = Downsample(r); c = Downsample(c);
            }
        }

        if (used.Count == 0) return 1.0;
        var sum = used.Sum();
        double logAcc = 0;
        for (int i = 0; i < products.Count; i++)
            logAcc += products[i] * (used[i] / sum);
        return Math.Exp(logAcc);
    }

    private static ImagePixelBuffer Downsample(ImagePixelBuffer src)
    {
        int w = src.Width / 2, h = src.Height / 2;
        if (w < 1 || h < 1) return src;
        var buf = new byte[w * h * src.Channels];
        var span = src.Data.Span;
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        for (int ch = 0; ch < src.Channels; ch++)
        {
            int sum = 0;
            for (int dy = 0; dy < 2; dy++)
            for (int dx = 0; dx < 2; dx++)
                sum += span[(y * 2 + dy) * src.Stride + (x * 2 + dx) * src.Channels + ch];
            buf[y * w * src.Channels + x * src.Channels + ch] = (byte)(sum / 4);
        }
        return new ImagePixelBuffer(buf, w, h, src.Channels, w * src.Channels);
    }
}

/// <summary>PSNR on luma, normalized to [0,1] with a 50 dB cap (above that = identical).</summary>
public sealed class PsnrQualityMetric : IImageQualityMetric
{
    public string Name => "PSNR";
    private const double CapDb = 50.0;
    public double Score(ImagePixelBuffer reference, ImagePixelBuffer candidate)
        => ComputeNormalized(reference, candidate);

    public static double ComputeNormalized(ImagePixelBuffer reference, ImagePixelBuffer candidate)
    {
        var r = LumaHelper.Compute(reference);
        var c = LumaHelper.Compute(candidate);
        double mse = 0;
        for (int i = 0; i < r.Length; i++)
        {
            var d = r[i] - c[i];
            mse += d * d;
        }
        mse /= r.Length;
        if (mse <= 0) return 1.0;
        var psnr = 10.0 * Math.Log10(255.0 * 255.0 / mse);
        return Math.Clamp(psnr / CapDb, 0.0, 1.0);
    }
}

/// <summary>
/// Ratio of Laplacian variances (candidate / reference). Score = 1 - |1 - ratio|.
/// Detects blur (ratio &lt; 1) and oversharpen artifacts (ratio &gt; 1) that SSIM misses.
/// </summary>
public sealed class SharpnessRatioQualityMetric : IImageQualityMetric
{
    public string Name => "SharpnessRatio";
    public double Score(ImagePixelBuffer reference, ImagePixelBuffer candidate)
        => Compute(reference, candidate);

    public static double Compute(ImagePixelBuffer reference, ImagePixelBuffer candidate)
    {
        var rv = LaplacianVariance(reference);
        var cv = LaplacianVariance(candidate);
        if (rv < 1e-6)
            return cv < 1e-6 ? 1.0 : 0.0;
        var ratio = cv / rv;
        return Math.Clamp(1.0 - Math.Abs(1.0 - ratio), 0.0, 1.0);
    }

    private static double LaplacianVariance(ImagePixelBuffer img)
    {
        var luma = LumaHelper.Compute(img);
        int w = img.Width, h = img.Height;
        double sum = 0, sumSq = 0; int n = 0;
        for (int y = 1; y < h - 1; y++)
        for (int x = 1; x < w - 1; x++)
        {
            var c = luma[y * w + x];
            var lap = 4 * c
                    - luma[y * w + x - 1] - luma[y * w + x + 1]
                    - luma[(y - 1) * w + x] - luma[(y + 1) * w + x];
            sum += lap; sumSq += lap * lap; n++;
        }
        if (n == 0) return 0;
        var mean = sum / n;
        return sumSq / n - mean * mean;
    }
}

/// <summary>
/// Ratio of mean Sobel gradient magnitudes. Score = 1 - |1 - ratio|.
/// Captures edge preservation separately from global sharpness.
/// </summary>
public sealed class GradientRatioQualityMetric : IImageQualityMetric
{
    public string Name => "GradientRatio";
    public double Score(ImagePixelBuffer reference, ImagePixelBuffer candidate)
        => Compute(reference, candidate);

    public static double Compute(ImagePixelBuffer reference, ImagePixelBuffer candidate)
    {
        var r = MeanGradient(reference);
        var c = MeanGradient(candidate);
        if (r < 1e-6)
            return c < 1e-6 ? 1.0 : 0.0;
        var ratio = c / r;
        return Math.Clamp(1.0 - Math.Abs(1.0 - ratio), 0.0, 1.0);
    }

    private static double MeanGradient(ImagePixelBuffer img)
    {
        var luma = LumaHelper.Compute(img);
        int w = img.Width, h = img.Height;
        double sum = 0; int n = 0;
        for (int y = 1; y < h - 1; y++)
        for (int x = 1; x < w - 1; x++)
        {
            var gx = -luma[(y - 1) * w + x - 1] + luma[(y - 1) * w + x + 1]
                    - 2 * luma[y * w + x - 1]     + 2 * luma[y * w + x + 1]
                    - luma[(y + 1) * w + x - 1]   + luma[(y + 1) * w + x + 1];
            var gy = -luma[(y - 1) * w + x - 1] - 2 * luma[(y - 1) * w + x] - luma[(y - 1) * w + x + 1]
                    + luma[(y + 1) * w + x - 1] + 2 * luma[(y + 1) * w + x] + luma[(y + 1) * w + x + 1];
            sum += Math.Sqrt(gx * gx + gy * gy); n++;
        }
        return n == 0 ? 0 : sum / n;
    }
}

/// <summary>Weighted composite of 5 metrics. Use <see cref="QualityBreakdownCalculator"/> for detail.</summary>
public sealed class CompositeQualityMetric : IImageQualityMetric
{
    public string Name => "Composite";
    public double Score(ImagePixelBuffer reference, ImagePixelBuffer candidate)
        => QualityBreakdownCalculator.Compute(reference, candidate).Composite;
}
