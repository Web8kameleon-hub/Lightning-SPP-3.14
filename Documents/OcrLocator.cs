namespace wwwmmm.Documents;

public sealed record OcrLocation(string Path, string Source);

/// <summary>
/// Finds the Tesseract executable. Resolution order: <c>SPP_TESSERACT_PATH</c>, then PATH,
/// then the standard Windows install directory, then <c>artifacts/tooling/tesseract</c>.
/// A configured value that does not exist is an error, never a silent fallback to a
/// different binary — the same contract Poppler follows in <see cref="PdfToTextLocator"/>.
/// </summary>
public static class OcrLocator
{
    public const string EnvironmentVariable = "SPP_TESSERACT_PATH";
    private const string Executable = "tesseract";

    public static OcrLocation Locate()
    {
        var configured = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (!File.Exists(configured))
                throw new FileNotFoundException(
                    $"{EnvironmentVariable} points to a missing file: {configured}", configured);
            return new OcrLocation(configured, "environment");
        }

        var onPath = SearchPath();
        if (onPath is not null)
            return new OcrLocation(onPath, "path");

        var installed = SearchStandardInstall();
        if (installed is not null)
            return new OcrLocation(installed, "install");

        var local = SearchRepository();
        if (local is not null)
            return new OcrLocation(local, "repository");

        throw new InvalidOperationException(
            $"Tesseract was not found. Install it or set {EnvironmentVariable} to the executable.");
    }

    public static OcrLocation? TryLocate()
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

    private static string? SearchStandardInstall()
    {
        if (!OperatingSystem.IsWindows())
            return null;

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (string.IsNullOrWhiteSpace(programFiles))
            return null;

        var candidate = Path.Combine(programFiles, "Tesseract-OCR", FileNamePattern());
        return File.Exists(candidate) ? candidate : null;
    }

    private static string? SearchRepository()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        for (var depth = 0; depth < 8 && directory is not null; depth++)
        {
            var tooling = Path.Combine(directory.FullName, "artifacts", "tooling", "tesseract");
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
