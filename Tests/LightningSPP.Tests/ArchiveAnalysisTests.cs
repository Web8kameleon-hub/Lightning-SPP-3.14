using System.IO.Compression;
using System.Text;
using System.Text.Json;
using wwwmmm.Web;
using Xunit;

namespace LightningSPP.Tests;

public class ArchiveAnalysisTests
{
    private static JsonElement Inspect(string path, byte[] bytes) => JsonSerializer.SerializeToElement(ArchiveAnalysis.Inspect(path, bytes));
    [Fact]
    public void StructuralModulesInspectActualContentWithoutExecutingCode()
    {
        var json = Inspect("data.json", "[{\"a\":1},{\"a\":2}]"u8.ToArray());
        Assert.Equal("INSPECTED", json.GetProperty("status").GetString());
        Assert.Equal(2, json.GetProperty("topLevelItems").GetInt32());
        var csv = Inspect("data.csv", "name,value\n\"a,b\",\"multi\nline\"\nc,0"u8.ToArray());
        Assert.Equal(2, csv.GetProperty("rowCount").GetInt32());
        Assert.Equal(2, csv.GetProperty("columnCount").GetInt32());
        var tsv = Inspect("data.tsv", "name\tvalue\na\t0"u8.ToArray());
        Assert.Equal(1, tsv.GetProperty("rowCount").GetInt32());
        var code = Inspect("main.py", "raise Exception('must never execute')\n"u8.ToArray());
        Assert.False(code.GetProperty("codeExecuted").GetBoolean());
        Assert.Equal(1, code.GetProperty("lines").GetInt32());
    }

    [Theory]
    [InlineData("bad.json", "{broken")]
    [InlineData("bad.csv", "a,a\n1,2")]
    [InlineData("bad.csv", "a,b\n1")]
    [InlineData("bad.csv", "a,b\n\"unterminated")]
    public void InvalidContentIsReportedWithoutInventingSuccessfulAnalysis(string path, string text)
    {
        var result = Inspect(path, Encoding.UTF8.GetBytes(text));
        Assert.Equal("INVALID", result.GetProperty("status").GetString());
        Assert.DoesNotContain(text, result.GetRawText());
    }

    [Fact]
    public void UnsupportedOversizedAndNonUtf8FilesHaveExplicitStatuses()
    {
        Assert.Equal("UNSUPPORTED", Inspect("nested.zip", [1,2]).GetProperty("status").GetString());
        Assert.Equal("SKIPPED_SIZE_LIMIT", Inspect("large.json", new byte[ArchiveAnalysis.MaxInspectionBytes + 1]).GetProperty("status").GetString());
        Assert.Equal("INVALID", Inspect("bad.txt", [255,254,255]).GetProperty("status").GetString());
    }

    [Fact]
    public void OptInAnalysisPreservesInvalidOriginalsAndRecordsEachOutcome()
    {
        using var source = new MemoryStream();
        using (var archive = new ZipArchive(source, ZipArchiveMode.Create, true))
        {
            using (var entry = archive.CreateEntry("bad.json").Open()) entry.Write("{broken"u8);
            using (var entry = archive.CreateEntry("main.py").Open()) entry.Write("raise Exception('never run')"u8);
        }
        using var result = new ZipArchive(new MemoryStream(ArchivePackage.Create(source.ToArray(), true)));
        using var bytes = new MemoryStream();
        result.GetEntry("originals/bad.json")!.Open().CopyTo(bytes);
        Assert.Equal("{broken", Encoding.UTF8.GetString(bytes.ToArray()));
        using var report = JsonDocument.Parse(result.GetEntry("report.json")!.Open());
        Assert.Equal("archive-analysis", report.RootElement.GetProperty("operation").GetString());
        var files = report.RootElement.GetProperty("files").EnumerateArray().ToArray();
        Assert.Equal("INVALID", files[0].GetProperty("analysis").GetProperty("status").GetString());
        Assert.Equal("INSPECTED", files[1].GetProperty("analysis").GetProperty("status").GetString());
    }
}
