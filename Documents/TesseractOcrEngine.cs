using System.Diagnostics;

namespace wwwmmm.Documents;

/// <summary>
/// Real Tesseract OCR. Each page image is written to a temporary file and Tesseract's own
/// stdout is returned verbatim — no cleaning, no guessing. An empty result means the engine
/// recognised nothing on that page, which the caller must treat as "no data", exactly like
/// an empty PDF text layer.
/// </summary>
public sealed class TesseractOcrEngine : IOcrEngine
{
    public const string LanguagesVariable = "SPP_OCR_LANGUAGES";
    private const string DefaultLanguages = "eng";
    private const int TimeoutSeconds = 120;

    private readonly OcrLocation? _location;

    /// <summary>Pass a resolved location to skip discovery; null resolves on each call.</summary>
    public TesseractOcrEngine(OcrLocation? location = null) => _location = location;

    public static bool IsAvailable() => OcrLocator.TryLocate() is not null;

    public async Task<string> ReadAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);
        if (imageBytes.Length == 0)
            throw new ArgumentException("OCR input image is empty.", nameof(imageBytes));

        var location = _location ?? OcrLocator.Locate();
        var directory = Path.Combine(Path.GetTempPath(), "spp-ocr-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var input = Path.Combine(directory, "page.png");
            await File.WriteAllBytesAsync(input, imageBytes, cancellationToken);

            var startInfo = new ProcessStartInfo(location.Path)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add(input);
            startInfo.ArgumentList.Add("stdout");
            startInfo.ArgumentList.Add("-l");
            startInfo.ArgumentList.Add(Languages());
            startInfo.ArgumentList.Add("--dpi");
            startInfo.ArgumentList.Add("300");

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Failed to start {location.Path}.");

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
                throw new TimeoutException($"Tesseract did not finish within {TimeoutSeconds}s.");
            }

            var text = await stdout;
            var error = await stderr;

            if (process.ExitCode != 0)
                throw new DocumentException($"Tesseract exited with {process.ExitCode}: {error.Trim()}");

            return text;
        }
        finally
        {
            TryDelete(directory);
        }
    }

    private static string Languages()
    {
        var configured = Environment.GetEnvironmentVariable(LanguagesVariable);
        return string.IsNullOrWhiteSpace(configured) ? DefaultLanguages : configured.Trim();
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
            // Already gone; nothing to clean up.
        }
    }

    private static void TryDelete(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
            // Temporary OCR workspace; leaving it behind must not fail the page.
        }
    }
}
