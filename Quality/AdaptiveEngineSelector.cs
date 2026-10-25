namespace wwwmmm.Quality;

public enum SelectionPolicy { SpeedBiased, QualityBiased, Balanced }

public sealed record EngineCandidate(
    string Id,
    Func<ImagePixelBuffer, CancellationToken, Task<ImagePixelBuffer>> Process);

public sealed record EngineBenchmarkResult(
    string EngineId, double QualityScore, double TimeMs, bool Failed, string? Error);

public sealed record EngineSelection(
    string EngineId, double QualityScore, double TimeMs,
    string Rationale, IReadOnlyList<EngineBenchmarkResult> All);

public sealed class AdaptiveEngineSelector
{
    private readonly IImageQualityMetric _metric;
    private readonly InputAnalyzer _analyzer;

    public AdaptiveEngineSelector(IImageQualityMetric metric, InputAnalyzer analyzer)
    {
        _metric = metric ?? throw new ArgumentNullException(nameof(metric));
        _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
    }

    public async Task<EngineSelection> SelectAsync(
        IReadOnlyList<EngineCandidate> candidates,
        ImagePixelBuffer source,
        SelectionPolicy policy,
        CancellationToken ct = default)
    {
        if (candidates.Count == 0) throw new ArgumentException("No candidates.");
        var analysis = _analyzer.Analyze(source);
        var crop = CropForBenchmark(source);
        var results = new List<EngineBenchmarkResult>(candidates.Count);

        foreach (var c in candidates)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var output = await c.Process(crop, ct).ConfigureAwait(false);
                sw.Stop();
                if (output.Width != crop.Width || output.Height != crop.Height)
                    throw new InvalidOperationException("Candidate changed dimensions.");
                var score = _metric.Score(crop, output);
                results.Add(new EngineBenchmarkResult(c.Id, score, sw.Elapsed.TotalMilliseconds, false, null));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                results.Add(new EngineBenchmarkResult(c.Id, 0, 0, true, ex.Message));
            }
        }

        var survivors = results.Where(r => !r.Failed).ToList();
        if (survivors.Count == 0)
            throw new InvalidOperationException(
                "All candidates failed. Errors: " +
                string.Join(" | ", results.Select(r => $"{r.EngineId}: {r.Error}")));

        var winner = survivors.OrderByDescending(r => Score(r, policy)).First();
        return new EngineSelection(
            winner.EngineId, winner.QualityScore, winner.TimeMs,
            $"{policy}: {analysis.Tier} input, {survivors.Count}/{candidates.Count} survived",
            results);
    }

    private static double Score(EngineBenchmarkResult r, SelectionPolicy p) => p switch
    {
        SelectionPolicy.QualityBiased => r.QualityScore,
        SelectionPolicy.SpeedBiased   => -r.TimeMs,
        SelectionPolicy.Balanced      => r.QualityScore / Math.Log10(r.TimeMs + 10),
        _ => throw new ArgumentOutOfRangeException(nameof(p))
    };

    private static ImagePixelBuffer CropForBenchmark(ImagePixelBuffer src)
    {
        const int side = 256;
        var w = Math.Min(side, src.Width);
        var h = Math.Min(side, src.Height);
        var x0 = (src.Width - w) / 2;
        var y0 = (src.Height - h) / 2;
        var outBuf = new byte[h * w * src.Channels];
        var srcSpan = src.Data.Span;
        for (int y = 0; y < h; y++)
            srcSpan.Slice((y0 + y) * src.Stride + x0 * src.Channels, w * src.Channels)
                   .CopyTo(outBuf.AsSpan(y * w * src.Channels, w * src.Channels));
        return new ImagePixelBuffer(outBuf, w, h, src.Channels, w * src.Channels);
    }
}
