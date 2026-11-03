using System.Text;
using wwwmmm.Documents;
using wwwmmm.Extraction;
using Xunit;

namespace LightningSPP.Tests;

public class FlowPdfTextTests
{
    [Fact]
    public async Task TextLayer_ReadsRealPdfPagesAndFeedsRules()
    {
        var directory = Path.Combine(Path.GetTempPath(), "spp-pdftext-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var pdfPath = Path.Combine(directory, "invoice.pdf");

        try
        {
            await File.WriteAllBytesAsync(pdfPath, BuildTwoPagePdf());

            DocumentText document;
            try
            {
                document = await TextLayerExtractor.ExtractAsync(pdfPath);
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or FileNotFoundException)
            {
                // Poppler is absent: the failure must be explicit, never a silent empty document.
                Assert.Contains("pdftotext", ex.Message + ex.GetType().Name, StringComparison.OrdinalIgnoreCase);
                return;
            }

            Assert.Equal(2, document.PageCount);
            Assert.Equal(64, document.Sha256.Length);

            Assert.Contains("INVOICE", document.Pages[0].Text, StringComparison.Ordinal);
            Assert.Contains("Date: 2026-03-14", document.Pages[0].Text, StringComparison.Ordinal);
            Assert.Contains("Total: 1299.00", document.Pages[1].Text, StringComparison.Ordinal);

            var rules = RuleSet.Parse("""
                { "version": 1, "rules": [
                  { "id": "total", "field": "invoice_total", "pattern": "Total:\\s*([0-9]+\\.[0-9]{2})", "required": true },
                  { "id": "date", "field": "invoice_date", "pattern": "Date:\\s*(\\d{4}-\\d{2}-\\d{2})", "required": true } ] }
                """);

            var result = FieldExtractor.Apply(document, rules);

            Assert.True(result.HasAllRequiredFields);
            var total = Assert.Single(result.Matches, m => m.Field == "invoice_total");
            Assert.Equal("1299.00", total.Value);
            Assert.Equal(2, total.Page);

            var date = Assert.Single(result.Matches, m => m.Field == "invoice_date");
            Assert.Equal("2026-03-14", date.Value);
            Assert.Equal(1, date.Page);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task TextLayer_RejectsNonPdfInput()
    {
        var path = Path.Combine(Path.GetTempPath(), "spp-not-a-pdf-" + Guid.NewGuid().ToString("N") + ".png");
        await File.WriteAllBytesAsync(path, new byte[] { 1, 2, 3 });
        try
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => TextLayerExtractor.ExtractAsync(path));
            Assert.Contains("requires a .pdf", error.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Builds a minimal two-page PDF with a real text layer and a correct cross-reference
    /// table, so Poppler parses it the same way it parses a vendor document.
    /// </summary>
    private static byte[] BuildTwoPagePdf()
    {
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 5 0 R >> >> /Contents 6 0 R >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 5 0 R >> >> /Contents 7 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            BuildContent("INVOICE", "Date: 2026-03-14", "Customer: ACME"),
            BuildContent("Total: 1299.00", "VAT: 19%", string.Empty)
        };

        var builder = new StringBuilder();
        var offsets = new List<int>();
        builder.Append("%PDF-1.4\n");

        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
            builder.Append(i + 1).Append(" 0 obj\n").Append(objects[i]).Append("\nendobj\n");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(builder.ToString());
        builder.Append("xref\n0 ").Append(objects.Count + 1).Append('\n');
        builder.Append("0000000000 65535 f \n");
        foreach (var offset in offsets)
            builder.Append(offset.ToString("D10")).Append(" 00000 n \n");

        builder.Append("trailer\n<< /Size ").Append(objects.Count + 1)
               .Append(" /Root 1 0 R >>\nstartxref\n")
               .Append(xrefOffset).Append("\n%%EOF\n");

        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    private static string BuildContent(params string[] lines)
    {
        var text = new StringBuilder("BT\n/F1 14 Tf\n72 760 Td\n");
        foreach (var line in lines.Where(l => l.Length > 0))
            text.Append('(').Append(line.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)"))
                .Append(") Tj\n0 -22 Td\n");
        text.Append("ET");

        var stream = text.ToString();
        return $"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}\nendstream";
    }
}
