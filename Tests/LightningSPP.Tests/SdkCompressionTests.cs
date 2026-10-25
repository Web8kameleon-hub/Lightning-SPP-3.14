using System.IO.Compression;
using System.Security.Cryptography;
using wwwmmm.SDK;
using Xunit;

namespace LightningSPP.Tests;

public sealed class SdkCompressionTests
{
    private static async Task<byte[]> Restore(byte[] encoded)
    {
        using var input = new MemoryStream(encoded);
        using var decoder = new BrotliStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        await decoder.CopyToAsync(output);
        return output.ToArray();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(19)]
    [InlineData(49)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task MobileProducesDecodableArtifactAndPreservesSource(int battery)
    {
        var source = RandomNumberGenerator.GetBytes(8193);
        var original = source.ToArray();
        var result = await MobileAPI.ProcessMobile(source, battery);
        Assert.NotSame(source, result);
        Assert.Equal(original, await Restore(result));
        Assert.Equal(original, source);
        Assert.Equal(original, await Restore(await MobileAPI.ProcessBackground(source)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(1024)]
    [InlineData(1025)]
    [InlineData(65539)]
    public async Task ConstrainedCompressionIsOneCompleteStreamEvenWhenDataExpands(int length)
    {
        var source = RandomNumberGenerator.GetBytes(length);
        var result = await IoTAPI.ProcessConstrained(source, 1);
        Assert.Equal(source, await Restore(result));
        Assert.Equal(new byte[length], await Restore(await IoTAPI.ProcessConstrained(new byte[length], 1)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public async Task InvalidChunkBudgetIsRejected(int budget) =>
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => IoTAPI.ProcessConstrained([1, 2], budget));

    [Fact]
    public async Task InvalidBatteryAndConcurrencyAreRejected()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => MobileAPI.ProcessMobile([1], -1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => MobileAPI.ProcessMobile([1], 101));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Lightning314.Parallel([[1]], 0));
    }

    [Fact]
    public async Task MedicalResultVerifiesOnlyLosslessBytesAndRejectsEmptyInput()
    {
        var source = RandomNumberGenerator.GetBytes(16385);
        var result = await MedicalAPI.ProcessMedicalScan(source, MedicalDeviceType.Scanner);
        Assert.True(result.IsValid);
        Assert.Equal(source, await Restore(result.Data));
        await Assert.ThrowsAsync<ArgumentException>(() => MedicalAPI.ProcessMedicalScan([], MedicalDeviceType.Scanner));
    }
}
