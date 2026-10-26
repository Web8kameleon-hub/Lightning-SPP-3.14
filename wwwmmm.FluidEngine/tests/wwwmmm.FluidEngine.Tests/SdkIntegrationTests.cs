using System.Text;
using Xunit;
using wwwmmm.SDK;

namespace wwwmmm.FluidEngine.Tests;

public class SdkIntegrationTests
{
    [Fact]
    public async Task IoTApi_RoundTrip_Works()
    {
        // 1. Përgatit të dhëna reale (simulim sensori)
        var input = Encoding.UTF8.GetBytes("Sensor-Data: Temp=22.5, Humidity=45.0, ID=" + Guid.NewGuid());

        // 2. Kompreso duke përdorur SDK-në
        var compressed = await IoTAPI.ProcessConstrainedWithHeader(input, maxMemoryKB: 1);
        
        // 3. Verifiko që framing-u u aplikua (Magic 'CL' = 0x43 0x4C)
        Assert.True(compressed.Length > 7);
        Assert.Equal(0x43, compressed[0]);
        Assert.Equal(0x4C, compressed[1]);

        // 4. Zbërthen duke përdorur SDK-në
        var unpacked = await IoTAPI.ProcessConstrainedUnpack(compressed);

        // 5. Hard Proof
        Assert.Equal(input, unpacked);
    }
}
