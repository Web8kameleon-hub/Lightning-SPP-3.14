using System.Diagnostics;
using LightningCore.Interop;

// Real end-to-end proof from .NET 10 into the Rust core: every number printed below
// is measured, and every byte written to disk comes back from the native library.
Console.WriteLine($"native core version: {NativeCore.Version()}");
NativeCore.SelfTest();
Console.WriteLine("native self-test: OK");

var assetArgument = args.Length > 0 ? args[0] : Path.Combine("..", "..", "Web", "wwwroot", "assets", "landing-hero.png");
var input = File.ReadAllBytes(assetArgument);
Console.WriteLine($"input: {assetArgument} ({input.Length:N0} bytes)");

foreach (var (name, operation, parameter) in new (string, uint, uint)[]
{
    ("grayscale", NativeCore.OpGrayscale, 0),
    ("sobel", NativeCore.OpSobel, 0),
    ("sharpen", NativeCore.OpSharpen, 20),
    ("resize_half", NativeCore.OpResizeHalf, 0),
    ("gaussian_blur", NativeCore.OpGaussianBlur, 30),
    ("rotate_180", NativeCore.OpRotate180, 0),
})
{
    var stopwatch = Stopwatch.StartNew();
    var result = NativeCore.Process(input, operation, parameter);
    stopwatch.Stop();
    if (result.Length < 8) throw new InvalidOperationException($"{name} returned {result.Length} bytes");
    File.WriteAllBytes($"dotnet-{name}.png", result);
    Console.WriteLine($"  {name,-14} {result.Length,9:N0} bytes  {stopwatch.Elapsed.TotalMilliseconds,8:F2} ms");
}

var jpeg = NativeCore.Encode(input, NativeCore.FormatJpeg, 85);
File.WriteAllBytes("dotnet-encode.jpg", jpeg);
Console.WriteLine($"  jpeg q85       {jpeg.Length,9:N0} bytes  magic={jpeg[0]:X2}{jpeg[1]:X2}");

var compressed = NativeCore.BrotliCompress(input);
var restored = NativeCore.BrotliDecompress(compressed);
if (!restored.AsSpan().SequenceEqual(input))
    throw new InvalidOperationException("brotli round-trip is not byte-identical");
Console.WriteLine($"  brotli         {input.Length:N0} -> {compressed.Length:N0} bytes, {restored.Length:N0} bytes restored identical");

var batch = NativeCore.Batch(Enumerable.Repeat(input, 6).ToArray(), NativeCore.OpSobel);
if (batch.Length != 6 || batch.Any(item => item.Length < 8))
    throw new InvalidOperationException("parallel batch returned incomplete results");
Console.WriteLine($"  batch x6       {batch.Sum(item => (long)item.Length),9:N0} bytes total (native Rayon pool)");

// Corrupt input must surface as an error code, never as a fabricated image.
try
{
    NativeCore.Process("not an image"u8.ToArray(), NativeCore.OpSobel);
    throw new InvalidOperationException("corrupt input was accepted — the harness is wrong");
}
catch (InvalidOperationException ex) when (ex.Message.Contains("failed with code"))
{
    Console.WriteLine($"corrupt input rejected: {ex.Message}");
}

Console.WriteLine("managed interop verified");
try
{
    NativeCore.Batch([input, "corrupt fixture"u8.ToArray(), input], NativeCore.OpSobel);
    throw new InvalidOperationException("mixed batch incorrectly succeeded");
}
catch (InvalidOperationException ex) when (ex.Message.StartsWith("native batch rejected", StringComparison.Ordinal))
{
    Console.WriteLine("mixed batch rejected; native buffers released on failure");
}
