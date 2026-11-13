using wwwmmm.Quality;
using Xunit;

namespace LightningSPP.Tests.Quality;

public class AdaptiveEngineSelectorTests
{
    [Fact]
    public async Task QualityBiased_PicksHigherSsim()
    {
        var src = TestImageFactory.Random(512, 512, 3, 1);
        var selector = new AdaptiveEngineSelector(new SsimQualityMetric(), new InputAnalyzer());
        var sel = await selector.SelectAsync(
            new[]
            {
                new EngineCandidate("lossy", (img, _) => Task.FromResult(TestImageFactory.AddNoise(img, 20, 2))),
                new EngineCandidate("lossless", (img, _) => Task.FromResult(img))
            },
            src, SelectionPolicy.QualityBiased);
        Assert.Equal("lossless", sel.EngineId);
        Assert.True(sel.QualityScore > 0.99);
    }

    [Fact]
    public async Task FailingCandidate_IsDisqualified()
    {
        var src = TestImageFactory.Random(128, 128, 3, 1);
        var selector = new AdaptiveEngineSelector(new SsimQualityMetric(), new InputAnalyzer());
        var sel = await selector.SelectAsync(
            new[]
            {
                new EngineCandidate("broken", (_, _) => throw new InvalidOperationException("simulated failure")),
                new EngineCandidate("good", (img, _) => Task.FromResult(img))
            },
            src, SelectionPolicy.Balanced);
        Assert.Equal("good", sel.EngineId);
        Assert.True(sel.All.Single(r => r.EngineId == "broken").Failed);
    }

    [Fact]
    public async Task AllFail_ThrowsWithDetail()
    {
        var src = TestImageFactory.Random(64, 64, 3, 1);
        var selector = new AdaptiveEngineSelector(new SsimQualityMetric(), new InputAnalyzer());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            selector.SelectAsync(
                new[] { new EngineCandidate("x", (_, _) => throw new Exception("boom")) },
                src, SelectionPolicy.Balanced));
        Assert.Contains("x: boom", ex.Message);
    }
}
