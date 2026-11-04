using Microsoft.Win32;
using wwwmmm.Documents;

namespace wwwmmm.Doctor;

/// <summary>One printer queue that exists on this host, with where the name came from.</summary>
public sealed record PrinterInfo(string Name, string Source);

public sealed record PrinterProbeResult(IReadOnlyList<PrinterInfo> Printers, string Note);

/// <summary>
/// Lists print queues that actually exist. On Linux it asks CUPS; on Windows it reads the
/// spooler's own registry key. It never invents a queue, and an empty list carries the reason.
/// </summary>
public static class PrinterProbe
{
    public static async Task<PrinterProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        if (OperatingSystem.IsLinux())
            return await ProbeCupsAsync(cancellationToken);
        if (OperatingSystem.IsWindows())
            return ProbeWindowsSpooler();
        return new PrinterProbeResult(Array.Empty<PrinterInfo>(),
            "No print-queue probe is implemented for this platform.");
    }

    private static async Task<PrinterProbeResult> ProbeCupsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var output = await new LinuxDeviceService().ListPrintersAsync(cancellationToken);
            var printers = ParseCupsPrinters(output);
            return new PrinterProbeResult(printers, printers.Count == 0
                ? "CUPS answered but listed no queues (lpstat -p -d)."
                : "Read from CUPS with lpstat -p -d.");
        }
        catch (Exception ex) when (ex is DocumentException
                                       or PlatformNotSupportedException
                                       or System.ComponentModel.Win32Exception
                                       or InvalidOperationException
                                       or TimeoutException
                                       or IOException)
        {
            return new PrinterProbeResult(Array.Empty<PrinterInfo>(),
                $"CUPS probe failed: {ex.Message}");
        }
    }

    /// <summary>Parses the <c>printer NAME is ...</c> / <c>system default destination: NAME</c> lines.</summary>
    internal static IReadOnlyList<PrinterInfo> ParseCupsPrinters(string lpstatOutput)
    {
        var printers = new List<PrinterInfo>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var raw in lpstatOutput.Split('\n'))
        {
            var line = raw.Trim();
            const string printerPrefix = "printer ";
            if (line.StartsWith(printerPrefix, StringComparison.Ordinal))
            {
                var rest = line[printerPrefix.Length..];
                var space = rest.IndexOf(' ');
                var name = space > 0 ? rest[..space] : rest;
                if (name.Length > 0 && seen.Add(name))
                    printers.Add(new PrinterInfo(name, "cups"));
                continue;
            }

            const string defaultPrefix = "system default destination:";
            if (line.StartsWith(defaultPrefix, StringComparison.Ordinal))
            {
                var name = line[defaultPrefix.Length..].Trim();
                if (name.Length > 0 && seen.Add(name))
                    printers.Add(new PrinterInfo(name, "cups-default"));
            }
        }

        return printers;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static PrinterProbeResult ProbeWindowsSpooler()
    {
        const string spoolerKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Print\Printers";
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(spoolerKey);
            if (key is null)
                return new PrinterProbeResult(Array.Empty<PrinterInfo>(),
                    $"Spooler registry key not readable: HKLM\\{spoolerKey}");

            var printers = key.GetSubKeyNames()
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Select(name => new PrinterInfo(name, "windows-spooler"))
                .ToArray();

            return new PrinterProbeResult(printers, printers.Length == 0
                ? "The spooler registry key exists but lists no printers."
                : "Read from the Windows spooler registry.");
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return new PrinterProbeResult(Array.Empty<PrinterInfo>(),
                $"Spooler registry read failed: {ex.Message}");
        }
    }
}
