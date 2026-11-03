using System.Security.Cryptography;

namespace wwwmmm.Batch;

/// <summary>Outcome of processing one document. <see cref="Error"/> set means failure.</summary>
public sealed record BatchDocumentResult(int Pages, int Fields, IReadOnlyList<string> MissingRequired, string? Error)
{
    public static BatchDocumentResult Ok(int pages, int fields, IReadOnlyList<string> missing) =>
        new(pages, fields, missing, null);

    public static BatchDocumentResult Failed(string error) =>
        new(0, 0, Array.Empty<string>(), error);
}

public delegate Task<BatchDocumentResult> DocumentProcessor(string path, CancellationToken cancellationToken);

public sealed record BatchRunSummary(int Processed, int Completed, int Failed, int Remaining, string CheckpointUtc);

/// <summary>
/// Resumable batch processing. Every document writes one manifest line, then the checkpoint
/// is advanced atomically. Resume accounts for durable manifest entries even when the
/// checkpoint lags. Unclean interrupted writes must be verified before recovery.
/// </summary>
public sealed class BatchRunner
{
    private readonly BatchWorkspace _workspace;

    public BatchRunner(BatchWorkspace workspace) =>
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));

    public async Task<BatchRunSummary> RunAsync(
        IReadOnlyList<string> inputs,
        DocumentProcessor processor,
        int? maxDocuments = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(processor);

        using var lease = new FileStream(Path.Combine(_workspace.Root, "run.lease"), FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
        var index = _workspace.EnsureIndex(inputs);
        var entries = _workspace.LoadEntries();
        var done = new HashSet<string>(
            entries.Select(entry => entry.RelativePath),
            StringComparer.Ordinal);

        var checkpoint = _workspace.LoadCheckpoint() ?? new BatchCheckpoint(0, 0, 0, DateTime.UtcNow.ToString("O"));
        checkpoint = checkpoint with
        {
            Completed = entries.Count(entry => entry.Status == BatchStatus.Completed),
            Failed = entries.Count(entry => entry.Status == BatchStatus.Failed)
        };
        var processedNow = 0;

        for (var i = checkpoint.NextIndex; i < index.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (maxDocuments.HasValue && processedNow >= maxDocuments.Value)
                break;

            var path = index[i];
            var relative = MakeRelative(path);

            if (done.Contains(relative))
            {
                checkpoint = checkpoint with { NextIndex = i + 1, UpdatedUtc = DateTime.UtcNow.ToString("O") };
                _workspace.SaveCheckpoint(checkpoint);
                continue;
            }

            var entry = await ProcessOneAsync(i, path, relative, processor, cancellationToken);
            _workspace.AppendEntry(entry);

            checkpoint = checkpoint with
            {
                NextIndex = i + 1,
                Completed = checkpoint.Completed + (entry.Status == BatchStatus.Completed ? 1 : 0),
                Failed = checkpoint.Failed + (entry.Status == BatchStatus.Failed ? 1 : 0),
                UpdatedUtc = DateTime.UtcNow.ToString("O")
            };
            _workspace.SaveCheckpoint(checkpoint);
            processedNow++;
        }

        var remaining = Math.Max(0, index.Count - checkpoint.NextIndex);
        return new BatchRunSummary(processedNow, checkpoint.Completed, checkpoint.Failed, remaining, checkpoint.UpdatedUtc);
    }

    private async Task<BatchEntry> ProcessOneAsync(
        int index,
        string path,
        string relative,
        DocumentProcessor processor,
        CancellationToken cancellationToken)
    {
        long size = 0;
        var sha256 = string.Empty;
        try
        {
            var info = new FileInfo(path);
            size = info.Exists ? info.Length : 0;
            sha256 = await ComputeSha256Async(path, cancellationToken);
            var result = await processor(path, cancellationToken);

            return new BatchEntry(
                index, path, relative, size, sha256,
                result.Error is null ? BatchStatus.Completed : BatchStatus.Failed,
                result.Pages, result.Fields, result.MissingRequired,
                result.Error);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or TimeoutException)
        {
            return new BatchEntry(
                index, path, relative, size, sha256, BatchStatus.Failed,
                0, 0, Array.Empty<string>(), $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>Recomputes every source hash and compares it with the manifest.</summary>
    public async Task<BatchVerification> VerifyAsync(CancellationToken cancellationToken = default)
    {
        var entries = _workspace.LoadEntries();
        var problems = new List<string>();
        var mismatches = 0;

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(entry.Source))
            {
                problems.Add($"missing source: {entry.RelativePath}");
                mismatches++;
                continue;
            }

            var current = await ComputeSha256Async(entry.Source, cancellationToken);
            if (!string.Equals(current, entry.Sha256, StringComparison.Ordinal))
            {
                problems.Add($"hash mismatch: {entry.RelativePath}");
                mismatches++;
            }
        }

        return new BatchVerification(mismatches == 0, entries.Count, mismatches, problems);
    }

    private string MakeRelative(string path)
    {
        try
        {
            return Path.GetRelativePath(_workspace.Root, path);
        }
        catch (ArgumentException)
        {
            return path;
        }
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
