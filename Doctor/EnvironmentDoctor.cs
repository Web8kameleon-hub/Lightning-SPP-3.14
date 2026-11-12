using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using wwwmmm.Intake;

namespace wwwmmm.Doctor;

public sealed record DoctorPrinter(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("source")] string Source);

public sealed record DoctorTool(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("found")] bool Found,
    [property: JsonPropertyName("path")] string? Path);

public sealed record DoctorScanProvider(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("transport")] string Transport,
    [property: JsonPropertyName("available")] bool Available,
    [property: JsonPropertyName("detail")] string Detail);

public sealed record DoctorReport(
    [property: JsonPropertyName("generatedUtc")] string GeneratedUtc,
    [property: JsonPropertyName("osDescription")] string OsDescription,
    [property: JsonPropertyName("osArchitecture")] string OsArchitecture,
    [property: JsonPropertyName("runtimeVersion")] string RuntimeVersion,
    [property: JsonPropertyName("printers")] IReadOnlyList<DoctorPrinter> Printers,
    [property: JsonPropertyName("printerNote")] string PrinterNote,
    [property: JsonPropertyName("scanProviders")] IReadOnlyList<DoctorScanProvider> ScanProviders,
    [property: JsonPropertyName("tools")] IReadOnlyList<DoctorTool> Tools,
    [property: JsonPropertyName("scope")] string Scope);

/// <summary>
/// Records what this host can actually do: which print queues exist, which scan transports
/// are present, which external tools resolve. It performs no printing and no scanning, so a
/// report is evidence of availability only.
/// </summary>
public static class EnvironmentDoctor
{
    private const string ScopeNote =
        "Availability probe only. No document was printed, scanned or transmitted.";

    public static async Task<DoctorReport> InspectAsync(CancellationToken cancellationToken = default)
    {
        var printers = await PrinterProbe.ProbeAsync(cancellationToken);

        return new DoctorReport(
            GeneratedUtc: DateTime.UtcNow.ToString("O"),
            OsDescription: RuntimeInformation.OSDescription,
            OsArchitecture: RuntimeInformation.OSArchitecture.ToString(),
            RuntimeVersion: RuntimeInformation.FrameworkDescription,
            Printers: printers.Printers.Select(p => new DoctorPrinter(p.Name, p.Source)).ToArray(),
            PrinterNote: printers.Note,
            ScanProviders: ScanProviderRegistry.Probe()
                .Select(p => new DoctorScanProvider(p.Name, p.Transport, p.Available, p.Detail))
                .ToArray(),
            Tools: new[]
            {
                LocatePdfToText(),
                ResolveTool("java", null)
            },
            Scope: ScopeNote);
    }

    public static async Task<string> WriteAsync(string outputPath, CancellationToken cancellationToken = default)
    {
        var report = await InspectAsync(cancellationToken);
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });

        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(outputPath, json + Environment.NewLine, cancellationToken);
        return outputPath;
    }

    /// <summary>Reports where Poppler was found and by which rule, or that it is absent.</summary>
    private static DoctorTool LocatePdfToText()
    {
        var location = Documents.PdfToTextLocator.TryLocate();
        return location is null
            ? new DoctorTool("pdftotext", false, null)
            : new DoctorTool("pdftotext", true, $"{location.Path} ({location.Source})");
    }

    private static DoctorTool ResolveTool(string name, string? environmentVariable)
    {
        if (environmentVariable is not null)
        {
            var configured = Environment.GetEnvironmentVariable(environmentVariable);
            if (!string.IsNullOrWhiteSpace(configured))
                return new DoctorTool(name, File.Exists(configured), File.Exists(configured) ? configured : null);
        }

        var path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(path))
        {
            var candidates = OperatingSystem.IsWindows()
                ? new[] { name + ".exe", name + ".cmd", name + ".bat" }
                : new[] { name };

            foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                foreach (var candidate in candidates)
                {
                    var full = Path.Combine(directory.Trim(), candidate);
                    if (File.Exists(full))
                        return new DoctorTool(name, true, full);
                }
            }
        }

        return new DoctorTool(name, false, null);
    }
}
