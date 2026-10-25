using wwwmmm.Quality;
using Xunit;

namespace LightningSPP.Tests.Quality;

public class AdvancedMetricsTests
{
    [Fact]
    public void MsSsim_IdenticalImages_ScoreOne()
    {
        var img = TestImageFactory.Random(256, 256, 3, 1);
        var score = MultiScaleSsimQualityMetric.Compute(img, img);
        Assert.InRange(score, 0.999, 1.0);
    }

    [Fact]
    public void Psnr_IdenticalImages_SaturatedAtOne()
    {
        var img = TestImageFactory.Random(64, 64, 3, 1);
        Assert.Equal(1.0, PsnrQualityMetric.ComputeNormalized(img, img), precision: 9);
    }

    [Fact]
    public void Sharpness_IdenticalImages_ScoreOne()
    {
        var img = TestImageFactory.Random(128, 128, 3, 1);
        Assert.Equal(1.0, SharpnessRatioQualityMetric.Compute(img, img), precision: 6);
    }

    [Fact]
    public void Sharpness_BlurredImage_ScoresLow()
    {
        var sharp = TestImageFactory.Random(128, 128, 3, 1);
        var blurred = TestImageFactory.BoxBlur(sharp, 3);
        var score = SharpnessRatioQualityMetric.Compute(sharp, blurred);
        Assert.True(score < 0.6, $"Expected <0.6, got {score:F4}");
    }

    [Fact]
    public void Gradient_IdenticalImages_ScoreOne()
    {
        var img = TestImageFactory.Random(128, 128, 3, 1);
        Assert.Equal(1.0, GradientRatioQualityMetric.Compute(img, img), precision: 6);
    }

    [Fact]
    public void Composite_IdenticalImages_HighScore()
    {
        var img = TestImageFactory.Random(128, 128, 3, 1);
        var c = QualityBreakdownCalculator.Compute(img, img).Composite;
        Assert.True(c > 0.99, $"Expected >0.99, got {c:F4}");
    }

    [Fact]
    public void Composite_NoisyImage_LowerThanIdentical()
    {
        var src = TestImageFactory.Random(128, 128, 3, 1);
        var noisy = TestImageFactory.AddNoise(src, 40, 2);
        var clean = QualityBreakdownCalculator.Compute(src, src).Composite;
        var dirty = QualityBreakdownCalculator.Compute(src, noisy).Composite;
        Assert.True(dirty < clean, $"clean={clean:F4} dirty={dirty:F4}");
    }
}
