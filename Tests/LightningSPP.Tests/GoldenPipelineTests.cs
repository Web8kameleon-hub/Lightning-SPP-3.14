using System.IO.Compression;
using Microsoft.Extensions.Logging.Abstractions;
using wwwmmm.Services;
using wwwmmm.Services.Implementation;
using Xunit;

namespace LightningSPP.Tests;

public class GoldenPipelineTests
{
    [Fact]
    public async Task Scan_TideWave_IsDeterministic()
    {
        var svc = new FastScanService(NullLogger<FastScanService>.Instance);
        var input = ImageFixture.Png();

        var first = await svc.ScanAsync(input, ScanMode.TideWave, 300);
        var second = await svc.ScanAsync(input, ScanMode.TideWave, 300);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(first.Data, second.Data);
    }

    [Fact]
    public async Task Process_Hybrid_IsDeterministic()
    {
        var svc = new RealAIProcessor();
        var input = DeterministicPayload(2048);
        var options = new ProcessingOptions(
            ProcessingEngine.Hybrid,
            UseAI: false,
            UseClisonic: false,
            OptimizationLevel: 5,
            UseStigmaMemory: true);

        var first = await svc.ProcessAsync(input, options);
        var second = await svc.ProcessAsync(input, options);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(first.ProcessedData, second.ProcessedData);
    }

    [Fact]
    public async Task Stigma_OutputArtifact_RoundTripsWithoutDataLoss()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"LightningSPP-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);

        try
        {
            var svc = new RealPrintService(outputDir);
            var input = DeterministicPayload(3072);

            var result = await svc.StigmaPrintAsync(input);

            Assert.True(result.Success, result.Error);
            Assert.EndsWith(".br", result.Metadata.Destination, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(result.Metadata.Destination));

            await using var source = File.OpenRead(result.Metadata.Destination);
            await using var decompressor = new BrotliStream(source, CompressionMode.Decompress);
            await using var restored = new MemoryStream();
            await decompressor.CopyToAsync(restored);

            Assert.Equal(input, restored.ToArray());
        }
        finally
        {
            Directory.Delete(outputDir, recursive: true);
        }
    }

    private static byte[] DeterministicPayload(int size)
    {
        var bytes = new byte[size];
        var state = 0x1234567;
        for (var i = 0; i < size; i++)
        {
            state = (state * 1103515245 + 12345) & 0x7fffffff;
            bytes[i] = (byte)(state & 0xFF);
        }

        return bytes;
    }
}
