using wwwmmm.Documents;

namespace wwwmmm.Extraction;

/// <summary>
/// One extracted value with the page it came from and the character offset inside that
/// page. The offset is what lets a reviewer jump to the exact place in the document.
/// </summary>
public sealed record FieldMatch(string RuleId, string Field, string Value, int Page, int Offset);

/// <summary>
/// Result of applying a rule set to one document. <see cref="MissingRequired"/> is not an
/// error: a scanned image with no text layer legitimately produces no fields, and the caller
/// must surface that as "not extracted", never as an empty value.
/// </summary>
public sealed record ExtractionResult(
    string Source,
    string DocumentSha256,
    string RuleSetSha256,
    IReadOnlyList<FieldMatch> Matches,
    IReadOnlyList<string> MissingRequired)
{
    public bool HasAllRequiredFields => MissingRequired.Count == 0;
    public int PageCount { get; init; }
}

/// <summary>Applies rules to page text. No inference, no model, no interpolation.</summary>
public static class FieldExtractor
{
    public static ExtractionResult Apply(DocumentText document, RuleSet ruleSet)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(ruleSet);

        var matches = new List<FieldMatch>();
        var foundRuleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var page in document.Pages)
        {
            foreach (var (rule, pattern) in ruleSet.Compiled)
            {
                foreach (var match in pattern.Matches(page.Text).Where(m => m.Success))
                {
                    var value = match.Groups[rule.Group].Value;
                    if (string.IsNullOrWhiteSpace(value))
                        continue;

                    matches.Add(new FieldMatch(rule.Id, rule.Field, value, page.PageNumber, match.Index));
                    foundRuleIds.Add(rule.Id);
                }
            }
        }

        var missing = ruleSet.Rules
            .Where(rule => rule.Required && !foundRuleIds.Contains(rule.Id))
            .Select(rule => rule.Id)
            .ToArray();

        return new ExtractionResult(
            document.Source,
            document.Sha256,
            ruleSet.Sha256,
            matches.OrderBy(m => m.Page).ThenBy(m => m.Offset).ToArray(),
            missing)
        {
            PageCount = document.PageCount
        };
    }
}
