using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using wwwmmm.Documents;
using wwwmmm.Services;
using wwwmmm.Web;
using Xunit;

namespace LightningSPP.Tests;

public class ArchiveDocumentPackageTests
{
    private static byte[] Zip(params (string Path, byte[] Data)[] files)
    {
        using var bytes = new MemoryStream();
        using (var zip = new ZipArchive(bytes, ZipArchiveMode.Create, true))
            foreach (var file in files)
            {
                using var entry = zip.CreateEntry(file.Path).Open();
                entry.Write(file.Data);
            }
        return bytes.ToArray();
    }

    [Fact]
    public async Task InvalidPdfsArePreservedAndAttemptsAreBounded()
    {
        var files = Enumerable.Range(0, 5).Select(i => ($"{i}.pdf", "invalid pdf"u8.ToArray())).ToArray();
        using var zip = new ZipArchive(new MemoryStream(await ArchiveDocumentPackage.CreateAsync(Zip(files), false, ScanMode.Lightning)));
        using var report = JsonDocument.Parse(zip.GetEntry("report.json")!.Open());
        var statuses = report.RootElement.GetProperty("files").EnumerateArray()
            .Select(f => f.GetProperty("documentProcessing").GetProperty("status").GetString()).ToArray();
        Assert.Equal(new[] { "FAILED", "FAILED", "FAILED", "FAILED", "SKIPPED_DOCUMENT_LIMIT" }, statuses);
        Assert.Equal(6, zip.Entries.Count);
        Assert.DoesNotContain("Exception", report.RootElement.GetRawText());
        foreach (var file in files) Assert.Equal(file.Item2, Read(zip, "originals/" + file.Item1));
    }

    [Fact]
    public async Task UnsafeArchiveIsRejectedBeforeProcessing() =>
        await Assert.ThrowsAsync<InvalidDataException>(() => ArchiveDocumentPackage.CreateAsync(
            Zip(("../escape.pdf", "invalid pdf"u8.ToArray())), false, ScanMode.Lightning));

    [Fact]
    public async Task CancellationDoesNotProduceAPackage()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ArchiveDocumentPackage.CreateAsync(
            Zip(("doc.pdf", "invalid pdf"u8.ToArray())), false, ScanMode.Lightning, cancelled.Token));
    }

    [NativeDocumentFact]
    public async Task MixedArchiveProcessesRealPdfPreservesOtherFilesAndVerifiesOutput()
    {
        var pdf = new PdfDocumentService().CreateFromImages([RealTestImage.Png(), RealTestImage.Png()]);
        var code = "raise Exception('must never execute')"u8.ToArray();
        var csv = "name,value\nitem,42\n"u8.ToArray();
        using var zip = new ZipArchive(new MemoryStream(await ArchiveDocumentPackage.CreateAsync(
            Zip(("docs/input.pdf", pdf), ("src/main.py", code), ("data.csv", csv), ("bad.pdf", "invalid"u8.ToArray())),
            true, ScanMode.Lightning)));
        Assert.Equal(pdf, Read(zip, "originals/docs/input.pdf"));
        Assert.Equal(code, Read(zip, "originals/src/main.py"));
        Assert.Equal(csv, Read(zip, "originals/data.csv"));
        using var report = JsonDocument.Parse(zip.GetEntry("report.json")!.Open());
        Assert.False(report.RootElement.GetProperty("codeExecuted").GetBoolean());
        var files = report.RootElement.GetProperty("files");
        var processed = files[0].GetProperty("documentProcessing");
        Assert.Equal("PROCESSED", processed.GetProperty("status").GetString());
        Assert.Equal(2, processed.GetProperty("totalPages").GetInt32());
        var output = Read(zip, processed.GetProperty("outputPath").GetString()!);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(output)), processed.GetProperty("outputSha256").GetString());
        var rendered = await new PdfDocumentService().RenderPageAsync(output, 2);
        Assert.Equal(2, rendered.TotalPages);
        Assert.Equal("NOT_REQUESTED_FOR_TYPE", files[1].GetProperty("documentProcessing").GetProperty("status").GetString());
        Assert.Equal("FAILED", files[3].GetProperty("documentProcessing").GetProperty("status").GetString());
    }

    private static byte[] Read(ZipArchive zip, string path)
    {
        using var output = new MemoryStream();
        using var input = zip.GetEntry(path)!.Open();
        input.CopyTo(output);
        return output.ToArray();
    }
}
