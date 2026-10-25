using wwwmmm.Quality;
using Xunit;

namespace LightningSPP.Tests.Quality;

public class CodecBridgeTests
{
    [Fact]
    public void PngRoundTrip_PreservesPixels()
    {
        var img = TestImageFactory.Random(128, 128, 3, seed: 7);
        var codec = new SkiaPngCodec();
        var round = codec.Decode(codec.Encode(img));
        Assert.True(new SsimQualityMetric().Score(img, round) > 0.999);
    }

    [Fact]
    public void GrayscaleRoundTrip_PreservesPixels()
    {
        var img = TestImageFactory.Random(64, 64, 1, seed: 9);
        var codec = new SkiaPngCodec();
        var round = codec.Decode(codec.Encode(img));
        Assert.True(new SsimQualityMetric().Score(img, round) > 0.999);
    }

    [Fact]
    public void CorruptInput_Throws()
    {
        var codec = new SkiaPngCodec();
        Assert.Throws<InvalidOperationException>(() => codec.Decode(new byte[] { 1, 2, 3, 4 }));
    }
}
