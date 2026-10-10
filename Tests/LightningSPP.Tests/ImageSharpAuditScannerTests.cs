using wwwmmm.Tools;
using Xunit;

namespace LightningSPP.Tests;

/// <summary>
/// Verifies the ImageSharp audit scanner against real temp-directory fixtures —
/// no mocked file system, actual files on disk.
/// </summary>
public class ImageSharpAuditScannerTests : IDisposable
{
    private readonly string _root;

    public ImageSharpAuditScannerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "imagesharp-audit-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Scan_MissingRoot_ThrowsDirectoryNotFound()
    {
        var missing = Path.Combine(_root, "does-not-exist");

        Assert.Throws<DirectoryNotFoundException>(() => ImageSharpAuditScanner.Scan(missing));
    }

    [Fact]
    public void Scan_DetectsCodeUsage_WithLineNumberAndSnippet()
    {
        File.WriteAllLines(Path.Combine(_root, "Sample.cs"),
        [
            "using SixLabors.ImageSharp;",
            "",
            "public class Sample",
            "{",
            "    // mentions ImageSharp in prose only, not a using/type reference",
            "    public void Load() => SixLabors.ImageSharp.Image.Load(\"x.png\");",
            "}"
        ]);

        var report = ImageSharpAuditScanner.Scan(_root);

        Assert.Equal(2, report.CodeUsageCount);
        Assert.Single(report.FilesWithCodeUsage);
        Assert.Contains("Sample.cs", report.FilesWithCodeUsage[0]);

        var firstFinding = report.Findings
            .Where(f => f.Kind == ImageSharpFindingKind.CodeUsage)
            .OrderBy(f => f.LineNumber)
            .First();
        Assert.Equal(1, firstFinding.LineNumber);
        Assert.Equal("using SixLabors.ImageSharp;", firstFinding.Snippet);
    }

    [Fact]
    public void Scan_DoesNotCountProseMentionsOfImageSharp_WithoutSixLaborsPrefix()
    {
        File.WriteAllLines(Path.Combine(_root, "Prose.cs"),
        [
            "public class Prose",
            "{",
            "    // This will use RealImageProcessor with actual ImageSharp processing!",
            "}"
        ]);

        var report = ImageSharpAuditScanner.Scan(_root);

        Assert.Equal(0, report.CodeUsageCount);
    }

    [Fact]
    public void Scan_DetectsPackageReference_InCsproj()
    {
        File.WriteAllLines(Path.Combine(_root, "Sample.csproj"),
        [
            "<Project Sdk=\"Microsoft.NET.Sdk\">",
            "  <ItemGroup>",
            "    <PackageReference Include=\"SixLabors.ImageSharp\" Version=\"4.1.3\" />",
            "  </ItemGroup>",
            "</Project>"
        ]);

        var report = ImageSharpAuditScanner.Scan(_root);

        Assert.Equal(1, report.PackageReferenceCount);
    }

    [Fact]
    public void Scan_ExcludesBinObjAndTmpImageSharpDirectories()
    {
        var binDir = Path.Combine(_root, "bin", "Release");
        var objDir = Path.Combine(_root, "obj");
        var tmpDir = Path.Combine(_root, "tmp-imagesharp-4.1.3");
        Directory.CreateDirectory(binDir);
        Directory.CreateDirectory(objDir);
        Directory.CreateDirectory(tmpDir);

        File.WriteAllText(Path.Combine(binDir, "Generated.cs"), "using SixLabors.ImageSharp;");
        File.WriteAllText(Path.Combine(objDir, "Generated.cs"), "using SixLabors.ImageSharp;");
        File.WriteAllText(Path.Combine(tmpDir, "Extracted.cs"), "using SixLabors.ImageSharp;");

        var report = ImageSharpAuditScanner.Scan(_root);

        Assert.Equal(0, report.CodeUsageCount);
    }

    [Fact]
    public void Scan_NoImageSharpAnywhere_ReturnsCleanReport()
    {
        File.WriteAllText(Path.Combine(_root, "Clean.cs"), "public class Clean { }");

        var report = ImageSharpAuditScanner.Scan(_root);

        Assert.Equal(0, report.CodeUsageCount);
        Assert.Equal(0, report.PackageReferenceCount);
        Assert.Empty(report.FilesWithCodeUsage);
        Assert.Equal(1, report.FilesScanned);
    }
}
