using wwwmmm.Doctor;
using Xunit;

namespace LightningSPP.Tests;

public class DoctorTests
{
    [Fact]
    public void CupsParser_ReadsQueuesAndDefaultDestinationWithoutDuplicates()
    {
        const string output = """
            printer TestPDF is idle.  enabled since Wed 05 Nov 2025
            printer Office is idle.  enabled since Wed 05 Nov 2025
            system default destination: TestPDF
            """;

        var printers = PrinterProbe.ParseCupsPrinters(output);

        Assert.Equal(2, printers.Count);
        Assert.Equal("TestPDF", printers[0].Name);
        Assert.Equal("cups", printers[0].Source);
        Assert.Equal("Office", printers[1].Name);
        Assert.DoesNotContain(printers, p => p.Source == "cups-default" && p.Name == "TestPDF");
    }

    [Fact]
    public void CupsParser_OnEmptyOrNoisyOutputReturnsNothing()
    {
        Assert.Empty(PrinterProbe.ParseCupsPrinters(""));
        Assert.Empty(PrinterProbe.ParseCupsPrinters("lpstat: No destinations added."));
        Assert.Empty(PrinterProbe.ParseCupsPrinters("printer \n"));
    }

    [Fact]
    public async Task Doctor_ReportsAvailabilityWithoutClaimingAnyPrint()
    {
        var report = await EnvironmentDoctor.InspectAsync();

        Assert.False(string.IsNullOrWhiteSpace(report.OsDescription));
        Assert.False(string.IsNullOrWhiteSpace(report.RuntimeVersion));
        Assert.False(string.IsNullOrWhiteSpace(report.PrinterNote));
        Assert.Contains("no document was printed", report.Scope, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(3, report.ScanProviders.Count);
        Assert.Contains(report.ScanProviders, p => p.Name == "twain" && !p.Available);
        Assert.All(report.ScanProviders, p => Assert.False(string.IsNullOrWhiteSpace(p.Detail)));

        Assert.Contains(report.Tools, t => t.Name == "pdftotext");
        Assert.Contains(report.Tools, t => t.Name == "java");
        Assert.All(report.Tools, t => Assert.Equal(t.Found, t.Path is not null));
    }

    [Fact]
    public async Task Doctor_WritesReadableJsonReport()
    {
        var directory = Path.Combine(Path.GetTempPath(), "spp-doctor-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "doctor.json");

        try
        {
            var written = await EnvironmentDoctor.WriteAsync(path);

            Assert.Equal(path, written);
            Assert.True(File.Exists(path));

            var json = await File.ReadAllTextAsync(path);
            Assert.Contains("\"printerNote\"", json, StringComparison.Ordinal);
            Assert.Contains("\"scanProviders\"", json, StringComparison.Ordinal);
            Assert.Contains("\"scope\"", json, StringComparison.Ordinal);

            var parsed = System.Text.Json.JsonDocument.Parse(json);
            Assert.True(parsed.RootElement.GetProperty("printers").GetArrayLength() >= 0);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
