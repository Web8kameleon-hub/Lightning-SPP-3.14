using SkiaSharp;
using wwwmmm.FluidEngine.Core.Context;
using wwwmmm.FluidEngine.Core.Contracts;

namespace wwwmmm.FluidEngine.Core.Vision;

public sealed class VisionPipeline : IFluidStep<byte[], byte[]>
{
    public string Name => "VisionPipeline/Skia";

    public Task<byte[]> ExecuteAsync(byte[] input, PipelineContext context, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        using var image = VisionImage.Load(input);
        
        var rotated = new SKBitmap(image.Height, image.Width);
        using (var canvas = new SKCanvas(rotated))
        {
            canvas.Translate(image.Height, 0);
            canvas.RotateDegrees(90);
            using var skImage = SKImage.FromBitmap(image.Bitmap);
            
            // API i saktë për SkiaSharp 3.x/4.x
            var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);
            canvas.DrawImage(skImage, 0, 0, sampling);
        }

        using var result = new VisionImage(rotated);
        var output = result.SavePng();

        sw.Stop();
        context.RecordStep(Name, "SkiaSharp", sw.Elapsed.TotalMilliseconds, true);
        return Task.FromResult(output);
    }
}
