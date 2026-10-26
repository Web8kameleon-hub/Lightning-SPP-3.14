using System.Text;
using Xunit;
using wwwmmm.SDK;

namespace LightningSPP.Tests;

public class SdkContractTests
{
    [Fact]
    public async Task ProcessConstrained_ReturnsRawBrotli_AndRoundTrips()
    {
        var input = Encoding.UTF8.GetBytes("legacy-raw-brotli-" + Guid.NewGuid());
        var compressed = await IoTAPI.ProcessConstrained(input, 1);

        Assert.False(compressed.Length >= 2 && compressed[0] == 0x43 && compressed[1] == 0x4C,
            "ProcessConstrained must return raw Brotli, not framed data.");

        var unpacked = await IoTAPI.ProcessConstrainedUnpack(compressed);
        Assert.Equal(input, unpacked);
    }

    [Fact]
    public async Task ProcessConstrainedWithHeader_ReturnsFramed_AndRoundTrips()
    {
        var input = Encoding.UTF8.GetBytes("framed-brotli-" + Guid.NewGuid());
        var compressed = await IoTAPI.ProcessConstrainedWithHeader(input, 1);

        Assert.True(compressed.Length >= 7);
        Assert.Equal(0x43, compressed[0]);
        Assert.Equal(0x4C, compressed[1]);

        var unpacked = await IoTAPI.ProcessConstrainedUnpack(compressed);
        Assert.Equal(input, unpacked);
    }

    [Fact]
    public async Task ProcessConstrainedUnpack_AcceptsBothFormats()
    {
        var input = Encoding.UTF8.GetBytes("dual-format-" + Guid.NewGuid());

        var raw = await IoTAPI.ProcessConstrained(input);
        var framed = await IoTAPI.ProcessConstrainedWithHeader(input);

        Assert.Equal(input, await IoTAPI.ProcessConstrainedUnpack(raw));
        Assert.Equal(input, await IoTAPI.ProcessConstrainedUnpack(framed));
    }
}
