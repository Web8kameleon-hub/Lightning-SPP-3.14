namespace wwwmmm.Classify;

/// <summary>
/// One reviewed category. <paramref name="Keywords"/> are matched case-insensitively against the
/// document text; <paramref name="FileNameHints"/> against the file name only. A category with no
/// rules can never win, which keeps an empty taxonomy from silently classifying everything.
/// </summary>
public sealed record CategoryRule(
    string Category,
    string Description,
    string[] Keywords,
    string[] FileNameHints,
    int Priority);

/// <summary>
/// The reviewed taxonomy. <paramref name="Fallback"/> is used when nothing scores above the
/// threshold; it is explicit so "unclassified" is a visible outcome instead of a guess.
/// </summary>
public sealed record Taxonomy(
    string Name,
    string Version,
    string Fallback,
    double MinimumScore,
    CategoryRule[] Categories);

/// <summary>
/// The outcome for one document. <paramref name="Score"/> is the sum of matched rule weights and
/// <paramref name="Evidence"/> names exactly which keywords matched, so a reviewer can check the
/// decision without re-reading the file.
/// </summary>
public sealed record ClassificationResult(
    string Category,
    double Score,
    bool IsFallback,
    string[] Evidence,
    string? RunnerUp,
    double RunnerUpScore);

/// <summary>Per-document record written to the run manifest.</summary>
public sealed record ClassifiedDocument(
    string SourcePath,
    string FileName,
    string Sha256,
    string TextSource,
    string Category,
    double Score,
    bool IsFallback,
    string[] Evidence,
    string FiledPath,
    bool FiledNow,
    string? SkipReason);

/// <summary>Totals for one run, computed from the per-document records.</summary>
public sealed record ClassificationRunReport(
    string RunId,
    string TaxonomyName,
    string TaxonomyVersion,
    string SourceRoot,
    string OutputRoot,
    int Examined,
    int Classified,
    int FellBack,
    int Skipped,
    int Failed,
    ClassifiedDocument[] Documents,
    string[] Failures);
