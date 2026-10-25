using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;
using wwwmmm.FluidEngine.Core.Contracts;
using wwwmmm.FluidEngine.Core.Context;

namespace wwwmmm.FluidEngine.Core.Binary;

public sealed class BitUnpacker : IFluidStep<byte[], byte[]>
{
    public string Name => "BitUnpacker/Validate";

    public Task<byte[]> ExecuteAsync(byte[] input, PipelineContext context, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (input.Length < 7 || input[0] != 0x43 || input[1] != 0x4C || input[2] != 0x01)
                throw new InvalidOperationException("BitPipeline: Invalid header or unsupported version.");

            var originalLength = BinaryPrimitives.ReadInt32LittleEndian(input.AsSpan(3, 4));
            if (originalLength < 0 || originalLength > 100_000_000)
                throw new InvalidOperationException("BitPipeline: Invalid payload length bounds.");

            using var inputStream = new MemoryStream(input, 7, input.Length - 7);
            using var brotli = new BrotliStream(inputStream, CompressionMode.Decompress);
            using var output = new MemoryStream(originalLength);
            brotli.CopyTo(output);
            var result = output.ToArray();

            if (result.Length != originalLength)
                throw new InvalidOperationException($"BitPipeline: Length mismatch. Expected {originalLength}, got {result.Length}.");

            sw.Stop();
            context.RecordStep(Name, "Brotli/Binary", sw.Elapsed.TotalMilliseconds, true);
            return Task.FromResult(result);
        }
        catch (Exception)
        {
            sw.Stop();
            context.RecordStep(Name, "Brotli/Binary", sw.Elapsed.TotalMilliseconds, false);
            throw;
        }
    }
}
