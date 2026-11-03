using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace wwwmmm.Documents;

/// <summary>Text of one PDF page, as produced by the PDF's own text layer.</summary>
public sealed record PageText(int PageNumber, string Text);

/// <summary>
/// The text layer of a document plus the hash of the exact bytes it came from, so any
/// extracted field can be traced back to a specific file and page.
/// </summary>
public sealed record DocumentText(string Source, string Sha256, IReadOnlyList<PageText> Pages)
{
    public int PageCount => Pages.Count;
}

/// <summary>
/// Reads the text layer of a PDF through Poppler's <c>pdftotext</c>. This is deterministic
/// text extraction, not OCR: a scanned image without a text layer yields no text and the
/// caller must treat that as "no data", never as an empty value.
/// </summary>
public static class TextLayerExtractor
{
    private const string PdfToTextEnv = "SPP_PDFTOTEXT_PATH";
    private const int TimeoutSeconds = 120;

    public static async Task<DocumentText> ExtractAsync(string pdfPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(pdfPath))
            throw new FileNotFoundException($"PDF not found: {pdfPath}", pdfPath);

        if (!string.Equals(Path.GetExtension(pdfPath), ".pdf", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Text-layer extraction requires a .pdf input. Render images first or supply a PDF.");

        var binary = ResolvePdfToText();
        var sha256 = await ComputeSha256Async(pdfPath, cancellationToken);

        var startInfo = new ProcessStartInfo(binary)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-layout");
        startInfo.ArgumentList.Add("-enc");
        startInfo.ArgumentList.Add("UTF-8");
        startInfo.ArgumentList.Add(pdfPath);
        startInfo.ArgumentList.Add("-");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start {binary}.");

        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            if (cancellationToken.IsCancellationRequested) throw;
            throw new TimeoutException($"pdftotext did not finish within {TimeoutSeconds}s for {pdfPath}.");
        }

        var text = await stdout;
        var error = await stderr;

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"pdftotext exited with {process.ExitCode}: {error.Trim()}");

        return new DocumentText(pdfPath, sha256, SplitPages(text));
    }

    private static IReadOnlyList<PageText> SplitPages(string text)
    {
        var raw = text.Split('\f');
        var pages = new List<PageText>(raw.Length);
        for (var i = 0; i < raw.Length; i++)
        {
            // pdftotext terminates the last page with a form feed, producing a trailing empty part.
            if (i == raw.Length - 1 && raw[i].Length == 0)
                break;
            pages.Add(new PageText(i + 1, raw[i]));
        }
        return pages;
    }

    private static string ResolvePdfToText()
    {
        var configured = Environment.GetEnvironmentVariable(PdfToTextEnv);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (!File.Exists(configured))
                throw new FileNotFoundException($"{PdfToTextEnv} points to a missing file: {configured}", configured);
            return configured;
        }

        return "pdftotext";
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Process already gone; nothing to clean up.
        }
    }
}
