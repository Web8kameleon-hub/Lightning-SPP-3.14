using System.Text.RegularExpressions;

namespace wwwmmm.Tools;

/// <summary>
/// A single location where SixLabors.ImageSharp is referenced: either a
/// compile-time code reference (using directive / fully qualified type) or a
/// PackageReference entry in a project file.
/// </summary>
public sealed record ImageSharpFinding(
    string RelativePath,
    int LineNumber,
    string Snippet,
    ImageSharpFindingKind Kind);

public enum ImageSharpFindingKind
{
    CodeUsage,
    PackageReference
}

/// <summary>
/// Audit report for a single scan of the repository.
/// </summary>
public sealed record ImageSharpAuditReport(
    string RepositoryRoot,
    DateTime GeneratedAtUtc,
    int FilesScanned,
    IReadOnlyList<ImageSharpFinding> Findings)
{
    public IReadOnlyList<string> FilesWithCodeUsage =>
        Findings
            .Where(f => f.Kind == ImageSharpFindingKind.CodeUsage)
            .Select(f => f.RelativePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public int CodeUsageCount => Findings.Count(f => f.Kind == ImageSharpFindingKind.CodeUsage);

    public int PackageReferenceCount => Findings.Count(f => f.Kind == ImageSharpFindingKind.PackageReference);
}

/// <summary>
/// Scans the repository for real SixLabors.ImageSharp usage: compile-time code
/// references (using directives / fully qualified types) and PackageReference
/// entries in project files. Deliberately narrow — matches "SixLabors." so
/// prose mentions of "ImageSharp" in comments/strings are not counted as
/// dependencies.
///
/// Pure, side-effect free, and directly testable (no console I/O here).
/// </summary>
public static class ImageSharpAuditScanner
{
    private static readonly string[] ExcludedPathSegments =
    [
        $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}.vs{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}TestResults{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}artifacts{Path.DirectorySeparatorChar}",
    ];

    private static readonly Regex CodeUsagePattern = new(@"\bSixLabors\.", RegexOptions.Compiled);
    private static readonly Regex PackageReferencePattern = new(
        @"<PackageReference\s+Include=""SixLabors\.[^""]*""",
        RegexOptions.Compiled);

    public static ImageSharpAuditReport Scan(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        var root = Path.GetFullPath(repositoryRoot);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException($"Repository root not found: {root}");

        var sourceFiles = Directory
            .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsExcluded(f))
            .Where(f => !f.StartsWith(Path.Combine(root, "tmp-imagesharp"), StringComparison.OrdinalIgnoreCase));

        var projectFiles = Directory
            .EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Where(f => !IsExcluded(f));

        var findings = new List<ImageSharpFinding>();
        var filesScanned = 0;

        foreach (var file in sourceFiles)
        {
            filesScanned++;
            findings.AddRange(ScanFile(root, file, CodeUsagePattern, ImageSharpFindingKind.CodeUsage));
        }

        foreach (var file in projectFiles)
        {
            filesScanned++;
            findings.AddRange(ScanFile(root, file, PackageReferencePattern, ImageSharpFindingKind.PackageReference));
        }

        return new ImageSharpAuditReport(
            RepositoryRoot: root,
            GeneratedAtUtc: DateTime.UtcNow,
            FilesScanned: filesScanned,
            Findings: findings);
    }

    private static IEnumerable<ImageSharpFinding> ScanFile(
        string root,
        string absolutePath,
        Regex pattern,
        ImageSharpFindingKind kind)
    {
        var relativePath = Path.GetRelativePath(root, absolutePath).Replace('\\', '/');
        var lines = File.ReadAllLines(absolutePath);

        // PackageReference matches are XML attribute values — always "inside
        // quotes" by nature, so the comment/string-literal filtering below
        // only applies to C# code usage, where it distinguishes a real
        // using-directive/type-reference from a comment or a console/log
        // string that merely mentions "SixLabors.".
        var filterCommentsAndStrings = kind == ImageSharpFindingKind.CodeUsage;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            if (filterCommentsAndStrings && IsCommentLine(line))
                continue;

            var match = pattern.Match(line);
            if (!match.Success)
                continue;

            if (filterCommentsAndStrings && IsInsideStringLiteral(line, match.Index))
                continue;

            yield return new ImageSharpFinding(
                RelativePath: relativePath,
                LineNumber: i + 1,
                Snippet: line.Trim(),
                Kind: kind);
        }
    }

    private static bool IsExcluded(string path) =>
        ExcludedPathSegments.Any(segment =>
            path.Contains(segment, StringComparison.OrdinalIgnoreCase));

    private static bool IsCommentLine(string line) =>
        line.TrimStart().StartsWith("//", StringComparison.Ordinal);

    /// <summary>
    /// Best-effort (not a full C# parser): counts unescaped double-quotes
    /// before <paramref name="matchIndex"/>. An odd count means the match sits
    /// inside a string literal (e.g. a Console.WriteLine or test fixture
    /// string), which is not a real package dependency.
    /// </summary>
    private static bool IsInsideStringLiteral(string line, int matchIndex)
    {
        var quoteCount = 0;
        for (var i = 0; i < matchIndex && i < line.Length; i++)
        {
            if (line[i] == '"' && (i == 0 || line[i - 1] != '\\'))
                quoteCount++;
        }
        return quoteCount % 2 == 1;
    }
}
