using LightningSPP.Licensing;
using Xunit;

namespace LightningSPP.Tests;

public class SigningAuditTrailTests
{
    private static readonly object EnvLock = new();

    [Fact]
    public void AuditTrail_Records_Success_WithoutSecrets()
    {
        lock (EnvLock)
        {
            var oldAuditPath = Environment.GetEnvironmentVariable("LSPP_SIGNING_AUDIT_PATH");
            var oldKeys = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEYS");
            var oldActive = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_ACTIVE_KID");

            var auditFile = Path.Combine(Path.GetTempPath(), $"signing-audit-{Guid.NewGuid():N}.jsonl");

            try
            {
                Environment.SetEnvironmentVariable("LSPP_SIGNING_AUDIT_PATH", auditFile);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEYS", "k1=alpha-secret;k2=beta-secret");
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_ACTIVE_KID", "k2");

                var lic = LicenseKey.Generate(LicenseKey.LicenseTier.Unlimited, "audit@acme.com", "ACME");
                Assert.StartsWith("LSP2-k2-", lic.Key, StringComparison.Ordinal);

                var lines = File.ReadAllLines(auditFile);
                Assert.NotEmpty(lines);
                var last = lines[^1];

                Assert.Contains("\"kid\":\"k2\"", last, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("\"status\":\"success\"", last, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("\"licenseHash\":\"", last, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("\"schemaVersion\":\"", last, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("alpha-secret", last, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("beta-secret", last, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                Environment.SetEnvironmentVariable("LSPP_SIGNING_AUDIT_PATH", oldAuditPath);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEYS", oldKeys);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_ACTIVE_KID", oldActive);
                if (File.Exists(auditFile))
                {
                    File.Delete(auditFile);
                }
            }
        }
    }

    [Fact]
    public void AuditTrail_Records_Failure_ForInvalidActiveKid()
    {
        lock (EnvLock)
        {
            var oldAuditPath = Environment.GetEnvironmentVariable("LSPP_SIGNING_AUDIT_PATH");
            var oldKeys = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEYS");
            var oldActive = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_ACTIVE_KID");
            var oldExp = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY_EXPIRATIONS");

            var auditFile = Path.Combine(Path.GetTempPath(), $"signing-audit-{Guid.NewGuid():N}.jsonl");

            try
            {
                var yesterday = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
                Environment.SetEnvironmentVariable("LSPP_SIGNING_AUDIT_PATH", auditFile);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEYS", "k1=alpha-secret");
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_ACTIVE_KID", "k1");
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY_EXPIRATIONS", $"k1={yesterday}");

                Assert.Throws<InvalidOperationException>(() =>
                    LicenseKey.Generate(LicenseKey.LicenseTier.Unlimited, "audit@acme.com", "ACME"));

                var lines = File.ReadAllLines(auditFile);
                Assert.NotEmpty(lines);
                var last = lines[^1];

                Assert.Contains("\"kid\":\"k1\"", last, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("\"status\":\"fail\"", last, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("\"licenseHash\":null", last, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                Environment.SetEnvironmentVariable("LSPP_SIGNING_AUDIT_PATH", oldAuditPath);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEYS", oldKeys);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_ACTIVE_KID", oldActive);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY_EXPIRATIONS", oldExp);
                if (File.Exists(auditFile))
                {
                    File.Delete(auditFile);
                }
            }
        }
    }
}
