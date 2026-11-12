using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace wwwmmm.Packaging;

public sealed record PackageMember(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("sizeBytes")] long SizeBytes,
    [property: JsonPropertyName("sha256")] string Sha256);

public sealed record PackageManifest(
    [property: JsonPropertyName("formatVersion")] int FormatVersion,
    [property: JsonPropertyName("createdUtc")] string CreatedUtc,
    [property: JsonPropertyName("members")] IReadOnlyList<PackageMember> Members);

public sealed record PackageVerification(bool IsValid, int Members, IReadOnlyList<string> Problems);

/// <summary>
/// Builds a reproducible archive: members are sorted, every timestamp is fixed and the
/// manifest records each member's SHA-256. Two runs over the same bytes produce the same
/// archive hash, so a package can be re-derived and compared.
/// </summary>
public static class DocumentPackager
{
    public const string ManifestName = "package-manifest.json";
    private static readonly DateTimeOffset FixedTimestamp = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static async Task<PackageManifest> CreateAsync(
        string outputPath,
        IReadOnlyList<string> memberPaths,
        DateTimeOffset? createdUtc = null,
        CancellationToken cancellationToken = default)
    {
        if (memberPaths.Count == 0)
            throw new ArgumentException("A package needs at least one member.", nameof(memberPaths));

        var members = new List<PackageMember>(memberPaths.Count);
        var payloads = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        foreach (var path in memberPaths.OrderBy(p => Path.GetFileName(p), StringComparer.Ordinal))
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Package member not found: {path}", path);

            var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            var name = Path.GetFileName(path);
            if (name == ManifestName)
                throw new InvalidOperationException($"'{ManifestName}' is reserved for the package manifest.");

            if (!payloads.TryAdd(name, bytes))
                throw new ArgumentException($"Duplicate package member name: {name}", nameof(memberPaths));
            members.Add(new PackageMember(name, bytes.LongLength, Sha256(bytes)));
        }

        var manifest = new PackageManifest(
            FormatVersion: 1,
            CreatedUtc: (createdUtc ?? FixedTimestamp).UtcDateTime.ToString("O"),
            Members: members);

        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var temporary = Path.GetFullPath(outputPath) + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
        await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
        {
            foreach (var member in members)
                await WriteEntryAsync(archive, member.Name, payloads[member.Name], cancellationToken);

            var manifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest, Json) + "\n");
            await WriteEntryAsync(archive, ManifestName, manifestBytes, cancellationToken);
        }

        File.Move(temporary, outputPath, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
        return manifest;
    }

    public static async Task<PackageVerification> VerifyAsync(string archivePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(archivePath))
            throw new FileNotFoundException($"Archive not found: {archivePath}", archivePath);

        using var archive = ZipFile.OpenRead(archivePath);
        var problems = new List<string>();

        if (archive.Entries.Select(entry => entry.FullName).Distinct(StringComparer.Ordinal).Count() != archive.Entries.Count)
            return new PackageVerification(false, archive.Entries.Count, new[] { "duplicate archive member name" });

        var manifestEntry = archive.GetEntry(ManifestName);
        if (manifestEntry is null)
            return new PackageVerification(false, archive.Entries.Count, new[] { $"missing {ManifestName}" });

        if (manifestEntry.Length > 16 * 1024 * 1024)
            return new PackageVerification(false, archive.Entries.Count, new[] { "manifest exceeds 16 MiB" });

        PackageManifest? manifest;
        using (var reader = new StreamReader(manifestEntry.Open(), Encoding.UTF8))
        {
            var json = await reader.ReadToEndAsync(cancellationToken);
            manifest = JsonSerializer.Deserialize<PackageManifest>(json, Json);
        }

        if (manifest?.Members is null)
            return new PackageVerification(false, archive.Entries.Count, new[] { "manifest is unreadable" });

        if (manifest.FormatVersion != 1 || manifest.Members.Any(m => m is null || string.IsNullOrWhiteSpace(m.Name) || m.Name == ManifestName || m.SizeBytes < 0 || m.Sha256 is null || m.Sha256.Length != 64) ||
            manifest.Members.Select(m => m.Name).Distinct(StringComparer.Ordinal).Count() != manifest.Members.Count)
            return new PackageVerification(false, archive.Entries.Count, new[] { "invalid manifest version or members" });

        var declared = manifest.Members.ToDictionary(m => m.Name, StringComparer.Ordinal);
        foreach (var entry in archive.Entries)
        {
            if (entry.FullName == ManifestName)
                continue;

            if (!declared.TryGetValue(entry.FullName, out var member))
            {
                problems.Add($"undeclared member: {entry.FullName}");
                continue;
            }

            using var stream = entry.Open();
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();

            if (entry.Length != member.SizeBytes)
                problems.Add($"size mismatch: {entry.FullName}");
            else if (!string.Equals(actual, member.Sha256, StringComparison.Ordinal))
                problems.Add($"hash mismatch: {entry.FullName}");
        }

        foreach (var missing in declared.Keys.Where(name => archive.GetEntry(name) is null))
            problems.Add($"missing member: {missing}");

        return new PackageVerification(problems.Count == 0, declared.Count, problems);
    }

    private static async Task WriteEntryAsync(ZipArchive archive, string name, byte[] content, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        entry.LastWriteTime = FixedTimestamp;
        await using var stream = entry.Open();
        await stream.WriteAsync(content, cancellationToken);
    }

    private static string Sha256(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
}
