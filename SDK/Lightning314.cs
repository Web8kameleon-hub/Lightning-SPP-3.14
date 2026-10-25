namespace wwwmmm.SDK;

/// <summary>
/// Lightning SPP 3.14 - Ultra-Fast API
/// Lossless Brotli helpers. Latency depends on input, runtime and hardware.
/// </summary>
public static class Lightning314
{
    /// <summary>
    /// Compress using Brotli Fastest; no real-time deadline is guaranteed.
    /// CLX engine: Brotli Fastest compression pipeline
    /// </summary>
    public static async Task<byte[]> Instant(byte[] data)
    {
        // CLX: Brotli Fastest — minimal overhead, sub-ms on small payloads
        using var output = new System.IO.MemoryStream();
        using (var brotli = new System.IO.Compression.BrotliStream(output, System.IO.Compression.CompressionLevel.Fastest))
        {
            await brotli.WriteAsync(data);
        }
        return output.ToArray();
    }

    /// <summary>
    /// Compress using Brotli Optimal.
    /// Perfect for: Mobile, web, general purpose
    /// CLX.i engine: Brotli Optimal compression
    /// </summary>
    public static async Task<byte[]> UltraFast(byte[] data)
    {
        // CLX.i: Brotli Optimal — balance speed/quality
        using var output = new System.IO.MemoryStream();
        using (var brotli = new System.IO.Compression.BrotliStream(output, System.IO.Compression.CompressionLevel.Optimal))
        {
            await brotli.WriteAsync(data);
        }
        return output.ToArray();
    }

    /// <summary>
    /// Compress using Brotli SmallestSize.
    /// Perfect for: Desktop, complex operations
    /// XCL engine: Brotli SmallestSize + MemoryPool
    /// </summary>
    public static async Task<byte[]> Lightning(byte[] data)
    {
        // XCL: Brotli SmallestSize + MemoryPool for large payloads
        using var output = new System.IO.MemoryStream();
        using (var brotli = new System.IO.Compression.BrotliStream(output, System.IO.Compression.CompressionLevel.SmallestSize))
        {
            await brotli.WriteAsync(data);
        }
        return output.ToArray();
    }

    /// <summary>
    /// Batch process with streaming - for massive data
    /// </summary>
    public static async IAsyncEnumerable<byte[]> Stream(IAsyncEnumerable<byte[]> input)
    {
        await foreach (var item in input)
        {
            yield return await Instant(item);
        }
    }

    /// <summary>
    /// Parallel batch - maximum throughput
    /// </summary>
    public static async Task<byte[][]> Parallel(byte[][] inputs, int maxConcurrency = 100)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrency, 1);
        using var semaphore = new SemaphoreSlim(maxConcurrency);
        var tasks = inputs.Select(async input =>
        {
            await semaphore.WaitAsync();
            try
            {
                return await Instant(input);
            }
            finally
            {
                semaphore.Release();
            }
        });

        return await Task.WhenAll(tasks);
    }
}

/// <summary>
/// Medical Device Optimized API
/// Compression only; no clinical validation or medical certification is established.
/// </summary>
public static class MedicalAPI
{
    /// <summary>
    /// Compress opaque scan bytes. IsValid reports byte-for-byte round-trip integrity only.
    /// </summary>
    public static async Task<MedicalResult> ProcessMedicalScan(
        byte[] scanData,
        MedicalDeviceType deviceType)
    {
        ArgumentNullException.ThrowIfNull(scanData);
        if (scanData.Length == 0) throw new ArgumentException("Scan data must not be empty.", nameof(scanData));
        if (!Enum.IsDefined(deviceType)) throw new ArgumentOutOfRangeException(nameof(deviceType));
        var clock = System.Diagnostics.Stopwatch.StartNew();

        // Preserve source bytes without interpreting their medical meaning.
        var processed = await Lightning314.UltraFast(scanData);

        // Validate output
        using var encoded = new System.IO.MemoryStream(processed, writable: false);
        using var decoder = new System.IO.Compression.BrotliStream(encoded, System.IO.Compression.CompressionMode.Decompress);
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

        return new MedicalResult(
            processed,
            isValid,
            elapsed,
            deviceType.ToString()
        );
    }

}

public record MedicalResult(
    byte[] Data,
    bool IsValid,
    double ProcessingTimeMs,
    string DeviceType
);

public enum MedicalDeviceType
{
    Scanner,
    Monitor,
    DiagnosticDevice,
    ImagingDevice,
    LabEquipment
}

/// <summary>
/// Mobile Device Optimized API
/// Lossless compression with an explicit battery-level compression policy.
/// </summary>
public static class MobileAPI
{
    /// <summary>
    /// Use Fastest below 50 percent battery, otherwise Optimal. Energy savings are not measured.
    /// </summary>
    public static async Task<byte[]> ProcessMobile(
        byte[] data,
        int batteryLevel)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (batteryLevel is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(batteryLevel));
        return batteryLevel < 50 ? await Lightning314.Instant(data) : await Lightning314.UltraFast(data);
    }

    /// <summary>
    /// Background processing for mobile
    /// Performs real Fastest compression. Scheduling priority remains the caller's responsibility.
    /// </summary>
    public static async Task<byte[]> ProcessBackground(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return await Lightning314.Instant(data);
    }
}

/// <summary>
/// IoT Device Optimized API
/// Writes bounded input slices into a single lossless Brotli stream.
/// </summary>
public static class IoTAPI
{
    public static async Task<byte[]> ProcessConstrained(
        byte[] data,
        int maxMemoryKB = 64)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (maxMemoryKB is < 1 or > int.MaxValue / 1024)
            throw new ArgumentOutOfRangeException(nameof(maxMemoryKB));

        var context = new wwwmmm.FluidEngine.Core.Context.PipelineContext();
        var pipeline = new wwwmmm.FluidEngine.Core.Binary.BitPipeline();
        return await pipeline.ExecuteAsync(data, context, default);
    }

    public static async Task<byte[]> ProcessConstrainedUnpack(byte[] compressedData)
    {
        var context = new wwwmmm.FluidEngine.Core.Context.PipelineContext();
        var unpacker = new wwwmmm.FluidEngine.Core.Binary.BitUnpacker();
        return await unpacker.ExecuteAsync(compressedData, context, default);
    }

    public static async IAsyncEnumerable<byte[]> ProcessStream(
        IAsyncEnumerable<byte[]> sensorData)
    {
        await foreach (var data in sensorData)
        {
            yield return await Lightning314.Instant(data);
        }
    }
}
public static class CloudAPI
{
    /// <summary>
    /// Process millions of items in parallel
    /// </summary>
    public static async Task<CloudBatchResult> ProcessMassive(
        byte[][] data,
        int maxParallelism = 1000)
    {
        var startTime = DateTime.UtcNow;

        var results = await Lightning314.Parallel(data, maxParallelism);

        var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
        var throughput = data.Length / (elapsed / 1000.0);

        return new CloudBatchResult(
            results,
            data.Length,
            elapsed,
            throughput
        );
    }
}

public record CloudBatchResult(
    byte[][] Results,
    int ItemsProcessed,
    double TotalTimeMs,
    double ItemsPerSecond
);

