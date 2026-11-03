using wwwmmm.Documents;
using wwwmmm.Extraction;
using wwwmmm.Intake;
using wwwmmm.Provenance;

namespace wwwmmm.Flow;

/// <summary>
/// The reference flow as CLI commands: intake → text → rules → values with references →
/// review → verified recovery. Commands print explicit results and return a failure exit code when verification fails.
/// </summary>
public static class FlowCommands
{
    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length < 2)
        {
            Usage();
            return 2;
        }

        try
        {
            return args[1].ToLowerInvariant() switch
            {
                "sources" => Sources(),
                "extract" => await ExtractAsync(args, cancellationToken),
                "review" => await ReviewAsync(args, cancellationToken),
                "trail-verify" => await TrailVerifyAsync(args, cancellationToken),
                "run" => await FlowBatch.RunAsync(args, resume: false, cancellationToken),
                "resume" => await FlowBatch.RunAsync(args, resume: true, cancellationToken),
                "verify" => await FlowBatch.VerifyAsync(args, cancellationToken),
                "report" => FlowBatch.Report(args),
                "package" => await FlowBatch.PackageAsync(args, cancellationToken),
                "package-verify" => await FlowBatch.PackageVerifyAsync(args, cancellationToken),
                _ => Unknown(args[1])
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Console.Error.WriteLine("Flow canceled. Verify the workspace before resuming.");
            return 130;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or ArgumentException or System.Text.Json.JsonException
                                       or FileNotFoundException or NotSupportedException or TimeoutException)
        {
            Console.Error.WriteLine($"flow error: {ex.Message}");
            return 1;
        }
    }

    private static int Sources()
    {
        Console.WriteLine("intake kinds: file, folder, watch-folder (one-shot snapshot), network-share (UNC), http-upload (served by the HTTP API), scanner");
        foreach (var provider in ScanProviderRegistry.Probe())
        {
            var state = provider.Available ? "available" : "unavailable";
            Console.WriteLine($"provider={provider.Name}|transport={provider.Transport}|{state}|{provider.Detail}");
        }
        return 0;
    }

    private static async Task<int> ExtractAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length < 4)
        {
            Console.Error.WriteLine("Usage: wwwmmm flow extract <document.pdf> <rules.json> [trail.jsonl]");
            return 2;
        }

        var rules = RuleSet.LoadFromFile(args[3]);
        var document = await TextLayerExtractor.ExtractAsync(args[2], cancellationToken);
        var result = FieldExtractor.Apply(document, rules);

        Console.WriteLine($"document={result.Source}");
        Console.WriteLine($"documentSha256={result.DocumentSha256}");
        Console.WriteLine($"ruleSetSha256={result.RuleSetSha256}");
        Console.WriteLine($"pages={result.PageCount}");

        foreach (var match in result.Matches)
            Console.WriteLine($"field={match.Field}|rule={match.RuleId}|page={match.Page}|offset={match.Offset}|value={match.Value}");

        foreach (var missing in result.MissingRequired)
            Console.WriteLine($"missingRequired={missing}");

        if (args.Length > 4 && args[4].Length > 0)
        {
            var trail = ProvenanceTrail.Open(args[4]);
            foreach (var match in result.Matches)
            {
                await trail.AppendExtractionAsync(
                    result.DocumentSha256, result.RuleSetSha256,
                    match.RuleId, match.Field, match.Value, match.Page, match.Offset,
                    cancellationToken);
            }
            Console.WriteLine($"trail={trail.Path}|records={result.Matches.Count}");
        }

        return result.HasAllRequiredFields ? 0 : 1;
    }

    private static async Task<int> ReviewAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length < 11)
        {
            Console.Error.WriteLine("Usage: wwwmmm flow review <trail.jsonl> <documentSha256> <ruleId> <field> <value> <page> <offset> <reviewer> <accept|reject>");
            return 2;
        }

        var decision = args[10].ToLowerInvariant() switch
        {
            "accept" or "accepted" => ReviewDecision.Accepted,
            "reject" or "rejected" => ReviewDecision.Rejected,
            _ => throw new ArgumentException("Decision must be 'accept' or 'reject'.")
        };

        var trail = ProvenanceTrail.Open(args[2]);
        var record = await trail.AppendReviewAsync(
            args[3], args[4], args[5], args[6],
            int.Parse(args[7], System.Globalization.CultureInfo.InvariantCulture),
            int.Parse(args[8], System.Globalization.CultureInfo.InvariantCulture),
            args[9], decision, cancellationToken);

        Console.WriteLine($"recorded seq={record.Seq} reviewer={record.Reviewer} decision={record.Decision}");
        return 0;
    }

    private static async Task<int> TrailVerifyAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("Usage: wwwmmm flow trail-verify <trail.jsonl>");
            return 2;
        }

        var verification = await ProvenanceTrail.Open(args[2]).VerifyAsync(cancellationToken);
        Console.WriteLine($"valid={verification.IsValid.ToString().ToLowerInvariant()}|records={verification.RecordCount}");
        if (!verification.IsValid)
        {
            Console.Error.WriteLine(verification.Error ?? "trail is not valid");
            return 1;
        }
        return 0;
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"Unknown flow command '{command}'.");
        Usage();
        return 2;
    }

    private static void Usage()
    {
        Console.WriteLine("wwwmmm flow <command>");
        Console.WriteLine("  sources                            - list intake kinds and scanner providers");
        Console.WriteLine("  extract <doc.pdf> <rules.json> [trail.jsonl]");
        Console.WriteLine("  review <trail> <docSha> <ruleId> <field> <value> <page> <offset> <reviewer> <accept|reject>");
        Console.WriteLine("  trail-verify <trail.jsonl>");
        Console.WriteLine("  run <input-path> <rules.json> <workspace> [--limit N]");
        Console.WriteLine("  resume <input-path> <rules.json> <workspace> [--limit N]");
        Console.WriteLine("  verify <workspace>                 - manifest integrity + trail verification");
        Console.WriteLine("  report <workspace>                 - report with per-field references");
        Console.WriteLine("  package <out.zip> <file...>        - reproducible archive with manifest");
        Console.WriteLine("  package-verify <archive.zip>");
    }
}
