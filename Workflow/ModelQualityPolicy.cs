using System.Globalization;
using System.Text.Json;

namespace wwwmmm.Workflow;

public sealed record ModelQualityEvidence(string Model, string Digest, string Language, string Scope,
    double Score, int SampleCount, string Metric, string Signature, string DatasetSha256,
    DateTimeOffset EvaluatedAt, DateTimeOffset ValidUntil);

public sealed record ModelQualitySelection(ModelIdentity Identity, ModelQualityEvidence Evidence);

/// <summary>Operator-owned measurements, bound to model digest. No model-name preference or latency-derived quality.</summary>
public sealed class ModelQualityPolicy
{
    private readonly ModelQualityEvidence[] evidence;
    private readonly double minimumScore;
    public ModelQualityPolicy(IEnumerable<ModelQualityEvidence> evidence, double minimumScore)
    {
        if (!double.IsFinite(minimumScore) || minimumScore is < 0 or > 100) throw new ArgumentException("Invalid quality threshold.");
        this.minimumScore = minimumScore;
        this.evidence = evidence.ToArray();
        if (this.evidence.Length > 10000 || this.evidence.Any(e =>
            string.IsNullOrWhiteSpace(e.Model) || !Hash(e.Digest) || !Hash(e.DatasetSha256) || string.IsNullOrWhiteSpace(e.Scope) ||
            !double.IsFinite(e.Score) || e.Score is < 0 or > 100 || e.SampleCount < 1 ||
            string.IsNullOrWhiteSpace(e.Metric) || string.IsNullOrWhiteSpace(e.Signature) ||
            e.ValidUntil <= e.EvaluatedAt || e.ValidUntil - e.EvaluatedAt > TimeSpan.FromDays(31)))
            throw new ArgumentException("Quality evidence requires bounded, attributable measurements.");
        foreach (var item in this.evidence) _ = Language(item.Language);
        if (this.evidence.Select(e => (e.DatasetSha256, e.Metric, e.Signature, e.Scope)).Distinct().Count() > 1)
            throw new ArgumentException("Quality scores from different evaluation cohorts cannot be ranked together.");
    }

    private static bool Hash(string? value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);
    internal static string Language(string tag)
    {
        if (tag.Length is < 1 or > 85 || tag.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')) throw new ArgumentException("Invalid language metadata.");
        var canonical = CultureInfo.GetCultureInfo(tag).Name.ToLowerInvariant();
        return canonical.Length == 0 ? "und" : canonical;
    }
    public static ModelQualityPolicy? FromEnvironment()
    {
        var file = Environment.GetEnvironmentVariable("LSPP_MODEL_QUALITY_FILE");
        if (string.IsNullOrWhiteSpace(file)) return null;
        if (!Path.IsPathFullyQualified(file) || new FileInfo(file).Length > 4 * 1024 * 1024) throw new ArgumentException("Quality policy requires a bounded operator-owned file.");
        using var data = JsonDocument.Parse(File.ReadAllBytes(file));
        if (data.RootElement.GetProperty("version").GetInt32() != 1) throw new ArgumentException("Unknown quality policy version.");
        var entries = data.RootElement.GetProperty("entries").Deserialize<ModelQualityEvidence[]>(WorkflowJson.Options) ?? throw new ArgumentException("Missing quality evidence.");
        return new(entries, data.RootElement.GetProperty("minimumScore").GetDouble());
    }
    public ModelQualityEvidence? Evidence(string model, string digest, string language, string scope, DateTimeOffset now)
    {
        var tag = Language(language);
        // Lookup truncation is based on BCP-47 metadata; an unknown language never becomes English.
        while (tag.Length != 0)
        {
            var latest = evidence.Where(e => e.Model == model && string.Equals(e.Digest, digest, StringComparison.OrdinalIgnoreCase) &&
                Language(e.Language) == tag && e.Scope == scope && e.EvaluatedAt <= now)
                .OrderByDescending(e => e.EvaluatedAt).FirstOrDefault();
            if (latest is not null) return latest.ValidUntil > now && latest.Score >= minimumScore ? latest : null;
            var separator = tag.LastIndexOf('-');
            tag = separator < 0 ? "" : tag[..separator];
        }
        return null;
    }
}
