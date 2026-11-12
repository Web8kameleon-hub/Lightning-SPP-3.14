using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace wwwmmm.Provenance;

public enum ProvenanceAction
{
    Extracted,
    Reviewed
}

public enum ReviewDecision
{
    Pending,
    Accepted,
    Rejected
}

/// <summary>
/// One traceability record. <see cref="PreviousHash"/> chains records, so deleting or
/// editing interior history breaks verification. Tail truncation requires an external trusted head hash to detect.
/// </summary>
public sealed record ProvenanceRecord(
    [property: JsonPropertyName("seq")] int Seq,
    [property: JsonPropertyName("utc")] string TimestampUtc,
    [property: JsonPropertyName("documentSha256")] string DocumentSha256,
    [property: JsonPropertyName("ruleSetSha256")] string RuleSetSha256,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("ruleId")] string RuleId,
    [property: JsonPropertyName("field")] string Field,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("offset")] int Offset,
    [property: JsonPropertyName("reviewer")] string? Reviewer,
    [property: JsonPropertyName("decision")] string Decision,
    [property: JsonPropertyName("previousHash")] string PreviousHash,
    [property: JsonPropertyName("hash")] string Hash);

public sealed record TrailVerification(bool IsValid, int RecordCount, string? Error);

/// <summary>
/// Append-only, hash-chained trail answering: which document, which page, which value,
/// which rule, and which reviewer — for every derived field.
/// </summary>
public sealed class ProvenanceTrail
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false
    };

    private ProvenanceTrail(string path) => Path = path;

    public string Path { get; }

    public static ProvenanceTrail Open(string trailPath)
    {
        var directory = System.IO.Path.GetDirectoryName(trailPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        return new ProvenanceTrail(trailPath);
    }

    public async Task<IReadOnlyList<ProvenanceRecord>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(Path))
            return Array.Empty<ProvenanceRecord>();

        var records = new List<ProvenanceRecord>();
        foreach (var line in await File.ReadAllLinesAsync(Path, cancellationToken))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            var record = JsonSerializer.Deserialize<ProvenanceRecord>(line, SerializerOptions)
                ?? throw new InvalidOperationException($"Unreadable trail line in {Path}.");
            records.Add(record);
        }
        return records;
    }

    public async Task<ProvenanceRecord> AppendExtractionAsync(
        string documentSha256,
        string ruleSetSha256,
        string ruleId,
        string field,
        string value,
        int page,
        int offset,
        CancellationToken cancellationToken = default)
    {
        using var lease = new FileStream(Path + ".lease", FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
        var verification = await VerifyAsync(cancellationToken);
        if (!verification.IsValid) throw new InvalidOperationException("Cannot append to an invalid provenance trail.");
        var existing = await LoadAsync(cancellationToken);
        var seq = existing.Count == 0 ? 1 : existing[^1].Seq + 1;
        var previousHash = existing.Count == 0 ? "genesis" : existing[^1].Hash;

        var draft = new ProvenanceRecord(
            seq,
            DateTime.UtcNow.ToString("O"),
            documentSha256,
            ruleSetSha256,
            nameof(ProvenanceAction.Extracted),
            ruleId,
            field,
            value,
            page,
            offset,
            Reviewer: null,
            Decision: nameof(ReviewDecision.Pending),
            previousHash,
            Hash: string.Empty);

        return await AppendAsync(draft, cancellationToken);
    }

    public async Task<ProvenanceRecord> AppendReviewAsync(
        string documentSha256,
        string ruleId,
        string field,
        string value,
        int page,
        int offset,
        string reviewer,
        ReviewDecision decision,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reviewer))
            throw new ArgumentException("A review needs a named reviewer.", nameof(reviewer));
        if (decision is not (ReviewDecision.Accepted or ReviewDecision.Rejected))
            throw new ArgumentException("A review decision must be Accepted or Rejected.", nameof(decision));

        using var lease = new FileStream(Path + ".lease", FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
        var verification = await VerifyAsync(cancellationToken);
        if (!verification.IsValid) throw new InvalidOperationException("Cannot append to an invalid provenance trail.");
        var existing = await LoadAsync(cancellationToken);
        var seq = existing.Count == 0 ? 1 : existing[^1].Seq + 1;
        var previousHash = existing.Count == 0 ? "genesis" : existing[^1].Hash;

        var extraction = existing.LastOrDefault(r => r.Action == nameof(ProvenanceAction.Extracted) &&
            r.DocumentSha256 == documentSha256 && r.RuleId == ruleId && r.Field == field &&
            r.Value == value && r.Page == page && r.Offset == offset)
            ?? throw new ArgumentException("Review must reference an existing extracted value.");

        var draft = new ProvenanceRecord(
            seq,
            DateTime.UtcNow.ToString("O"),
            documentSha256,
            RuleSetSha256: extraction.RuleSetSha256,
            nameof(ProvenanceAction.Reviewed),
            ruleId,
            field,
            value,
            page,
            offset,
            reviewer,
            decision.ToString(),
            previousHash,
            Hash: string.Empty);

        return await AppendAsync(draft, cancellationToken);
    }

    public async Task<TrailVerification> VerifyAsync(CancellationToken cancellationToken = default)
    {
        var records = await LoadAsync(cancellationToken);
        var previousHash = "genesis";
        var expectedSeq = 1;

        foreach (var record in records)
        {
            if (record.Seq != expectedSeq)
                return new TrailVerification(false, records.Count, $"Sequence break at {record.Seq}; expected {expectedSeq}.");
            if (!string.Equals(record.PreviousHash, previousHash, StringComparison.Ordinal))
                return new TrailVerification(false, records.Count, $"Chain break at sequence {record.Seq}.");
            if (!string.Equals(record.Hash, ComputeHash(record), StringComparison.Ordinal))
                return new TrailVerification(false, records.Count, $"Record {record.Seq} was modified after it was written.");

            previousHash = record.Hash;
            expectedSeq++;
        }

        return new TrailVerification(true, records.Count, null);
    }

    private async Task<ProvenanceRecord> AppendAsync(ProvenanceRecord draft, CancellationToken cancellationToken)
    {
        var sealedRecord = draft with { Hash = ComputeHash(draft) };
        var line = JsonSerializer.Serialize(sealedRecord, SerializerOptions) + Environment.NewLine;
        await File.AppendAllTextAsync(Path, line, new UTF8Encoding(false), cancellationToken);
        return sealedRecord;
    }

    private static string ComputeHash(ProvenanceRecord record)
    {
        // Fixed property order with JSON escaping prevents field-separator collisions.
        var canonical = JsonSerializer.Serialize(record with { Hash = string.Empty }, SerializerOptions);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
}
