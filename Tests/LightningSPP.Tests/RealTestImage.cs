using SkiaSharp;

namespace LightningSPP.Tests;

internal static class RealTestImage
{
    public static byte[] Png(int width = 96, int height = 64)
    {
        using var bitmap = new SKBitmap(
            width,
            height,
            SKColorType.Rgba8888,
            SKAlphaType.Premul);

        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);

            using var paint = new SKPaint
            {
                Color = SKColors.Black,
                IsAntialias = true,
                StrokeWidth = 3
            };

            canvas.DrawLine(8, 8, width - 8, height - 8, paint);
            canvas.DrawRect(16, 16, width / 2f, height / 3f, paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);

        if (encoded is null)
            throw new InvalidOperationException("SkiaSharp failed to encode the test PNG.");

        return encoded.ToArray();
    }
}
