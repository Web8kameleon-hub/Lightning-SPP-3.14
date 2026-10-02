using System.Diagnostics;
using System.IO.Compression;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace wwwmmm.Services.Implementation;

/// <summary>Brotli compression and explicitly configured ONNX float-tensor inference.</summary>
public class RealAIProcessor : IProcessingService
{
    public async Task<ProcessingResult> ProcessAsync(byte[] data, ProcessingOptions options, CancellationToken cancellationToken = default)
    {
        var clock = Stopwatch.StartNew();
        try
        {
            ArgumentNullException.ThrowIfNull(data);
            cancellationToken.ThrowIfCancellationRequested();
            if (options.Engine == ProcessingEngine.Clisonic || options.UseAI || options.UseClisonic)
                return RunInference(data, clock, cancellationToken);
            return options.Engine switch
            {
                ProcessingEngine.CLX => await ClxProcessAsync(data, cancellationToken),
                ProcessingEngine.CLI_I => await CliProcessAsync(data, cancellationToken),
                ProcessingEngine.XCL or ProcessingEngine.Hybrid => await XclProcessAsync(data, cancellationToken),
                ProcessingEngine.Auto => data.Length < 1024 ? await ClxProcessAsync(data, cancellationToken)
                    : await CliProcessAsync(data, cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(options.Engine))
            };
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return new ProcessingResult([], new ProcessingMetadata(DateTime.UtcNow, clock.ElapsedMilliseconds,
                "Unavailable", 0, new() { ["Error"] = ex.Message }), false, ex.Message);
        }
    }

    public Task<ProcessingResult> ClxProcessAsync(byte[] data, CancellationToken cancellationToken = default) =>
        CompressAsync(data, CompressionLevel.Fastest, cancellationToken);
    public Task<ProcessingResult> CliProcessAsync(byte[] data, CancellationToken cancellationToken = default) =>
        CompressAsync(data, CompressionLevel.Optimal, cancellationToken);
    public Task<ProcessingResult> XclProcessAsync(byte[] data, CancellationToken cancellationToken = default) =>
        CompressAsync(data, CompressionLevel.SmallestSize, cancellationToken);

    private static async Task<ProcessingResult> CompressAsync(byte[] data, CompressionLevel level, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(data);
        var clock = Stopwatch.StartNew();
        using var output = new MemoryStream();
        using (var compressor = new BrotliStream(output, level, leaveOpen: true))
            await compressor.WriteAsync(data, token);
        var bytes = output.ToArray();
        return new ProcessingResult(bytes, new ProcessingMetadata(DateTime.UtcNow, clock.ElapsedMilliseconds,
            "Brotli-" + level, (int)level, new()
            {
                ["Algorithm"] = "Brotli", ["UseAI"] = false,
                ["InputBytes"] = data.Length, ["OutputBytes"] = bytes.Length,
                ["CompressionRatio"] = (double)data.Length / bytes.Length
            }), true);
    }

    private static ProcessingResult RunInference(byte[] data, Stopwatch clock, CancellationToken token)
    {
        var model = Environment.GetEnvironmentVariable("LSPP_ONNX_MODEL_PATH");
        if (string.IsNullOrWhiteSpace(model) || !File.Exists(model))
            throw new InvalidOperationException("AI inference unavailable: configure an existing LSPP_ONNX_MODEL_PATH.");
        // Explicit raw float32 contract; never reinterpret an image or compressed artifact as model features.
        var shapeText = Environment.GetEnvironmentVariable("LSPP_ONNX_INPUT_SHAPE");
        if (string.IsNullOrWhiteSpace(shapeText))
            throw new InvalidOperationException("Set LSPP_ONNX_INPUT_SHAPE and provide little-endian float32 tensor bytes.");
        var shape = shapeText.Split(',').Select(int.Parse).ToArray();
        if (shape.Length == 0 || shape.Any(x => x < 1)) throw new ArgumentException("Invalid ONNX input shape.");
        var count = shape.Aggregate(1L, (a, b) => checked(a * b));
        if (checked(count * sizeof(float)) != data.LongLength) throw new ArgumentException("Input bytes do not match the configured float32 tensor shape.");
        var values = new float[checked((int)count)];
        for (var i = 0; i < values.Length; i++)
            values[i] = System.Buffers.Binary.BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(i * 4, 4));
        using var session = new InferenceSession(model);
        if (session.InputMetadata.Count != 1 || session.OutputMetadata.Count != 1)
            throw new InvalidOperationException("This adapter requires exactly one float tensor input and output.");
        var input = session.InputMetadata.Single();
        if (input.Value.ElementType != typeof(float)) throw new InvalidOperationException("Model input must be float32.");
        using var run = new RunOptions();
        using var registration = token.Register(() => run.Terminate = true);
        using var result = session.Run(new[] { NamedOnnxValue.CreateFromTensor(input.Key, new DenseTensor<float>(values, shape)) }, session.OutputNames, run);
        token.ThrowIfCancellationRequested();
        var tensor = result.Single().AsTensor<float>();
        var floats = tensor.ToArray();
        var bytes = new byte[checked(floats.Length * 4)];
        for (var i = 0; i < floats.Length; i++)
            System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * 4, 4), floats[i]);
        return new ProcessingResult(bytes, new ProcessingMetadata(DateTime.UtcNow, clock.ElapsedMilliseconds,
            "ONNX Runtime", 0, new()
            {
                ["UseAI"] = true, ["Model"] = Path.GetFileName(model),
                ["OutputShape"] = tensor.Dimensions.ToArray(), ["OutputFormat"] = "float32-le"
            }), true);
    }
}
