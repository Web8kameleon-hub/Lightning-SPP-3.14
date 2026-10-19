using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using LightningSPP.Licensing;

namespace wwwmmm.Web;

/// <summary>Creates bounded output-only archives; never extracts user supplied ZIPs.</summary>
internal static class DocumentPackage
{
    internal static byte[] Create(byte[] input, TryDocumentResult result, bool isPdf, string profile)
    {
        if (input.Length == 0 || result.Output.Length == 0 || result.Preview.Length == 0)
            throw new ArgumentException("A package requires complete processing output.");
        if (isPdf && result.TotalPages is < 1 or > 32)
            throw new ArgumentException("Invalid PDF page count.");
        var name = isPdf ? "processed.pdf" : "processed.png";
        var license = LicenseManager.GetActiveLicense();
        var report = new
        {
            schemaVersion = 1, createdUtc = DateTimeOffset.UtcNow, profile, aiEnabled = false,
            input = new { bytes = input.Length, sha256 = Hash(input), type = isPdf ? "pdf" : "image" },
            classification = new { status = "NOT_PERFORMED", rule = "whole-document", supplierSplitting = false },
            pageMapping = Enumerable.Range(1, isPdf ? result.TotalPages : 1)
                .Select(page => new { inputPage = page, outputFile = name, outputPage = page }),
            files = new[] { new { path = name, bytes = result.Output.Length, sha256 = Hash(result.Output) },
                new { path = "preview.png", bytes = result.Preview.Length, sha256 = Hash(result.Preview) } },
            processing = new { result.Engine, result.ScanTimeMs, result.ProcessTimeMs, result.TotalTimeMs,
                pdfRasterized = isPdf, preservesPdfStructure = false },
            license = new { scope = "server-runtime-snapshot-not-customer-entitlement", demoRequiresLicense = false,
                active = license?.IsValid() == true, tier = license?.Tier.ToString() ?? "Free" },
            verification = new { scope = "Processing completed; hashes identify exact exported bytes. Page mapping records whole-document order; no independent output re-render, OCR, supplier recognition or regulatory validation is claimed." }
        };
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(archive, name, result.Output);
            Write(archive, "preview.png", result.Preview);
            Write(archive, "report.json", JsonSerializer.SerializeToUtf8Bytes(report, new JsonSerializerOptions { WriteIndented = true }));
        }
        return stream.ToArray();
    }

    private static string Hash(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));
    private static void Write(ZipArchive archive, string name, byte[] data)
    {
        using var entry = archive.CreateEntry(name, CompressionLevel.Optimal).Open();
        entry.Write(data);
    }
}
