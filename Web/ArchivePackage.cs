using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace wwwmmm.Web;

// Inventory-only import. Entries are never extracted to disk or executed.
internal static class ArchivePackage
{
    internal const int MaxUploadBytes = 8 * 1024 * 1024;
    internal const int MaxExpandedBytes = 32 * 1024 * 1024;
    internal const int MaxEntries = 256;

    internal static byte[] Create(byte[] input)
    {
        if (input.Length == 0 || input.Length > MaxUploadBytes)
            throw new InvalidDataException("ZIP upload must be between 1 byte and 8 MiB.");
        using var source = new ZipArchive(new MemoryStream(input), ZipArchiveMode.Read);
        if (source.Entries.Count == 0 || source.Entries.Count > MaxEntries)
            throw new InvalidDataException("ZIP must contain 1 to 256 entries.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new List<object>();
        long expanded = 0;
        using var output = new MemoryStream();
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in source.Entries)
            {
                var path = entry.FullName;
                var directory = path.EndsWith('/');
                var parts = path.TrimEnd('/').Split('/');
                if (path.Length > 512 || path.Contains('\\') || path.Contains(':') ||
                    path.Any(char.IsControl) || parts.Any(p => p.Length == 0 || p is "." or ".." || p.EndsWith('.') || p.EndsWith(' ')) ||
                    !names.Add(path.TrimEnd('/')))
                    throw new InvalidDataException("ZIP contains an unsafe or duplicate path.");
                var mode = (entry.ExternalAttributes >> 16) & 0xF000;
                if (mode != 0 && mode != 0x8000 && mode != 0x4000)
                    throw new InvalidDataException("ZIP links and special files are not supported.");
                if (entry.Length > MaxExpandedBytes || entry.Length > Math.Max(1, entry.CompressedLength) * 200L)
                    throw new InvalidDataException("ZIP expansion limit exceeded.");
                if (directory)
                {
                    if (entry.Length != 0) throw new InvalidDataException("Invalid directory entry.");
                    target.CreateEntry("originals/" + path);
                    continue;
                }
                using var read = entry.Open();
                using var bytes = new MemoryStream();
                var buffer = new byte[8192];
                int count;
                while ((count = read.Read(buffer)) > 0)
                {
                    expanded += count;
                    if (expanded > MaxExpandedBytes || bytes.Length + count > entry.Length)
                        throw new InvalidDataException("ZIP expansion limit exceeded.");
                    bytes.Write(buffer, 0, count);
                }
                if (bytes.Length != entry.Length) throw new InvalidDataException("ZIP entry length mismatch.");
                var data = bytes.ToArray();
                var destination = "originals/" + path;
                using (var write = target.CreateEntry(destination, CompressionLevel.Optimal).Open()) write.Write(data);
                files.Add(new { path, outputPath = destination, bytes = data.Length,
                    sha256 = Convert.ToHexStringLower(SHA256.HashData(data)),
                    category = Category(path), identification = "extension-only", status = "PRESERVED",
                    processing = "NOT_PERFORMED" });
            }
            var report = new { schemaVersion = 1, operation = "archive-inventory", createdUtc = DateTimeOffset.UtcNow,
                inputSha256 = Convert.ToHexStringLower(SHA256.HashData(input)), expandedBytes = expanded,
                files, codeExecuted = false, nestedArchivesExpanded = false,
                scope = "Original bytes preserved. Categories use filename extensions only; no semantic analysis, document conversion or malware scan is claimed." };
            using var reportStream = target.CreateEntry("report.json").Open();
            JsonSerializer.Serialize(reportStream, report, new JsonSerializerOptions { WriteIndented = true });
        }
        return output.ToArray();
    }

    private static string Category(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".cs" or ".py" or ".js" or ".ts" or ".tsx" or ".jsx" or ".java" or ".go" or ".rs" or ".cpp" or ".c" => "code",
        ".json" or ".csv" or ".tsv" or ".xml" or ".yaml" or ".yml" => "data",
        ".pdf" or ".docx" or ".txt" or ".md" or ".xlsx" => "document",
        ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".tiff" => "image",
        ".zip" or ".gz" or ".7z" or ".tar" => "archive",
        _ => "unknown"
    };
}
