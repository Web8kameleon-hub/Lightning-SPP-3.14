using wwwmmm.Documents;
using SkiaSharp;
using Xunit;

namespace LightningSPP.Tests;
[CollectionDefinition("OcrEnvironment", DisableParallelization = true)]
public sealed class OcrEnvironmentCollection { }

/// <summary>
/// Proof that the OCR path is real rather than asserted: the engine reads an image whose text
/// this test drew itself, and the reader is kept from degrading a PDF that has its own text
/// layer. Environment variables are process-wide, so the class runs alone.
/// </summary>
[Collection("OcrEnvironment")]
public class OcrTests
{
    private const string KnownText = "LIGHTNING SPP 314";

    [Fact]
    public void Locator_ConfiguredPathThatDoesNotExist_IsAnExplicitError()
    {
        using var scope = new EnvironmentScope(OcrLocator.EnvironmentVariable,
            Path.Combine(Path.GetTempPath(), "missing-tesseract-" + Guid.NewGuid().ToString("N") + ".exe"));

        var error = Assert.Throws<FileNotFoundException>(OcrLocator.Locate);

        Assert.Contains(OcrLocator.EnvironmentVariable, error.Message, StringComparison.Ordinal);
    }
[Fact]
    public async Task Engine_RecognizesTextFromAnImageItCannotCheat()
    {
        var png = RenderTextImage(KnownText);

        Assert.True(TesseractOcrEngine.IsAvailable(),
            "Tesseract is required for OCR coverage: install it or set SPP_TESSERACT_PATH.");

        var text = await new TesseractOcrEngine().ReadAsync(png);

        var recognized = Normalize(text);
        Assert.Contains("LIGHTNING", recognized, StringComparison.Ordinal);
        Assert.Contains("314", recognized, StringComparison.Ordinal);
    }

    private static string Normalize(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
private static byte[] RenderTextImage(string text)
    {
        using var bitmap = new SKBitmap(1400, 320);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        using var typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) ?? SKTypeface.Default;
        using var font = new SKFont(typeface, 92);
        using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        canvas.DrawText(text, 40, 190, SKTextAlign.Left, font, paint);
        canvas.Flush();

        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }
[Fact]
    public async Task Reader_WithOcrDisabled_LeavesAScannedPdfEmptyAndNeverCallsTheEngine()
    {
        var png = RenderTextImage(KnownText);
        var path = await WriteTempPdfAsync(new PdfDocumentService().CreateFromImages([png]));
        var engine = new StubEngine(_ => throw new InvalidOperationException("OCR must not run"));

        try
        {
            using var scope = new EnvironmentScope(OcrTextReader.EnabledVariable, null);
            var document = await OcrTextReader.ReadAsync(path, engine: engine);

            Assert.Equal(DocumentText.TextLayerSource, document.TextSource);
            Assert.Equal(0, engine.Calls);
            Assert.All(document.Pages, page => Assert.True(string.IsNullOrWhiteSpace(page.Text)));
        }
        finally
        {
            File.Delete(path);
        }
    }
[Fact]
    public async Task Reader_WithOcrEnabled_RecognizesARealRasterPage()
    {
        var png = RenderTextImage(KnownText);
        var path = await WriteTempPdfAsync(new PdfDocumentService().CreateFromImages([png]));
        try
        {
            using var scope = new EnvironmentScope(OcrTextReader.EnabledVariable, "true");
            Assert.True(TesseractOcrEngine.IsAvailable(),
                "Tesseract is required for OCR coverage: install it or set SPP_TESSERACT_PATH.");
            var document = await OcrTextReader.ReadAsync(path);
Assert.Equal(DocumentText.OcrSource, document.TextSource);
            Assert.Equal(64, document.Sha256.Length);
            Assert.NotEmpty(document.Pages);
Assert.Contains("LIGHTNING", Normalize(document.Pages[0].Text), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }
private static async Task<string> WriteTempPdfAsync(byte[] pdf)
    {
        var path = Path.Combine(Path.GetTempPath(), "spp-ocr-" + Guid.NewGuid().ToString("N") + ".pdf");
        await File.WriteAllBytesAsync(path, pdf);
        return path;
    }

    private sealed class StubEngine(Func<byte[], string> read) : IOcrEngine
    {
        internal int Calls { get; private set; }

        public Task<string> ReadAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(read(imageBytes));
        }
    }

    private sealed class EnvironmentScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _previous;

        internal EnvironmentScope(string name, string? value)
        {
            _name = name;
            _previous = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose() => Environment.SetEnvironmentVariable(_name, _previous);
    }
}
