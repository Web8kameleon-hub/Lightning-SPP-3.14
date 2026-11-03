using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace wwwmmm.Extraction;

/// <summary>One declared extraction rule. Rules are operator-owned data, not model output.</summary>
public sealed record FieldRule(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("field")] string Field,
    [property: JsonPropertyName("pattern")] string Pattern,
    [property: JsonPropertyName("group")] int Group = 1,
    [property: JsonPropertyName("required")] bool Required = false);

/// <summary>A validated set of rules that can be applied to a document's text layer.</summary>
public sealed class RuleSet
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(2);

    private RuleSet(IReadOnlyList<(FieldRule Rule, Regex Pattern)> compiled, string sha256)
    {
        Rules = compiled.Select(entry => entry.Rule).ToArray();
        _compiled = compiled;
        Sha256 = sha256;
    }

    private readonly IReadOnlyList<(FieldRule Rule, Regex Pattern)> _compiled;

    public IReadOnlyList<FieldRule> Rules { get; }

    /// <summary>Hash of the exact rule file, recorded with every extraction result.</summary>
    public string Sha256 { get; }

    public static RuleSet LoadFromFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Rule file not found: {path}", path);

        var json = File.ReadAllText(path);
        return Parse(json);
    }

    public static RuleSet Parse(string json)
    {
        RuleDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<RuleDocument>(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Rule file is not valid JSON: {ex.Message}", ex);
        }

        if (document?.Rules is null || document.Rules.Count == 0)
            throw new InvalidOperationException("Rule file must contain at least one rule.");

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var compiled = new List<(FieldRule, Regex)>(document.Rules.Count);

        foreach (var rule in document.Rules)
        {
            Validate(rule, seenIds);
            var regex = new Regex(rule.Pattern!, RegexOptions.CultureInvariant, MatchTimeout);
            if (rule.Group < 0 || rule.Group > regex.GetGroupNumbers().Max())
                throw new InvalidOperationException($"Rule '{rule.Id}' references group {rule.Group}, which the pattern does not define.");
            compiled.Add((rule, regex));
        }

        var sha256 = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json)))
            .ToLowerInvariant();

        return new RuleSet(compiled, sha256);
    }

    internal IEnumerable<(FieldRule Rule, Regex Pattern)> Compiled => _compiled;

    private static void Validate(FieldRule rule, HashSet<string> seenIds)
    {
        if (string.IsNullOrWhiteSpace(rule.Id))
            throw new InvalidOperationException("Every rule needs a non-empty 'id'.");
        if (string.IsNullOrWhiteSpace(rule.Field))
            throw new InvalidOperationException($"Rule '{rule.Id}' needs a non-empty 'field'.");
        if (string.IsNullOrWhiteSpace(rule.Pattern))
            throw new InvalidOperationException($"Rule '{rule.Id}' needs a non-empty 'pattern'.");
        if (!seenIds.Add(rule.Id))
            throw new InvalidOperationException($"Duplicate rule id '{rule.Id}'.");
    }

    private sealed record RuleDocument(
        [property: JsonPropertyName("version")] int Version,
        [property: JsonPropertyName("rules")] List<FieldRule>? Rules);
}
