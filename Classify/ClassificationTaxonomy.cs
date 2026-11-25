using System.Text.Json;
using System.Text.RegularExpressions;

namespace wwwmmm.Classify;

/// <summary>
/// Loads and validates a reviewed taxonomy. The default template is a starting point for an
/// operator, not a validated classifier: it must be reviewed against real customer documents
/// before its output is trusted, and that limit is stated wherever the template is used.
/// </summary>
public static partial class ClassificationTaxonomy
{
    public const string EnvironmentVariable = "LSPP_CLASSIFY_TAXONOMY";
    private const int MaxBytes = 1024 * 1024;

    [GeneratedRegex("^[a-z0-9][a-z0-9_-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex CategoryPattern();

    /// <summary>Reads the operator's taxonomy file; every structural problem is an error.</summary>
    public static Taxonomy Load(string path)
    {
        if (!Path.IsPathFullyQualified(path))
            throw new ArgumentException("The taxonomy path must be absolute so the reviewed file cannot be swapped by the working directory.");
        if (!File.Exists(path))
            throw new FileNotFoundException($"Taxonomy file not found: {path}", path);
        var info = new FileInfo(path);
        if (info.Length is 0 or > MaxBytes)
            throw new ArgumentException("The taxonomy file must be between 1 byte and 1 MiB.");
        Taxonomy? taxonomy;
        try { taxonomy = JsonSerializer.Deserialize<Taxonomy>(File.ReadAllBytes(path), ClassifyJson.Options); }
        catch (JsonException ex) { throw new ArgumentException("The taxonomy file is not valid JSON for its schema: " + ex.Message, ex); }
        if (taxonomy is null) throw new ArgumentException("The taxonomy file is empty.");
        return Validate(taxonomy);
    }

    /// <summary>Uses <see cref="EnvironmentVariable"/> when set, otherwise the built-in template.</summary>
    public static Taxonomy FromEnvironment()
    {
        var configured = Environment.GetEnvironmentVariable(EnvironmentVariable);
        return string.IsNullOrWhiteSpace(configured) ? DefaultTemplate() : Load(configured);
    }

    public static Taxonomy Validate(Taxonomy taxonomy)
    {
        ArgumentNullException.ThrowIfNull(taxonomy);
        if (string.IsNullOrWhiteSpace(taxonomy.Name) || taxonomy.Name.Length > 120)
            throw new ArgumentException("The taxonomy needs a name of 1 to 120 characters.");
        if (string.IsNullOrWhiteSpace(taxonomy.Version) || taxonomy.Version.Length > 40)
            throw new ArgumentException("The taxonomy needs a version of 1 to 40 characters.");
        if (taxonomy.Fallback is null || !CategoryPattern().IsMatch(taxonomy.Fallback))
            throw new ArgumentException("The fallback must be 1-64 lowercase letters, digits, underscores or hyphens.");
        if (!double.IsFinite(taxonomy.MinimumScore) || taxonomy.MinimumScore is < 1 or > 1000)
            throw new ArgumentException("MinimumScore must be between 1 and 1000.");
        if (taxonomy.Categories is null || taxonomy.Categories.Length is < 1 or > 64)
            throw new ArgumentException("Supply 1 to 64 categories.");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rule in taxonomy.Categories)
        {
            if (rule is null || rule.Category is null || !CategoryPattern().IsMatch(rule.Category))
                throw new ArgumentException("Every category must be 1-64 lowercase letters, digits, underscores or hyphens.");
            if (!seen.Add(rule.Category))
                throw new ArgumentException($"Duplicate category: {rule.Category}");
            if (rule.Priority is < 0 or > 100)
                throw new ArgumentException($"Category {rule.Category} has a priority outside 0-100.");
            if (rule.Keywords is null || rule.FileNameHints is null)
                throw new ArgumentException($"Category {rule.Category} must declare both arrays, even when empty.");
            if (rule.Keywords.Length == 0 && rule.FileNameHints.Length == 0)
                throw new ArgumentException($"Category {rule.Category} has no rules and could never be selected.");
            ValidateTerms(rule.Keywords, rule.Category, "keyword");
            ValidateTerms(rule.FileNameHints, rule.Category, "file-name hint");
        }
        return taxonomy;
    }

    private static void ValidateTerms(string[] terms, string category, string kind)
    {
        if (terms.Length > 200)
            throw new ArgumentException($"Category {category} declares more than 200 {kind}s.");
        if (terms.Distinct(StringComparer.OrdinalIgnoreCase).Count() != terms.Length)
            throw new ArgumentException($"Category {category} has duplicate {kind}s.");
        foreach (var term in terms)
            if (string.IsNullOrWhiteSpace(term) || term.Trim().Length > 120 || term.Any(char.IsControl))
                throw new ArgumentException($"Category {category} has a {kind} outside 1-120 printable characters.");
            else if (term != term.Trim()) throw new ArgumentException($"Category {category} has an untrimmed {kind}.");
    }

    /// <summary>
    /// A generic multi-lingual business template (English, German, Albanian). It is deliberately
    /// conservative and small: an operator replaces it with categories reviewed for their own work.
    /// </summary>
    public static Taxonomy DefaultTemplate() => new(
        "generic-business-template",
        "1",
        "unclassified",
        MinimumScore: 2,
        Categories:
        [
            Rule("invoice", "Billing document requesting payment",
                ["invoice", "rechnung", "fattur", "amount due", "zahlbar", "invoice number", "rechnungsnummer", "total due"],
                ["invoice", "rechnung", "fattur"], 10),
            Rule("bank-statement", "Account statement from a bank",
                ["bank statement", "kontoauszug", "account statement", "iban", "bic", "opening balance", "closing balance"],
                ["statement", "kontoauszug", "bank"], 10),
            Rule("contract", "Signed agreement between parties",
                ["contract", "vertrag", "kontrat", "hereby agree", "terms and conditions", "signature", "effective date"],
                ["contract", "vertrag", "kontrat"], 10),
            Rule("id-document", "Identity or registration document",
                ["identity card", "personalausweis", "passport", "reisepass", "date of birth", "place of birth", "document number"],
                ["passport", "id", "ausweis", "leternjohjes"], 10),
            Rule("tax-document", "Tax assessment or filing",
                ["tax", "steuer", "taksa", "tax id", "steuernummer", "vat", "ust", "fiscal year"],
                ["tax", "steuer", "taksa"], 10),
            Rule("delivery-note", "Goods delivery paperwork",
                ["delivery note", "lieferschein", "fletedergesa", "goods received", "shipped", "delivery date"],
                ["delivery", "lieferschein", "dorezim"], 10),
            Rule("purchase-order", "Order placed on a supplier",
                ["purchase order", "bestellung", "porosi", "order number", "supplier", "lieferant"],
                ["order", "bestellung", "porosi"], 10),
            Rule("report", "Analysis or status report",
                ["report", "bericht", "raport", "summary", "findings", "conclusion", "prepared by"],
                ["report", "bericht", "raport"], 5),
            Rule("letter", "General correspondence",
                ["dear sir", "dear madam", "sehr geehrte", "i nderuar", "sincerely", "kind regards", "mit freundlichen"],
                ["letter", "brief"], 5)
        ]);

    private static CategoryRule Rule(string category, string description, string[] keywords,
        string[] hints, int priority) => new(category, description, keywords, hints, priority);
}
