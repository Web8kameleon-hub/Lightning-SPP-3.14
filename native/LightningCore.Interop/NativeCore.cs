using System.Runtime.InteropServices;

namespace LightningCore.Interop;

/// <summary>
/// Managed surface over the native Lightning core. Every call crosses a real C ABI;
/// nothing here re-implements the operation in managed code or falls back silently.
/// </summary>
public static unsafe partial class NativeCore
{
    private const string Library = "lightning_core";

    public const uint OpNone = 0;
    public const uint OpGrayscale = 1;
    public const uint OpSobel = 2;
    public const uint OpSharpen = 3;
    public const uint OpResizeHalf = 4;
    public const uint OpGaussianBlur = 5;
    public const uint OpRotate180 = 6;

    public const uint FormatPng = 1;
    public const uint FormatJpeg = 2;
    public const uint FormatBmp = 3;
    public const uint FormatTiff = 4;

    public const int Ok = 0;

    [LibraryImport(Library, EntryPoint = "lightning_version")]
    private static partial nint VersionNative();

    [LibraryImport(Library, EntryPoint = "lightning_self_test")]
    private static partial int SelfTestNative();

    [LibraryImport(Library, EntryPoint = "lightning_process")]
    private static partial int ProcessNative(byte* input, nuint inputLength, uint operation, uint parameter, byte** output, nuint* outputLength);

    [LibraryImport(Library, EntryPoint = "lightning_encode")]
    private static partial int EncodeNative(byte* input, nuint inputLength, uint format, uint quality, byte** output, nuint* outputLength);

    [LibraryImport(Library, EntryPoint = "lightning_brotli_compress")]
    private static partial int BrotliCompressNative(byte* input, nuint inputLength, uint quality, uint window, byte** output, nuint* outputLength);

    [LibraryImport(Library, EntryPoint = "lightning_brotli_decompress")]
    private static partial int BrotliDecompressNative(byte* input, nuint inputLength, byte** output, nuint* outputLength);

    [LibraryImport(Library, EntryPoint = "lightning_free")]
    private static partial void FreeNative(byte* buffer);

    public static string Version() => Marshal.PtrToStringUTF8(VersionNative())
        ?? throw new InvalidOperationException("Native version pointer is null.");

    /// <summary>Throws when the native core reports any problem; a silent success is impossible.</summary>
    public static void SelfTest()
    {
        var code = SelfTestNative();
        if (code != Ok) throw new InvalidOperationException($"native self-test failed with code {code}");
    }

    public static byte[] Process(byte[] input, uint operation, uint parameter = 0)
    {
        fixed (byte* pinned = input)
        {
            byte* output = null;
            nuint length = 0;
            var code = ProcessNative(pinned, (nuint)input.Length, operation, parameter, &output, &length);
            if (code != Ok) throw new InvalidOperationException($"lightning_process failed with code {code}");
            return Take(output, length);
        }
    }

    public static byte[] Encode(byte[] input, uint format, uint quality = 90)
    {
        fixed (byte* pinned = input)
        {
            byte* output = null;
            nuint length = 0;
            var code = EncodeNative(pinned, (nuint)input.Length, format, quality, &output, &length);
            if (code != Ok) throw new InvalidOperationException($"lightning_encode failed with code {code}");
            return Take(output, length);
        }
    }

    public static byte[] BrotliCompress(byte[] input, uint quality = 9, uint window = 22)
    {
        fixed (byte* pinned = input)
        {
            byte* output = null;
            nuint length = 0;
            var code = BrotliCompressNative(pinned, (nuint)input.Length, quality, window, &output, &length);
            if (code != Ok) throw new InvalidOperationException($"lightning_brotli_compress failed with code {code}");
            return Take(output, length);
        }
    }

    public static byte[] BrotliDecompress(byte[] input)
    {
        fixed (byte* pinned = input)
        {
            byte* output = null;
            nuint length = 0;
            var code = BrotliDecompressNative(pinned, (nuint)input.Length, &output, &length);
            if (code != Ok) throw new InvalidOperationException($"lightning_brotli_decompress failed with code {code}");
            return Take(output, length);
        }
    }

    [LibraryImport(Library, EntryPoint = "lightning_batch_process")]
    private static partial int BatchProcessNative(byte** inputs, nuint* lengths, nuint count, uint operation, uint parameter,
        byte** outputs, nuint* outputLengths, int* codes);

    /// <summary>
    /// Runs one operation over many images on the native Rayon pool. Buffers are copied out and
    /// freed one by one; a failing item is reported, never replaced with a placeholder result.
    /// </summary>
    public static byte[][] Batch(byte[][] items, uint operation, uint parameter = 0)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Length == 0) return [];
        if (items.Length > 256 || items.Any(item => item is null) || items.Sum(item => (long)item.Length) > 64L * 1024 * 1024)
            throw new ArgumentException("Supply at most 256 non-null images and 64 MiB of input.");
        var count = (nuint)items.Length;
        var handles = new GCHandle[items.Length];
        byte** inputs = null; nuint* lengths = null; byte** outputs = null;
        nuint* outputLengths = null; int* codes = null;
        try
        {
            inputs = (byte**)NativeMemory.AllocZeroed(count, (nuint)sizeof(byte*));
            lengths = (nuint*)NativeMemory.AllocZeroed(count, (nuint)sizeof(nuint));
            outputs = (byte**)NativeMemory.AllocZeroed(count, (nuint)sizeof(byte*));
            outputLengths = (nuint*)NativeMemory.AllocZeroed(count, (nuint)sizeof(nuint));
            codes = (int*)NativeMemory.AllocZeroed(count, (nuint)sizeof(int));
            for (var index = 0; index < items.Length; index++)
            {
                handles[index] = GCHandle.Alloc(items[index], GCHandleType.Pinned);
                inputs[index] = (byte*)handles[index].AddrOfPinnedObject();
                lengths[index] = (nuint)items[index].Length;
                outputs[index] = null;
                outputLengths[index] = 0;
                codes[index] = 0;
            }
            var call = BatchProcessNative(inputs, lengths, count, operation, parameter, outputs, outputLengths, codes);
            if (call != Ok) throw new InvalidOperationException($"lightning_batch_process failed with code {call}");
            var results = new byte[items.Length][];
            var failures = new List<string>();
            for (var index = 0; index < items.Length; index++)
            {
                if (codes[index] != Ok)
                {
                    failures.Add($"item {index}: code {codes[index]}");
                    results[index] = [];
                    continue;
                }
                var length = outputLengths[index];
                var buffer = new byte[checked((int)length)];
                if (length > 0) Marshal.Copy((IntPtr)outputs[index], buffer, 0, buffer.Length);
                results[index] = buffer;
            }
            if (failures.Count > 0)
                throw new InvalidOperationException("native batch rejected " + string.Join(", ", failures));
            return results;
        }
        finally
        {
            if (outputs != null)
                for (var index = 0; index < items.Length; index++)
                    if (outputs[index] != null) FreeNative(outputs[index]);
            foreach (var handle in handles)
                if (handle.IsAllocated) handle.Free();
            NativeMemory.Free(inputs);
            NativeMemory.Free(lengths);
            NativeMemory.Free(outputs);
            NativeMemory.Free(outputLengths);
            NativeMemory.Free(codes);
        }
    }

    private static byte[] Take(byte* buffer, nuint length)
    {
        try
        {
            var result = new byte[checked((int)length)];
            if (length > 0) Marshal.Copy((IntPtr)buffer, result, 0, result.Length);
            return result;
        }
        finally
        {
            FreeNative(buffer);
        }
    }
}
