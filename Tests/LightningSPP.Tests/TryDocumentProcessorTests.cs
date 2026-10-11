using System.IO.Compression;
using SkiaSharp;
using wwwmmm.Configuration;
using wwwmmm.Documents;
using wwwmmm.Services;
using wwwmmm.Web;
using Xunit;

namespace LightningSPP.Tests;

public class TryDocumentProcessorTests
{
    [Fact]
    public async Task ImageOutputIsExplicitlyDecodedFromRealProcessedData()
    {
        var input = RealTestImage.Png();
        var result = await NewProcessor().ProcessAsync(input, false, ScanMode.Lightning);
        Assert.NotNull(result.ImageArtifact);
        Assert.Equal(Restore(result.ImageArtifact), result.Preview);
        Assert.Equal(result.Preview, result.Output);
        using var image = SKBitmap.Decode(result.Output);
        Assert.NotNull(image);
        Assert.Equal(0, result.TotalPages);
        Assert.True(result.TotalTimeMs >= result.ScanTimeMs + result.ProcessTimeMs);
    }

    [Fact]
    public async Task InvalidImageFailsClosed() =>
        await Assert.ThrowsAsync<DocumentException>(() => NewProcessor().ProcessAsync(
            "not an image"u8.ToArray(), false, ScanMode.Lightning));

    [Fact]
    public async Task CompressedImageAbovePixelLimitIsRejectedBeforeProcessing()
    {
        using var bitmap = new SKBitmap(4001, 2000);
        bitmap.Erase(SKColors.White);
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        Assert.True(png.Size < 8 * 1024 * 1024);
        var observed = new ObservedProcessor(ServiceFactory.CreateProcessingService());
        var error = await Assert.ThrowsAsync<DocumentException>(() =>
            new TryDocumentProcessor(ServiceFactory.CreateScanService(), observed)
                .ProcessAsync(png.ToArray(), false, ScanMode.Lightning));
        Assert.Contains("eight million pixels", error.Message);
        Assert.Empty(observed.Results);
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("truncated")]
    [InlineData("empty")]
    [InlineData("invalid-image")]
    public async Task InvalidHybridOutputIsDocumentFailureWithoutOutput(string fault)
    {
        var recording = new ObservedProcessor(ServiceFactory.CreateProcessingService(), artifactFault: fault);
        TryDocumentResult? output = null;
        var error = await Assert.ThrowsAsync<DocumentException>(async () => output =
            await new TryDocumentProcessor(ServiceFactory.CreateScanService(), recording)
                .ProcessAsync(RealTestImage.Png(), false, ScanMode.Lightning));
        Assert.Null(output);
        Assert.Single(recording.Results);
        Assert.True(recording.Results[0].Success);
        Assert.NotEmpty(recording.Results[0].ProcessedData);
        // DocumentException selects the existing sanitized 422 handler, not ArgumentException's 400.
        if (fault == "malformed")
            Assert.Contains("Hybrid image restoration failed", error.Message);
        if (fault == "empty")
            Assert.Contains("Restored Hybrid image validation failed", error.Message);
    }

    [Theory]
    [InlineData("io")]
    [InlineData("runtime")]
    public async Task UnexpectedProcessingFailuresAreNotReclassifiedAsDocumentErrors(string failure)
    {
        var recording = new ObservedProcessor(ServiceFactory.CreateProcessingService(), serverFailure: failure);
        var error = await Record.ExceptionAsync(() =>
            new TryDocumentProcessor(ServiceFactory.CreateScanService(), recording)
                .ProcessAsync(RealTestImage.Png(), false, ScanMode.Lightning));
        if (failure == "io") Assert.IsType<IOException>(error);
        else Assert.IsType<InvalidOperationException>(error);
        Assert.Empty(recording.Results);
    }

    [NativeDocumentFact]
    public async Task SecondPageRestorationFailureNeverReturnsPartialPdf()
    {
        var pdf = CreatePdf(3);
        foreach (var fault in new[] { "malformed", "truncated", "empty", "invalid-image" })
        {
            var recording = new ObservedProcessor(ServiceFactory.CreateProcessingService(),
                artifactFault: fault, artifactFaultAt: 2);
            TryDocumentResult? output = null;
            await Assert.ThrowsAsync<DocumentException>(async () => output =
                await new TryDocumentProcessor(ServiceFactory.CreateScanService(), recording)
                    .ProcessAsync(pdf, true, ScanMode.Lightning));
            Assert.Null(output);
            Assert.Equal(2, recording.Results.Count);
            Assert.All(recording.Results, result => Assert.True(result.Success));
        }
    }

    [NativeDocumentFact]
    public async Task RealTwoPagePdfProcessesBothPagesAndWritesRealPdf()
    {
        var core = ServiceFactory.CreateProcessingService();
        var recording = new ObservedProcessor(core);
        var input = new PdfDocumentService().CreateFromImages(
            [PdfDocumentTests.Image(SKColors.Red), PdfDocumentTests.Image(SKColors.Blue)]);
        var result = await new TryDocumentProcessor(ServiceFactory.CreateScanService(), recording)
            .ProcessAsync(input, true, ScanMode.Lightning);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(2, recording.Results.Count);
        Assert.Null(result.ImageArtifact);
        Assert.True(result.Output.AsSpan(0, 5).SequenceEqual("%PDF-"u8));
        Assert.Equal(Restore(recording.Results[0].ProcessedData), result.Preview);
        Assert.Equal(recording.Results.Sum(r => r.Metadata.ProcessingTimeMs), result.ProcessTimeMs);
        Assert.True(result.TotalTimeMs >= result.ScanTimeMs + result.ProcessTimeMs);
        for (var page = 1; page <= 2; page++)
        {
            var rendered = await new PdfDocumentService().RenderPageAsync(result.Output, page);
            Assert.Equal(2, rendered.TotalPages);
            using var actual = SKBitmap.Decode(rendered.Png);
            using var expected = SKBitmap.Decode(Restore(recording.Results[page - 1].ProcessedData));
            Assert.Equal(expected.GetPixel(expected.Width / 2, expected.Height / 2),
                actual.GetPixel(actual.Width / 2, actual.Height / 2));
        }
    }

    [NativeDocumentFact]
    public async Task ThirtyThreePagesAreRejectedBeforeAnyProcessing()
    {
        var recording = new ObservedProcessor(ServiceFactory.CreateProcessingService());
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new TryDocumentProcessor(ServiceFactory.CreateScanService(), recording)
                .ProcessAsync(CreatePdf(33), true, ScanMode.Lightning));
        Assert.Empty(recording.Results);
    }

    [NativeDocumentFact]
    public async Task SecondPageProcessingFailureAbortsJobWithoutReturningPartialPdf()
    {
        // Fault injection is confined to this test; rendering and successful processing are real.
        var recording = new ObservedProcessor(ServiceFactory.CreateProcessingService(), failAt: 2);
        TryDocumentResult? output = null;
        await Assert.ThrowsAsync<DocumentException>(async () => output =
            await new TryDocumentProcessor(ServiceFactory.CreateScanService(), recording)
                .ProcessAsync(CreatePdf(3), true, ScanMode.Lightning));
        Assert.Null(output);
        Assert.Equal(2, recording.Results.Count);
        Assert.True(recording.Results[0].Success);
        Assert.False(recording.Results[1].Success);
    }

    internal static byte[] CreatePdf(int count)
    {
        // The same Skia engine as PdfDocumentService; permits a real over-limit test fixture.
        using var output = new MemoryStream();
        using var document = SKDocument.CreatePdf(output);
        for (var page = 0; page < count; page++)
        {
            var canvas = document.BeginPage(24, 24);
            canvas.Clear(page % 2 == 0 ? SKColors.Red : SKColors.Blue);
            document.EndPage();
        }
        document.Close();
        return output.ToArray();
    }

    internal static byte[] Restore(byte[] artifact)
    {
        for (var stage = 0; stage < 3; stage++)
        {
            using var source = new MemoryStream(artifact);
            using var decoder = new BrotliStream(source, CompressionMode.Decompress);
            using var output = new MemoryStream();
            decoder.CopyTo(output);
            artifact = output.ToArray();
        }
        return artifact;
    }

    private static TryDocumentProcessor NewProcessor() =>
        new(ServiceFactory.CreateScanService(), ServiceFactory.CreateProcessingService());

    private sealed class ObservedProcessor(IProcessingService core, int failAt = 0,
        string? artifactFault = null, int artifactFaultAt = 1, string? serverFailure = null) : IProcessingService
    {
        internal List<ProcessingResult> Results { get; } = [];

        public async Task<ProcessingResult> ProcessAsync(byte[] data, ProcessingOptions options,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal(new ProcessingOptions(ProcessingEngine.Hybrid, UseAI: false, UseClisonix: false,
                OptimizationLevel: 3, UseStigmaMemory: false), options);
            if (serverFailure == "io") throw new IOException("Test-injected server I/O failure");
            if (serverFailure == "runtime") throw new InvalidOperationException("Test-injected runtime failure");
            var result = await core.ProcessAsync(data, options, cancellationToken);
            // Test-only artifact faults; empty/invalid image artifacts use the real Hybrid encoder.
            if (Results.Count + 1 == artifactFaultAt && artifactFault is not null)
            {
                result = artifactFault switch
                {
                    "malformed" => result with
                    {
                        ProcessedData = Enumerable.Repeat((byte)0xff, result.ProcessedData.Length).ToArray()
                    },
                    "truncated" => result with { ProcessedData = result.ProcessedData[..(result.ProcessedData.Length / 2)] },
                    "empty" => await core.ProcessAsync([], options, cancellationToken),
                    "invalid-image" => await core.ProcessAsync("not an image"u8.ToArray(), options, cancellationToken),
                    _ => throw new ArgumentException("Unknown test fault")
                };
            }
            if (Results.Count + 1 == failAt)
                result = result with { Success = false, Error = "Test-injected processing failure" };
            Results.Add(result);
            return result;
        }

        public Task<ProcessingResult> ClxProcessAsync(byte[] data, CancellationToken cancellationToken = default) =>
            core.ClxProcessAsync(data, cancellationToken);
        public Task<ProcessingResult> ClxIProcessAsync(byte[] data, CancellationToken cancellationToken = default) =>
            core.ClxIProcessAsync(data, cancellationToken);
        public Task<ProcessingResult> XclProcessAsync(byte[] data, CancellationToken cancellationToken = default) =>
            core.XclProcessAsync(data, cancellationToken);
    }
}
