using SkiaSharp;

namespace wwwmmm.FluidEngine.Core.Vision;

public sealed class VisionImage : IDisposable
{
    public SKBitmap Bitmap { get; }
    public int Width => Bitmap.Width;
    public int Height => Bitmap.Height;

    public VisionImage(SKBitmap bitmap) => Bitmap = bitmap;

    public static VisionImage Load(byte[] data)
    {
        using var skData = SKData.CreateCopy(data);
        var bitmap = SKBitmap.Decode(skData) ?? throw new InvalidOperationException("Vision: Decode failed.");
        
        // Normalizo në RGBA
        var normalized = new SKBitmap(new SKImageInfo(bitmap.Width, bitmap.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        if (!bitmap.CopyTo(normalized, SKColorType.Rgba8888))
            throw new InvalidOperationException("Vision: Color conversion failed.");
            
        return new VisionImage(normalized);
    }

    public byte[] SavePng()
    {
        using var image = SKImage.FromBitmap(Bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    public void Dispose() => Bitmap.Dispose();
}
