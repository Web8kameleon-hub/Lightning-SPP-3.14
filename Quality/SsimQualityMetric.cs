namespace wwwmmm.Quality;

public sealed class SsimQualityMetric : IImageQualityMetric
{
    public string Name => "SSIM-8x8";
    private const int Window = 8;
    private const double C1 = (0.01 * 255) * (0.01 * 255);
    private const double C2 = (0.03 * 255) * (0.03 * 255);

    public double Score(ImagePixelBuffer reference, ImagePixelBuffer candidate)
    {
        reference.Validate();
        candidate.Validate();
        if (reference.Width != candidate.Width || reference.Height != candidate.Height)
            throw new ArgumentException("Dimension mismatch.");

        var refLuma = LumaHelper.Compute(reference);
        var candLuma = LumaHelper.Compute(candidate);
        var rx = reference.Width; var ry = reference.Height;

        double sum = 0; int n = 0;
        for (int by = 0; by + Window <= ry; by += Window)
        for (int bx = 0; bx + Window <= rx; bx += Window)
        { sum += WindowSsim(refLuma, candLuma, rx, bx, by); n++; }
        return n == 0 ? 1.0 : sum / n;
    }

    private static double WindowSsim(double[] a, double[] b, int width, int bx, int by)
    {
        double muA = 0, muB = 0;
        for (int y = by; y < by + Window; y++)
        for (int x = bx; x < bx + Window; x++)
        { muA += a[y * width + x]; muB += b[y * width + x]; }
        var N = Window * Window;
        muA /= N; muB /= N;

        double varA = 0, varB = 0, cov = 0;
        for (int y = by; y < by + Window; y++)
        for (int x = bx; x < bx + Window; x++)
        {
            var dA = a[y * width + x] - muA;
            var dB = b[y * width + x] - muB;
            varA += dA * dA; varB += dB * dB; cov += dA * dB;
        }
        varA /= N; varB /= N; cov /= N;
        return ((2 * muA * muB + C1) * (2 * cov + C2))
             / ((muA * muA + muB * muB + C1) * (varA + varB + C2));
    }
}
