using System.IO.Compression;
using System.Text;
using System.Text.Json;
using wwwmmm.Web;
using Xunit;

namespace LightningSPP.Tests;

public class ArchivePackageTests
{
    private static byte[] Zip(params (string Path, byte[] Data)[] entries)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
            foreach (var entry in entries)
            {
                using var stream = zip.CreateEntry(entry.Path).Open();
                stream.Write(entry.Data);
            }
        return output.ToArray();
    }

    [Fact]
    public void MixedArchivePreservesBytesAndReportsScope()
    {
        var code = Encoding.UTF8.GetBytes("raise Exception('must never run')");
        var data = Encoding.UTF8.GetBytes("{\"value\":42}");
        using var zip = new ZipArchive(new MemoryStream(ArchivePackage.Create(Zip(("src/main.py", code), ("data/input.json", data), ("unknown.bin", new byte[] { 0, 1, 2 })))));
        using var contents = new MemoryStream();
        zip.GetEntry("originals/src/main.py")!.Open().CopyTo(contents);
        Assert.Equal(code, contents.ToArray());
        using var report = JsonDocument.Parse(zip.GetEntry("report.json")!.Open());
        Assert.False(report.RootElement.GetProperty("codeExecuted").GetBoolean());
        var files = report.RootElement.GetProperty("files").EnumerateArray().ToArray();
        Assert.Equal(new[] { "code", "data", "unknown" }, files.Select(f => f.GetProperty("category").GetString()));
        Assert.All(files, f => Assert.Equal("NOT_PERFORMED", f.GetProperty("processing").GetString()));
    }

    [Theory]
    [InlineData("../escape.py")]
    [InlineData("/absolute")]
    [InlineData("C:/absolute")]
    [InlineData("a\\evil")]
    [InlineData("a/./file")]
    [InlineData("a//file")]
    public void UnsafePathsAreRejected(string path) => Assert.Throws<InvalidDataException>(() => ArchivePackage.Create(Zip((path, new byte[] { 1 }))));

    [Fact]
    public void DuplicatePathsAndExpansionBombsAreRejected()
    {
        Assert.Throws<InvalidDataException>(() => ArchivePackage.Create(Zip(("a.py", new byte[] { 1 }), ("A.py", new byte[] { 2 }))));
        Assert.Throws<InvalidDataException>(() => ArchivePackage.Create(Zip(("bomb.txt", new byte[1024 * 1024]))));
        Assert.Throws<InvalidDataException>(() => ArchivePackage.Create(Zip()));
        Assert.Throws<InvalidDataException>(() => ArchivePackage.Create(new byte[] { 1, 2, 3 }));
    }
}
