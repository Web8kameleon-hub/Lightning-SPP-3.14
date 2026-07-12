using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using wwwmmm.Services;

namespace LightningSPP.Licensing;

internal static class SigningAuditTrail
{
    private const string AuditPathEnv = "LSPP_SIGNING_AUDIT_PATH";
    private static readonly object Sync = new();

    public static void Record(string kid, string? licenseKey, string status, string? reason = null)
    {
        try
        {
            var path = ResolveAuditPath();
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var entry = new
            {
                timestampUtc = DateTime.UtcNow.ToString("O"),
                kid,
                licenseHash = ComputeHashHex(licenseKey),
                status,
                schemaVersion = StigmaSchema.Version,
                reason
            };

            var line = JsonSerializer.Serialize(entry);
            lock (Sync)
            {
                using var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
                using var sw = new StreamWriter(fs, new UTF8Encoding(false));
                sw.WriteLine(line);
            }
        }
        catch
        {
            // Audit trail must not block licensing operations.
        }
    }

    private static string ResolveAuditPath()
    {
        var overridePath = Environment.GetEnvironmentVariable(AuditPathEnv);
        if (!string.IsNullOrWhiteSpace(overridePath))
            return overridePath;

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LightningSPP",
            "audit",
            "signing-audit.jsonl");
    }

    private static string? ComputeHashHex(string? licenseKey)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
            return null;

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(licenseKey));
        return Convert.ToHexString(hash);
    }
}
