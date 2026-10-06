using System.Security.Cryptography;
using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using wwwmmm.Services;
using wwwmmm.Services.Implementation;
using Xunit;

namespace LightningSPP.Tests;

public class GoldenPipelineTests
{
    private static readonly string GoldenDir = Path.Combine(FindRepoRoot(), "Tests", "LightningSPP.Tests", "Golden");

    [Fact]
    public async Task Golden_Scan_TideWave_Hash_IsStable()
    {
        var svc = new FastScanService(NullLogger<FastScanService>.Instance);
        var input = RealTestImage.Png();

        var result = await svc.ScanAsync(input, ScanMode.TideWave, 300);

        Assert.True(result.Success);
        // The scanner now uses ImageSharp (the previous baseline used SkiaSharp).
        using var image = SixLabors.ImageSharp.Image.Load(result.Data);
        Assert.True(image.Width > 0 && image.Height > 0);
        var repeated = await svc.ScanAsync(input, ScanMode.TideWave, 300);
        Assert.True(repeated.Success);
        Assert.Equal(result.Data, repeated.Data);
        var hash = Sha256Hex(result.Data);
        AssertGolden("scan_tidewave.sha256", hash);
    }

    [Fact]
    public async Task Golden_Process_Hybrid_Hash_IsStable()
    {
        var svc = new RealAIProcessor();
        var input = DeterministicPayload(2048);

        var result = await svc.ProcessAsync(
            input,
            new ProcessingOptions(ProcessingEngine.Hybrid, UseAI: false, UseClisonix: false, OptimizationLevel: 5, UseStigmaMemory: false));

        Assert.True(result.Success);
        var hash = Sha256Hex(result.ProcessedData);
        AssertGolden("process_hybrid.sha256", hash);
    }

    [Fact]
    public async Task Print_Stigma_OutputArtifact_RoundTripsWithoutDataLoss()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), "LightningSPP-GoldenTests");
        Directory.CreateDirectory(outputDir);

        var svc = new RealPrintService(outputDir);
        var input = DeterministicPayload(3072);

        var result = await svc.StigmaPrintAsync(input);
        Assert.True(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Metadata.Destination));

        var artifact = await File.ReadAllBytesAsync(result.Metadata.Destination);
        // Brotli bytes may change with runtime versions; the lossless payload is the contract.
        using var source = new MemoryStream(artifact);
        using var decoder = new BrotliStream(source, CompressionMode.Decompress);
        using var restored = new MemoryStream();
        await decoder.CopyToAsync(restored);
        Assert.Equal(input, restored.ToArray());
    }

    private static void AssertGolden(string fileName, string actual)
    {
        var path = Path.Combine(GoldenDir, fileName);

        var update = string.Equals(
            Environment.GetEnvironmentVariable("LIGHTNING_GOLDEN_UPDATE"),
            "1",
            StringComparison.OrdinalIgnoreCase);

        if (update)
        {
            Directory.CreateDirectory(GoldenDir);
            File.WriteAllText(path, actual + Environment.NewLine, new UTF8Encoding(false));
        }

        Assert.True(
            File.Exists(path),
            $"Golden baseline is missing: {path}. Set LIGHTNING_GOLDEN_UPDATE=1 only for an explicit baseline update.");

        var expected = File.ReadAllText(path, Encoding.UTF8).Trim();

        Assert.True(
            string.Equals(expected, actual, StringComparison.Ordinal),
            $"Golden mismatch.{Environment.NewLine}Expected: {expected}{Environment.NewLine}Actual:   {actual}");
    }

    private static string Sha256Hex(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return Convert.ToHexString(hash);
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

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "wwwmmm.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        // Fallback for IDE test runners
        return Directory.GetCurrentDirectory();
    }
}
