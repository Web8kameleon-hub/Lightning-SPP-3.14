using System.Security.Cryptography;
using LightningSPP.Licensing;
using Xunit;

namespace LightningSPP.Tests;

/// <summary>
/// Covers the asymmetric (ECDSA P-256) license path: the issuer signs with a private
/// key, and a device verifies with a public key only. No issuer secret is present on
/// the verifying side in any of these tests.
/// </summary>
public class LicenseAsymmetricTests
{
    private static readonly object EnvLock = new();

    [Fact]
    public void AsymmetricKey_ValidatesWithPublicKeyOnly()
    {
        lock (EnvLock)
        {
            using var scope = new EnvScope();
            var (publicKey, privateKey) = NewKeyPair();

            scope.Set("LSPP_LICENSE_SIGNING_PRIVATE_KEYS", $"k1={privateKey}");
            scope.Set("LSPP_LICENSE_SIGNING_ACTIVE_KID", "k1");

            var generated = LicenseKey.Generate(LicenseKey.LicenseTier.Unlimited, "a@b.com", "ACME", validityDays: 30);

            Assert.StartsWith("LSP3-k1-", generated.Key, StringComparison.Ordinal);

            // Device side: private key removed, only the public key remains.
            scope.Set("LSPP_LICENSE_SIGNING_PRIVATE_KEYS", null);
            scope.Set("LSPP_LICENSE_PUBLIC_KEYS", $"k1={publicKey}");

            Assert.True(LicenseKey.Validate(generated.Key, out var validated));
            Assert.NotNull(validated);
            Assert.Equal(LicenseKey.LicenseTier.Unlimited, validated!.Tier);
        }
    }

    [Fact]
    public void AsymmetricKey_TamperedTier_IsRejected()
    {
        lock (EnvLock)
        {
            using var scope = new EnvScope();
            var (publicKey, privateKey) = NewKeyPair();

            scope.Set("LSPP_LICENSE_SIGNING_PRIVATE_KEYS", $"k1={privateKey}");
            scope.Set("LSPP_LICENSE_SIGNING_ACTIVE_KID", "k1");
            var key = LicenseKey.Generate(LicenseKey.LicenseTier.Free, "a@b.com", "ACME").Key;

            scope.Set("LSPP_LICENSE_SIGNING_PRIVATE_KEYS", null);
            scope.Set("LSPP_LICENSE_PUBLIC_KEYS", $"k1={publicKey}");

            // Escalate FREE -> ENTER without a new signature.
            var forged = key.Replace("-FREE-", "-ENTER-", StringComparison.Ordinal);
            Assert.NotEqual(key, forged);
            Assert.False(LicenseKey.Validate(forged, out _));
        }
    }

    [Fact]
    public void AsymmetricKey_WithoutPublicKey_IsRejected()
    {
        lock (EnvLock)
        {
            using var scope = new EnvScope();
            var (_, privateKey) = NewKeyPair();

            scope.Set("LSPP_LICENSE_SIGNING_PRIVATE_KEYS", $"k1={privateKey}");
            scope.Set("LSPP_LICENSE_SIGNING_ACTIVE_KID", "k1");
            var key = LicenseKey.Generate(LicenseKey.LicenseTier.Unlimited, "a@b.com", "ACME").Key;

            scope.Set("LSPP_LICENSE_SIGNING_PRIVATE_KEYS", null);
            scope.Set("LSPP_LICENSE_PUBLIC_KEYS", null);

            Assert.False(LicenseKey.Validate(key, out _));
        }
    }

    [Fact]
    public void AsymmetricKey_WithDifferentPublicKey_IsRejected()
    {
        lock (EnvLock)
        {
            using var scope = new EnvScope();
            var (_, privateKey) = NewKeyPair();
            var (otherPublic, _) = NewKeyPair();

            scope.Set("LSPP_LICENSE_SIGNING_PRIVATE_KEYS", $"k1={privateKey}");
            scope.Set("LSPP_LICENSE_SIGNING_ACTIVE_KID", "k1");
            var key = LicenseKey.Generate(LicenseKey.LicenseTier.Unlimited, "a@b.com", "ACME").Key;

            scope.Set("LSPP_LICENSE_SIGNING_PRIVATE_KEYS", null);
            scope.Set("LSPP_LICENSE_PUBLIC_KEYS", $"k1={otherPublic}");

            Assert.False(LicenseKey.Validate(key, out _));
        }
    }

    [Fact]
    public void AsymmetricKey_PreservesExpiryAndTier()
    {
        lock (EnvLock)
        {
            using var scope = new EnvScope();
            var (publicKey, privateKey) = NewKeyPair();

            scope.Set("LSPP_LICENSE_SIGNING_PRIVATE_KEYS", $"k1={privateKey}");
            scope.Set("LSPP_LICENSE_SIGNING_ACTIVE_KID", "k1");
            var generated = LicenseKey.Generate(LicenseKey.LicenseTier.Enterprise, "a@b.com", "ACME", validityDays: 10);

            scope.Set("LSPP_LICENSE_SIGNING_PRIVATE_KEYS", null);
            scope.Set("LSPP_LICENSE_PUBLIC_KEYS", $"k1={publicKey}");

            Assert.True(LicenseKey.Validate(generated.Key, out var validated));
            Assert.Equal(LicenseKey.LicenseTier.Enterprise, validated!.Tier);
            Assert.Equal(generated.ExpirationDate.Date, validated.ExpirationDate.Date);
        }
    }

    private static (string PublicKey, string PrivateKey) NewKeyPair()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return (
            Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo()),
            Convert.ToBase64String(ecdsa.ExportPkcs8PrivateKey()));
    }

    /// <summary>
    /// Sets environment variables and restores the previous values on dispose.
    /// </summary>
    private sealed class EnvScope : IDisposable
    {
        private readonly Dictionary<string, string?> previous = new(StringComparer.Ordinal);

        internal void Set(string name, string? value)
        {
            if (!previous.ContainsKey(name))
                previous[name] = Environment.GetEnvironmentVariable(name);

            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            foreach (var pair in previous)
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
        }
    }
}
