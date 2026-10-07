using SkiaSharp;
using System.IO.Compression;

namespace wwwmmm.Services.Implementation;

/// <summary>
/// REAL output adapters: encodes processed PNG rasters into PNG, JPEG, BMP, PDF and TIFF.
/// PDF and TIFF multi-page output are real containers. This adapter does not drive hardware.
/// </summary>
public sealed class OutputAdapter : IOutputAdapter
{
    private const int DpiX = 96;
    private const int DpiY = 96;

    public byte[] Encode(byte[] rasterImage, OutputFormat format, int quality = 90)
    {
        ValidateRaster(rasterImage);
        ValidateQuality(quality);

        return format switch
        {
            OutputFormat.Png => EncodeWithSkia(rasterImage, SKEncodedImageFormat.Png, 100),
            OutputFormat.Jpeg => EncodeWithSkia(rasterImage, SKEncodedImageFormat.Jpeg, quality),
            OutputFormat.Bmp => EncodeWithSkia(rasterImage, SKEncodedImageFormat.Bmp, 100),
            OutputFormat.Tiff => TiffEncoder.Encode(new[] { DecodeToRgb24(rasterImage) }),
            OutputFormat.Pdf => EncodePdf(new[] { rasterImage }),
            OutputFormat.Xlsx => EncodeXlsx(rasterImage),
            OutputFormat.Docx => EncodeDocx(rasterImage),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported output format.")
        };
    }

    public byte[] EncodeMultiPage(IReadOnlyList<byte[]> rasterPages, OutputFormat format, int quality = 90)
    {
        ArgumentNullException.ThrowIfNull(rasterPages);
        if (rasterPages.Count == 0)
            throw new ArgumentException("At least one raster page is required.", nameof(rasterPages));
        foreach (var page in rasterPages)
            ValidateRaster(page);
        ValidateQuality(quality);

        return format switch
        {
            OutputFormat.Pdf => EncodePdf(rasterPages),
            OutputFormat.Tiff => TiffEncoder.Encode(
                rasterPages.Select(DecodeToRgb24).ToList()),
            _ => throw new NotSupportedException(
                $"{format} is a single-image format; use Encode() for one page.")
        };
    }

    private static void ValidateRaster(byte[] rasterImage)
    {
        if (rasterImage is null || rasterImage.Length == 0)
            throw new ArgumentException("Raster image data is empty.", nameof(rasterImage));
    }

    private static void ValidateQuality(int quality)
    {
        if (quality is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(quality), "Quality must be between 1 and 100.");
    }

    private static byte[] EncodeWithSkia(byte[] rasterImage, SKEncodedImageFormat format, int quality)
    {
        using var bitmap = SKBitmap.Decode(rasterImage)
            ?? throw new InvalidDataException("Unsupported or corrupt raster image.");
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(format, quality)
            ?? throw new InvalidOperationException($"SkiaSharp failed to encode {format}.");
        return encoded.ToArray();
    }

    private static byte[] EncodePdf(IReadOnlyList<byte[]> rasterPages)
    {
        using var output = new MemoryStream();
        using (var document = SKDocument.CreatePdf(output))
        {
            foreach (var page in rasterPages)
            {
                using var bitmap = SKBitmap.Decode(page)
                    ?? throw new InvalidDataException("Unsupported or corrupt raster image in PDF page.");
                var widthPoints = bitmap.Width * 72f / DpiX;
                var heightPoints = bitmap.Height * 72f / DpiY;
                using var canvas = document.BeginPage(widthPoints, heightPoints);
                canvas.DrawBitmap(bitmap, new SKRect(0, 0, widthPoints, heightPoints));
                document.EndPage();
            }
            document.Close();
        }
        return output.ToArray();
    }

    private static byte[] EncodeDocx(byte[] rasterImage)
    {
        using var bitmap = SKBitmap.Decode(rasterImage)
            ?? throw new InvalidDataException("Unsupported or corrupt raster image for DOCX output.");
        var png = EncodeWithSkia(rasterImage, SKEncodedImageFormat.Png, 100);

        var widthEmu = Math.Max(1L, bitmap.Width) * 9525L;
        var heightEmu = Math.Max(1L, bitmap.Height) * 9525L;

        var contentTypes =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
            "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
            "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
            "<Default Extension=\"png\" ContentType=\"image/png\"/>" +
            "<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>" +
            "</Types>";

        var rootRels =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/>" +
            "</Relationships>";

        var documentRels =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/image\" Target=\"media/image1.png\"/>" +
            "</Relationships>";

        var document =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:pic=\"http://schemas.openxmlformats.org/drawingml/2006/picture\">" +
            "<w:body><w:p><w:r><w:drawing><wp:inline distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\"><wp:extent cx=\"" + widthEmu + "\" cy=\"" + heightEmu + "\"/><wp:docPr id=\"1\" name=\"LightningImage\"/><a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/picture\"><pic:pic><pic:nvPicPr><pic:cNvPr id=\"0\" name=\"image1.png\"/></pic:nvPicPr><pic:blipFill><a:blip r:embed=\"rId1\"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill><pic:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"" + widthEmu + "\" cy=\"" + heightEmu + "\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></pic:spPr></pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing></w:r></w:p><w:sectPr><w:pgSz w:w=\"11906\" w:h=\"16838\"/><w:pgMar w:top=\"1440\" w:right=\"1440\" w:bottom=\"1440\" w:left=\"1440\"/></w:sectPr></w:body></w:document>";

        return CreateZipPackage(new Dictionary<string, byte[]>
        {
            ["[Content_Types].xml"] = EncodeUtf8(contentTypes),
            ["_rels/.rels"] = EncodeUtf8(rootRels),
            ["word/document.xml"] = EncodeUtf8(document),
            ["word/_rels/document.xml.rels"] = EncodeUtf8(documentRels),
            ["word/media/image1.png"] = png
        });
    }

    private static byte[] EncodeXlsx(byte[] rasterImage)
    {
        using var bitmap = SKBitmap.Decode(rasterImage)
            ?? throw new InvalidDataException("Unsupported or corrupt raster image for XLSX output.");
        var png = EncodeWithSkia(rasterImage, SKEncodedImageFormat.Png, 100);

        var widthEmu = Math.Max(1L, bitmap.Width) * 9525L;
        var heightEmu = Math.Max(1L, bitmap.Height) * 9525L;

        var contentTypes =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
            "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
            "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
            "<Default Extension=\"png\" ContentType=\"image/png\"/>" +
            "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
            "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
            "<Override PartName=\"/xl/drawings/drawing1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.drawing+xml\"/>" +
            "</Types>";

        var rootRels =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
            "</Relationships>";

        var workbook =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Sheet1\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>";

        var workbookRels =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
            "</Relationships>";

        var worksheet =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheetData/><drawing r:id=\"rId1\"/></worksheet>";

        var worksheetRels =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/drawing\" Target=\"../drawings/drawing1.xml\"/>" +
            "</Relationships>";

        var drawing =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<xdr:wsDr xmlns:xdr=\"http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><xdr:oneCellAnchor><xdr:from><xdr:col>0</xdr:col><xdr:colOff>0</xdr:colOff><xdr:row>0</xdr:row><xdr:rowOff>0</xdr:rowOff></xdr:from><xdr:ext cx=\"" + widthEmu + "\" cy=\"" + heightEmu + "\"/><xdr:pic><xdr:nvPicPr><xdr:cNvPr id=\"2\" name=\"image1.png\"/><xdr:cNvPicPr/></xdr:nvPicPr><xdr:blipFill><a:blip r:embed=\"rId1\"/><a:stretch><a:fillRect/></a:stretch></xdr:blipFill><xdr:spPr><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></xdr:spPr></xdr:pic><xdr:clientData/></xdr:oneCellAnchor></xdr:wsDr>";

        var drawingRels =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/image\" Target=\"../media/image1.png\"/>" +
            "</Relationships>";

        return CreateZipPackage(new Dictionary<string, byte[]>
        {
            ["[Content_Types].xml"] = EncodeUtf8(contentTypes),
            ["_rels/.rels"] = EncodeUtf8(rootRels),
            ["xl/workbook.xml"] = EncodeUtf8(workbook),
            ["xl/_rels/workbook.xml.rels"] = EncodeUtf8(workbookRels),
            ["xl/worksheets/sheet1.xml"] = EncodeUtf8(worksheet),
            ["xl/worksheets/_rels/sheet1.xml.rels"] = EncodeUtf8(worksheetRels),
            ["xl/drawings/drawing1.xml"] = EncodeUtf8(drawing),
            ["xl/drawings/_rels/drawing1.xml.rels"] = EncodeUtf8(drawingRels),
            ["xl/media/image1.png"] = png
        });
    }

    private static byte[] CreateZipPackage(IReadOnlyDictionary<string, byte[]> entries)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in entries)
            {
                var zipEntry = zip.CreateEntry(entry.Key, CompressionLevel.SmallestSize);
                using var entryStream = zipEntry.Open();
                entryStream.Write(entry.Value, 0, entry.Value.Length);
            }
        }
        return stream.ToArray();
    }

    private static byte[] EncodeUtf8(string xml) => System.Text.Encoding.UTF8.GetBytes(xml);

    /// <summary>
    /// Decode any supported raster into a tightly packed RGB-24 buffer with its dimensions.
    /// </summary>
    internal static Rgb24Image DecodeToRgb24(byte[] rasterImage)
    {
        using var decoded = SKBitmap.Decode(rasterImage)
            ?? throw new InvalidDataException("Unsupported or corrupt raster image.");
        using var rgba = decoded.Copy(SKColorType.Rgba8888);

        var width = rgba.Width;
        var height = rgba.Height;
        var source = rgba.GetPixelSpan();
        var rgb = new byte[checked(width * height * 3)];

        for (var i = 0; i < width * height; i++)
        {
            var s = i * 4;
            var d = i * 3;
            rgb[d] = source[s];
            rgb[d + 1] = source[s + 1];
            rgb[d + 2] = source[s + 2];
        }

        return new Rgb24Image(width, height, rgb);
    }
}

/// <summary>A decoded RGB-24 image: three bytes per pixel, row-major.</summary>
internal sealed record Rgb24Image(int Width, int Height, byte[] PixelBytes);
