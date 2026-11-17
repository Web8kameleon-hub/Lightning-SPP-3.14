using System.Text.Json;
using System.Text.RegularExpressions;
using wwwmmm.Documents;

namespace wwwmmm.Workflow;

internal sealed class DelegateTool(ToolContract contract, Func<JsonElement, ToolContext, CancellationToken, Task<ToolResult>> execute) : IWorkflowTool
{
    public ToolContract Contract => contract;
    public Task<ToolResult> ExecuteAsync(JsonElement arguments, ToolContext context, CancellationToken cancellationToken) => execute(arguments, context, cancellationToken);
}
internal static class FileTools
{
    internal static IEnumerable<IWorkflowTool> Create()
    {
        yield return new DelegateTool(new("files.list", "List scoped files with pagination; links and protected paths excluded.", ["files.read"], [], ["directory", "offset", "limit"]), List);
        yield return new DelegateTool(new("files.read", "Read actual UTF-8 text, retain immutable bytes and source hash.", ["files.read"], ["path"], []), Read);
        yield return new DelegateTool(new("files.search", "Literal search with line references, no inferred semantics.", ["files.read"], ["path", "query"], []), Search);
        yield return new DelegateTool(new("files.compare", "Compare two real text sources with changed line evidence.", ["files.read"], ["left", "right"], []), Compare);
        yield return new DelegateTool(new("code.audit-patterns", "Find customer-supplied candidate patterns; findings require semantic review.", ["files.read"], ["path", "patterns"], []), Audit);
        yield return new DelegateTool(new("documents.pdf-text", "Extract PDF text with Poppler; OCR textless PDFs only when LSPP_OCR_ENABLED=true, up to 32 pages.", ["files.read"], ["path"], [], Dependency: "pdftotext"), Pdf);
        yield return new DelegateTool(new("reports.export", "Persist a source-referenced JSON, CSV or PDF report without changing originals.", ["reports.write"], ["data", "format"], []), Report);
        yield return new DelegateTool(new("documents.file-classified", "File a metadata reference under reports/classified/<category>/<sha256>/ to the one canonical source snapshot. No document payload copy is created. Requires a reviewed category and expected source hash.", ["files.read", "reports.write"], ["path", "category", "sha256"], []), FileClassified);
    }
    private static async Task<ToolResult> FileClassified(JsonElement a, ToolContext c, CancellationToken token)
    {
        var category = WorkflowJson.Text(a, "category");
        if (!Regex.IsMatch(category, "^[a-z0-9][a-z0-9_-]{0,63}$", RegexOptions.CultureInvariant))
            throw new ArgumentException("Category must contain 1–64 lowercase ASCII letters, digits, underscores or hyphens.");
        var input = await c.Workspace.ReadAsync(WorkflowJson.Text(a, "path"), token);
        if (!string.Equals(input.Source.Sha256, WorkflowJson.Text(a, "sha256"), StringComparison.Ordinal))
            throw new IOException("Document changed since classification review; review it again before filing.");
        var filename = Path.GetFileName(input.Source.Path);
        var relative = $"reports/classified/{category}/{input.Source.Sha256}/{filename}.reference.json";
        var referenceBytes = JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, documentPath = input.Source.Path,
            sha256 = input.Source.Sha256, canonicalSource = "workspace-original:" + input.Source.Sha256,
            category, classification = "REQUIRES_REVIEW" }, WorkflowJson.Options);
        var destination = c.Workspace.Resolve(relative, false);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        WorkspaceScope.CheckAncestors(Path.GetDirectoryName(destination)!);
        WorkspaceScope.PrivateDirectory(Path.GetDirectoryName(destination)!);
        token.ThrowIfCancellationRequested();
        // Publish only a complete private snapshot, without overwriting a conflicting destination.
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await WorkspaceScope.AtomicWriteAsync(temporary, referenceBytes, token);
            token.ThrowIfCancellationRequested();
            File.Move(temporary, destination, overwrite: false);
        }
        catch (IOException) when (File.Exists(destination))
        {
            if (new FileInfo(destination).Length != referenceBytes.Length ||
                !(await File.ReadAllBytesAsync(destination, token)).AsSpan().SequenceEqual(referenceBytes))
                throw new IOException("Classified destination differs; refusing to overwrite it.");
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        if (!(await File.ReadAllBytesAsync(destination, token)).AsSpan().SequenceEqual(referenceBytes)) throw new IOException("Classified reference integrity failure.");
        return new(WorkflowJson.Value(new { path = relative, documentPath = input.Source.Path, category, sha256 = input.Source.Sha256,
            originalRetained = true, payloadCopied = false, canonicalSource = "workspace-original:" + input.Source.Sha256, classification = "REQUIRES_REVIEW" }), [input.Source]);
    }
    private static Task<ToolResult> List(JsonElement a, ToolContext c, CancellationToken token)
    {
        var root = a.TryGetProperty("directory", out var directory) ? c.Workspace.Resolve(directory.GetString()!) : c.Workspace.Root;
        var offset = a.TryGetProperty("offset", out var start) ? start.GetInt32() : 0;
        var limit = a.TryGetProperty("limit", out var count) ? count.GetInt32() : 100;
        if (offset < 0 || limit is < 1 or > 1000) throw new ArgumentException("Invalid listing page.");
        var files = new List<string>(); var queue = new Stack<string>(); queue.Push(root); var skipped = 0; var more = false;
        while (!more && queue.TryPop(out var path))
        {
            token.ThrowIfCancellationRequested();
            foreach (var item in Directory.EnumerateFileSystemEntries(path).Order(StringComparer.Ordinal))
            {
                var relative = Path.GetRelativePath(c.Workspace.Root, item).Replace('\\', '/');
                try { _ = c.Workspace.Resolve(relative); } catch (UnauthorizedAccessException) { continue; }
                if (Directory.Exists(item)) queue.Push(item);
                else if (skipped++ >= offset)
                { if (files.Count == limit) { more = true; break; } files.Add(WorkspaceScope.CanonicalReference(relative) ? relative[..^".reference.json".Length] : relative); }
            }
        }
        return Task.FromResult(new ToolResult(WorkflowJson.Value(new { files, offset, nextOffset = more ? (int?)(offset + files.Count) : null }), []));
    }
    private static async Task<ToolResult> Read(JsonElement a, ToolContext c, CancellationToken token)
    {
        var input = await c.Workspace.ReadAsync(WorkflowJson.Text(a, "path"), token);
        return new(WorkflowJson.Value(new { text = WorkspaceScope.Decode(input.Bytes) }), [input.Source]);
    }
    private static async Task<ToolResult> Search(JsonElement a, ToolContext c, CancellationToken token)
    {
        var input = await c.Workspace.ReadAsync(WorkflowJson.Text(a, "path"), token); var query = WorkflowJson.Text(a, "query");
        var matches = new List<(int Line, string Text)>(); var line = 0; var truncated = false;
        using var reader = new StringReader(WorkspaceScope.Decode(input.Bytes)); string? text;
        while ((text = reader.ReadLine()) is not null)
        {
            token.ThrowIfCancellationRequested(); line++;
            if (!text.Contains(query, StringComparison.Ordinal)) continue;
            if (matches.Count == 1000) { truncated = true; break; }
            matches.Add((line, text));
        }
        return new(WorkflowJson.Value(new { matches = matches.Select(m => new { line = m.Line, text = m.Text }), truncated }), matches.Select(m => input.Source with { Line = m.Line }).ToArray());
    }
    private static async Task<ToolResult> Compare(JsonElement a, ToolContext c, CancellationToken token)
    {
        var left = await c.Workspace.ReadAsync(WorkflowJson.Text(a, "left"), token); var right = await c.Workspace.ReadAsync(WorkflowJson.Text(a, "right"), token);
        var l = WorkspaceScope.Decode(left.Bytes).Split('\n'); var r = WorkspaceScope.Decode(right.Bytes).Split('\n');
        var changed = Enumerable.Range(0, Math.Max(l.Length, r.Length)).Where(i => l.ElementAtOrDefault(i) != r.ElementAtOrDefault(i)).Take(1001).Select(i => new { line = i + 1, left = l.ElementAtOrDefault(i), right = r.ElementAtOrDefault(i) }).ToArray();
        return new(WorkflowJson.Value(new { equalBytes = left.Source.Sha256 == right.Source.Sha256, changed = changed.Take(1000), truncated = changed.Length > 1000 }), [left.Source, right.Source]);
    }
    private static async Task<ToolResult> Audit(JsonElement a, ToolContext c, CancellationToken token)
    {
        var input = await c.Workspace.ReadAsync(WorkflowJson.Text(a, "path"), token);
        var patterns = a.GetProperty("patterns").EnumerateArray().Select(p => p.GetString() ?? throw new ArgumentException("Pattern string required.")).ToArray();
        if (patterns.Length is < 1 or > 64 || patterns.Any(p => p.Length is < 1 or > 500)) throw new ArgumentException("Supply 1–64 bounded patterns.");
        var findings = new List<object>(); var sources = new List<SourceReference>();
        var lines = WorkspaceScope.Decode(input.Bytes).Split('\n');
        foreach (var pattern in patterns)
        {
            var regex = new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
            for (var i = 0; i < lines.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                if (!regex.IsMatch(lines[i])) continue;
                if (findings.Count == 1000) return new(WorkflowJson.Value(new { findings, truncated = true, semanticAuditPerformed = false, testOrProductionClassification = "UNVERIFIED" }), sources.ToArray(), "CANDIDATE");
                findings.Add(new { line = i + 1, pattern, text = lines[i], state = "CANDIDATE_REQUIRES_REVIEW" }); sources.Add(input.Source with { Line = i + 1 });
            }
        }
        return new(WorkflowJson.Value(new { findings, truncated = false, semanticAuditPerformed = false, testOrProductionClassification = "UNVERIFIED" }), sources.ToArray(), "CANDIDATE");
    }
    private static async Task<ToolResult> Pdf(JsonElement a, ToolContext c, CancellationToken token)
    {
        var input = await c.Workspace.ReadAsync(WorkflowJson.Text(a, "path"), token); PdfDocumentService.ValidatePdf(input.Bytes);
        var directory = Path.Combine(Path.GetTempPath(), "lightning-pdf-text-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory); WorkspaceScope.PrivateDirectory(directory);
        try
        {
            var pdf = Path.Combine(directory, "original.pdf"); await WorkspaceScope.AtomicWriteAsync(pdf, input.Bytes, token);
            var document = await OcrTextReader.ReadAsync(pdf, token);
            var pages = document.Pages.Select(page => new { page = page.PageNumber, text = page.Text }).ToArray();
            var hasText = pages.Any(page => !string.IsNullOrWhiteSpace(page.text));
            var truncated = pages.Length < document.TotalPageCount;
            var ocrAttempted = document.TextSource == DocumentText.OcrSource;
            var extraction = ocrAttempted ? "ocr" : "native_text";
            return new(WorkflowJson.Value(new { pages, extraction, ocrAttempted, truncated, requiresOcr = !hasText || truncated }), pages.Select(page => input.Source with { Page = page.page }).ToArray());
        }
        finally { Directory.Delete(directory, true); }
    }
    private static async Task<ToolResult> Report(JsonElement a, ToolContext c, CancellationToken token)
    {
        var format = WorkflowJson.Text(a, "format"); var data = a.GetProperty("data"); string text;
        var sources = c.DependencySources?.ToArray() ?? [];
        if (format is "json" or "pdf") text = JsonSerializer.Serialize(new { data, sources }, WorkflowJson.Options);
        else if (format == "csv")
        {
            if (data.ValueKind != JsonValueKind.Array) throw new ArgumentException("CSV data must be an array of objects.");
            var rows = data.EnumerateArray().ToArray(); if (rows.Any(r => r.ValueKind != JsonValueKind.Object)) throw new ArgumentException("CSV rows must be objects.");
            var headers = rows.SelectMany(r => r.EnumerateObject().Select(p => p.Name)).Distinct().ToArray();
            static string Cell(string s) => "\"" + ((s.Length > 0 && "=+-@\t\r".Contains(s[0])) ? "'" + s : s).Replace("\"", "\"\"") + "\"";
            text = string.Join(',', headers.Select(Cell)) + "\r\n" + string.Join("\r\n", rows.Select(row => string.Join(',', headers.Select(h => Cell(row.TryGetProperty(h, out var v) ? v.ToString() : "")))));
        }
        else throw new ArgumentException("Supported report formats: json, csv, pdf.");
        var relative = "reports/" + c.JobId + "-" + c.StepId + "." + format;
        var bytes = format == "pdf" ? ReportPdf(text) : System.Text.Encoding.UTF8.GetBytes(text); var path = c.Workspace.Resolve(relative, false);
        if (File.Exists(path) && WorkflowJson.Hash(await File.ReadAllBytesAsync(path, token)) != WorkflowJson.Hash(bytes)) throw new IOException("Report output changed; refusing to overwrite.");
        await WorkspaceScope.AtomicWriteAsync(path, bytes, token);
        await WorkspaceScope.AtomicWriteAsync(path + ".evidence.json", JsonSerializer.SerializeToUtf8Bytes(new { report = relative, sha256 = WorkflowJson.Hash(bytes), sources }, WorkflowJson.Options), token);
        return new(WorkflowJson.Value(new { path = relative, evidencePath = relative + ".evidence.json", bytes = bytes.Length, sha256 = WorkflowJson.Hash(bytes) }), sources);
    }
    // SkiaSharp.NativeAssets.Linux.NoDependencies has no fontconfig: family lookups return a glyph-less
    // typeface and would silently produce a PDF without text. Prefer explicit font files and fail closed.
    private static readonly string[] ReportFontFiles =
    [
        "/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf",
        "/usr/share/fonts/dejavu/DejaVuSansMono.ttf",
        "/usr/share/fonts/TTF/DejaVuSansMono.ttf",
        // Fedora, RHEL and Rocky package the family under its own directory, which is
        // the layout of the production host; without it a release built there reports
        // "no report font" even though the package is installed.
        "/usr/share/fonts/dejavu-sans-mono-fonts/DejaVuSansMono.ttf",
        "/System/Library/Fonts/Menlo.ttc",
        @"C:\Windows\Fonts\consola.ttf",
    ];
    private static SkiaSharp.SKTypeface ReportTypeface()
    {
        var configured = Environment.GetEnvironmentVariable("LSPP_REPORT_FONT_FILE");
        var candidates = string.IsNullOrWhiteSpace(configured) ? ReportFontFiles : [configured, .. ReportFontFiles];
        foreach (var file in candidates.Where(File.Exists))
        {
            var typeface = SkiaSharp.SKTypeface.FromFile(file);
            if (Usable(typeface)) return typeface!;
            typeface?.Dispose();
        }
        var family = SkiaSharp.SKTypeface.FromFamilyName("monospace");
        if (Usable(family)) return family!;
        family?.Dispose();
        throw new IOException("No report font with the required glyphs is available. Install fonts-dejavu-core (Debian) or dejavu-sans-mono-fonts (Fedora/RHEL/Rocky), or set LSPP_REPORT_FONT_FILE.");
        static bool Usable(SkiaSharp.SKTypeface? typeface)
        {
            if (typeface is null || typeface.GlyphCount == 0) return false;
            using var font = new SkiaSharp.SKFont(typeface);
            // Probe printable ASCII (JSON structure, digits, Latin) so a real font is required without
            // rejecting reports that contain scripts the chosen font lacks.
            return font.ContainsGlyphs(" !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~");
        }
    }
    private static byte[] ReportPdf(string text)
    {
        if (text.Length > 250_000) throw new ArgumentException("Split large PDF reports into source-referenced volumes.");
        using var output = new MemoryStream(); using var document = SkiaSharp.SKDocument.CreatePdf(output) ?? throw new IOException("PDF writer unavailable.");
        using var typeface = ReportTypeface();
        using var font = new SkiaSharp.SKFont(typeface, 10); using var paint = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColors.Black, IsAntialias = true };
        SkiaSharp.SKCanvas? page = null; var row = 0;
        foreach (var line in text.Split('\n').SelectMany(line => line.Length == 0 ? [""] : Enumerable.Range(0, (line.Length + 89) / 90).Select(i => line.Substring(i * 90, Math.Min(90, line.Length - i * 90)))))
        {
            if (row == 0) { page = document.BeginPage(595, 842); page.Clear(SkiaSharp.SKColors.White); }
            page!.DrawText(line, 30, 35 + row * 13, SkiaSharp.SKTextAlign.Left, font, paint);
            if (++row == 59) { document.EndPage(); row = 0; }
        }
        if (row > 0) document.EndPage(); document.Close(); return output.ToArray();
    }
}
