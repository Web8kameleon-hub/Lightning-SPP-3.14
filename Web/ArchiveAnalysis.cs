using System.Text;
using System.Text.Json;
using Microsoft.VisualBasic.FileIO;
using wwwmmm.Workflow;

namespace wwwmmm.Web;

internal static class ArchiveAnalysis
{
    internal const int MaxInspectionBytes = 1024 * 1024;
    private static readonly UTF8Encoding Utf8 = new(false, true);

    internal static object Inspect(string path, byte[] bytes)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        var supported = extension is ".json" or ".csv" or ".tsv" or ".txt" or ".md" or ".cs" or ".py" or ".js" or ".ts" or ".tsx" or ".jsx" or ".java" or ".go" or ".rs" or ".cpp" or ".c";
        if (!supported) return new { status = "UNSUPPORTED", module = "none" };
        if (bytes.Length > MaxInspectionBytes) return new { status = "SKIPPED_SIZE_LIMIT", limitBytes = MaxInspectionBytes };
        try
        {
            var text = Utf8.GetString(bytes).TrimStart('\uFEFF');
            if (extension == ".json")
            {
                using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 64 });
                var root = document.RootElement;
                return new { status = "INSPECTED", module = "System.Text.Json", rootType = root.ValueKind.ToString(),
                    topLevelItems = root.ValueKind == JsonValueKind.Array ? root.GetArrayLength() :
                        root.ValueKind == JsonValueKind.Object ? root.EnumerateObject().Count() : 1,
                    schemaValidated = false, duplicatePropertyValidation = false };
            }
            if (extension is ".csv" or ".tsv")
            {
                var delimiter = extension == ".tsv" ? "\t" : ",";
                var (columns, rows) = TableTools.ParseCsv(text, delimiter);
                return new { status = "INSPECTED", module = "tables.read-csv", columnCount = columns.Length,
                    rowCount = rows.Count, delimiter, interpretation = "STRUCTURE_ONLY_NO_BUSINESS_SEMANTICS" };
            }
            var lines = 0;
            using var reader = new StringReader(text);
            while (reader.ReadLine() is not null) lines++;
            return new { status = "INSPECTED", module = "utf8-text", lines, characters = text.Length,
                codeExecuted = false, syntaxValidated = false, semanticAuditPerformed = false };
        }
        catch (Exception ex) when (ex is JsonException or DecoderFallbackException or ArgumentException or MalformedLineException)
        {
            // No source content, exception messages, or snippets in the public report.
            return new { status = "INVALID", module = extension is ".csv" or ".tsv" ? "tables.read-csv" : extension == ".json" ? "System.Text.Json" : "utf8-text", errorType = ex.GetType().Name };
        }
    }
}
