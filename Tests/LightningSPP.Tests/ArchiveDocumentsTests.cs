using System.IO.Compression;
using System.Text.Json;
using wwwmmm.Documents;
using wwwmmm.Web;
using Xunit;

namespace LightningSPP.Tests;

public sealed class ArchiveNativeFactAttribute : FactAttribute
{
    public ArchiveNativeFactAttribute() { if (Environment.GetEnvironmentVariable("SPP_TEST_NATIVE_DOCUMENTS") != "1") Skip = "Requires real Poppler tools."; }
}

public class ArchiveDocumentsTests
{
    private static byte[] Zip(params (string Name, byte[] Data)[] files)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
            foreach (var file in files) { using var entry = archive.CreateEntry(file.Name).Open(); entry.Write(file.Data); }
        return stream.ToArray();
    }
    [ArchiveNativeFact]
    public async Task MixedArchiveProcessesPdfPreservesOriginalsAndReportsFailure()
    {
        var pdf = new PdfDocumentService().CreateFromImages(new[] { RealTestImage.Png(), RealTestImage.Png() });
        var code = "raise Exception('never execute')"u8.ToArray();
        var input = Zip(("docs/input.pdf", pdf), ("src/main.py", code), ("data/table.csv", "a,b\n1,2"u8.ToArray()), ("docs/bad.pdf", "invalid PDF"u8.ToArray()));
        using var zip = new ZipArchive(new MemoryStream(await ArchiveDocuments.ProcessAsync(input, true)));
        static byte[] Read(ZipArchive z, string name) { using var output = new MemoryStream(); using var read = z.GetEntry(name)!.Open(); read.CopyTo(output); return output.ToArray(); }
        Assert.Equal(pdf, Read(zip, "originals/docs/input.pdf")); Assert.Equal(code, Read(zip, "originals/src/main.py"));
        var processed = Read(zip, "processed/docs/input.pdf");
        Assert.Equal(2, (await new PdfDocumentService().RenderPageAsync(processed, 2)).TotalPages);
        Assert.Null(zip.GetEntry("processed/docs/bad.pdf"));
        using var report = JsonDocument.Parse(Read(zip, "report.json"));
        var rows = report.RootElement.GetProperty("files").EnumerateArray().ToArray();
        Assert.Equal("PROCESSED", rows[0].GetProperty("processing").GetString());
        Assert.Equal(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(processed)), rows[0].GetProperty("processedOutput").GetProperty("sha256").GetString());
        Assert.Equal("FAILED", rows[3].GetProperty("processing").GetString());
        Assert.False(report.RootElement.GetProperty("codeExecuted").GetBoolean());
    }
    [Fact]
    public async Task UnsafeArchiveIsRejectedBeforeDocumentWork() => await Assert.ThrowsAsync<InvalidDataException>(() => ArchiveDocuments.ProcessAsync(Zip(("../input.pdf", "bad"u8.ToArray())), false));

    /// <summary>
    /// Runs with or without native PDF tools. Whatever the environment allows, the archive
    /// must preserve every original byte, emit a real report, and never fabricate a
    /// processed document it did not produce.
    /// </summary>
    [Fact]
    public async Task ArchiveAlwaysPreservesOriginalsAndNeverFabricatesProcessedOutput()
    {
        var pdf = new PdfDocumentService().CreateFromImages([RealTestImage.Png()]);
        var code = "raise Exception('never execute')"u8.ToArray();
        var input = Zip(("docs/input.pdf", pdf), ("src/main.py", code), ("data/table.csv", "a,b\n1,2"u8.ToArray()));

        using var zip = new ZipArchive(new MemoryStream(await ArchiveDocuments.ProcessAsync(input, true)));

        static byte[] Read(ZipArchive archive, string name)
        {
            var entry = archive.GetEntry(name);
            Assert.NotNull(entry);
            using var output = new MemoryStream();
            using var read = entry.Open();
            read.CopyTo(output);
            return output.ToArray();
        }

        Assert.Equal(pdf, Read(zip, "originals/docs/input.pdf"));
        Assert.Equal(code, Read(zip, "originals/src/main.py"));

        using var report = JsonDocument.Parse(Read(zip, "report.json"));
        var root = report.RootElement;
        Assert.False(root.GetProperty("codeExecuted").GetBoolean());
        Assert.False(root.GetProperty("nestedArchivesExpanded").GetBoolean());

        var row = root.GetProperty("files").EnumerateArray()
            .Single(file => file.GetProperty("path").GetString() == "docs/input.pdf");
        var status = row.GetProperty("processing").GetString();

        if (status == "PROCESSED")
        {
            var outputPath = row.GetProperty("processedOutput").GetProperty("path").GetString();
            var processed = Read(zip, outputPath!);
            Assert.Equal(
                row.GetProperty("processedOutput").GetProperty("sha256").GetString(),
                Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(processed)));
        }
        else
        {
            Assert.Equal("FAILED", status);
            Assert.Null(zip.GetEntry("processed/docs/input.pdf"));
            Assert.False(row.TryGetProperty("processedOutput", out _));
        }
    }
}
