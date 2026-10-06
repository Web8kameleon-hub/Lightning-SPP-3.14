using System.IO.Compression;
using System.Xml.Linq;

namespace LightningSPP.Tests;

// Genuine XLSX test fixtures. Generated values are never production measurements.
internal static class ExcelTestWorkbook
{
    internal static byte[] Create(params object[][] rows)
    {
        XNamespace sheets = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace relations = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        XNamespace packages = "http://schemas.openxmlformats.org/package/2006/relationships";
        XNamespace types = "http://schemas.openxmlformats.org/package/2006/content-types";
        var sheetData = new XElement(sheets + "sheetData");
        for (var index = 0; index < rows.Length; index++)
        {
            var row = new XElement(sheets + "row", new XAttribute("r", index + 1));
            for (var column = 0; column < rows[index].Length; column++)
            {
                var value = rows[index][column];
                var cell = new XElement(sheets + "c", new XAttribute("r", $"{(char)('A' + column)}{index + 1}"));
                if (value is string text)
                    cell.Add(new XAttribute("t", "inlineStr"), new XElement(sheets + "is", new XElement(sheets + "t", text)));
                else
                    cell.Add(new XAttribute("t", value is bool ? "b" : "n"), new XElement(sheets + "v",
                        value is bool flag ? (flag ? "1" : "0") : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)));
                row.Add(cell);
            }
            sheetData.Add(row);
        }
        var sheet = new XDocument(new XElement(sheets + "worksheet",
            new XElement(sheets + "dimension", new XAttribute("ref", $"A1:{(char)('A' + rows.Max(row => row.Length) - 1)}{rows.Length}")), sheetData));
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(archive, "[Content_Types].xml", new XDocument(new XElement(types + "Types",
                new XElement(types + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(types + "Override", new XAttribute("PartName", "/xl/workbook.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                new XElement(types + "Override", new XAttribute("PartName", "/xl/worksheets/sheet1.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")))));
            Write(archive, "_rels/.rels", Relationship(packages, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument", "xl/workbook.xml"));
            Write(archive, "xl/workbook.xml", new XDocument(new XElement(sheets + "workbook",
                new XElement(sheets + "sheets", new XElement(sheets + "sheet", new XAttribute("name", "Fixture"),
                    new XAttribute("sheetId", "1"), new XAttribute(relations + "id", "rId1"))))));
            Write(archive, "xl/_rels/workbook.xml.rels", Relationship(packages, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet", "worksheets/sheet1.xml"));
            Write(archive, "xl/worksheets/sheet1.xml", sheet);
        }
        return output.ToArray();
    }

    private static XDocument Relationship(XNamespace ns, string type, string target) => new(new XElement(ns + "Relationships",
        new XElement(ns + "Relationship", new XAttribute("Id", "rId1"), new XAttribute("Type", type), new XAttribute("Target", target))));

    private static void Write(ZipArchive archive, string name, XDocument document)
    {
        using var stream = archive.CreateEntry(name).Open();
        document.Save(stream);
    }
}
