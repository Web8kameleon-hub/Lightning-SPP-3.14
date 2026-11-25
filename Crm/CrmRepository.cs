using System.Text.Json;
using wwwmmm.Workflow;

namespace wwwmmm.Crm;

/// <summary>Private JSON records with an exclusive OS lease and recoverable record/audit commits.
/// The journal is flushed before record publication. Readers recover pending commits before reading.
/// This is local single-writer storage, without distributed replication or physical erase guarantees.</summary>
public sealed class CrmRepository
{
    private static readonly string[] Categories = ["customers", "contacts", "documents", "activities"];
    private const int MaxRecordBytes = 1024 * 1024;
    private readonly string root;
    private string Journal => Path.Combine(root, "transaction.json");
    internal sealed record Write(string Category, string Id, JsonElement Value, string? ExpectedSha256 = null);
    private sealed record Transaction(int Version, Write[] Writes);

    public CrmRepository(WorkspaceScope workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        _ = CrmStoragePolicy.FromEnvironment();
        root = Path.Combine(workspace.StateDirectory, "crm");
        WorkspaceScope.CheckAncestors(workspace.StateDirectory);
        EnsurePrivateDirectory(root);
        foreach (var category in Categories)
        {
            var directory = Path.Combine(root, category);
            EnsurePrivateDirectory(directory);
        }
        WorkspaceScope.PrivateDirectory(root);
        using var lease = Acquire(); Recover();
    }

    internal static Write Record<T>(string category, string id, T value, string? expected = null) =>
        new(category, id, JsonSerializer.SerializeToElement(value, CrmJson.Options), expected);

    internal Task CommitAsync(Write[] writes, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var lease = Acquire(); Recover();
        if (writes.Length is < 1 or > 2) throw new ArgumentException("A CRM commit contains a record and its audit entry.");
        if (writes.Select(w => (w.Category, w.Id)).Distinct().Count() != writes.Length)
            throw new ArgumentException("Duplicate CRM transaction destination.");
        foreach (var write in writes)
        {
            var path = RecordPath(write.Category, write.Id);
            var bytes = Payload(write);
            var existing = File.Exists(path) ? ReadBytes(path) : null;
            if (existing is not null && existing.AsSpan().SequenceEqual(bytes)) continue;
            if (write.ExpectedSha256 is null && existing is not null)
                throw new IOException("CRM record exists with different content; refusing to overwrite it.");
            if (write.ExpectedSha256 is not null && (existing is null || WorkflowJson.Hash(existing) != write.ExpectedSha256))
                throw new IOException("CRM record changed; reload it before updating.");
        }
        token.ThrowIfCancellationRequested();
        AtomicWrite(Journal, JsonSerializer.SerializeToUtf8Bytes(new Transaction(1, writes), CrmJson.Options));
        // Once committed, finish publication even if cancellation arrives.
        Recover();
        return Task.CompletedTask;
    }

    internal Task SaveActivityAsync(CrmActivity activity, CancellationToken token) =>
        CommitAsync([Record("activities", activity.Id, activity)], token);

    internal CrmCustomer? FindCustomer(string id)
    {
        if (!SafeId(id)) return null;
        using var lease = Acquire(); Recover(); return Read<CrmCustomer>("customers", id);
    }
    internal IReadOnlyList<CrmCustomer> ListCustomers() => List<CrmCustomer>("customers");
    internal IReadOnlyList<CrmContact> ListContacts(string? id = null) => List<CrmContact>("contacts").Where(x => id is null || x.CustomerId == id).ToArray();
    internal IReadOnlyList<CrmDocument> ListDocuments(string? id = null) => List<CrmDocument>("documents").Where(x => id is null || x.CustomerId == id).ToArray();
    internal IReadOnlyList<CrmActivity> ListActivities(string? id = null) => List<CrmActivity>("activities").Where(x => id is null || x.CustomerId == id).ToArray();

    internal CrmCustomerDetail Detail(string id)
    {
        using var lease = Acquire(); Recover();
        var customer = Read<CrmCustomer>("customers", id) ?? throw new ArgumentException("Unknown customer.");
        return new(customer, ReadAll<CrmContact>("contacts").Where(x => x.CustomerId == id).ToArray(),
            ReadAll<CrmDocument>("documents").Where(x => x.CustomerId == id).ToArray(),
            ReadAll<CrmActivity>("activities").Where(x => x.CustomerId == id).ToArray());
    }
    internal CrmSummary Summary()
    {
        using var lease = Acquire(); Recover();
        var customers = ReadAll<CrmCustomer>("customers"); var documents = ReadAll<CrmDocument>("documents");
        return new(customers.Length, customers.Count(x => x.Status == CrmValidation.ActiveStatus),
            ReadAll<CrmContact>("contacts").Length, documents.Length, documents.Count(x => x.Category is null), ReadAll<CrmActivity>("activities").Length);
    }

    private T[] List<T>(string category) where T : class
    { using var lease = Acquire(); Recover(); return ReadAll<T>(category); }
    private T[] ReadAll<T>(string category) where T : class
    {
        var files = Directory.EnumerateFiles(Path.Combine(root, category), "*.json").Take(50_001).ToArray();
        if (files.Length > 50_000 || files.Sum(path => new FileInfo(path).Length) > 64L * 1024 * 1024)
            throw new IOException("CRM category exceeds the local snapshot read budget; archive or migrate it before continuing.");
        return files.Order(StringComparer.Ordinal).Select(path => Read<T>(category, Path.GetFileNameWithoutExtension(path))
            ?? throw new IOException("CRM record disappeared during a locked read.")).ToArray();
    }
    private T? Read<T>(string category, string id) where T : class
    {
        var path = RecordPath(category, id);
        if (!File.Exists(path)) return null;
        try
        {
            var bytes = ReadBytes(path);
            using var document = JsonDocument.Parse(bytes);
            if (!document.RootElement.TryGetProperty("id", out var storedId) || storedId.GetString() != id)
                throw new IOException("CRM record identity mismatch.");
            return JsonSerializer.Deserialize<T>(bytes, CrmJson.Options) ?? throw new IOException("Null CRM record.");
        }
        catch (JsonException error) { throw new IOException("CRM record is corrupt; no empty fallback was substituted.", error); }
    }

    private FileStream Acquire()
    {
        WorkspaceScope.CheckAncestors(root);
        var path = Path.Combine(root, "writer.lease"); CheckFile(path);
        var lease = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        return lease;
    }
    private void Recover()
    {
        CheckFile(Journal);
        if (!File.Exists(Journal)) return;
        Transaction transaction;
        try { transaction = JsonSerializer.Deserialize<Transaction>(ReadBytes(Journal, 3 * MaxRecordBytes), CrmJson.Options) ?? throw new IOException("Null CRM journal."); }
        catch (JsonException error) { throw new IOException("CRM recovery journal is corrupt; manual recovery required.", error); }
        if (transaction.Version != 1 || transaction.Writes is null || transaction.Writes.Length is < 1 or > 2)
            throw new IOException("Unsupported CRM recovery journal.");
        foreach (var write in transaction.Writes) { _ = RecordPath(write.Category, write.Id); _ = Payload(write); }
        foreach (var write in transaction.Writes) AtomicWrite(RecordPath(write.Category, write.Id), Payload(write));
        File.Delete(Journal);
    }
    private string RecordPath(string category, string id)
    {
        if (!Categories.Contains(category) || !SafeId(id)) throw new IOException("Invalid CRM record destination.");
        var path = Path.Combine(root, category, id + ".json");
        WorkspaceScope.CheckAncestors(Path.GetDirectoryName(path)!); CheckFile(path); return path;
    }
    private static byte[] Payload(Write write)
    {
        if (write.Value.ValueKind != JsonValueKind.Object || !write.Value.TryGetProperty("id", out var id) || id.GetString() != write.Id)
            throw new IOException("CRM payload identity does not match its destination.");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(write.Value, CrmJson.Options);
        if (bytes.Length > MaxRecordBytes) throw new IOException("CRM record exceeds its resource budget.");
        return bytes;
    }
    private static byte[] ReadBytes(string path, int limit = MaxRecordBytes)
    {
        CheckFile(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is < 1 || stream.Length > limit) throw new IOException("CRM file exceeds its resource budget or is empty.");
        var bytes = new byte[checked((int)stream.Length)]; stream.ReadExactly(bytes); return bytes;
    }
    private static void AtomicWrite(string path, byte[] bytes)
    {
        CheckFile(path); WorkspaceScope.CheckAncestors(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                file.Write(bytes); file.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static void CheckFile(string path)
    { if (new FileInfo(path).LinkTarget is not null || (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)) throw new UnauthorizedAccessException("Linked CRM files are forbidden."); }
    private static void EnsurePrivateDirectory(string path)
    {
        if (new DirectoryInfo(path).LinkTarget is not null) throw new UnauthorizedAccessException("Linked CRM directories are forbidden.");
        WorkspaceScope.CheckAncestors(Path.GetDirectoryName(path)!);
        if (Directory.Exists(path)) WorkspaceScope.CheckAncestors(path);
        Directory.CreateDirectory(path);
        WorkspaceScope.CheckAncestors(path); WorkspaceScope.PrivateDirectory(path);
    }
    private static bool SafeId(string? id) => id is { Length: > 0 and <= 64 } && id.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c is '_' or '-');
}
