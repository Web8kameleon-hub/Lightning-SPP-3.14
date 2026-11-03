using System.Runtime.InteropServices;

namespace wwwmmm.Intake;

/// <summary>One supported or explicitly unsupported scanner transport.</summary>
public sealed record ScanProvider(string Name, string Transport, bool Available, string Detail);

/// <summary>
/// Reports which scanner transports exist on this host. It never pretends a device is
/// present: <see cref="ScanProvider.Available"/> means the transport exists, not that a
/// physical scanner will respond.
/// </summary>
public static class ScanProviderRegistry
{
    public const string SaneProvider = "sane";
    public const string WiaProvider = "wia";
    public const string TwainProvider = "twain";

    public static IReadOnlyList<ScanProvider> Probe() => new[]
    {
        ProbeSane(),
        ProbeWia(),
        ProbeTwain()
    };

    public static ScanProvider Find(string name) =>
        Probe().FirstOrDefault(provider =>
            string.Equals(provider.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown scan provider '{name}'. Known: {SaneProvider}, {WiaProvider}, {TwainProvider}.", nameof(name));

    private static ScanProvider ProbeSane()
    {
        if (!OperatingSystem.IsLinux())
            return new ScanProvider(SaneProvider, "scanimage", false, "SANE is only wired on Linux in this build.");

        var binary = ResolveOnPath("scanimage");
        return binary is null
            ? new ScanProvider(SaneProvider, "scanimage", false, "scanimage was not found on PATH; install sane-utils.")
            : new ScanProvider(SaneProvider, "scanimage", true, $"scanimage found at {binary}. Physical scanning is unverified in this repository.");
    }

    private static ScanProvider ProbeWia()
    {
        if (!OperatingSystem.IsWindows())
            return new ScanProvider(WiaProvider, "WIA COM", false, "WIA is a Windows-only transport.");

        try
        {
            var type = Type.GetTypeFromProgID("WIA.DeviceManager");
            return type is null
                ? new ScanProvider(WiaProvider, "WIA COM", false, "WIA.DeviceManager is not registered on this host.")
                : new ScanProvider(WiaProvider, "WIA COM", true, "WIA device manager is registered. Device acquisition is not implemented in this build.");
        }
        catch (COMException ex)
        {
            return new ScanProvider(WiaProvider, "WIA COM", false, $"WIA probe failed: {ex.Message}");
        }
    }

    private static ScanProvider ProbeTwain()
    {
        // Deliberately not advertised as available: a TWAIN data source manager must be
        // loaded as native code, which this build does not do. Reporting it available would
        // be a claim without a working acquisition path.
        return new ScanProvider(TwainProvider, "TWAIN DSM", false, "TWAIN requires a native data source manager; not implemented in this build.");
    }

    private static string? ResolveOnPath(string executable)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
            return null;

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory.Trim(), executable);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
