using System.Security.Cryptography;
using System.Text;
using wwwmmm.Web;

namespace LightningSPP.Licensing;

/// <summary>
/// License Manager for Lightning SPP
/// Handles license activation, validation, and usage tracking.
/// license.json is stored AES-256-GCM encrypted (key derived from machine entropy).
/// </summary>
public class LicenseManager
{
    public enum PaymentMode
    {
        StripeOnly,
        InternalQuantum
    }

    private static readonly string LicenseFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LightningSPP",
        "license.json"
    );

    private static LicenseKey? _activeLicense;

    /// <summary>
    /// Activate a license key
    /// </summary>
    public static bool ActivateLicense(string licenseKey)
    {
        if (!LicenseKey.Validate(licenseKey, out var license))
        {
            Console.WriteLine("❌ Invalid license key format.");
            return false;
        }

        if (license == null)
        {
            Console.WriteLine("❌ License validation failed.");
            return false;
        }

        // In production, this would call activation server
        // For now, we save locally
        SaveLicense(license);
        _activeLicense = license;

        Console.WriteLine("✅ License activated successfully!");
        Console.WriteLine(license.ToString());

        return true;
    }

    /// <summary>
    /// Get the current active license
    /// </summary>
    public static LicenseKey? GetActiveLicense()
    {
        if (_activeLicense != null)
            return _activeLicense;

        if (File.Exists(LicenseFilePath))
        {
            try
            {
                var encrypted = File.ReadAllBytes(LicenseFilePath);
                var content = DecryptLicense(encrypted);
                _activeLicense = LicenseKey.FromStorageString(content);
            }
            catch
            {
                return null;
            }
        }

        return _activeLicense;
    }

    /// <summary>
    /// Check if there's a valid active license
    /// </summary>
    public static bool HasValidLicense()
    {
        var license = GetActiveLicense();
        return license != null && license.IsValid();
    }

    /// <summary>
    /// Get the tier of the active license
    /// </summary>
    public static LicenseKey.LicenseTier GetLicenseTier()
    {
        var license = GetActiveLicense();
        return license?.Tier ?? LicenseKey.LicenseTier.Free;
    }

    /// <summary>
    /// Deactivate the current license
    /// </summary>
    public static void DeactivateLicense()
    {
        if (File.Exists(LicenseFilePath))
        {
            File.Delete(LicenseFilePath);
        }

        _activeLicense = null;
        Console.WriteLine("✅ License deactivated.");
    }

    /// <summary>
    /// Check if a feature is available in the current license
    /// </summary>
    public static bool IsFeatureAvailable(string feature)
    {
        var license = GetActiveLicense();
        if (license == null)
            return false;

        var (maxOps, maxDevices, hasAI, hasPremium) = license.GetLimits();

        return feature.ToLower() switch
        {
            "ai" => hasAI,
            "premium" => hasPremium,
            "clisonic" => hasAI,
            "stigma" => hasPremium,
            "nanodecibel" => hasPremium,
            "resonance" => hasPremium,
            _ => true // Basic features always available
        };
    }

    /// <summary>
    /// Track usage (operations count)
    /// In production, this would sync with billing server
    /// </summary>
    private static long _operationsThisMonth = 0;

    public static bool CheckAndIncrementUsage()
    {
        var license = GetActiveLicense();
        if (license == null)
        {
            Console.WriteLine("⚠️  No license found. Running in FREE tier mode (limited to 1,000 ops/month).");
            if (_operationsThisMonth >= 1000)
            {
                Console.WriteLine("❌ FREE tier monthly limit reached. Please upgrade to continue.");
                return false;
            }
        }
        else
        {
            var (maxOps, _, _, _) = license.GetLimits();
            if (maxOps > 0 && _operationsThisMonth >= maxOps)
            {
                Console.WriteLine($"❌ Monthly operation limit ({maxOps:N0}) reached. Please upgrade or wait for next billing cycle.");
                return false;
            }
        }

        _operationsThisMonth++;
        return true;
    }

    public static long GetUsageThisMonth() => _operationsThisMonth;

    /// <summary>
    /// Create a new license key with explicit security/payment strategy for internal provisioning.
    /// </summary>
    public static LicenseKey CreateLicenseKey(
        string email,
        string company,
        string tier,
        int devices,
        int validityDays,
        bool useInternalQuantumSecurity,
        PaymentMode paymentMode = PaymentMode.StripeOnly)
    {
        if (paymentMode == PaymentMode.StripeOnly)
        {
            if (!StripeBilling.EnsureConfigured(out var stripeMessage))
            {
                InternalAuditLogger.Error($"Stripe configuration error: {stripeMessage}");
                throw new InvalidOperationException(stripeMessage);
            }

            InternalAuditLogger.Info("Stripe configuration validated for key issuance.");
        }

        var normalizedTier = tier.Trim().ToLowerInvariant();
        var selectedTier = normalizedTier switch
        {
            "free" => LicenseKey.LicenseTier.Free,
            "paygo" => LicenseKey.LicenseTier.PayAsYouGo,
            "pay-as-you-go" => LicenseKey.LicenseTier.PayAsYouGo,
            "unlimited" => LicenseKey.LicenseTier.Unlimited,
            "enterprise" => LicenseKey.LicenseTier.Enterprise,
            _ => throw new ArgumentException($"Unknown tier: {tier}")
        };

        var maxOps = selectedTier switch
        {
            LicenseKey.LicenseTier.Free => 1000,
            _ => -1
        };

        var maxDevices = selectedTier switch
        {
            LicenseKey.LicenseTier.Free => Math.Clamp(devices, 1, 1),
            LicenseKey.LicenseTier.PayAsYouGo => Math.Clamp(devices, 1, 5),
            LicenseKey.LicenseTier.Unlimited => Math.Clamp(devices, 1, 10),
            LicenseKey.LicenseTier.Enterprise => devices <= 0 ? -1 : devices,
            _ => devices
        };

        var license = LicenseKey.Generate(
            selectedTier,
            email,
            company,
            validityDays,
            maxDevices,
            maxOps);

        InternalAuditLogger.Info($"Issued key tier={selectedTier} email={email} company={company} paymentMode={paymentMode} internalSecurity={useInternalQuantumSecurity}");

        return license;
    }

    /// <summary>
    /// Display license status
    /// </summary>
    public static void ShowLicenseStatus()
    {
        var license = GetActiveLicense();

        if (license == null)
        {
            Console.WriteLine("""
                ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                Lightning SPP - FREE TIER
                ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                Status:       No license activated
                Tier:         FREE (Developer)
                Limit:        1,000 operations/month
                Usage:        {0:N0} / 1,000 ops this month
                Features:     Basic features only
                
                💡 Upgrade to unlock all features:
                   • UNLIMITED: €99.99/year (unlimited ops)
                   • PAY-AS-YOU-GO: €0.001/operation
                   • ENTERPRISE: Custom pricing
                
                Activate: lightning-spp activate <LICENSE-KEY>
                Purchase: https://aba-gmbh.de/lightning-spp
                ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                """, _operationsThisMonth);
        }
        else
        {
            Console.WriteLine(license.ToString());
            Console.WriteLine($"Usage this month: {_operationsThisMonth:N0} operations");
        }
    }

    private static void SaveLicense(LicenseKey license)
    {
        var directory = Path.GetDirectoryName(LicenseFilePath);
        if (directory != null && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var encrypted = EncryptLicense(license.ToStorageString());
        File.WriteAllBytes(LicenseFilePath, encrypted);
    }

    // AES-256-GCM encryption using a machine-local key derived from machine name + app name.
    // Provides confidentiality at rest without requiring a user-supplied passphrase.
    private static byte[] DeriveKey()
    {
        var entropy = Encoding.UTF8.GetBytes($"LightningSPP:{Environment.MachineName}:{Environment.UserName}");
        return SHA256.HashData(entropy); // 32 bytes = AES-256
    }

    private static byte[] EncryptLicense(string plaintext)
    {
        var key = DeriveKey();
        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize]; // 12 bytes
        RandomNumberGenerator.Fill(nonce);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize]; // 16 bytes

        using var aes = new AesGcm(key, AesGcm.TagByteSizes.MaxSize);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        // Format: [nonce(12)] [tag(16)] [ciphertext]
        var result = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, result, nonce.Length + tag.Length, ciphertext.Length);
        return result;
    }

    private static string DecryptLicense(byte[] data)
    {
        var key = DeriveKey();
        const int nonceLen = 12, tagLen = 16;
        if (data.Length < nonceLen + tagLen)
            throw new CryptographicException("Invalid license data.");

        var nonce = data[..nonceLen];
        var tag = data[nonceLen..(nonceLen + tagLen)];
        var ciphertext = data[(nonceLen + tagLen)..];
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(key, tagLen);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        return Encoding.UTF8.GetString(plaintext);
    }

    /// <summary>
    /// Generate sample licenses for testing
    /// </summary>
    public static class SampleLicenses
    {
        public static LicenseKey GenerateFreeLicense(string email)
        {
            return LicenseKey.Generate(
                LicenseKey.LicenseTier.Free,
                email,
                "Developer",
                validityDays: 365,
                maxDevices: 1,
                maxOperationsPerMonth: 1000
            );
        }

        public static LicenseKey GenerateUnlimitedLicense(string email, string company)
        {
            return LicenseKey.Generate(
                LicenseKey.LicenseTier.Unlimited,
                email,
                company,
                validityDays: 365,
                maxDevices: 10,
                maxOperationsPerMonth: -1 // unlimited
            );
        }

        public static LicenseKey GenerateEnterpriseLicense(string email, string company, int devices)
        {
            return LicenseKey.Generate(
                LicenseKey.LicenseTier.Enterprise,
                email,
                company,
                validityDays: 365,
                maxDevices: devices,
                maxOperationsPerMonth: -1 // unlimited
            );
        }
    }
}
