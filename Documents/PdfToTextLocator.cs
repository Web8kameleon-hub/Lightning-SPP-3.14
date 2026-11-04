namespace wwwmmm.Documents;

public sealed record PdfToTextLocation(string Path, string Source)
{
    public static PdfToTextLocation? None => null;
}

/// <summary>
/// Finds the Poppler <c>pdftotext</c> binary. Resolution order: <c>SPP_PDFTOTEXT_PATH</c>,
/// then PATH, then the Poppler copy shipped under <c>artifacts/tooling</c>. A configured value
/// that does not exist is an error, never a silent fallback to a different binary.
/// </summary>
public static class PdfToTextLocator
{
    public const string EnvironmentVariable = "SPP_PDFTOTEXT_PATH";
    private const string Executable = "pdftotext";

    public static PdfToTextLocation Locate()
    {
        var configured = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (!File.Exists(configured))
                throw new FileNotFoundException(
                    $"{EnvironmentVariable} points to a missing file: {configured}", configured);
            return new PdfToTextLocation(configured, "environment");
        }

        var onPath = SearchPath();
        if (onPath is not null)
            return new PdfToTextLocation(onPath, "path");

        var local = SearchRepository();
        if (local is not null)
            return new PdfToTextLocation(local, "repository");

        throw new InvalidOperationException(
            $"pdftotext was not found. Install Poppler or set {EnvironmentVariable} to the binary.");
    }

    public static PdfToTextLocation? TryLocate()
    {
        try { return Locate(); }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException) { return null; }
    }

    private static string? SearchPath()
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
            return null;
        return FindInDirectories(path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string? SearchRepository()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        for (var depth = 0; depth < 8 && directory is not null; depth++)
        {
            var tooling = Path.Combine(directory.FullName, "artifacts", "tooling", "poppler");
            if (Directory.Exists(tooling))
            {
                var found = Directory
                    .EnumerateFiles(tooling, FileNamePattern(), SearchOption.AllDirectories)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();
                if (found is not null)
                    return found;
            }
            directory = directory.Parent;
        }
        return null;
    }

    private static string? FindInDirectories(IEnumerable<string> directories)
    {
        foreach (var directory in directories)
        {
            var candidate = Path.Combine(directory.Trim(), FileNamePattern());
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }

    private static string FileNamePattern() =>
        OperatingSystem.IsWindows() ? Executable + ".exe" : Executable;
}
