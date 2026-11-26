using System.Text.Json;
using wwwmmm.Classify;
using wwwmmm.Documents;

namespace wwwmmm.Workflow;

internal static class ClassificationTools
{
    internal static IEnumerable<IWorkflowTool> Create()
    {
        yield return new DelegateTool(new("documents.classify", "Apply a supplied reviewed JSON taxonomy to actual UTF-8/PDF/image text and persist source-referenced rule evidence. Scores are matched-rule counts, not AI probabilities.",
            ["files.read", "reports.write"], ["path", "taxonomy"], [], Dependency: "Poppler for PDF; enabled Tesseract for scanned documents"), Classify);
    }

    private static async Task<ToolResult> Classify(JsonElement arguments, ToolContext context, CancellationToken token)
    {
        var input = await context.Workspace.ReadAsync(WorkflowJson.Text(arguments, "path"), token);
        var taxonomyFile = await context.Workspace.ReadAsync(WorkflowJson.Text(arguments, "taxonomy"), token);
        if (taxonomyFile.Bytes.Length > 1024 * 1024) throw new ArgumentException("Taxonomy exceeds 1 MiB.");
        var taxonomy = JsonSerializer.Deserialize<Taxonomy>(taxonomyFile.Bytes, ClassifyJson.Options) ?? throw new ArgumentException("A reviewed taxonomy is required.");
        var classifier = new DocumentClassifier(taxonomy);
        var extension = Path.GetExtension(input.Source.Path).ToLowerInvariant();
        string text; string textSource; var extractionTruncated = false;
        if (extension == ".pdf")
        {
            var extraction = await FileTools.Create().Single(tool => tool.Contract.Id == "documents.pdf-text").ExecuteAsync(
                WorkflowJson.Value(new { path = input.Source.Path }), context, token);
            if (extraction.Sources.Any(source => source.Sha256 != input.Source.Sha256)) throw new IOException("Document changed during classification.");
            text = string.Join('\n', extraction.Data.GetProperty("pages").EnumerateArray().Select(page => page.GetProperty("text").GetString()));
            textSource = extraction.Data.GetProperty("ocrAttempted").GetBoolean() ? "ocr" : "text-layer";
            extractionTruncated = extraction.Data.GetProperty("truncated").GetBoolean();
        }
        else if (extension is ".png" or ".jpg" or ".jpeg" or ".tif" or ".tiff" or ".bmp")
        {
            if (!OcrTextReader.IsEnabled) throw new InvalidOperationException("Image classification requires explicitly enabled OCR.");
            using var bitmap = PdfDocumentService.DecodeImage(input.Bytes);
            text = await new TesseractOcrEngine().ReadAsync(input.Bytes, token);
            textSource = "ocr";
        }
        else if (extension is ".txt" or ".csv" or ".json" or ".md")
        { text = WorkspaceScope.Decode(input.Bytes); textSource = "utf8"; }
        else throw new ArgumentException("Supported classification sources: UTF-8 txt/csv/json/md, PDF and raster images.");
        if (string.IsNullOrWhiteSpace(text)) textSource = "none";
        var result = classifier.Classify(Path.GetFileName(input.Source.Path), text);
        var data = WorkflowJson.Value(new { schemaVersion = 1, path = input.Source.Path, sha256 = input.Source.Sha256,
            taxonomy = new { taxonomy.Name, taxonomy.Version, sha256 = taxonomyFile.Source.Sha256 }, result.Category, result.Score,
            result.IsFallback, result.Evidence, result.RunnerUp, result.RunnerUpScore, textSource,
            truncated = extractionTruncated || text.Length > DocumentClassifier.MaxScanCharacters,
            requiresReview = true, method = "reviewed-taxonomy-rule-match" });
        var sources = new[] { input.Source, taxonomyFile.Source };
        var report = await FileTools.Create().Single(tool => tool.Contract.Id == "reports.export").ExecuteAsync(
            WorkflowJson.Value(new { format = "json", data }), context with { DependencySources = sources }, token);
        var output = JsonSerializer.SerializeToNode(data)!.AsObject();
        output["evidencePath"] = report.Data.GetProperty("path").GetString();
        return new(JsonSerializer.SerializeToElement(output), sources, "RULE_MATCH_REQUIRES_REVIEW");
    }
}
