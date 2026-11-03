using wwwmmm.Documents;
using wwwmmm.Extraction;
using wwwmmm.Intake;
using wwwmmm.Provenance;
using Xunit;

namespace LightningSPP.Tests;

public class FlowExtractionTests
{
    private const string Rules = """
        {
          "version": 1,
          "rules": [
            { "id": "total", "field": "invoice_total", "pattern": "Total\\s*:\\s*([0-9]+\\.[0-9]{2})", "required": true },
            { "id": "date", "field": "invoice_date", "pattern": "Date\\s*:\\s*(\\d{4}-\\d{2}-\\d{2})", "required": true }
          ]
        }
        """;

    [Fact]
    public void RuleSet_RejectsDuplicateIds()
    {
        var json = """
            { "version": 1, "rules": [
              { "id": "dup", "field": "a", "pattern": "(x)" },
              { "id": "DUP", "field": "b", "pattern": "(y)" } ] }
            """;

        var error = Assert.Throws<InvalidOperationException>(() => RuleSet.Parse(json));
        Assert.Contains("Duplicate rule id", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RuleSet_RejectsGroupThatPatternDoesNotDefine()
    {
        var json = """{ "version": 1, "rules": [ { "id": "one", "field": "a", "pattern": "(x)", "group": 3 } ] }""";
        var error = Assert.Throws<InvalidOperationException>(() => RuleSet.Parse(json));
        Assert.Contains("group 3", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Extractor_ReturnsValueWithPageAndOffset()
    {
        var rules = RuleSet.Parse(Rules);
        var document = new DocumentText(
            "invoice.pdf",
            "abc123",
            new[]
            {
                new PageText(1, "INVOICE\nDate: 2026-03-14\nCustomer: ACME\n"),
                new PageText(2, "Total: 1299.00\nVAT: 19%\n")
            });

        var result = FieldExtractor.Apply(document, rules);

        Assert.True(result.HasAllRequiredFields);
        Assert.Equal(2, result.PageCount);

        var total = Assert.Single(result.Matches, m => m.Field == "invoice_total");
        Assert.Equal("1299.00", total.Value);
        Assert.Equal(2, total.Page);
        Assert.Equal(0, total.Offset);

        var date = Assert.Single(result.Matches, m => m.Field == "invoice_date");
        Assert.Equal("2026-03-14", date.Value);
        Assert.Equal(1, date.Page);
        Assert.Equal("date", date.RuleId);
    }

    [Fact]
    public void Extractor_ReportsMissingRequiredInsteadOfEmptyValue()
    {
        var rules = RuleSet.Parse(Rules);
        var document = new DocumentText("scan.pdf", "def456", new[] { new PageText(1, "Total: 10.00\n") });

        var result = FieldExtractor.Apply(document, rules);

        Assert.False(result.HasAllRequiredFields);
        Assert.Equal(new[] { "date" }, result.MissingRequired);
        Assert.DoesNotContain(result.Matches, m => m.Field == "invoice_date");
    }

    [Fact]
    public void Extractor_OnEmptyTextLayerProducesNoFields()
    {
        var rules = RuleSet.Parse(Rules);
        var document = new DocumentText("scan.pdf", "fff", new[] { new PageText(1, string.Empty) });

        var result = FieldExtractor.Apply(document, rules);

        Assert.Empty(result.Matches);
        Assert.Equal(2, result.MissingRequired.Count);
    }

    [Fact]
    public async Task Trail_ChainsRecordsAndDetectsTampering()
    {
        var directory = Path.Combine(Path.GetTempPath(), "spp-trail-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "trail.jsonl");

        try
        {
            var trail = ProvenanceTrail.Open(path);
            await trail.AppendExtractionAsync("doc-sha", "rules-sha", "total", "invoice_total", "1299.00", 2, 0);
            await trail.AppendExtractionAsync("doc-sha", "rules-sha", "date", "invoice_date", "2026-03-14", 1, 7);
            await trail.AppendReviewAsync("doc-sha", "total", "invoice_total", "1299.00", 2, 0, "reviewer@example.com", ReviewDecision.Accepted);

            var valid = await trail.VerifyAsync();
            Assert.True(valid.IsValid, valid.Error);
            Assert.Equal(3, valid.RecordCount);

            var records = await trail.LoadAsync();
            Assert.Equal("reviewer@example.com", records[2].Reviewer);
            Assert.Equal(nameof(ReviewDecision.Accepted), records[2].Decision);
            Assert.Equal(records[0].Hash, records[1].PreviousHash);
            Assert.Equal(records[1].Hash, records[2].PreviousHash);

            // Tamper with the recorded value; the chain must reject the file.
            var lines = await File.ReadAllLinesAsync(path);
            var original = lines[0];
            lines[0] = lines[0].Replace("\"1299.00\"", "\"1.00\"", StringComparison.Ordinal);
            Assert.NotEqual(original, lines[0]);
            await File.WriteAllLinesAsync(path, lines);

            var tampered = await trail.VerifyAsync();
            Assert.False(tampered.IsValid);
            Assert.NotNull(tampered.Error);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Intake_EnumeratesAcceptedExtensionsInOrdinalOrder()
    {
        var directory = Path.Combine(Path.GetTempPath(), "spp-intake-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            File.WriteAllText(Path.Combine(directory, "b.png"), "x");
            File.WriteAllText(Path.Combine(directory, "a.pdf"), "x");
            File.WriteAllText(Path.Combine(directory, "notes.txt"), "x");

            var files = IntakeEnumerator.Enumerate(IntakeSource.FromPath(directory));

            Assert.Equal(2, files.Count);
            Assert.EndsWith("a.pdf", files[0], StringComparison.Ordinal);
            Assert.EndsWith("b.png", files[1], StringComparison.Ordinal);
            Assert.False(IntakeEnumerator.IsAccepted("notes.txt"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ScanProviders_ProbeWithoutThrowingAndNeverClaimTwain()
    {
        var providers = ScanProviderRegistry.Probe();

        Assert.Contains(providers, p => p.Name == ScanProviderRegistry.SaneProvider);
        Assert.Contains(providers, p => p.Name == ScanProviderRegistry.WiaProvider);

        var twain = Assert.Single(providers, p => p.Name == ScanProviderRegistry.TwainProvider);
        Assert.False(twain.Available);
        Assert.Contains("not implemented", twain.Detail, StringComparison.OrdinalIgnoreCase);

        foreach (var provider in providers)
            Assert.False(string.IsNullOrWhiteSpace(provider.Detail));
    }
}
