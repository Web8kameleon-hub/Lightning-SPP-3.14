using LightningSPP.Licensing;
using Xunit;

namespace LightningSPP.Tests;

public class LicenseKeyRotationTests
{
    private static readonly object EnvLock = new();

    [Fact]
    public void SignedKey_WithKidRotation_ValidatesAcrossRollover()
    {
        lock (EnvLock)
        {
            var oldPacked = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEYS");
            var oldActive = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_ACTIVE_KID");
            var oldSingle = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY");
            var oldExp = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY_EXPIRATIONS");
            var oldRollouts = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY_ROLLOUTS");
            var oldGrace = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_GRACE_DAYS");
            var oldDeprec = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_DEPRECATION_DAYS");
            var oldSecondary = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_SECONDARY_KID");
            var oldSecondaryUntil = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_SECONDARY_KID_UNTIL_UTC");

            try
            {
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEYS", "k1=alpha-secret;k2=beta-secret");
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_ACTIVE_KID", "k1");
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY", null);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY_EXPIRATIONS", null);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY_ROLLOUTS", null);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_GRACE_DAYS", null);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_DEPRECATION_DAYS", null);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_SECONDARY_KID", null);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_SECONDARY_KID_UNTIL_UTC", null);

                var keyFromK1 = LicenseKey.Generate(LicenseKey.LicenseTier.Unlimited, "a@b.com", "ACME").Key;
                Assert.StartsWith("LSP2-k1-", keyFromK1, StringComparison.Ordinal);

                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_ACTIVE_KID", "k2");
                var keyFromK2 = LicenseKey.Generate(LicenseKey.LicenseTier.Unlimited, "a@b.com", "ACME").Key;
                Assert.StartsWith("LSP2-k2-", keyFromK2, StringComparison.Ordinal);

                Assert.True(LicenseKey.Validate(keyFromK1, out _));
                Assert.True(LicenseKey.Validate(keyFromK2, out _));
            }
            finally
            {
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEYS", oldPacked);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_ACTIVE_KID", oldActive);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY", oldSingle);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY_EXPIRATIONS", oldExp);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY_ROLLOUTS", oldRollouts);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_GRACE_DAYS", oldGrace);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_DEPRECATION_DAYS", oldDeprec);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_SECONDARY_KID", oldSecondary);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_SECONDARY_KID_UNTIL_UTC", oldSecondaryUntil);
            }
        }
    }

    [Fact]
    public void SignedKey_WithExpiredActiveKid_ThrowsUntilRotated()
    {
        lock (EnvLock)
        {
            var oldPacked = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEYS");
            var oldActive = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_ACTIVE_KID");
            var oldSingle = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY");
            var oldExp = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY_EXPIRATIONS");
            var oldRollouts = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY_ROLLOUTS");
            var oldGrace = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_GRACE_DAYS");
            var oldDeprec = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_DEPRECATION_DAYS");
            var oldSecondary = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_SECONDARY_KID");
            var oldSecondaryUntil = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_SECONDARY_KID_UNTIL_UTC");

            try
            {
                var yesterday = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
                var tomorrow = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd");

                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEYS", "k1=alpha-secret;k2=beta-secret");
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_ACTIVE_KID", "k1");
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY", null);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY_EXPIRATIONS", $"k1={yesterday};k2={tomorrow}");
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY_ROLLOUTS", null);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_GRACE_DAYS", null);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_DEPRECATION_DAYS", null);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_SECONDARY_KID", null);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_SECONDARY_KID_UNTIL_UTC", null);

                Assert.Throws<InvalidOperationException>(() =>
                    LicenseKey.Generate(LicenseKey.LicenseTier.Unlimited, "a@b.com", "ACME"));

                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_ACTIVE_KID", "k2");
                var keyFromK2 = LicenseKey.Generate(LicenseKey.LicenseTier.Unlimited, "a@b.com", "ACME").Key;
                Assert.StartsWith("LSP2-k2-", keyFromK2, StringComparison.Ordinal);
                Assert.True(LicenseKey.Validate(keyFromK2, out _));
            }
            finally
            {
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEYS", oldPacked);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_ACTIVE_KID", oldActive);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY", oldSingle);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY_EXPIRATIONS", oldExp);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY_ROLLOUTS", oldRollouts);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_GRACE_DAYS", oldGrace);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_DEPRECATION_DAYS", oldDeprec);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_SECONDARY_KID", oldSecondary);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_SECONDARY_KID_UNTIL_UTC", oldSecondaryUntil);
            }
        }
    }

    [Fact]
    public void SignedKey_RolloverPhases_GraceAndDeprecatedValidate_InvalidRejected()
    {
        lock (EnvLock)
        {
            var oldPacked = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEYS");
            var oldActive = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_ACTIVE_KID");
            var oldSingle = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY");
            var oldExp = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY_EXPIRATIONS");
            var oldRollouts = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY_ROLLOUTS");
            var oldGrace = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_GRACE_DAYS");
            var oldDeprec = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_DEPRECATION_DAYS");
            var oldSecondary = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_SECONDARY_KID");
            var oldSecondaryUntil = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_SECONDARY_KID_UNTIL_UTC");

            try
            {
                var d10 = DateTime.UtcNow.AddDays(-10).ToString("yyyy-MM-dd");
                var d40 = DateTime.UtcNow.AddDays(-40).ToString("yyyy-MM-dd");
                var d70 = DateTime.UtcNow.AddDays(-70).ToString("yyyy-MM-dd");

                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEYS", "kGrace=s1;kDep=s2;kInv=s3;kNew=s4");
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY", null);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY_EXPIRATIONS", null);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_GRACE_DAYS", "30");
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_DEPRECATION_DAYS", "60");
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY_ROLLOUTS", $"kGrace={d10};kDep={d40};kInv={d70};kNew={d10}");
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_SECONDARY_KID", null);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_SECONDARY_KID_UNTIL_UTC", null);

                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_ACTIVE_KID", "kGrace");
                var keyGrace = LicenseKey.Generate(LicenseKey.LicenseTier.Unlimited, "a@b.com", "ACME").Key;
                Assert.StartsWith("LSP2-kGrace-", keyGrace, StringComparison.Ordinal);
                Assert.True(LicenseKey.Validate(keyGrace, out _));

                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_ACTIVE_KID", "kDep");
                var keyDep = LicenseKey.Generate(LicenseKey.LicenseTier.Unlimited, "a@b.com", "ACME").Key;
                Assert.StartsWith("LSP2-kDep-", keyDep, StringComparison.Ordinal);
                Assert.True(LicenseKey.Validate(keyDep, out _));

                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_ACTIVE_KID", "kInv");
                Assert.Throws<InvalidOperationException>(() =>
                    LicenseKey.Generate(LicenseKey.LicenseTier.Unlimited, "a@b.com", "ACME"));

                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_ACTIVE_KID", "kNew");
                var keyNew = LicenseKey.Generate(LicenseKey.LicenseTier.Unlimited, "a@b.com", "ACME").Key;
                Assert.StartsWith("LSP2-kNew-", keyNew, StringComparison.Ordinal);

                var status = LicenseKey.GetSigningStatusText();
                Assert.Contains("kid=kGrace|phase=grace", status, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("kid=kDep|phase=deprecated", status, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("kid=kInv|phase=invalid", status, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEYS", oldPacked);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_ACTIVE_KID", oldActive);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY", oldSingle);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY_EXPIRATIONS", oldExp);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY_ROLLOUTS", oldRollouts);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_GRACE_DAYS", oldGrace);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_DEPRECATION_DAYS", oldDeprec);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_SECONDARY_KID", oldSecondary);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_SECONDARY_KID_UNTIL_UTC", oldSecondaryUntil);
            }
        }
    }

    [Fact]
    public void SignedKey_DualValidationWindow_AllowsSecondaryKidTemporarily()
    {
        lock (EnvLock)
        {
            var oldPacked = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEYS");
            var oldActive = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_ACTIVE_KID");
            var oldSingle = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY");
            var oldExp = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY_EXPIRATIONS");
            var oldRollouts = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY_ROLLOUTS");
            var oldGrace = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_GRACE_DAYS");
            var oldDeprec = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_DEPRECATION_DAYS");
            var oldSecondary = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_SECONDARY_KID");
            var oldSecondaryUntil = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_SECONDARY_KID_UNTIL_UTC");

            try
            {
                var d70 = DateTime.UtcNow.AddDays(-70).ToString("yyyy-MM-dd");
                var futureWindow = DateTime.UtcNow.AddHours(24).ToString("O");
                var pastWindow = DateTime.UtcNow.AddHours(-1).ToString("O");

                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEYS", "kOld=old-secret;kNew=new-secret");
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_ACTIVE_KID", "kOld");
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY", null);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY_EXPIRATIONS", null);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_GRACE_DAYS", "30");
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_DEPRECATION_DAYS", "60");
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY_ROLLOUTS", $"kOld={d70}");

                // old key is invalid by phase without dual-window
                var oldKey = "LSP2-kOld-UNLTD-20991231-A1B2C3D4-" + ComputeSigForTest("UNLTD|20991231|A1B2C3D4", "old-secret");
                Assert.False(LicenseKey.Validate(oldKey, out _));

                // enable dual validation window for old key
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_SECONDARY_KID", "kOld");
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_SECONDARY_KID_UNTIL_UTC", futureWindow);
                Assert.True(LicenseKey.Validate(oldKey, out _));

                // after window closes key is invalid again
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_SECONDARY_KID_UNTIL_UTC", pastWindow);
                Assert.False(LicenseKey.Validate(oldKey, out _));

                // generation still uses only active kid
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_ACTIVE_KID", "kNew");
                var newKey = LicenseKey.Generate(LicenseKey.LicenseTier.Unlimited, "a@b.com", "ACME").Key;
                Assert.StartsWith("LSP2-kNew-", newKey, StringComparison.Ordinal);
            }
            finally
            {
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEYS", oldPacked);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_ACTIVE_KID", oldActive);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY", oldSingle);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY_EXPIRATIONS", oldExp);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY_ROLLOUTS", oldRollouts);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_GRACE_DAYS", oldGrace);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_DEPRECATION_DAYS", oldDeprec);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_SECONDARY_KID", oldSecondary);
                Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_SECONDARY_KID_UNTIL_UTC", oldSecondaryUntil);
            }
        }
    }

    private static string ComputeSigForTest(string payload, string secret)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash[..8]).ToUpperInvariant();
    }
}
