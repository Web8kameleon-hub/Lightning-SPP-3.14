using System.Buffers;
using System.Diagnostics;
using System.IO.Compression;

namespace wwwmmm.Services.Implementation;

/// <summary>
/// Creates durable, lossless output artifacts. This service does not claim to drive
/// a physical printer; printer adapters can consume the returned artifact path.
/// </summary>
public class RealPrintService : IPrintService
{
    private readonly string _outputDirectory;

    public RealPrintService(string? outputDirectory = null)
    {
        _outputDirectory = outputDirectory ?? Path.Combine(Path.GetTempPath(), "LightningSPP");
        Directory.CreateDirectory(_outputDirectory);
    }

    public async Task<PrintResult> PrintAsync(
        byte[] data,
        PrintOptions options,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            Validate(data, options);
            var usePremium = options.UseStigma && options.Quality == PrintQuality.Stigma;
            var level = options.Quality switch
            {
                PrintQuality.Draft => CompressionLevel.Fastest,
                PrintQuality.Normal => CompressionLevel.Optimal,
                PrintQuality.High or PrintQuality.Stigma => CompressionLevel.SmallestSize,
                _ => CompressionLevel.Optimal
            };

            var paths = new List<string>(options.Copies);
            for (var copy = 0; copy < options.Copies; copy++)
            {
                var path = BuildArtifactPath(options.Destination, usePremium ? "stigma" : "output", copy);
                await WriteLosslessArtifactAsync(data, path, level, cancellationToken);
                paths.Add(path);
            }

            sw.Stop();
            return new PrintResult(
                true,
                new PrintMetadata(
                    DateTime.UtcNow,
                    sw.ElapsedMilliseconds,
                    usePremium ? "Stigma Lossless Artifact" : "Lossless Artifact",
                    paths.Count,
                    paths[0]));
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new PrintResult(
                false,
                new PrintMetadata(DateTime.UtcNow, sw.ElapsedMilliseconds, "Error", 0, options.Destination),
                ex.Message);
        }
    }

    public Task<PrintResult> StigmaPrintAsync(
        byte[] data,
        CancellationToken cancellationToken = default) =>
        PrintAsync(
            data,
            new PrintOptions(Quality: PrintQuality.Stigma, UseStigma: true, UseResonance: true),
            cancellationToken);

    private static void Validate(byte[] data, PrintOptions options)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length == 0)
            throw new ArgumentException("Output data cannot be empty.", nameof(data));
        if (options.Copies < 1)
            throw new ArgumentOutOfRangeException(nameof(options.Copies), "Copies must be at least one.");
    }

    private string BuildArtifactPath(string destination, string prefix, int copy)
    {
        if (!string.IsNullOrWhiteSpace(destination) &&
            !string.Equals(destination, "default", StringComparison.OrdinalIgnoreCase))
        {
            var requested = Path.GetFullPath(destination);
            var directory = Path.GetDirectoryName(requested);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            return copy == 0
                ? EnsureBrotliExtension(requested)
                : Path.Combine(
                    directory ?? _outputDirectory,
                    $"{Path.GetFileNameWithoutExtension(requested)}_copy{copy}.br");
        }

        var suffix = copy == 0 ? string.Empty : $"_copy{copy}";
        return Path.Combine(
            _outputDirectory,
            $"{prefix}_{DateTime.UtcNow:yyyyMMdd_HHmmss_fffffff}_{Guid.NewGuid():N}{suffix}.br");
    }

    private static string EnsureBrotliExtension(string path) =>
        string.Equals(Path.GetExtension(path), ".br", StringComparison.OrdinalIgnoreCase)
            ? path
            : path + ".br";

    private static async Task WriteLosslessArtifactAsync(
        byte[] data,
        string path,
        CompressionLevel level,
        CancellationToken cancellationToken)
    {
        using var owner = MemoryPool<byte>.Shared.Rent(data.Length);
        data.AsSpan().CopyTo(owner.Memory.Span);
        var payload = owner.Memory[..data.Length];

        await using var output = new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var compressor = new BrotliStream(output, level, leaveOpen: false);
        await compressor.WriteAsync(payload, cancellationToken);
    }

    public string[] GetPrintedFiles()
    {
        if (!Directory.Exists(_outputDirectory))
            return Array.Empty<string>();

        return Directory.GetFiles(_outputDirectory, "*.br")
            .OrderByDescending(File.GetCreationTimeUtc)
            .ToArray();
    }

    public void ClearPrintQueue()
    {
        if (!Directory.Exists(_outputDirectory))
            return;

        foreach (var file in Directory.GetFiles(_outputDirectory, "*.br"))
        {
            try { File.Delete(file); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    public void CleanupOldFiles(int olderThanDays = 7)
    {
        if (olderThanDays < 0)
            throw new ArgumentOutOfRangeException(nameof(olderThanDays));
        if (!Directory.Exists(_outputDirectory))
            return;

        var cutoff = DateTime.UtcNow.AddDays(-olderThanDays);
        foreach (var file in Directory.GetFiles(_outputDirectory, "*.br"))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff)
                    File.Delete(file);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
