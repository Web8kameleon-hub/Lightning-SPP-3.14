using wwwmmm.Batch;
using wwwmmm.Documents;
using wwwmmm.Extraction;
using wwwmmm.Intake;
using wwwmmm.Packaging;
using wwwmmm.Provenance;

namespace wwwmmm.Flow;

/// <summary>Batch side of the reference flow: resumable runs, integrity verification and reports.</summary>
public static class FlowBatch
{
    public static async Task<int> RunAsync(string[] args, bool resume, CancellationToken cancellationToken)
    {
        if (args.Length < 5)
        {
            Console.Error.WriteLine("Usage: wwwmmm flow run|resume <input-path> <rules.json> <workspace> [--limit N]");
            return 2;
        }

        var rules = RuleSet.LoadFromFile(args[3]);
        var source = IntakeSource.FromPath(args[2]);
        var workspace = BatchWorkspace.Open(args[4]);
        var trail = ProvenanceTrail.Open(Path.Combine(workspace.Root, "provenance", "trail.jsonl"));
        var limit = ParseLimit(args, 5);

        var inputs = IntakeEnumerator.Enumerate(source);
        if (inputs.Count == 0)
        {
            Console.Error.WriteLine("No accepted documents found for this intake.");
            return 1;
        }

        var runner = new BatchRunner(workspace);
        var summary = await runner.RunAsync(inputs, ProcessAsync, limit, cancellationToken);

        Console.WriteLine($"mode={(resume ? "resume" : "run")}|intake={source.Kind}|discovered={inputs.Count}");
        Console.WriteLine($"processedNow={summary.Processed}|completed={summary.Completed}|failed={summary.Failed}|remaining={summary.Remaining}");
        Console.WriteLine($"workspace={workspace.Root}");
        Console.WriteLine($"trail={trail.Path}");

        return summary.Failed > 0 ? 1 : 0;

        async Task<BatchDocumentResult> ProcessAsync(string path, CancellationToken token)
        {
            if (!string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase))
                return BatchDocumentResult.Failed(
                    "No text layer: this input is an image. OCR is not implemented, so no fields were extracted.");

            var document = await TextLayerExtractor.ExtractAsync(path, token);
            var result = FieldExtractor.Apply(document, rules);

            foreach (var match in result.Matches)
            {
                await trail.AppendExtractionAsync(
                    result.DocumentSha256, result.RuleSetSha256,
                    match.RuleId, match.Field, match.Value, match.Page, match.Offset, token);
            }

            return BatchDocumentResult.Ok(result.PageCount, result.Matches.Count, result.MissingRequired);
        }
    }

    public static async Task<int> VerifyAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("Usage: wwwmmm flow verify <workspace>");
            return 2;
        }

        if (!Directory.Exists(args[2]) || !File.Exists(Path.Combine(args[2], "manifest.jsonl")) ||
            !File.Exists(Path.Combine(args[2], "index.txt")) || !File.Exists(Path.Combine(args[2], "checkpoint.json")))
        {
            Console.Error.WriteLine("Batch verification requires an existing manifest, input index and checkpoint.");
            return 1;
        }
        var workspace = BatchWorkspace.Open(args[2]);
        var integrity = await new BatchRunner(workspace).VerifyAsync(cancellationToken);
        var trailPath = Path.Combine(workspace.Root, "provenance", "trail.jsonl");
        if (workspace.LoadEntries().Any(entry => entry.Fields > 0) && !File.Exists(trailPath))
        {
            Console.Error.WriteLine("Provenance trail is missing for extracted fields.");
            return 1;
        }
        var trail = await ProvenanceTrail.Open(trailPath).VerifyAsync(cancellationToken);

        Console.WriteLine($"manifestValid={integrity.IsValid.ToString().ToLowerInvariant()}|entries={integrity.Entries}|mismatches={integrity.Mismatches}");
        Console.WriteLine($"trailValid={trail.IsValid.ToString().ToLowerInvariant()}|records={trail.RecordCount}");

        foreach (var problem in integrity.Problems)
            Console.Error.WriteLine($"integrity: {problem}");
        if (!trail.IsValid)
            Console.Error.WriteLine($"trail: {trail.Error}");

        return integrity.IsValid && trail.IsValid ? 0 : 1;
    }

    public static int Report(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("Usage: wwwmmm flow report <workspace>");
            return 2;
        }

        if (VerifyAsync(args, CancellationToken.None).GetAwaiter().GetResult() != 0) return 1;
        var workspace = BatchWorkspace.Open(args[2]);
        var entries = workspace.LoadEntries();
        var checkpoint = workspace.LoadCheckpoint();
        var trailPath = Path.Combine(workspace.Root, "provenance", "trail.jsonl");
        var records = File.Exists(trailPath)
            ? ProvenanceTrail.Open(trailPath).LoadAsync().GetAwaiter().GetResult()
            : Array.Empty<ProvenanceRecord>();

        var accepted = records.Count(r => r.Decision == nameof(ReviewDecision.Accepted));
        var rejected = records.Count(r => r.Decision == nameof(ReviewDecision.Rejected));
        var reportPath = Path.Combine(workspace.Root, "report.md");

        var builder = new System.Text.StringBuilder();
        builder.AppendLine("# Raport i rrjedhës — dokumente, fusha dhe referenca");
        builder.AppendLine();
        builder.AppendLine($"- Dokumente në manifest: {entries.Count}");
        builder.AppendLine($"- Të përfunduara: {checkpoint?.Completed ?? 0} | Dështuara: {checkpoint?.Failed ?? 0}");
        builder.AppendLine($"- Regjistrime prove: {records.Count} | Pranuar: {accepted} | Refuzuar: {rejected}");
        builder.AppendLine();
        builder.AppendLine("| Dokument | Faqe | Fusha | Pa fusha të detyrueshme | Statusi |");
        builder.AppendLine("| --- | --- | --- | --- | --- |");

        foreach (var entry in entries)
        {
            var missing = entry.MissingRequired.Count == 0 ? "—" : string.Join(", ", entry.MissingRequired);
            builder.AppendLine($"| {entry.RelativePath} | {entry.Pages} | {entry.Fields} | {missing} | {entry.Status} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Referenca për çdo fushë");
        builder.AppendLine();
        builder.AppendLine("| Dokument (sha256) | Faqe | Fusha | Vlera | Rregulli | Shqyrtuesi | Vendimi |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");

        foreach (var record in records.OrderBy(r => r.Seq))
        {
            var reviewer = string.IsNullOrEmpty(record.Reviewer) ? "—" : record.Reviewer;
            builder.AppendLine($"| {record.DocumentSha256[..12]}… | {record.Page} | {record.Field} | {record.Value} | {record.RuleId} | {reviewer} | {record.Decision} |");
        }

        File.WriteAllText(reportPath, builder.ToString(), new System.Text.UTF8Encoding(false));
        Console.WriteLine($"report={reportPath}");
        return 0;
    }

    public static async Task<int> PackageAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length < 4)
        {
            Console.Error.WriteLine("Usage: wwwmmm flow package <out.zip> <file...>");
            return 2;
        }

        var members = args.Skip(3).ToArray();
        var manifest = await DocumentPackager.CreateAsync(args[2], members, cancellationToken: cancellationToken);
        Console.WriteLine($"package={args[2]}|members={manifest.Members.Count}");
        return 0;
    }

    public static async Task<int> PackageVerifyAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("Usage: wwwmmm flow package-verify <archive.zip>");
            return 2;
        }

        var verification = await DocumentPackager.VerifyAsync(args[2], cancellationToken);
        Console.WriteLine($"valid={verification.IsValid.ToString().ToLowerInvariant()}|members={verification.Members}");
        foreach (var problem in verification.Problems)
            Console.Error.WriteLine(problem);
        return verification.IsValid ? 0 : 1;
    }

    private static int? ParseLimit(string[] args, int startIndex)
    {
        for (var i = startIndex; i < args.Length - 1; i++)
        {
            if (args[i] == "--limit" && int.TryParse(args[i + 1], out var limit) && limit > 0)
                return limit;
        }
        return null;
    }
}
