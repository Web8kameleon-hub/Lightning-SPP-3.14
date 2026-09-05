using wwwmmm.Services;
using Xunit;

namespace LightningSPP.Tests;

public class StigmaExecutionProfileTests
{
    private static readonly PipelineRequest Baseline = new("input.bin");

    [Fact]
    public void Quality_Profile_EnablesFullQualityPath()
    {
        var request = StigmaProfiles.Apply(Baseline, StigmaExecutionProfile.Quality);

        Assert.Equal(ScanMode.NanoDecibel, request.ScanMode);
        Assert.Equal(ProcessingEngine.Hybrid, request.ProcessingEngine);
        Assert.True(request.UseAI);
        Assert.True(request.UseStigmaMemory);
        Assert.Equal(PrintQuality.Stigma, request.PrintQuality);
    }

    [Fact]
    public void Balanced_Profile_UsesControlledDefaults()
    {
        var request = StigmaProfiles.Apply(Baseline, StigmaExecutionProfile.Balanced);

        Assert.Equal(ScanMode.TideWave, request.ScanMode);
        Assert.Equal(ProcessingEngine.Auto, request.ProcessingEngine);
        Assert.True(request.UseAI);
        Assert.False(request.UseNanoDecibel);
        Assert.Equal(PrintQuality.High, request.PrintQuality);
    }

    [Fact]
    public void Lightning_Profile_DisablesExpensiveStages()
    {
        var request = StigmaProfiles.Apply(Baseline, StigmaExecutionProfile.Lightning);

        Assert.Equal(ScanMode.Lightning, request.ScanMode);
        Assert.Equal(ProcessingEngine.CLX, request.ProcessingEngine);
        Assert.False(request.UseAI);
        Assert.False(request.UseClisonic);
        Assert.False(request.UseResonance);
        Assert.False(request.UseStigmaMemory);
        Assert.Equal(PrintQuality.Normal, request.PrintQuality);
    }
}
