using System.Buffers.Binary;
using System.Text;
using SkiaSharp;
using wwwmmm.Services;
using wwwmmm.Services.Implementation;
using Xunit;

namespace LightningSPP.Tests;

/// <summary>
/// Proves the output adapters emit real, structurally valid files — not renamed bytes.
/// TIFF is verified by walking its IFD chain and comparing decoded pixels.
/// </summary>
public class OutputAdapterTests
{
    private static readonly OutputAdapter Adapter = new();

    [Fact]
    public void Png_HasPngSignatureAndDecodesToSourceSize()
    {
        var png = Adapter.Encode(RealTestImage.Png(), OutputFormat.Png);
        Assert.True(png.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }));
        using var bitmap = SKBitmap.Decode(png);
        Assert.NotNull(bitmap);
        Assert.Equal(96, bitmap.Width);
        Assert.Equal(64, bitmap.Height);
    }

    [Fact]
    public void Jpeg_HasSoiMarkerAndDecodes()
    {
        var jpeg = Adapter.Encode(RealTestImage.Png(), OutputFormat.Jpeg, 85);
        Assert.Equal(0xFF, jpeg[0]);
        Assert.Equal(0xD8, jpeg[1]);
        using var bitmap = SKBitmap.Decode(jpeg);
        Assert.NotNull(bitmap);
        Assert.Equal(96, bitmap.Width);
        Assert.Equal(64, bitmap.Height);
    }

    [Fact]
    public void Bmp_HasValidBitmapHeadersAndExactPixels()
    {
        var source = RealTestImage.Png();
        var bmp = Adapter.Encode(source, OutputFormat.Bmp);

        Assert.Equal((byte)'B', bmp[0]);
        Assert.Equal((byte)'M', bmp[1]);
        Assert.Equal(bmp.Length, BinaryPrimitives.ReadInt32LittleEndian(bmp.AsSpan(2, 4)));
        Assert.Equal(54, BinaryPrimitives.ReadInt32LittleEndian(bmp.AsSpan(10, 4)));
        Assert.Equal(40, BinaryPrimitives.ReadInt32LittleEndian(bmp.AsSpan(14, 4)));
        Assert.Equal(96, BinaryPrimitives.ReadInt32LittleEndian(bmp.AsSpan(18, 4)));
        Assert.Equal(64, BinaryPrimitives.ReadInt32LittleEndian(bmp.AsSpan(22, 4)));
        Assert.Equal(1, BinaryPrimitives.ReadInt16LittleEndian(bmp.AsSpan(26, 2)));
        Assert.Equal(24, BinaryPrimitives.ReadInt16LittleEndian(bmp.AsSpan(28, 2)));
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(bmp.AsSpan(30, 4)));

        var stride = ((96 * 3) + 3) & ~3;
        Assert.Equal(stride * 64, BinaryPrimitives.ReadInt32LittleEndian(bmp.AsSpan(34, 4)));

        using var decoded = SKBitmap.Decode(source);
        Assert.NotNull(decoded);
        foreach (var (x, y) in new[] { (0, 0), (20, 20), (95, 63) })
        {
            var expected = decoded.GetPixel(x, y);
            var index = 54 + ((63 - y) * stride) + (x * 3);   // bottom-up BGR rows
            Assert.Equal(expected.Blue, bmp[index]);
            Assert.Equal(expected.Green, bmp[index + 1]);
            Assert.Equal(expected.Red, bmp[index + 2]);
        }
    }

    [Fact]
    public void Tiff_WritesBaselineHeaderAndExactPixels()
    {
        var source = RealTestImage.Png();
        var tiff = Adapter.Encode(source, OutputFormat.Tiff);

        Assert.Equal((byte)'I', tiff[0]);
        Assert.Equal((byte)'I', tiff[1]);
        Assert.Equal(42, BinaryPrimitives.ReadUInt16LittleEndian(tiff.AsSpan(2, 2)));

        var ifds = ReadIfds(tiff);
        var page = Assert.Single(ifds);

        Assert.Equal(96u, page[256].Value);                             // ImageWidth
        Assert.Equal(64u, page[257].Value);                             // ImageLength
        Assert.Equal(1u, page[259].Value);                              // Compression = none
        Assert.Equal(2u, page[262].Value);                              // Photometric = RGB
        Assert.Equal(3u, page[277].Value);                              // SamplesPerPixel
        Assert.Equal(1u, page[284].Value);                              // PlanarConfiguration = chunky
        Assert.Equal((uint)(96 * 64 * 3), page[279].Value);             // StripByteCounts

        var bitsOffset = page[258].Value;
        Assert.Equal(8, BinaryPrimitives.ReadUInt16LittleEndian(tiff.AsSpan((int)bitsOffset, 2)));
        Assert.Equal(8, BinaryPrimitives.ReadUInt16LittleEndian(tiff.AsSpan((int)bitsOffset + 2, 2)));
        Assert.Equal(8, BinaryPrimitives.ReadUInt16LittleEndian(tiff.AsSpan((int)bitsOffset + 4, 2)));

        using var decoded = SKBitmap.Decode(source);
        Assert.NotNull(decoded);
        var strip = (int)page[273].Value;
        foreach (var (x, y) in new[] { (0, 0), (20, 20), (95, 63), (48, 40) })
        {
            var expected = decoded.GetPixel(x, y);
            var index = strip + ((y * 96) + x) * 3;
            Assert.Equal(expected.Red, tiff[index]);
            Assert.Equal(expected.Green, tiff[index + 1]);
            Assert.Equal(expected.Blue, tiff[index + 2]);
        }
    }

    [Fact]
    public void Tiff_MultiPageChainsOneIfdPerPage()
    {
        var tiff = Adapter.EncodeMultiPage(
            [RealTestImage.Png(), RealTestImage.Png(40, 30)],
            OutputFormat.Tiff);

        var ifds = ReadIfds(tiff);
        Assert.Equal(2, ifds.Count);
        Assert.Equal(96u, ifds[0][256].Value);
        Assert.Equal(40u, ifds[1][256].Value);
        Assert.Equal(30u, ifds[1][257].Value);
    }

    [Fact]
    public void Pdf_WritesMultiPageDocument()
    {
        var pdf = Adapter.EncodeMultiPage(
            [RealTestImage.Png(), RealTestImage.Png(40, 30)],
            OutputFormat.Pdf);

        Assert.True(pdf.AsSpan(0, 5).SequenceEqual("%PDF-"u8));
        Assert.Contains("%%EOF", Encoding.ASCII.GetString(pdf));
        Assert.Contains("/Type /Page", Encoding.ASCII.GetString(pdf), StringComparison.Ordinal);
    }

    [Fact]
    public void Pdf_RejectsMoreThanThirtyTwoPages()
    {
        var page = RealTestImage.Png();
        var pages = Enumerable.Range(0, 33).Select(_ => page).ToArray();
        Assert.Throws<ArgumentException>(() => Adapter.EncodeMultiPage(pages, OutputFormat.Pdf));
    }

    [Theory]
    [InlineData(OutputFormat.Png)]
    [InlineData(OutputFormat.Jpeg)]
    [InlineData(OutputFormat.Bmp)]
    public void MultiPage_IsRejectedForSingleImageFormats(OutputFormat format)
    {
        Assert.Throws<NotSupportedException>(() =>
            Adapter.EncodeMultiPage([RealTestImage.Png()], format));
    }

    [Fact]
    public void NonImageBytes_AreRejectedInsteadOfEncoded()
    {
        var garbage = "this is not an image"u8.ToArray();
        Assert.ThrowsAny<Exception>(() => Adapter.Encode(garbage, OutputFormat.Tiff));
        Assert.ThrowsAny<Exception>(() => Adapter.Encode(garbage, OutputFormat.Jpeg));
        Assert.ThrowsAny<Exception>(() => Adapter.Encode(garbage, OutputFormat.Pdf));
    }

    [Fact]
    public void EmptyInput_AndBadQuality_AreRejected()
    {
        Assert.Throws<ArgumentException>(() => Adapter.Encode([], OutputFormat.Png));
        Assert.Throws<ArgumentOutOfRangeException>(() => Adapter.Encode(RealTestImage.Png(), OutputFormat.Jpeg, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Adapter.Encode(RealTestImage.Png(), OutputFormat.Jpeg, 101));
        Assert.Throws<ArgumentException>(() => Adapter.EncodeMultiPage([], OutputFormat.Tiff));
    }

    private readonly record struct IfdEntry(ushort Type, uint Count, uint Value);

    /// <summary>Walks the next-IFD chain and returns each page's tag table.</summary>
    private static List<Dictionary<ushort, IfdEntry>> ReadIfds(byte[] tiff)
    {
        var pages = new List<Dictionary<ushort, IfdEntry>>();
        var offset = (long)BinaryPrimitives.ReadUInt32LittleEndian(tiff.AsSpan(4, 4));

        while (offset > 0)
        {
            var start = (int)offset;
            var count = BinaryPrimitives.ReadUInt16LittleEndian(tiff.AsSpan(start, 2));
            var tags = new Dictionary<ushort, IfdEntry>();

            for (var index = 0; index < count; index++)
            {
                var entry = start + 2 + (index * 12);
                var tag = BinaryPrimitives.ReadUInt16LittleEndian(tiff.AsSpan(entry, 2));
                var type = BinaryPrimitives.ReadUInt16LittleEndian(tiff.AsSpan(entry + 2, 2));
                var length = BinaryPrimitives.ReadUInt32LittleEndian(tiff.AsSpan(entry + 4, 4));
                var value = BinaryPrimitives.ReadUInt32LittleEndian(tiff.AsSpan(entry + 8, 4));
                tags[tag] = new IfdEntry(type, length, value);
            }

            pages.Add(tags);
            offset = BinaryPrimitives.ReadUInt32LittleEndian(tiff.AsSpan(start + 2 + (count * 12), 4));
        }

        return pages;
    }
}
