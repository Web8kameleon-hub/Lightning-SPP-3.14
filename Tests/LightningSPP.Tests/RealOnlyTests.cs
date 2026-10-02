using System.IO.Compression;
using SkiaSharp;
using wwwmmm.SDK;
using wwwmmm.Services;
using wwwmmm.Services.Implementation;
using Xunit;

namespace LightningSPP.Tests;

internal static class ImageFixture
{
    internal static byte[] Png()
    {
        using var bitmap = new SKBitmap(32, 32);
        for (var y = 0; y < 32; y++)
            for (var x = 0; x < 32; x++) bitmap.SetPixel(x, y, new SKColor((byte)(x * 8), (byte)(y * 8), 90));
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }
}

public class RealOnlyTests
{
    [Fact]
    public async Task InvalidImageDoesNotBecomeAnInventedScan()
    {
        var scan = await new RealImageProcessor().ScanAsync(new byte[] { 1, 2, 3 });
        Assert.False(scan.Success);
        Assert.Empty(scan.Data);
        Assert.Contains("image", scan.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MissingModelCannotReportSuccessfulAI()
    {
        var old = Environment.GetEnvironmentVariable("LSPP_ONNX_MODEL_PATH");
        try
        {
            Environment.SetEnvironmentVariable("LSPP_ONNX_MODEL_PATH", null);
            var result = await new RealAIProcessor().ProcessAsync([1, 2, 3], new ProcessingOptions(ProcessingEngine.Clisonic));
            Assert.False(result.Success);
            Assert.Empty(result.ProcessedData);
            Assert.Contains("LSPP_ONNX_MODEL_PATH", result.Error);
        }
        finally { Environment.SetEnvironmentVariable("LSPP_ONNX_MODEL_PATH", old); }
    }

    [Theory]
    [InlineData(10)]
    [InlineData(80)]
    public async Task MobileProducesLosslessCompressedArtifact(int battery)
    {
        var input = new byte[8192];
        Random.Shared.NextBytes(input);
        Assert.Equal(input, await Restore(await MobileAPI.ProcessMobile(input, battery)));
    }

    [Fact]
    public async Task IotChunksFormOneValidLosslessStream()
    {
        var input = new byte[5000];
        Random.Shared.NextBytes(input);
        Assert.Equal(input, await Restore(await IoTAPI.ProcessConstrained(input, 1)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => IoTAPI.ProcessConstrained(input, 0));
    }

    [Fact]
    public async Task MedicalProfilePreservesBytesAndOnlyValidatesIntegrity()
    {
        var input = new byte[] { 255, 0, 50, 120, 30, 70 };
        Assert.Equal(input, await Restore(await UniversalSDK.Initialize(DeviceProfile.Medical).ProcessAsync(input)));
        var result = await MedicalAPI.ProcessMedicalScan(input, MedicalDeviceType.Scanner);
        Assert.True(result.IsValid);
        Assert.Equal(input, await Restore(result.Data));
    }

    private static async Task<byte[]> Restore(byte[] data)
    {
        using var stream = new MemoryStream(data);
        using var decoder = new BrotliStream(stream, CompressionMode.Decompress);
        using var output = new MemoryStream();
        await decoder.CopyToAsync(output);
        return output.ToArray();
    }
}
