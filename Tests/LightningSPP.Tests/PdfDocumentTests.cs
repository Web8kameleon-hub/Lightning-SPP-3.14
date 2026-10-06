using SkiaSharp;
using wwwmmm.Documents;
using Xunit;

namespace LightningSPP.Tests;

public class PdfDocumentTests
{
    [Fact]
    public void CreatesRealMultipagePdfFromImages()
    {
        var pdf = new PdfDocumentService().CreateFromImages([Image(SKColors.Red), Image(SKColors.Blue)]);
        Assert.True(pdf.AsSpan(0, 5).SequenceEqual("%PDF-"u8));
        Assert.Contains("%%EOF", System.Text.Encoding.ASCII.GetString(pdf));
    }

    [Fact]
    public void InvalidImageIsRejectedInsteadOfVisualizingBytes() =>
        Assert.Throws<DocumentException>(() => new PdfDocumentService().CreateFromImages(["not an image"u8.ToArray()]));

    [Theory]
    [InlineData(0)]
    [InlineData(71)]
    [InlineData(601)]
    public void InvalidResolutionIsRejected(int dpi) =>
        Assert.Throws<ArgumentException>(() => new PdfDocumentService().CreateFromImages([Image(SKColors.Red)], dpi));

    [Fact]
    public void EmptyPageSetIsRejected() =>
        Assert.Throws<ArgumentException>(() => new PdfDocumentService().CreateFromImages([]));

    [Fact]
    public void CancellationStopsPdfCreation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => new PdfDocumentService().CreateFromImages(
            [Image(SKColors.Red)], cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task ExistingOutputIsPreserved()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".pdf");
        var original = "existing fixture"u8.ToArray();
        await File.WriteAllBytesAsync(path, original);
        try
        {
            await Assert.ThrowsAsync<IOException>(() => DocumentFiles.SaveNewAsync(path, [1, 2], ".pdf", default));
            Assert.Equal(original, await File.ReadAllBytesAsync(path));
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + ".*.tmp"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task InvalidPdfHeaderIsRejectedBeforeNativeTools() =>
        await Assert.ThrowsAsync<ArgumentException>(() => new PdfDocumentService().RenderPageAsync("invalid pdf"u8.ToArray()));

    [Fact]
    public async Task UnsafePrinterAndJobNamesCannotBecomeCommandArguments()
    {
        var device = new LinuxDeviceService();
        if (OperatingSystem.IsLinux())
            await Assert.ThrowsAsync<ArgumentException>(() => device.SubmitPdfAsync("-h evil", [1]));
        else
            await Assert.ThrowsAsync<PlatformNotSupportedException>(() => device.SubmitPdfAsync("printer", [1]));
        await Assert.ThrowsAsync<ArgumentException>(() => device.GetPrintQueueAsync("--help"));
    }

    [NativeDocumentFact]
    public async Task RealPopplerRendersCorrectPageAndPreservesTotalCount()
    {
        var service = new PdfDocumentService();
        var pdf = service.CreateFromImages([Image(SKColors.Red), Image(SKColors.Blue)]);
        var page = await service.RenderPageAsync(pdf, 2);
        Assert.Equal(2, page.Page);
        Assert.Equal(2, page.TotalPages);
        using var bitmap = SKBitmap.Decode(page.Png);
        Assert.NotNull(bitmap);
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2));
        Assert.True(Math.Max(bitmap.Width, bitmap.Height) <= 2400);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.RenderPageAsync(pdf, 3));
    }

    [NativeDocumentFact]
    public async Task RealPopplerRejectsMalformedPdfAndRemovesTemporaryFiles()
    {
        var before = Directory.GetDirectories(Path.GetTempPath(), "spp-pdf-*").ToHashSet();
        await Assert.ThrowsAsync<DocumentException>(() => new PdfDocumentService().RenderPageAsync("%PDF-1.7\nbroken fixture"u8.ToArray()));
        Assert.Empty(Directory.GetDirectories(Path.GetTempPath(), "spp-pdf-*").Where(path => !before.Contains(path)));
    }

    internal static byte[] Image(SKColor color)
    {
        using var bitmap = new SKBitmap(64, 64);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(color);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}

public sealed class NativeDocumentFactAttribute : FactAttribute
{
    public NativeDocumentFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("SPP_TEST_NATIVE_DOCUMENTS") != "1")
            Skip = "Real Poppler verification requires installed pdfinfo/pdftoppm and SPP_TEST_NATIVE_DOCUMENTS=1.";
    }
}
