namespace wwwmmm.Services;

/// <summary>
/// Controls the complete Scan-Process-Print execution path.
/// Values intentionally match WWWMMM_STIGMA_LEVEL: 1=quality, 2=balanced, 3=lightning.
/// </summary>
public enum StigmaExecutionProfile
{
    Quality = 1,
    Balanced = 2,
    Lightning = 3
}

public static class StigmaProfiles
{
    public const string EnvironmentVariable = "WWWMMM_STIGMA_LEVEL";

    public static StigmaExecutionProfile FromEnvironment(
        StigmaExecutionProfile fallback = StigmaExecutionProfile.Balanced)
    {
        var value = Environment.GetEnvironmentVariable(EnvironmentVariable)?.Trim();
        if (string.IsNullOrEmpty(value))
            return fallback;

        if (int.TryParse(value, out var numeric) &&
            Enum.IsDefined(typeof(StigmaExecutionProfile), numeric))
            return (StigmaExecutionProfile)numeric;

        if (Enum.TryParse<StigmaExecutionProfile>(value, true, out var named))
            return named;

        throw new InvalidOperationException(
            $"{EnvironmentVariable} must be 1 (quality), 2 (balanced), or 3 (lightning); received '{value}'.");
    }

    public static PipelineRequest Apply(PipelineRequest request, StigmaExecutionProfile profile)
    {
        return profile switch
        {
            StigmaExecutionProfile.Quality => request with
            {
                ScanMode = ScanMode.NanoDecibel,
                Quality = Math.Max(request.Quality, 600),
                UseResonance = true,
                UseNanoDecibel = true,
                ProcessingEngine = ProcessingEngine.Hybrid,
                UseAI = true,
                UseClisonic = true,
                OptimizationLevel = 5,
                UseStigmaMemory = true,
                PrintQuality = PrintQuality.Stigma,
                UseStigma = true
            },
            StigmaExecutionProfile.Balanced => request with
            {
                ScanMode = ScanMode.TideWave,
                Quality = Math.Clamp(request.Quality, 200, 400),
                UseResonance = true,
                UseNanoDecibel = false,
                ProcessingEngine = ProcessingEngine.Auto,
                UseAI = true,
                UseClisonic = true,
                OptimizationLevel = 3,
                UseStigmaMemory = true,
                PrintQuality = PrintQuality.High,
                UseStigma = false
            },
            StigmaExecutionProfile.Lightning => request with
            {
                ScanMode = ScanMode.Lightning,
                Quality = Math.Min(request.Quality, 200),
                UseResonance = false,
                UseNanoDecibel = false,
                ProcessingEngine = ProcessingEngine.CLX,
                UseAI = false,
                UseClisonic = false,
                OptimizationLevel = 1,
                UseStigmaMemory = false,
                PrintQuality = PrintQuality.Normal,
                UseStigma = false
            },
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, null)
        };
    }
}
