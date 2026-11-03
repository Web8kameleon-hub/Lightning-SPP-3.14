using System.Text.Json;
using System.Text.Json.Serialization;

namespace wwwmmm.Batch;

public static class BatchStatus
{
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Skipped = "skipped";
}

/// <summary>One processed document, written to the manifest as a single JSON line.</summary>
public sealed record BatchEntry(
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("relativePath")] string RelativePath,
    [property: JsonPropertyName("sizeBytes")] long SizeBytes,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("pages")] int Pages,
    [property: JsonPropertyName("fields")] int Fields,
    [property: JsonPropertyName("missingRequired")] IReadOnlyList<string> MissingRequired,
    [property: JsonPropertyName("error")] string? Error);

/// <summary>Durable position of a run. Written after every document, atomically.</summary>
public sealed record BatchCheckpoint(
    [property: JsonPropertyName("nextIndex")] int NextIndex,
    [property: JsonPropertyName("completed")] int Completed,
    [property: JsonPropertyName("failed")] int Failed,
    [property: JsonPropertyName("updatedUtc")] string UpdatedUtc);

public sealed record BatchVerification(bool IsValid, int Entries, int Mismatches, IReadOnlyList<string> Problems);

/// <summary>Filesystem layout and atomic-write helpers for a batch workspace.</summary>
public sealed class BatchWorkspace
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    private BatchWorkspace(string root)
    {
        Root = root;
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(IntermediateDirectory);
    }

    public string Root { get; }
    public string ManifestPath => Path.Combine(Root, "manifest.jsonl");
    public string CheckpointPath => Path.Combine(Root, "checkpoint.json");
    public string IndexPath => Path.Combine(Root, "index.txt");
    public string IntermediateDirectory => Path.Combine(Root, "work");

    public static BatchWorkspace Open(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
            throw new ArgumentException("Batch workspace root must not be empty.", nameof(root));
        return new BatchWorkspace(Path.GetFullPath(root));
    }

    public IReadOnlyList<BatchEntry> LoadEntries()
    {
        if (!File.Exists(ManifestPath))
            return Array.Empty<BatchEntry>();

        var entries = new List<BatchEntry>();
        foreach (var line in File.ReadAllLines(ManifestPath))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            var entry = JsonSerializer.Deserialize<BatchEntry>(line, Options)
                ?? throw new InvalidOperationException($"Unreadable manifest line in {ManifestPath}.");
            entries.Add(entry);
        }
        return entries;
    }

    public BatchCheckpoint? LoadCheckpoint()
    {
        if (!File.Exists(CheckpointPath))
            return null;
        return JsonSerializer.Deserialize<BatchCheckpoint>(File.ReadAllText(CheckpointPath), Options);
    }

    public void SaveCheckpoint(BatchCheckpoint checkpoint) =>
        WriteAtomic(CheckpointPath, JsonSerializer.Serialize(checkpoint, Options));

    public void AppendEntry(BatchEntry entry)
    {
        var line = JsonSerializer.Serialize(entry, Options) + Environment.NewLine;
        File.AppendAllText(ManifestPath, line, new System.Text.UTF8Encoding(false));
    }

    /// <summary>Writes the deterministic input index once, so a resume uses the same list.</summary>
    public IReadOnlyList<string> EnsureIndex(IReadOnlyList<string> inputs)
    {
        if (File.Exists(IndexPath))
            return File.ReadAllLines(IndexPath).Where(l => l.Length > 0).ToArray();

        WriteAtomic(IndexPath, string.Join(Environment.NewLine, inputs) + Environment.NewLine);
        return inputs;
    }

    /// <summary>Temp file plus move: an interruption never leaves a half-written checkpoint.</summary>
    public static void WriteAtomic(string path, string content)
    {
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, content, new System.Text.UTF8Encoding(false));
        File.Move(temporary, path, overwrite: true);
    }
}
