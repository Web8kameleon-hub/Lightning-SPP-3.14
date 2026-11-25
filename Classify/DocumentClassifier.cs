namespace wwwmmm.Classify;

/// <summary>
/// Deterministic, auditable document classifier. A category earns one point per keyword found in
/// the text and two per hint found in the file name; the highest score above the taxonomy's
/// threshold wins, ties are broken by the reviewer-set priority and then by name. The score is a
/// count of matched rules, not a probability, and the matched terms are returned as evidence so
/// no decision has to be taken on trust.
/// </summary>
public sealed class DocumentClassifier
{
    /// <summary>Only the opening part of a document is scanned; a huge file cannot stall a run.</summary>
    public const int MaxScanCharacters = 200_000;
    private const double KeywordWeight = 1;
    private const double FileNameHintWeight = 2;
    private readonly Taxonomy taxonomy;

    public DocumentClassifier(Taxonomy taxonomy)
    {
        var validated = ClassificationTaxonomy.Validate(taxonomy);
        this.taxonomy = validated with { Categories = validated.Categories.Select(rule => rule with
            { Keywords = [.. rule.Keywords], FileNameHints = [.. rule.FileNameHints] }).ToArray() };
    }

    public Taxonomy Taxonomy => taxonomy with { Categories = taxonomy.Categories.Select(rule => rule with
        { Keywords = [.. rule.Keywords], FileNameHints = [.. rule.FileNameHints] }).ToArray() };

    public ClassificationResult Classify(string fileName, string text)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        var haystack = Trim(text);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var scored = taxonomy.Categories
            .Select(rule => (Rule: rule, Score: Score(rule, stem, haystack)))
            .OrderByDescending(candidate => candidate.Score.Points)
            .ThenByDescending(candidate => candidate.Rule.Priority)
            .ThenBy(candidate => candidate.Rule.Category, StringComparer.Ordinal)
            .ToArray();

        var best = scored[0];
        var runnerUp = scored.Skip(1).FirstOrDefault();
        if (best.Score.Points < taxonomy.MinimumScore)
            return new ClassificationResult(taxonomy.Fallback, best.Score.Points, true, best.Score.Evidence,
                runnerUp.Rule?.Category, runnerUp.Score.Points);

        return new ClassificationResult(best.Rule.Category, best.Score.Points, false, best.Score.Evidence,
            runnerUp.Rule?.Category, runnerUp.Score.Points);
    }

    private static (double Points, string[] Evidence) Score(CategoryRule rule, string stem, string text)
    {
        var evidence = new List<string>();
        var points = 0d;
        foreach (var keyword in rule.Keywords)
        {
            if (!Contains(text, keyword)) continue;
            points += KeywordWeight;
            evidence.Add("text:" + keyword);
        }
        foreach (var hint in rule.FileNameHints)
        {
            if (!Contains(stem, hint)) continue;
            points += FileNameHintWeight;
            evidence.Add("name:" + hint);
        }
        return (points, evidence.ToArray());
    }

    private static string Trim(string? text) =>
        string.IsNullOrEmpty(text) ? string.Empty
        : text.Length <= MaxScanCharacters ? text
        : text[..MaxScanCharacters];

    /// <summary>
    /// Case-insensitive search where a single-word term must sit on a word boundary, so "tax"
    /// does not match "syntax". Multi-word phrases are matched as written.
    /// </summary>
    internal static bool Contains(string haystack, string term)
    {
        if (haystack.Length == 0 || string.IsNullOrWhiteSpace(term)) return false;
        var singleWord = !term.Any(char.IsWhiteSpace);
        var start = 0;
        while (start <= haystack.Length - term.Length)
        {
            var index = haystack.IndexOf(term, start, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return false;
            if (!singleWord || IsBoundary(haystack, index, term.Length)) return true;
            start = index + 1;
        }
        return false;
    }

    private static bool IsBoundary(string haystack, int index, int length)
    {
        var beforeOk = index == 0 || !char.IsLetterOrDigit(haystack[index - 1]);
        var afterIndex = index + length;
        var afterOk = afterIndex >= haystack.Length || !char.IsLetterOrDigit(haystack[afterIndex]);
        return beforeOk && afterOk;
    }
}
