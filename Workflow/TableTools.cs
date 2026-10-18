using Microsoft.VisualBasic.FileIO;

namespace wwwmmm.Workflow;

internal static class TableTools
{
    internal static IEnumerable<IWorkflowTool> Create()
    {
        yield return new DelegateTool(new("tables.read-csv", "Parse real UTF-8 CSV with quoted fields and explicit delimiter; no inferred bank sign/currency semantics.", ["files.read"], ["path", "delimiter"], []), async (a, c, token) =>
        {
            var input = await c.Workspace.ReadAsync(WorkflowJson.Text(a, "path"), token); var delimiter = WorkflowJson.Text(a, "delimiter");
            if (delimiter.Length != 1 || delimiter is not ("," or ";" or "\t")) throw new ArgumentException("Explicit comma, semicolon or tab delimiter required.");
            using var reader = new TextFieldParser(new StringReader(WorkspaceScope.Decode(input.Bytes))) { HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false };
            reader.SetDelimiters(delimiter); var columns = reader.ReadFields() ?? throw new ArgumentException("CSV header required.");
            if (columns.Any(string.IsNullOrWhiteSpace) || columns.Distinct(StringComparer.Ordinal).Count() != columns.Length) throw new ArgumentException("Unique nonempty column names required.");
            var rows = new List<Dictionary<string, string>>();
            while (!reader.EndOfData)
            {
                token.ThrowIfCancellationRequested(); var fields = reader.ReadFields()!;
                if (fields.Length != columns.Length) throw new ArgumentException("CSV row width differs from header.");
                rows.Add(columns.Select((name, i) => (name, value: fields[i])).ToDictionary(p => p.name, p => p.value, StringComparer.Ordinal));
                if (rows.Count > 30_000) throw new ArgumentException("Split imports larger than the configured 30,000-row batch budget.");
            }
            return new ToolResult(WorkflowJson.Value(new { columns, rows, interpretation = "RAW_FIELDS_REQUIRE_EXPLICIT_SCHEMA_AND_BANK_CONVENTION" }), [input.Source]);
        });
    }
}
