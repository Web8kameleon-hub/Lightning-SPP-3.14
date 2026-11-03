using System.Security.Cryptography;
using System.Text;

namespace LightningSPP.Licensing;

/// <summary>
/// Asymmetric license signatures (ECDSA P-256 / SHA-256).
///
/// The issuer signs with a private key. A customer device verifies with a public
/// key only, so no issuer secret ever has to leave the issuer's environment.
/// This removes the requirement that made offline commercial activation unsafe.
/// </summary>
internal static class LicenseSignature
{
    internal const string Prefix = "LSP3";

    internal const string PublicKeysEnv = "LSPP_LICENSE_PUBLIC_KEYS";
    internal const string PrivateKeysEnv = "LSPP_LICENSE_SIGNING_PRIVATE_KEYS";
    internal const string ActiveKidEnv = "LSPP_LICENSE_SIGNING_ACTIVE_KID";

    /// <summary>True when at least one verification key is configured.</summary>
    internal static bool IsConfigured => ResolvePublicKeys().Count > 0;

    internal static string ActiveKid()
    {
        var kid = Environment.GetEnvironmentVariable(ActiveKidEnv);
        return string.IsNullOrWhiteSpace(kid) ? "k1" : kid.Trim();
    }

    internal static Dictionary<string, string> ResolvePublicKeys() =>
        ParsePacked(Environment.GetEnvironmentVariable(PublicKeysEnv));

    internal static Dictionary<string, string> ResolvePrivateKeys() =>
        ParsePacked(Environment.GetEnvironmentVariable(PrivateKeysEnv));

    /// <summary>
    /// Sign <paramref name="payload"/> and return the fixed-width signature as
    /// uppercase hex (128 characters). Hex is used because a license key is
    /// dash-separated and base64url would introduce dashes.
    /// </summary>
    internal static string Sign(string payload, string kid)
    {
        var keys = ResolvePrivateKeys();
        if (!keys.TryGetValue(kid, out var encoded))
        {
            throw new InvalidOperationException(
                $"No ECDSA private key configured for KID '{kid}' in {PrivateKeysEnv}. " +
                "Configure the issuer key or fall back to the HMAC LSP2 format.");
        }

        using var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(Convert.FromBase64String(encoded), out _);
        var signature = ecdsa.SignData(
            Encoding.UTF8.GetBytes(payload),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return Convert.ToHexString(signature);
    }

    /// <summary>
    /// Verify a signature using only the public key for <paramref name="kid"/>.
    /// Any missing key, malformed input or mismatch returns false.
    /// </summary>
    internal static bool Verify(string payload, string signatureHex, string kid)
    {
        if (string.IsNullOrWhiteSpace(signatureHex) || string.IsNullOrWhiteSpace(kid))
            return false;

        var keys = ResolvePublicKeys();
        if (!keys.TryGetValue(kid, out var encoded))
            return false;

        byte[] signature;
        try
        {
            signature = Convert.FromHexString(signatureHex);
        }
        catch (FormatException)
        {
            return false;
        }

        if (signature.Length != 64)
            return false;

        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(encoded), out _);
            return ecdsa.VerifyData(
                Encoding.UTF8.GetBytes(payload),
                signature,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// Create a new P-256 key pair for an issuer KID. Used by operator tooling and
    /// by tests; the private value must never be deployed to a customer device.
    /// </summary>
    internal static (string PublicKey, string PrivateKey) CreateKeyPair()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return (
            Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo()),
            Convert.ToBase64String(ecdsa.ExportPkcs8PrivateKey()));
    }

    private static Dictionary<string, string> ParsePacked(string? packed)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(packed))
            return result;

        foreach (var entry in packed.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var index = entry.IndexOf('=');
            if (index <= 0 || index >= entry.Length - 1)
                continue;

            var kid = entry[..index].Trim();
            var value = entry[(index + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(kid) && !string.IsNullOrWhiteSpace(value))
                result[kid] = value;
        }

        return result;
    }
}
