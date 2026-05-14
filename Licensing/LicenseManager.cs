using System.Text.Json;

namespace LightningSPP.Licensing;

/// <summary>
/// License Manager for Lightning SPP
/// Handles license activation, validation, and usage tracking
/// </summary>
public class LicenseManager
{
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

        // Try to load from file
        if (File.Exists(LicenseFilePath))
        {
            try
            {
                var json = File.ReadAllText(LicenseFilePath);
                _activeLicense = LicenseKey.FromJson(json);
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
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(LicenseFilePath, license.ToJson());
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
