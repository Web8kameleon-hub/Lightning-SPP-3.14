using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LightningSPP.Licensing;

/// <summary>
/// Lightning SPP License Key System - JSON Source Generator Context
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(LicenseKey))]
internal partial class LicenseKeyJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Lightning SPP License Key System
/// Generates and validates license keys for different pricing tiers
/// </summary>
public class LicenseKey
{
    public string Key { get; set; } = string.Empty;
    public LicenseTier Tier { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; }
    public DateTime ExpirationDate { get; set; }
    public int MaxDevices { get; set; }
    public long MaxOperationsPerMonth { get; set; }
    public bool IsActive { get; set; }
    public string Features { get; set; } = string.Empty;

    public enum LicenseTier
    {
        Free,              // Developer tier - 1,000 ops/month
        PayAsYouGo,        // €0.001/unit, min €9.99/month
        Unlimited,         // €99.99/year unlimited
        Enterprise         // Custom pricing
    }

    /// <summary>
    /// Generate a new Lightning SPP license key
    /// Format: LSPP-{TIER}-{RANDOM}-{CHECKSUM}
    /// Example: LSPP-UNLTD-X7K9-M2P4-Q8W6-5A3C
    /// </summary>
    public static LicenseKey Generate(
        LicenseTier tier,
        string customerEmail,
        string companyName,
        int validityDays = 365,
        int maxDevices = 1,
        long maxOperationsPerMonth = -1) // -1 = unlimited
    {
        var license = new LicenseKey
        {
            Tier = tier,
            CustomerEmail = customerEmail,
            CompanyName = companyName,
            IssueDate = DateTime.UtcNow,
            ExpirationDate = DateTime.UtcNow.AddDays(validityDays),
            MaxDevices = maxDevices,
            MaxOperationsPerMonth = maxOperationsPerMonth,
            IsActive = true,
            Features = GetFeaturesForTier(tier)
        };

        license.Key = GenerateKeyString(license);
        return license;
    }

    private static string GenerateKeyString(LicenseKey license)
    {
        var tierCode = license.Tier switch
        {
            LicenseTier.Free => "FREE",
            LicenseTier.PayAsYouGo => "PAYGO",
            LicenseTier.Unlimited => "UNLTD",
            LicenseTier.Enterprise => "ENTER",
            _ => "TRIAL"
        };

        // Generate random segments (4 segments of 4 chars each)
        var segments = new List<string>();
        using (var rng = RandomNumberGenerator.Create())
        {
            for (int i = 0; i < 4; i++)
            {
                var bytes = new byte[2];
                rng.GetBytes(bytes);
                var segment = Convert.ToHexString(bytes).ToUpper();
                segments.Add(segment);
            }
        }

        // Create payload for checksum
        var payload = $"{license.CustomerEmail}|{license.Tier}|{license.ExpirationDate:yyyyMMdd}";
        var checksum = ComputeChecksum(payload);

        // Format: LSPP-{TIER}-{SEG1}-{SEG2}-{SEG3}-{CHECKSUM}
        return $"LSPP-{tierCode}-{segments[0]}-{segments[1]}-{segments[2]}-{checksum}";
    }

    private static string ComputeChecksum(string payload)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(payload));
        // Take first 4 bytes and convert to hex
        return Convert.ToHexString(hash[..2]).ToUpper();
    }

    /// <summary>
    /// Validate a Lightning SPP license key
    /// </summary>
    public static bool Validate(string key, out LicenseKey? license)
    {
        license = null;

        if (string.IsNullOrEmpty(key) || !key.StartsWith("LSPP-"))
            return false;

        var parts = key.Split('-');
        if (parts.Length != 6)
            return false;

        // Parse tier
        var tier = parts[1] switch
        {
            "FREE" => LicenseTier.Free,
            "PAYGO" => LicenseTier.PayAsYouGo,
            "UNLTD" => LicenseTier.Unlimited,
            "ENTER" => LicenseTier.Enterprise,
            _ => (LicenseTier?)null
        };

        if (tier == null)
            return false;

        // Basic validation passed
        license = new LicenseKey
        {
            Key = key,
            Tier = tier.Value,
            IsActive = true,
            Features = GetFeaturesForTier(tier.Value)
        };

        return true;
    }

    /// <summary>
    /// Check if license is still valid (not expired)
    /// </summary>
    public bool IsValid()
    {
        if (!IsActive)
            return false;

        if (DateTime.UtcNow > ExpirationDate)
            return false;

        return true;
    }

    /// <summary>
    /// Get features enabled for each tier
    /// </summary>
    private static string GetFeaturesForTier(LicenseTier tier)
    {
        return tier switch
        {
            LicenseTier.Free => "Basic scanning, Standard processing, Draft printing, 1 device, 1,000 ops/month",
            LicenseTier.PayAsYouGo => "All features, Pay per use, €0.001/unit, Min €9.99/month, Email support",
            LicenseTier.Unlimited => "All features, Unlimited operations, €99.99/year, Priority email support, 10 devices",
            LicenseTier.Enterprise => "All features, Unlimited operations, Custom SLA, Dedicated support, White-label, Custom integrations, Unlimited devices",
            _ => "Trial"
        };
    }

    /// <summary>
    /// Get usage limits for the license tier
    /// </summary>
    public (long maxOperations, int maxDevices, bool hasAI, bool hasPremium) GetLimits()
    {
        return Tier switch
        {
            LicenseTier.Free => (1000, 1, false, false),
            LicenseTier.PayAsYouGo => (-1, 5, true, true), // -1 = pay per use
            LicenseTier.Unlimited => (-1, 10, true, true), // -1 = unlimited
            LicenseTier.Enterprise => (-1, -1, true, true), // -1 = unlimited
            _ => (0, 0, false, false)
        };
    }

    /// <summary>
    /// Export license to JSON for storage/transmission
    /// </summary>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, LicenseKeyJsonContext.Default.LicenseKey);
    }

    /// <summary>
    /// Import license from JSON
    /// </summary>
    public static LicenseKey? FromJson(string json)
    {
        return JsonSerializer.Deserialize(json, LicenseKeyJsonContext.Default.LicenseKey);
    }

    public override string ToString()
    {
        return $"""
            Lightning SPP License Key
            ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            Key:          {Key}
            Tier:         {Tier}
            Customer:     {CustomerEmail}
            Company:      {CompanyName}
            Valid From:   {IssueDate:yyyy-MM-dd}
            Valid Until:  {ExpirationDate:yyyy-MM-dd}
            Max Devices:  {(MaxDevices == -1 ? "Unlimited" : MaxDevices.ToString())}
            Max Ops/Mo:   {(MaxOperationsPerMonth == -1 ? "Unlimited" : MaxOperationsPerMonth.ToString("N0"))}
            Status:       {(IsValid() ? "✓ ACTIVE" : "✗ EXPIRED")}
            Features:     {Features}
            ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            """;
    }
}
