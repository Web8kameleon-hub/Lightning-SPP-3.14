using System.Text;
using System.Text.Json;

namespace wwwmmm.Workflow;

/// <summary>Explicit private root. No traversal, links, credentials or arbitrary output paths.</summary>
public sealed class WorkspaceScope
{
    public string Root { get; }
    public int MaxFileBytes { get; }
    private static readonly HashSet<string> Forbidden = new(StringComparer.OrdinalIgnoreCase)
    { ".git", ".ssh", ".aws", ".azure", ".codex", ".lightning", "node_modules", "bin", "obj", "credentials", "secrets", "cookies.txt", "register-body.json", "id_rsa", "id_ed25519" };
    public WorkspaceScope(string root, int maxFileBytes = 8 * 1024 * 1024)
    {
        if (maxFileBytes < 1024 || maxFileBytes > 128 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(maxFileBytes));
        Root = Path.GetFullPath(root); MaxFileBytes = maxFileBytes;
        Directory.CreateDirectory(Root); CheckAncestors(Root); PrivateDirectory(Root);
        Directory.CreateDirectory(StateDirectory); CheckAncestors(StateDirectory); PrivateDirectory(StateDirectory);
    }
    internal string StateDirectory => Path.Combine(Root, ".lightning");
    internal static void PrivateDirectory(string path)
    { if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
    internal static void CheckAncestors(string path)
    {
        for (var item = new DirectoryInfo(path); item is not null; item = item.Parent)
            if ((item.Attributes & FileAttributes.ReparsePoint) != 0) throw new UnauthorizedAccessException("Linked workspace paths are not permitted.");
    }
    public string Resolve(string relative, bool mustExist = true)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(':') || relative.Contains('\0')) throw new UnauthorizedAccessException("Relative workspace path required.");
        var parts = relative.Replace('\\', '/').Split('/');
        if (parts.Any(p => p is ".." or "." or "" || p != p.TrimEnd(' ', '.') || Forbidden.Contains(p) || p.StartsWith(".env", StringComparison.OrdinalIgnoreCase) ||
            p.EndsWith(".lic", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".pem", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".key", StringComparison.OrdinalIgnoreCase)))
            throw new UnauthorizedAccessException("Protected workspace path.");
        var path = Path.GetFullPath(Path.Combine(Root, Path.Combine(parts)));
        if (!path.StartsWith(Root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) throw new UnauthorizedAccessException("Path escapes workspace.");
        var current = Root;
        foreach (var part in parts)
        {
            current = Path.Combine(current, part);
            if (new FileInfo(current).LinkTarget is not null || ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)) throw new UnauthorizedAccessException("Linked files are not permitted.");
        }
        if (mustExist && !File.Exists(path) && !Directory.Exists(path)) throw new FileNotFoundException("Workspace path not found.");
        return path;
    }
    public async Task<(byte[] Bytes, SourceReference Source)> ReadAsync(string relative, CancellationToken token)
    {
        var logical = Resolve(relative, mustExist: false);
        if (!File.Exists(logical) && CanonicalReference(relative + ".reference.json"))
        {
            var referencePath = Resolve(relative + ".reference.json");
            if (new FileInfo(referencePath).Length > 8192) throw new IOException("Document reference exceeds its budget.");
            using var reference = JsonDocument.Parse(await File.ReadAllBytesAsync(referencePath, token));
            var metadata = reference.RootElement;
            var parts = relative.Replace('\\', '/').Split('/'); var canonicalHash = parts[1];
            if (metadata.GetProperty("schemaVersion").GetInt32() != 1 || metadata.GetProperty("sha256").GetString() != canonicalHash ||
                metadata.GetProperty("filename").GetString() != parts[2]) throw new IOException("Invalid canonical document reference.");
            var canonicalSnapshot = Path.Combine(StateDirectory, "originals", canonicalHash);
            CheckAncestors(Path.GetDirectoryName(canonicalSnapshot)!);
            if (new FileInfo(canonicalSnapshot).LinkTarget is not null) throw new UnauthorizedAccessException("Linked canonical objects are forbidden.");
            var info = new FileInfo(canonicalSnapshot);
            if (info.Length > MaxFileBytes || info.Length != metadata.GetProperty("bytes").GetInt32()) throw new IOException("Canonical object size mismatch.");
            var canonicalBytes = await File.ReadAllBytesAsync(canonicalSnapshot, token);
            if (WorkflowJson.Hash(canonicalBytes) != canonicalHash) throw new IOException("Canonical object integrity failure.");
            return (canonicalBytes, new(relative.Replace('\\', '/'), canonicalHash));
        }
        var path = Resolve(relative);
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, FileOptions.Asynchronous);
        if (stream.Length > MaxFileBytes) throw new ArgumentException("File exceeds configured per-file resource budget.");
        using var data = new MemoryStream(); var buffer = new byte[8192]; int read;
        while ((read = await stream.ReadAsync(buffer, token)) != 0)
        { if (data.Length + read > MaxFileBytes) throw new ArgumentException("File exceeds configured resource budget."); await data.WriteAsync(buffer.AsMemory(0, read), token); }
        var bytes = data.ToArray(); var hash = WorkflowJson.Hash(bytes);
        var originals = Path.Combine(StateDirectory, "originals"); Directory.CreateDirectory(originals); PrivateDirectory(originals);
        var snapshot = Path.Combine(originals, hash);
        if (!File.Exists(snapshot)) await AtomicWriteAsync(snapshot, bytes, token);
        else if (WorkflowJson.Hash(await File.ReadAllBytesAsync(snapshot, token)) != hash) throw new IOException("Original snapshot integrity failure.");
        return (bytes, new(relative.Replace('\\', '/'), hash));
    }
    internal static bool CanonicalReference(string path)
    {
        var parts = path.Replace('\\', '/').Split('/');
        return parts.Length == 3 && parts[0] == "documents" && parts[1].Length == 64 &&
            parts[1].All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f') && parts[2].EndsWith(".reference.json", StringComparison.Ordinal);
    }
    /// <summary>Store one private content-addressed object; visible filenames are metadata references.</summary>
    public async Task<string> ImportCanonicalAsync(string filename, byte[] bytes, CancellationToken token)
    {
        if (filename != Path.GetFileName(filename) || bytes.Length == 0 || bytes.Length > MaxFileBytes)
            throw new ArgumentException("Invalid canonical import name or size.");
        var hash = WorkflowJson.Hash(bytes); var relative = "documents/" + hash + "/" + filename;
        _ = Resolve(relative, false);
        var originals = Path.Combine(StateDirectory, "originals");
        Directory.CreateDirectory(originals); CheckAncestors(originals); PrivateDirectory(originals);
        var snapshot = Path.Combine(originals, hash);
        if (new FileInfo(snapshot).LinkTarget is not null) throw new UnauthorizedAccessException("Linked canonical objects are forbidden.");
        if (File.Exists(snapshot))
        {
            if (new FileInfo(snapshot).Length != bytes.Length || WorkflowJson.Hash(await File.ReadAllBytesAsync(snapshot, token)) != hash)
                throw new IOException("Existing canonical object failed integrity verification.");
        }
        else await AtomicWriteAsync(snapshot, bytes, token);
        var referencePath = Resolve(relative + ".reference.json", false);
        var referenceBytes = JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, sha256 = hash, filename, bytes = bytes.Length }, WorkflowJson.Options);
        if (File.Exists(referencePath))
        {
            if (new FileInfo(referencePath).Length != referenceBytes.Length || !(await File.ReadAllBytesAsync(referencePath, token)).AsSpan().SequenceEqual(referenceBytes))
                throw new IOException("Existing canonical reference differs; refusing to overwrite.");
        }
        else await AtomicWriteAsync(referencePath, referenceBytes, token);
        return relative;
    }
    public async Task<string> ImportAsync(string filename, byte[] bytes, CancellationToken token)
    {
        if (filename != Path.GetFileName(filename) || bytes.Length == 0 || bytes.Length > MaxFileBytes) throw new ArgumentException("Invalid import name or size.");
        var hash = WorkflowJson.Hash(bytes); var relative = "inputs/" + hash + "/" + filename;
        var path = Resolve(relative, false); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path)) { if (WorkflowJson.Hash(await File.ReadAllBytesAsync(path, token)) != hash) throw new IOException("Existing import changed."); }
        else await AtomicWriteAsync(path, bytes, token);
        return relative;
    }
    internal static async Task AtomicWriteAsync(string path, byte[] bytes, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!); CheckAncestors(Path.GetDirectoryName(path)!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 8192, FileOptions.Asynchronous))
            {
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(temp, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                await file.WriteAsync(bytes, token); file.Flush(true);
            }
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    internal static string Decode(byte[] bytes) => new UTF8Encoding(false, true).GetString(bytes);
    internal async Task SaveJsonAsync<T>(string category, string id, T value, CancellationToken token) =>
        await AtomicWriteAsync(Path.Combine(StateDirectory, category, id + ".json"), JsonSerializer.SerializeToUtf8Bytes(value, WorkflowJson.Options), token);
}
