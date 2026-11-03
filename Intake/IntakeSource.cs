namespace wwwmmm.Intake;

/// <summary>Where a document comes from. Every kind maps to real code or an explicit refusal.</summary>
public enum IntakeKind
{
    File,
    Folder,
    WatchFolder,
    NetworkShare,
    HttpUpload,
    Scanner
}

/// <summary>
/// A declared intake. <paramref name="Location"/> is a filesystem path for the file-based
/// kinds, a UNC path for <see cref="IntakeKind.NetworkShare"/>, and a provider name for
/// <see cref="IntakeKind.Scanner"/>.
/// </summary>
public sealed record IntakeSource(IntakeKind Kind, string Location, bool Recursive = true)
{
    public static IntakeSource FromPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path must not be empty.", nameof(path));

        var normalised = Path.GetFullPath(path);
        if (File.Exists(normalised))
            return new IntakeSource(IntakeKind.File, normalised, Recursive: false);
        if (Directory.Exists(normalised))
            return new IntakeSource(IntakeKind.Folder, normalised, Recursive: true);

        throw new FileNotFoundException($"Intake path does not exist: {normalised}", normalised);
    }
}

/// <summary>
/// Enumerates accepted documents for a declared intake. Ordering is deterministic
/// (ordinal path sort) so runs are reproducible and a resume can rely on position.
/// </summary>
public static class IntakeEnumerator
{
    private static readonly HashSet<string> AcceptedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff", ".pdf"
    };

    public static bool IsAccepted(string path) =>
        AcceptedExtensions.Contains(Path.GetExtension(path));

    public static IReadOnlyList<string> Enumerate(IntakeSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source.Kind switch
        {
            IntakeKind.File => File.Exists(source.Location) && IsAccepted(source.Location)
                ? new[] { source.Location }
                : Array.Empty<string>(),

            IntakeKind.Folder or IntakeKind.WatchFolder or IntakeKind.NetworkShare => EnumerateFolder(source),

            IntakeKind.HttpUpload => throw new NotSupportedException(
                "HTTP upload intake is served by the running HTTP API; it is not a local enumeration."),

            IntakeKind.Scanner => throw new NotSupportedException(
                "Scanner intake requires a device provider; see ScanProviderRegistry for availability."),

            _ => throw new ArgumentOutOfRangeException(nameof(source), source.Kind, "Unsupported intake kind.")
        };
    }

    private static IReadOnlyList<string> EnumerateFolder(IntakeSource source)
    {
        if (!Directory.Exists(source.Location))
            throw new DirectoryNotFoundException($"Intake folder does not exist: {source.Location}");

        var option = source.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return Directory
            .EnumerateFiles(source.Location, "*", option)
            .Where(IsAccepted)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }
}
