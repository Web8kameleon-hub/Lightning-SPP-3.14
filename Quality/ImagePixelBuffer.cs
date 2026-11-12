namespace wwwmmm.Quality;

public readonly record struct ImagePixelBuffer(
    ReadOnlyMemory<byte> Data, int Width, int Height, int Channels, int Stride)
{
    public int RowBytes => Width * Channels;

    public void Validate()
    {
        if (Width <= 0 || Height <= 0) throw new ArgumentException("Non-positive dimensions.");
        if (Channels is < 1 or > 4) throw new ArgumentException("Unsupported channel count.");
        if (Stride < RowBytes) throw new ArgumentException("Stride smaller than row bytes.");
        if (Data.Length < Height * Stride) throw new ArgumentException("Buffer smaller than declared geometry.");
    }
}
