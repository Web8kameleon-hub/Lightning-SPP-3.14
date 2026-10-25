using System.Text.Json;
using wwwmmm.Services;
using wwwmmm.Quality;
using wwwmmm.Services.Imaging;

namespace wwwmmm.Tools;

public static class AesBenchmarkCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Usage: wwwmmm aes-bench <input-directory> <report.json> [options]");
            Console.WriteLine("  --modes Lightning,NanoDecibel,Resonance   (default: Lightning)");
            Console.WriteLine("  --dpis 150,300,600                        (default: 300)");
            Console.WriteLine("  --min-dim 256                             (default: 256)");
            Console.WriteLine("  --max-images 50                           (default: 200)");
            return 2;
        }
        var dir = args[1];
        var reportPath = args[2];
        if (!Directory.Exists(dir))
        {
            Console.Error.WriteLine($"Input directory not found: {dir}");
            return 2;
        }

        var opts = ParseOptions(args, from: 3);
        var modes = ResolveModes(opts.ModeNames);
        if (modes.Count == 0)
        {
            Console.Error.WriteLine($"No valid ScanMode names. Tried: {string.Join(",", opts.ModeNames)}");
            Console.Error.WriteLine($"Valid ScanMode values: {string.Join(", ", Enum.GetNames<ScanMode>())}");
            return 2;
        }

        var files = Directory.EnumerateFiles(dir)
            .Where(IsImageFile)
            .OrderBy(f => f)
            .Take(opts.MaxImages)
            .ToList();
        if (files.Count == 0)
        {
            Console.Error.WriteLine($"No PNG/JPEG images found in {dir}.");
            return 2;
        }

        var adapter = AesBootstrap.CreateAdapter();
        var codec = new SkiaPngCodec();

        // Aggregation: (mode, dpi, engine) -> running sums
        var cells = new Dictionary<(string Mode, int Dpi), Dictionary<string, AggregateCell>>();
        var selectorWins = new Dictionary<(string Mode, int Dpi), Dictionary<string, int>>();
        int skippedSmall = 0, failedLoad = 0;

        Console.WriteLine($"Matrix: {modes.Count} mode(s) x {opts.Dpis.Length} DPI(s) x 2 engines x {files.Count} image(s)");

        foreach (var f in files)
        {
            ImagePixelBuffer source;
            try
            {
                var bytes = await File.ReadAllBytesAsync(f);
                source = codec.Decode(bytes);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Skip {Path.GetFileName(f)}: {ex.Message}");
                failedLoad++;
                continue;
            }
            if (source.Width < opts.MinDimension || source.Height < opts.MinDimension)
            {
                skippedSmall++;
                continue;
            }

            foreach (var mode in modes)
            foreach (var dpi in opts.Dpis)
            {
                var key = (mode.ToString(), dpi);
                if (!cells.TryGetValue(key, out var cell))
                {
                    cell = new Dictionary<string, AggregateCell>();
                    cells[key] = cell;
                    selectorWins[key] = new Dictionary<string, int>();
                }

                var breakdowns = new Dictionary<string, QualityBreakdown>();
                foreach (var engine in new[] { ImageEngine.Skia, ImageEngine.ImageSharp })
                {
                    var id = engine.ToString();
                    if (!cell.TryGetValue(id, out var agg))
                    {
                        agg = new AggregateCell();
                        cell[id] = agg;
                    }

                    try
                    {
                        var candidate = ImageEngineCandidates.For(adapter, engine, codec, mode, dpi);
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        var output = await candidate.Process(source, CancellationToken.None);
                        sw.Stop();
                        var b = QualityBreakdownCalculator.Compute(source, output);
                        breakdowns[id] = b;
                        agg.Add(b, sw.Elapsed.TotalMilliseconds);
                    }
                    catch (Exception ex)
                    {
                        agg.Fail(ex.Message);
                    }
                }

                foreach (var policy in new[] { SelectionPolicy.QualityBiased, SelectionPolicy.SpeedBiased, SelectionPolicy.Balanced })
                {
                    if (breakdowns.Count == 0) break;
                    var best = breakdowns
                        .OrderByDescending(kv => ScoreForPolicy(kv.Value, policy))
                        .First().Key;
                    var wins = selectorWins[key];
                    wins[best] = wins.TryGetValue(best, out var n) ? n + 1 : 1;
                }
            }
        }

        var report = BuildReport(opts, modes, files.Count, skippedSmall, failedLoad, cells, selectorWins);
        await File.WriteAllTextAsync(reportPath,
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));

        Console.WriteLine();
        Console.WriteLine($"{"Mode",-14} {"DPI",-6} {"Engine",-12} {"N",-4} {"Fail",-5} {"SSIM",-8} {"MS-SSIM",-8} {"PSNR",-8} {"Sharp",-8} {"Grad",-8} {"Composite",-10}");
        foreach (var ((mode, dpi), engines) in cells.OrderBy(k => k.Key.Mode).ThenBy(k => k.Key.Dpi))
        foreach (var (engine, agg) in engines.OrderBy(e => e.Key))
        {
            var m = agg.Mean();
            Console.WriteLine($"{mode,-14} {dpi,-6} {engine,-12} {agg.Count,-4} {agg.Failures,-5} " +
                $"{m.Ssim,-8:F4} {m.MsSsim,-8:F4} {m.PsnrNormalized,-8:F4} " +
                $"{m.SharpnessRatio,-8:F4} {m.GradientRatio,-8:F4} {m.Composite,-10:F4}");
        }
        Console.WriteLine();
        Console.WriteLine($"Report: {reportPath}");
        Console.WriteLine($"Skipped small: {skippedSmall}  Failed load: {failedLoad}");
        return 0;
    }

    private static bool IsImageFile(string f) =>
        f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
        || f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
        || f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase);

    private static List<ScanMode> ResolveModes(string[] names)
    {
        var result = new List<ScanMode>();
        foreach (var n in names)
            if (Enum.TryParse<ScanMode>(n, ignoreCase: true, out var m))
                result.Add(m);
        return result;
    }

    private static double ScoreForPolicy(QualityBreakdown b, SelectionPolicy p) => p switch
    {
        SelectionPolicy.QualityBiased => b.Composite,
        SelectionPolicy.SpeedBiased   => -0.001, // no timing data here; ties resolved stably
        SelectionPolicy.Balanced      => b.Composite,
        _ => b.Composite
    };

    private static BenchmarkOptions ParseOptions(string[] args, int from)
    {
        var o = new BenchmarkOptions();
        for (int i = from; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--modes": o.ModeNames = args[++i].Split(','); break;
                case "--dpis": o.Dpis = args[++i].Split(',').Select(int.Parse).ToArray(); break;
                case "--min-dim": o.MinDimension = int.Parse(args[++i]); break;
                case "--max-images": o.MaxImages = int.Parse(args[++i]); break;
            }
        }
        return o;
    }

    private static object BuildReport(
        BenchmarkOptions opts, List<ScanMode> modes, int totalFiles, int skippedSmall, int failedLoad,
        Dictionary<(string, int), Dictionary<string, AggregateCell>> cells,
        Dictionary<(string, int), Dictionary<string, int>> wins) => new
    {
        measured_at = DateTime.UtcNow.ToString("O"),
        options = new { modes = modes.Select(m => m.ToString()), dpis = opts.Dpis,
                        min_dim = opts.MinDimension, max_images = opts.MaxImages },
        corpus = new { files_seen = totalFiles, skipped_small = skippedSmall, failed_load = failedLoad },
        cells = cells.Select(kv => new
        {
            mode = kv.Key.Item1,
            dpi = kv.Key.Item2,
            engines = kv.Value.ToDictionary(
                e => e.Key,
                e => new
                {
                    count = e.Value.Count,
                    failures = e.Value.Failures,
                    mean = e.Value.Mean(),
                    errors = e.Value.ErrorMessages
                }),
            selector_wins = wins.TryGetValue(kv.Key, out var w) ? w : new Dictionary<string, int>()
        }).ToArray()
    };

    private sealed class BenchmarkOptions
    {
        public string[] ModeNames { get; set; } = { "Lightning" };
        public int[] Dpis { get; set; } = { 300 };
        public int MinDimension { get; set; } = 256;
        public int MaxImages { get; set; } = 200;
    }

    private sealed class AggregateCell
    {
        public int Count { get; private set; }
        public int Failures { get; private set; }
        private double _ssim, _msSsim, _psnr, _sharp, _grad, _comp;
        private double _msTotal;
        public List<string> ErrorMessages { get; } = new();

        public void Add(QualityBreakdown b, double ms)
        {
            Count++;
            _ssim += b.Ssim; _msSsim += b.MsSsim; _psnr += b.PsnrNormalized;
            _sharp += b.SharpnessRatio; _grad += b.GradientRatio; _comp += b.Composite;
            _msTotal += ms;
        }

        public void Fail(string message)
        {
            Failures++;
            ErrorMessages.Add(message);
        }

        public QualityBreakdown Mean()
        {
            if (Count == 0) return default;
            return new QualityBreakdown(
                _ssim / Count, _msSsim / Count, _psnr / Count,
                _sharp / Count, _grad / Count, _comp / Count);
        }
    }
}
