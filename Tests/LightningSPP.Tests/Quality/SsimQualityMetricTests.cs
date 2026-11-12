using wwwmmm.Quality;
using Xunit;

namespace LightningSPP.Tests.Quality;

public class SsimQualityMetricTests
{
    [Fact]
    public void IdenticalImages_ScoreOne()
    {
        var img = TestImageFactory.Random(64, 64, 3, seed: 1);
        var score = new SsimQualityMetric().Score(img, img);
        Assert.InRange(score, 1.0 - 1e-9, 1.0 + 1e-9);
    }

    [Fact]
    public void NoiseReducesScore()
    {
        var a = TestImageFactory.Random(64, 64, 3, 1);
        var b = TestImageFactory.AddNoise(a, 30, 2);
        Assert.True(new SsimQualityMetric().Score(a, b) < 0.98);
    }

    [Fact]
    public void DimensionMismatch_Throws()
    {
        var a = TestImageFactory.Random(64, 64, 3, 1);
        var b = TestImageFactory.Random(32, 32, 3, 2);
        Assert.Throws<ArgumentException>(() => new SsimQualityMetric().Score(a, b));
    }

    [Fact]
    public void ChannelMismatch_DoesNotThrow()
    {
        var a = TestImageFactory.Random(64, 64, 3, 1);
        var b = TestImageFactory.Random(64, 64, 4, 1);
        var ex = Record.Exception(() => new SsimQualityMetric().Score(a, b));
        Assert.Null(ex);
    }
}
