using System.IO.Compression;
using System.Text.Json;
using wwwmmm.Documents;
using wwwmmm.Web;
using Xunit;

namespace LightningSPP.Tests;

public class DocumentPackageTests
{
    [Fact]
    public void PackagePreservesCompletePdfAndMapsEveryPageOnce()
    {
        var image = RealTestImage.Png();
        var pdf = new PdfDocumentService().CreateFromImages(new[] { image, image });
        var result = new TryDocumentResult(image, pdf, null, 2, 1, 2, 3, "test-fixture");
        using var zip = new ZipArchive(new MemoryStream(DocumentPackage.Create(pdf, result, true, "lightning")));
        Assert.Equal(3, zip.Entries.Count);
        using var contents = new MemoryStream();
        zip.GetEntry("processed.pdf")!.Open().CopyTo(contents);
        Assert.Equal(pdf, contents.ToArray());
        using var report = JsonDocument.Parse(zip.GetEntry("report.json")!.Open());
        var pages = report.RootElement.GetProperty("pageMapping").EnumerateArray().ToArray();
        Assert.Equal(new[] { 1, 2 }, pages.Select(p => p.GetProperty("inputPage").GetInt32()));
        Assert.Equal(new[] { 1, 2 }, pages.Select(p => p.GetProperty("outputPage").GetInt32()));
        Assert.All(pages, p => Assert.Equal("processed.pdf", p.GetProperty("outputFile").GetString()));
        Assert.False(report.RootElement.GetProperty("classification").GetProperty("supplierSplitting").GetBoolean());
        var text = report.RootElement.GetRawText();
        Assert.DoesNotContain("CustomerEmail", text);
        Assert.DoesNotContain("licenseKey", text);
        Assert.Throws<ArgumentException>(() => DocumentPackage.Create([], result, true, "lightning"));
        Assert.Throws<ArgumentException>(() => DocumentPackage.Create(pdf, result with { TotalPages = 33 }, true, "lightning"));
    }
}
