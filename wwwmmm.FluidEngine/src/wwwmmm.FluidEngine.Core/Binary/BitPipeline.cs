using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;
using wwwmmm.FluidEngine.Core.Contracts;
using wwwmmm.FluidEngine.Core.Context;

namespace wwwmmm.FluidEngine.Core.Binary;

public sealed class BitPipeline : IFluidStep<byte[], byte[]>
{
    public string Name => "BitPipeline/Brotli";

    public Task<byte[]> ExecuteAsync(byte[] input, PipelineContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        var sw = Stopwatch.StartNew();

        using var output = new MemoryStream();
        using (var brotli = new BrotliStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            brotli.Write(input, 0, input.Length);
        }

        var compressed = output.ToArray();
        var framed = new byte[7 + compressed.Length];
        framed[0] = 0x43;
        framed[1] = 0x4C;
        framed[2] = 0x01;
        BinaryPrimitives.WriteInt32LittleEndian(framed.AsSpan(3, 4), input.Length);
        compressed.CopyTo(framed.AsSpan(7));

        sw.Stop();
        context.RecordStep(Name, "Brotli/Binary", sw.Elapsed.TotalMilliseconds, true);
        return Task.FromResult(framed);
    }
}
