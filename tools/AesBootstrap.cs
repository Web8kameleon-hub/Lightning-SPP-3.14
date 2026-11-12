using Microsoft.Extensions.Logging.Abstractions;
using wwwmmm.Services.Implementation;
using wwwmmm.Services.Imaging;

namespace wwwmmm.Tools;

internal static class AesBootstrap
{
    public static ImageEngineAdapter CreateAdapter()
    {
        var skia = new RealImageProcessor();
        var sharp = new FastScanService(NullLogger<FastScanService>.Instance);
        return new ImageEngineAdapter(skia, sharp);
    }
}
