using System.IO.Compression;
using wwwmmm.PublicAPI;
using wwwmmm.Services;
using Xunit;

namespace LightningSPP.Tests;

public class OutputAdapterTests
{
    [Fact]
    public void EncodeOutput_Pdf_HasPdfHeader()
    {
        var png = ImageFixture.Png();

        var bytes = LightningAPI.EncodeOutput(png, OutputFormat.Pdf);

        Assert.True(bytes.Length > 8);
        Assert.Equal(0x25, bytes[0]);
        Assert.Equal(0x50, bytes[1]);
        Assert.Equal(0x44, bytes[2]);
        Assert.Equal(0x46, bytes[3]);
    }

    [Fact]
    public void EncodeOutput_Xlsx_IsOpenXmlWorkbook()
    {
        var png = ImageFixture.Png();

        var bytes = LightningAPI.EncodeOutput(png, OutputFormat.Xlsx);

        AssertZipHasEntry(bytes, "xl/workbook.xml");
        AssertZipHasEntry(bytes, "xl/worksheets/sheet1.xml");
    }

    [Fact]
    public void EncodeOutput_Docx_IsOpenXmlDocument()
    {
        var png = ImageFixture.Png();

        var bytes = LightningAPI.EncodeOutput(png, OutputFormat.Docx);

        AssertZipHasEntry(bytes, "word/document.xml");
        AssertZipHasEntry(bytes, "word/media/image1.png");
    }

    private static void AssertZipHasEntry(byte[] bytes, string entryPath)
    {
        using var stream = new MemoryStream(bytes);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        Assert.NotNull(zip.GetEntry(entryPath));
    }
}
