using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using wwwmmm.FluidEngine.Core.Binary;
using wwwmmm.FluidEngine.Core.Context;

namespace wwwmmm.SDK;

public static class Lightning314
{
    public static async Task<byte[]> Instant(byte[] data)
    {
        using var output = new MemoryStream();
        using (var brotli = new BrotliStream(output, CompressionLevel.Fastest))
            await brotli.WriteAsync(data);
        return output.ToArray();
    }

    public static async Task<byte[]> UltraFast(byte[] data)
    {
        using var output = new MemoryStream();
        using (var brotli = new BrotliStream(output, CompressionLevel.Optimal))
            await brotli.WriteAsync(data);
        return output.ToArray();
    }

    public static async Task<byte[]> Lightning(byte[] data)
    {
        using var output = new MemoryStream();
        using (var brotli = new BrotliStream(output, CompressionLevel.SmallestSize))
            await brotli.WriteAsync(data);
        return output.ToArray();
    }

    public static async IAsyncEnumerable<byte[]> Stream(IAsyncEnumerable<byte[]> input)
    {
        await foreach (var item in input)
            yield return await Instant(item);
    }

    public static async Task<byte[][]> Parallel(byte[][] inputs, int maxConcurrency = 100)
    {
        using var semaphore = new SemaphoreSlim(maxConcurrency);
        var tasks = inputs.Select(async input =>
        {
            await semaphore.WaitAsync();
            try { return await Instant(input); }
            finally { semaphore.Release(); }
        });
        return await Task.WhenAll(tasks);
    }
}

public static class MedicalAPI
{
    public static async Task<MedicalResult> ProcessMedicalScan(byte[] scanData, MedicalDeviceType deviceType)
    {
        ArgumentNullException.ThrowIfNull(scanData);
        if (scanData.Length == 0) throw new ArgumentException("Scan data must not be empty.", nameof(scanData));
        if (!Enum.IsDefined(deviceType)) throw new ArgumentOutOfRangeException(nameof(deviceType));
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var processed = await Lightning314.UltraFast(scanData);
        using var encoded = new MemoryStream(processed, writable: false);
        using var decoder = new BrotliStream(encoded, CompressionMode.Decompress);
        var buffer = new byte[8192];
        var offset = 0;
        int read;
        var isValid = true;
        while ((read = await decoder.ReadAsync(buffer)) > 0)
        {
            if (read > scanData.Length - offset || !buffer.AsSpan(0, read).SequenceEqual(scanData.AsSpan(offset, read)))
            { isValid = false; break; }
            offset += read;
        }
        isValid &= offset == scanData.Length;
        var elapsed = clock.Elapsed.TotalMilliseconds;
        return new MedicalResult(processed, isValid, elapsed, deviceType.ToString());
    }
}

public record MedicalResult(byte[] Data, bool IsValid, double ProcessingTimeMs, string DeviceType);

public enum MedicalDeviceType { Scanner, Monitor, DiagnosticDevice, ImagingDevice, LabEquipment }

public static class MobileAPI
{
    public static async Task<byte[]> ProcessMobile(byte[] data, int batteryLevel)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (batteryLevel is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(batteryLevel));
        return batteryLevel < 50 ? await Lightning314.Instant(data) : await Lightning314.UltraFast(data);
    }

    public static async Task<byte[]> ProcessBackground(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return await Lightning314.Instant(data);
    }
}

public static class IoTAPI
{
    /// <summary>
    /// Legacy contract: returns raw Brotli bytes.
    /// </summary>
    public static async Task<byte[]> ProcessConstrained(byte[] data, int maxMemoryKB = 64)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (maxMemoryKB is < 1 or > int.MaxValue / 1024)
            throw new ArgumentOutOfRangeException(nameof(maxMemoryKB));
        return await Lightning314.Instant(data);
    }

    /// <summary>
    /// New framed contract: returns a 7-byte header (CL + version + length) followed by Brotli bytes.
    /// </summary>
    public static async Task<byte[]> ProcessConstrainedWithHeader(byte[] data, int maxMemoryKB = 64)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (maxMemoryKB is < 1 or > int.MaxValue / 1024)
            throw new ArgumentOutOfRangeException(nameof(maxMemoryKB));
        var context = new PipelineContext();
        var pipeline = new BitPipeline();
        return await pipeline.ExecuteAsync(data, context, default);
    }

    /// <summary>
    /// Unpacks either a raw Brotli stream or a framed BitPipeline stream.
    /// </summary>
    public static async Task<byte[]> ProcessConstrainedUnpack(byte[] compressedData)
    {
        if (compressedData.Length >= 7 && compressedData[0] == 0x43 && compressedData[1] == 0x4C)
        {
            var context = new PipelineContext();
            var unpacker = new BitUnpacker();
            return await unpacker.ExecuteAsync(compressedData, context, default);
        }
        using var input = new MemoryStream(compressedData);
        using var brotli = new BrotliStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        await brotli.CopyToAsync(output);
        return output.ToArray();
    }

    public static async IAsyncEnumerable<byte[]> ProcessStream(IAsyncEnumerable<byte[]> sensorData)
    {
        await foreach (var data in sensorData)
            yield return await Lightning314.Instant(data);
    }
}

public static class CloudAPI
{
    public static async Task<CloudBatchResult> ProcessMassive(byte[][] data, int maxParallelism = 1000)
    {
        var startTime = DateTime.UtcNow;
        var results = await Lightning314.Parallel(data, maxParallelism);
        var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
        var throughput = data.Length / (elapsed / 1000.0);
        return new CloudBatchResult(results, data.Length, elapsed, throughput);
    }
}

public record CloudBatchResult(byte[][] Results, int ItemsProcessed, double TotalTimeMs, double ItemsPerSecond);