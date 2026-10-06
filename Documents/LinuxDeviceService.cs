using System.Text.RegularExpressions;

namespace wwwmmm.Documents;

public sealed record PrintSubmission(string JobId, string State);

/// <summary>Uses installed CUPS queues and SANE backends; never invents devices or completed pages.</summary>
public sealed class LinuxDeviceService
{
    public Task<string> ListPrintersAsync(CancellationToken token = default) => RunAsync("lpstat", ["-p", "-d"], token);
    public Task<string> ListScannersAsync(CancellationToken token = default) => RunAsync("scanimage", ["-L"], token);

    public Task<string> GetPrintQueueAsync(string jobId, CancellationToken token = default)
    {
        if (!Regex.IsMatch(jobId ?? "", @"\A[A-Za-z0-9_.-]+-[1-9][0-9]*\z"))
            throw new ArgumentException("Supply the CUPS job identifier returned by submission.");
        // This lists scheduler history, not sensor-confirmed physical output.
        return RunAsync("lpstat", ["-W", "all", "-o", jobId!], token);
    }

    public async Task<PrintSubmission> SubmitPdfAsync(string printer, byte[] pdf, CancellationToken token = default)
    {
        RequireLinux();
        if (!Regex.IsMatch(printer ?? "", @"\A[A-Za-z0-9_][A-Za-z0-9_.-]{0,126}\z"))
            throw new ArgumentException("Supply an explicit CUPS printer queue name.");
        PdfDocumentService.ValidatePdf(pdf);
        // Parse with the real PDF engine before sending malformed or encrypted data to CUPS.
        await new PdfDocumentService().RenderPageAsync(pdf, cancellationToken: token);
        var path = Path.Combine(Path.GetTempPath(), "spp-print-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            await File.WriteAllBytesAsync(path, pdf, token);
            var output = await RunAsync("lp", ["-d", printer!, "-n", "1", "--", path], token);
            var match = Regex.Match(output, @"\brequest id is ([A-Za-z0-9_.-]+-[1-9][0-9]*)\b");
            if (!match.Success)
                throw new DocumentException("CUPS did not return a verifiable job ID. Check the queue before retrying.");
            return new(match.Groups[1].Value, "queued");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    public async Task<byte[]> ScanAsync(string device, int dpi = 300, CancellationToken token = default)
    {
        RequireLinux();
        if (string.IsNullOrWhiteSpace(device) || device.Length > 512 || device.Any(char.IsControl) || dpi is < 75 or > 600)
            throw new ArgumentException("Supply a SANE device identifier and a resolution from 75 to 600 DPI.");
        var directory = Path.Combine(Path.GetTempPath(), "spp-scan-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "scan.png");
            await NativeCommand.RunAsync("scanimage", ["--device-name", device, "--resolution",
                dpi.ToString(System.Globalization.CultureInfo.InvariantCulture), "--format=png", "--output-file", path],
                TimeSpan.FromMinutes(2), token);
            var bytes = await DocumentFiles.ReadAsync(path, 32 * 1024 * 1024, token);
            using var bitmap = PdfDocumentService.DecodeImage(bytes, 32 * 1024 * 1024);
            return bytes;
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static Task<string> RunAsync(string executable, string[] arguments, CancellationToken token)
    {
        RequireLinux();
        return NativeCommand.RunAsync(executable, arguments, TimeSpan.FromSeconds(30), token);
    }

    private static void RequireLinux()
    {
        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("This device adapter requires Linux CUPS/SANE and configured drivers. Windows device support is not implemented.");
    }
}
