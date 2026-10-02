using System.Security.Cryptography;
using System.Text;

namespace wwwmmm.Web;

internal static class RuntimeSecurity
{
    public static bool IsProduction => new[] { "LSPP_ENVIRONMENT", "DOTNET_ENVIRONMENT", "ASPNETCORE_ENVIRONMENT" }
        .Any(name => string.Equals(Environment.GetEnvironmentVariable(name), "Production", StringComparison.OrdinalIgnoreCase));

    public static string DataDirectory => Environment.GetEnvironmentVariable("LSPP_DATA_DIR")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LightningSPP");

    public static bool SecretEquals(string expected, string supplied) => CryptographicOperations.FixedTimeEquals(
        SHA256.HashData(Encoding.UTF8.GetBytes(expected)), SHA256.HashData(Encoding.UTF8.GetBytes(supplied)));

    public static void ValidateProduction()
    {
        if (!IsProduction) return;
        RequireSecret("LSPP_ADMIN_TOKEN");
        RequireSecret("LSPP_STORAGE_KEY");
        LightningSPP.Licensing.LicenseKey.ValidateProductionConfiguration();
        var directory = Environment.GetEnvironmentVariable("LSPP_DATA_DIR");
        if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory))
            throw new InvalidOperationException("Production requires an absolute LSPP_DATA_DIR.");
        Directory.CreateDirectory(directory);
        var probe = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".probe");
        using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) { }
    }

    public static void RequireSecret(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value) || Encoding.UTF8.GetByteCount(value) < 32 ||
            value.Contains("CHANGE", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"{name} must contain a private random secret of at least 32 bytes.");
    }
}

internal sealed class RequestRateLimiter
{
    private readonly Dictionary<string, (int Count, DateTimeOffset Start)> clients = new();
    private readonly object sync = new();
    public bool IsLimited(string ip, DateTimeOffset now)
    {
        lock (sync)
        {
            foreach (var key in clients.Where(x => now - x.Value.Start >= TimeSpan.FromMinutes(1)).Select(x => x.Key).ToArray())
                clients.Remove(key);
            if (!clients.TryGetValue(ip, out var entry))
            {
                if (clients.Count >= 4096) return true;
                entry = (0, now);
            }
            if (entry.Count >= 120) return true;
            clients[ip] = (entry.Count + 1, entry.Start);
            return false;
        }
    }
}
