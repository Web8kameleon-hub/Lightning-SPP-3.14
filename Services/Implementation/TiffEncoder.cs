namespace wwwmmm.Services.Implementation;

/// <summary>
/// Minimal but correct baseline TIFF 6.0 encoder (little-endian, uncompressed RGB-24,
/// single strip per page, multi-page via the next-IFD chain). Needed because SkiaSharp
/// does not ship a TIFF encoder.
/// </summary>
internal static class TiffEncoder
{
    // 2 (entry count) + 13 tags * 12 bytes + 4 (next-IFD offset) + 22 bytes of extra values.
    private const int EntryCount = 13;
    private const int IfdBlockSize = 2 + (EntryCount * 12) + 4 + 22;
    private const ushort TypeShort = 3;
    private const ushort TypeLong = 4;
    private const ushort TypeRational = 5;
    private const ushort CompressionNone = 1;
    private const ushort PhotometricRgb = 2;
    private const ushort PlanarChunky = 1;
    private const ushort ResolutionUnitInch = 2;
    private const uint ResolutionNumerator = 96;
    private const uint ResolutionDenominator = 1;

    public static byte[] Encode(IReadOnlyList<Rgb24Image> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        if (pages.Count == 0)
            throw new ArgumentException("At least one page is required.", nameof(pages));

        var stripOffsets = new int[pages.Count];
        var ifdOffsets = new int[pages.Count];

        var cursor = 8;
        for (var i = 0; i < pages.Count; i++)
        {
            stripOffsets[i] = cursor;
            cursor += pages[i].PixelBytes.Length;
        }

        var firstIfdOffset = cursor;
        for (var i = 0; i < pages.Count; i++)
        {
            ifdOffsets[i] = cursor;
            cursor += IfdBlockSize;
        }

        using var buffer = new MemoryStream(cursor);
        using var writer = new BinaryWriter(buffer);

        writer.Write((byte)'I');
        writer.Write((byte)'I');
        writer.Write((ushort)42);
        writer.Write((uint)firstIfdOffset);

        foreach (var page in pages)
            writer.Write(page.PixelBytes);

        for (var i = 0; i < pages.Count; i++)
        {
            var nextIfd = i + 1 < pages.Count ? ifdOffsets[i + 1] : 0;
            WriteIfd(writer, pages[i], stripOffsets[i], ifdOffsets[i], nextIfd);
        }

        writer.Flush();
        return buffer.ToArray();
    }

    private static void WriteIfd(
        BinaryWriter writer,
        Rgb24Image page,
        int stripOffset,
        int ifdOffset,
        int nextIfdOffset)
    {
        var bitsOffset = ifdOffset + 2 + (EntryCount * 12) + 4;
        var xResOffset = bitsOffset + 6;
        var yResOffset = xResOffset + 8;

        writer.Write((ushort)EntryCount);

        WriteLongEntry(writer, 256, (uint)page.Width);        // ImageWidth
        WriteLongEntry(writer, 257, (uint)page.Height);       // ImageLength
        WriteShortArrayEntry(writer, 258, 3, (uint)bitsOffset); // BitsPerSample [8,8,8]
        WriteShortEntry(writer, 259, CompressionNone);        // Compression
        WriteShortEntry(writer, 262, PhotometricRgb);         // PhotometricInterpretation
        WriteLongEntry(writer, 273, (uint)stripOffset);       // StripOffsets
        WriteShortEntry(writer, 277, 3);                      // SamplesPerPixel
        WriteLongEntry(writer, 278, (uint)page.Height);       // RowsPerStrip (single strip)
        WriteLongEntry(writer, 279, (uint)page.PixelBytes.Length); // StripByteCounts
        WriteRationalEntry(writer, 282, (uint)xResOffset);    // XResolution
        WriteRationalEntry(writer, 283, (uint)yResOffset);    // YResolution
        WriteShortEntry(writer, 284, PlanarChunky);           // PlanarConfiguration
        WriteShortEntry(writer, 296, ResolutionUnitInch);     // ResolutionUnit

        writer.Write((uint)nextIfdOffset);

        // Extra values, written in the same order their offsets were computed.
        writer.Write((ushort)8);
        writer.Write((ushort)8);
        writer.Write((ushort)8);
        writer.Write(ResolutionNumerator);
        writer.Write(ResolutionDenominator);
        writer.Write(ResolutionNumerator);
        writer.Write(ResolutionDenominator);
    }

    private static void WriteShortEntry(BinaryWriter writer, ushort tag, ushort value)
    {
        writer.Write(tag);
        writer.Write(TypeShort);
        writer.Write(1u);
        writer.Write(value);
        writer.Write((ushort)0);
    }

    private static void WriteShortArrayEntry(BinaryWriter writer, ushort tag, uint count, uint offset)
    {
        writer.Write(tag);
        writer.Write(TypeShort);
        writer.Write(count);
        writer.Write(offset);
    }

    private static void WriteLongEntry(BinaryWriter writer, ushort tag, uint value)
    {
        writer.Write(tag);
        writer.Write(TypeLong);
        writer.Write(1u);
        writer.Write(value);
    }

    private static void WriteRationalEntry(BinaryWriter writer, ushort tag, uint offset)
    {
        writer.Write(tag);
        writer.Write(TypeRational);
        writer.Write(1u);
        writer.Write(offset);
    }
}
