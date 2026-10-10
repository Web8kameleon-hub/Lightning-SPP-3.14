namespace wwwmmm.Tools;

/// <summary>
/// CLI entry point for "audit-imagesharp": scans the repository for real
/// SixLabors.ImageSharp usage and prints a summary. Thin console wrapper
/// around the pure <see cref="ImageSharpAuditScanner"/>.
/// </summary>
public static class ImageSharpAuditCommand
{
    public static Task<int> RunAsync(string[] args)
    {
        var root = args.Length > 1 ? args[1] : Directory.GetCurrentDirectory();
        var outputPath = args.Length > 2 ? args[2] : null;

        ImageSharpAuditReport report;
        try
        {
            report = ImageSharpAuditScanner.Scan(root);
        }
        catch (DirectoryNotFoundException ex)
        {
            Console.WriteLine($"✗ {ex.Message}");
            return Task.FromResult(1);
        }

        Console.WriteLine("📋 SixLabors.ImageSharp audit");
        Console.WriteLine($"  Repository:          {report.RepositoryRoot}");
        Console.WriteLine($"  Generated (UTC):     {report.GeneratedAtUtc:O}");
        Console.WriteLine($"  Files scanned:       {report.FilesScanned}");
        Console.WriteLine($"  Code usages:         {report.CodeUsageCount}");
        Console.WriteLine($"  PackageReference(s): {report.PackageReferenceCount}");
        Console.WriteLine($"  Files with usage:    {report.FilesWithCodeUsage.Count}");
        Console.WriteLine();

        if (report.FilesWithCodeUsage.Count > 0)
        {
            Console.WriteLine("  By file:");
            foreach (var path in report.FilesWithCodeUsage)
            {
                var count = report.Findings.Count(f =>
                    f.Kind == ImageSharpFindingKind.CodeUsage &&
                    string.Equals(f.RelativePath, path, StringComparison.OrdinalIgnoreCase));
                Console.WriteLine($"    {count,3}  {path}");
            }
            Console.WriteLine();
        }

        Console.WriteLine("  Note: Services/Implementation/FastScanService.cs is the documented");
        Console.WriteLine("  'R' (ImageSharp) half of the hybrid engine in ImageEngineAdapter —");
        Console.WriteLine("  by design, not a migration candidate. Other files above are the");
        Console.WriteLine("  ones worth reviewing individually.");

        if (outputPath != null)
        {
            WriteMarkdownReport(report, outputPath);
            Console.WriteLine();
            Console.WriteLine($"  Report written: {outputPath}");
        }

        return Task.FromResult(0);
    }

    private static void WriteMarkdownReport(ImageSharpAuditReport report, string outputPath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        using var writer = new StreamWriter(outputPath, append: false);
        writer.WriteLine("# SixLabors.ImageSharp Audit Report");
        writer.WriteLine();
        writer.WriteLine($"- Repository: `{report.RepositoryRoot}`");
        writer.WriteLine($"- Generated (UTC): {report.GeneratedAtUtc:O}");
        writer.WriteLine($"- Files scanned: {report.FilesScanned}");
        writer.WriteLine($"- Code usages: {report.CodeUsageCount}");
        writer.WriteLine($"- PackageReference(s): {report.PackageReferenceCount}");
        writer.WriteLine();
        writer.WriteLine("## Findings");
        writer.WriteLine();
        writer.WriteLine("| File | Line | Kind | Snippet |");
        writer.WriteLine("|---|---|---|---|");
        foreach (var finding in report.Findings
                     .OrderBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(f => f.LineNumber))
        {
            var snippet = finding.Snippet.Replace("|", "\\|");
            writer.WriteLine($"| `{finding.RelativePath}` | {finding.LineNumber} | {finding.Kind} | `{snippet}` |");
        }
    }
}
