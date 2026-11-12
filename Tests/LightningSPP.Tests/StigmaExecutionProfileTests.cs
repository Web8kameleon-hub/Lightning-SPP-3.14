using wwwmmm.Services;
using Xunit;

namespace LightningSPP.Tests;

public class StigmaExecutionProfileTests
{
    [Theory]
    [InlineData(StigmaExecutionProfile.Quality, wwwmmm.Services.Imaging.ImageEngine.Skia)]
    [InlineData(StigmaExecutionProfile.Balanced, wwwmmm.Services.Imaging.ImageEngine.Skia)]
    [InlineData(StigmaExecutionProfile.Lightning, wwwmmm.Services.Imaging.ImageEngine.Skia)]
    [InlineData(StigmaExecutionProfile.Quality, wwwmmm.Services.Imaging.ImageEngine.ImageSharp)]
    [InlineData(StigmaExecutionProfile.Balanced, wwwmmm.Services.Imaging.ImageEngine.ImageSharp)]
    [InlineData(StigmaExecutionProfile.Lightning, wwwmmm.Services.Imaging.ImageEngine.ImageSharp)]
    public async Task EveryProfileCompletesRealFactoryPipeline(StigmaExecutionProfile profile, wwwmmm.Services.Imaging.ImageEngine engine)
    {
        var directory = Path.Combine(Path.GetTempPath(), "spp-profile-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var source = Path.Combine(directory, "source.png");
            await File.WriteAllBytesAsync(source, RealTestImage.Png());
            wwwmmm.Configuration.ServiceFactory.Configure(new() { StigmaProfile = profile, ImageEngine = engine, PrintOutputDirectory = directory });
            var result = await wwwmmm.Configuration.ServiceFactory.CreatePipeline().ExecuteAsync(new(source));
            Assert.True(result.Success, result.Error);
            var artifact = Assert.Single(Directory.GetFiles(directory, "*.br"));
            using var input = File.OpenRead(artifact);
            using var decoder = new System.IO.Compression.BrotliStream(input, System.IO.Compression.CompressionMode.Decompress);
            using var bytes = new MemoryStream();
            await decoder.CopyToAsync(bytes);
            Assert.NotEmpty(bytes.ToArray());
        }
        finally
        {
            wwwmmm.Configuration.ServiceFactory.Configure(new());
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(1024 * 1024 - 1)]
    [InlineData(1024 * 1024)]
    [InlineData(1024 * 1024 + 1)]
    public async Task BalancedCompressionRoundTripsAcrossAutoThreshold(int length)
    {
        var request = StigmaProfiles.Apply(Baseline, StigmaExecutionProfile.Balanced);
        var source = System.Security.Cryptography.RandomNumberGenerator.GetBytes(length);
        var processor = new wwwmmm.Services.Implementation.RealAIProcessor();
        var result = await processor.ProcessAsync(source, new(request.ProcessingEngine, request.UseAI, request.UseClisonix, request.OptimizationLevel, request.UseStigmaMemory));
        Assert.True(result.Success, result.Error);
        var restored = result.ProcessedData;
        for (var stage = 0; stage < (length < 1024 * 1024 ? 1 : 3); stage++)
        {
            using var input = new MemoryStream(restored);
            using var decoder = new System.IO.Compression.BrotliStream(input, System.IO.Compression.CompressionMode.Decompress);
            using var output = new MemoryStream();
            await decoder.CopyToAsync(output);
            restored = output.ToArray();
        }
        Assert.Equal(source, restored);
    }
    private static readonly PipelineRequest Baseline = new("input.bin");

    [Fact]
    public void Quality_Profile_EnablesFullQualityPath()
    {
        var request = StigmaProfiles.Apply(Baseline, StigmaExecutionProfile.Quality);

        Assert.Equal(ScanMode.NanoDecibel, request.ScanMode);
        Assert.Equal(ProcessingEngine.Hybrid, request.ProcessingEngine);
        Assert.False(request.UseAI);
        Assert.False(request.UseClisonix);
        Assert.False(request.UseStigmaMemory);
        Assert.Equal(PrintQuality.Stigma, request.PrintQuality);
    }

    [Fact]
    public void Balanced_Profile_UsesControlledDefaults()
    {
        var request = StigmaProfiles.Apply(Baseline, StigmaExecutionProfile.Balanced);

        Assert.Equal(ScanMode.TideWave, request.ScanMode);
        Assert.Equal(ProcessingEngine.Auto, request.ProcessingEngine);
        Assert.False(request.UseAI);
        Assert.False(request.UseClisonix);
        Assert.False(request.UseStigmaMemory);
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
        Assert.False(request.UseClisonix);
        Assert.False(request.UseResonance);
        Assert.False(request.UseStigmaMemory);
        Assert.Equal(PrintQuality.Normal, request.PrintQuality);
    }
}
