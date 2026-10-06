using System.Security.Cryptography;
using System.Text;
using System.Globalization;

namespace LightningSPP.Licensing;

/// <summary>
/// Lightning SPP License Key System
/// Generates and validates license keys for different pricing tiers
/// </summary>
public class LicenseKey
{
    private const string SignedPrefix = "LSP2";
    private const string SigningKeyEnv = "LSPP_LICENSE_SIGNING_KEY";
    private const string SigningKeysEnv = "LSPP_LICENSE_SIGNING_KEYS";
    private const string ActiveKidEnv = "LSPP_LICENSE_SIGNING_ACTIVE_KID";
    private const string SigningKeyExpirationsEnv = "LSPP_LICENSE_SIGNING_KEY_EXPIRATIONS";
    private const string SigningKeyRolloutsEnv = "LSPP_LICENSE_SIGNING_KEY_ROLLOUTS";
    private const string GraceWindowDaysEnv = "LSPP_LICENSE_SIGNING_GRACE_DAYS";
    private const string DeprecationWindowDaysEnv = "LSPP_LICENSE_SIGNING_DEPRECATION_DAYS";
    private const string SecondaryKidEnv = "LSPP_LICENSE_SIGNING_SECONDARY_KID";
    private const string SecondaryKidUntilUtcEnv = "LSPP_LICENSE_SIGNING_SECONDARY_KID_UNTIL_UTC";

    private enum KidPhase
    {
        Active,
        Grace,
        Deprecated,
        Invalid
    }

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
    /// Format (signed): LSP2-{KID}-{TIER}-{EXP_yyyyMMdd}-{SEG}-{SIGNATURE}
    /// Example: LSP2-k1-UNLTD-20270524-AB12CD34-9F04C4F7AA2E1B5C
    /// Legacy LSPP-* keys remain supported for backward compatibility.
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

        try
        {
            license.Key = GenerateKeyString(license);
            SigningAuditTrail.Record(TryExtractKid(license.Key) ?? ResolveActiveKid(), license.Key, "success");
        }
        catch (Exception ex)
        {
            SigningAuditTrail.Record(ResolveActiveKid(), null, "fail", ex.Message);
            throw;
        }

        return license;
    }

    private static string? TryExtractKid(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        var parts = key.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 6 && string.Equals(parts[0], SignedPrefix, StringComparison.Ordinal))
            return parts[1];
        return null;
    }

    private static string GenerateKeyString(LicenseKey license)
    {
        var tierCode = license.Tier switch
        {
            LicenseTier.Free => "FREE",
            LicenseTier.PayAsYouGo => "PAYGO",
            LicenseTier.Unlimited => "UNLTD",
            LicenseTier.Enterprise => "ENTER",
            _ => throw new ArgumentOutOfRangeException(nameof(license.Tier), license.Tier, "Unsupported license tier.")
        };

        var expiration = license.ExpirationDate.ToString("yyyyMMdd");
        string segment;
        using (var rng = RandomNumberGenerator.Create())
        {
            var bytes = new byte[4];
            rng.GetBytes(bytes);
            segment = Convert.ToHexString(bytes).ToUpper();
        }

        var kid = ResolveActiveKid();
        var key = ResolveSigningKeys();
        if (!key.TryGetValue(kid, out var signingSecret))
        {
            throw new InvalidOperationException(
                $"Active signing KID '{kid}' is not configured in {SigningKeysEnv}. Configure the active KID and signing key explicitly.");
        }

        if (IsKidExpired(kid, DateTime.UtcNow, out var expiresOn))
        {
            throw new InvalidOperationException($"Signing key '{kid}' expired on {expiresOn:yyyy-MM-dd}. Rotate active KID.");
        }

        var phase = ResolveKidPhase(kid, DateTime.UtcNow, out var rolloutDate);
        if (phase == KidPhase.Invalid)
        {
            throw new InvalidOperationException($"Signing key '{kid}' is invalid after rollover window (rollout={rolloutDate:yyyy-MM-dd}). Rotate active KID.");
        }

        var payload = $"{tierCode}|{expiration}|{segment}";
        var signature = ComputeSignature(payload, signingSecret);

        // Signed format with KID: LSP2-{KID}-{TIER}-{EXP}-{SEG}-{SIGNATURE}
        return $"{SignedPrefix}-{kid}-{tierCode}-{expiration}-{segment}-{signature}";
    }

    private static string ComputeSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash[..8]).ToUpper();
    }

    private static string ResolveDefaultSigningSecret()
    {
        var secret = Environment.GetEnvironmentVariable(SigningKeyEnv);
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException(
                $"License signing requires {SigningKeyEnv} or {SigningKeysEnv}. No default signing secret is permitted.");
        }
        return secret;
    }

    private static string ResolveActiveKid()
    {
        var kid = Environment.GetEnvironmentVariable(ActiveKidEnv);
        return string.IsNullOrWhiteSpace(kid) ? "k1" : kid.Trim();
    }

    private static Dictionary<string, string> ResolveSigningKeys()
    {
        var keys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var packed = Environment.GetEnvironmentVariable(SigningKeysEnv);

        if (!string.IsNullOrWhiteSpace(packed))
        {
            var entries = packed.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var entry in entries)
            {
                var idx = entry.IndexOf('=');
                if (idx <= 0 || idx >= entry.Length - 1)
                {
                    idx = entry.IndexOf(':');
                }

                if (idx <= 0 || idx >= entry.Length - 1)
                {
                    continue;
                }

                var kid = entry[..idx].Trim();
                var secret = entry[(idx + 1)..].Trim();
                if (!string.IsNullOrWhiteSpace(kid) && !string.IsNullOrWhiteSpace(secret))
                {
                    keys[kid] = secret;
                }
            }
        }

        if (keys.Count == 0)
        {
            keys["k1"] = ResolveDefaultSigningSecret();
        }

        return keys;
    }

    private static Dictionary<string, DateOnly> ResolveSigningKeyExpirations()
    {
        var expirations = new Dictionary<string, DateOnly>(StringComparer.OrdinalIgnoreCase);
        var packed = Environment.GetEnvironmentVariable(SigningKeyExpirationsEnv);
        if (string.IsNullOrWhiteSpace(packed))
            return expirations;

        var entries = packed.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var entry in entries)
        {
            var idx = entry.IndexOf('=');
            if (idx <= 0 || idx >= entry.Length - 1)
                idx = entry.IndexOf(':');

            if (idx <= 0 || idx >= entry.Length - 1)
                continue;

            var kid = entry[..idx].Trim();
            var dateText = entry[(idx + 1)..].Trim();
            if (DateOnly.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                expirations[kid] = date;
            }
        }

        return expirations;
    }

    private static bool IsKidExpired(string kid, DateTime utcNow, out DateOnly? expiresOn)
    {
        var expirations = ResolveSigningKeyExpirations();
        if (!expirations.TryGetValue(kid, out var date))
        {
            expiresOn = null;
            return false;
        }

        expiresOn = date;
        var today = DateOnly.FromDateTime(utcNow.Date);
        return today > date;
    }

    private static Dictionary<string, DateOnly> ResolveSigningKeyRollouts()
    {
        var rollouts = new Dictionary<string, DateOnly>(StringComparer.OrdinalIgnoreCase);
        var packed = Environment.GetEnvironmentVariable(SigningKeyRolloutsEnv);
        if (string.IsNullOrWhiteSpace(packed))
            return rollouts;

        var entries = packed.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var entry in entries)
        {
            var idx = entry.IndexOf('=');
            if (idx <= 0 || idx >= entry.Length - 1)
                idx = entry.IndexOf(':');

            if (idx <= 0 || idx >= entry.Length - 1)
                continue;

            var kid = entry[..idx].Trim();
            var dateText = entry[(idx + 1)..].Trim();
            if (DateOnly.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                rollouts[kid] = date;
            }
        }

        return rollouts;
    }

    private static int ResolveWindowDays(string envName, int fallback)
    {
        var raw = Environment.GetEnvironmentVariable(envName);
        if (int.TryParse(raw, out var parsed) && parsed > 0)
            return parsed;
        return fallback;
    }

    private static KidPhase ResolveKidPhase(string kid, DateTime utcNow, out DateOnly? rolloutDate)
    {
        var rollouts = ResolveSigningKeyRollouts();
        if (!rollouts.TryGetValue(kid, out var rollout))
        {
            rolloutDate = null;
            return KidPhase.Active;
        }

        rolloutDate = rollout;
        var today = DateOnly.FromDateTime(utcNow.Date);
        var ageDays = today.DayNumber - rollout.DayNumber;
        if (ageDays < 0)
            return KidPhase.Active;

        var graceDays = ResolveWindowDays(GraceWindowDaysEnv, 30);
        var deprecationDays = ResolveWindowDays(DeprecationWindowDaysEnv, 60);

        if (ageDays < graceDays)
            return KidPhase.Grace;
        if (ageDays < deprecationDays)
            return KidPhase.Deprecated;
        return KidPhase.Invalid;
    }

    private static bool IsKidUsableForValidation(string kid, DateTime utcNow)
    {
        if (IsKidExpired(kid, utcNow, out _))
            return false;

        // Dual-validation window: allow temporary secondary KID for safe cluster rollout.
        if (IsKidInDualValidationWindow(kid, utcNow))
            return true;

        var phase = ResolveKidPhase(kid, utcNow, out _);
        return phase != KidPhase.Invalid;
    }

    private static bool IsKidInDualValidationWindow(string kid, DateTime utcNow)
    {
        var secondaryKid = Environment.GetEnvironmentVariable(SecondaryKidEnv);
        if (string.IsNullOrWhiteSpace(secondaryKid))
            return false;

        var untilRaw = Environment.GetEnvironmentVariable(SecondaryKidUntilUtcEnv);
        if (!DateTime.TryParse(untilRaw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var untilUtc))
            return false;

        if (utcNow > untilUtc)
            return false;

        return string.Equals(kid, secondaryKid.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static string GetSigningStatusText()
    {
        var keys = ResolveSigningKeys();
        var activeKid = ResolveActiveKid();
        var now = DateTime.UtcNow;
        var graceDays = ResolveWindowDays(GraceWindowDaysEnv, 30);
        var deprecationDays = ResolveWindowDays(DeprecationWindowDaysEnv, 60);
        var secondaryKid = Environment.GetEnvironmentVariable(SecondaryKidEnv) ?? string.Empty;
        var secondaryUntilRaw = Environment.GetEnvironmentVariable(SecondaryKidUntilUtcEnv) ?? string.Empty;
        var dualActive = IsKidInDualValidationWindow(secondaryKid, now);

        var sb = new StringBuilder();
        sb.AppendLine("ok=true");
        sb.AppendLine($"activeKid={activeKid}");
        sb.AppendLine($"loadedKids={string.Join(',', keys.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))}");
        sb.AppendLine($"dualValidationActive={dualActive.ToString().ToLowerInvariant()}");
        sb.AppendLine($"secondaryKid={(string.IsNullOrWhiteSpace(secondaryKid) ? "none" : secondaryKid)}");
        sb.AppendLine($"secondaryKidValidUntilUtc={(string.IsNullOrWhiteSpace(secondaryUntilRaw) ? "none" : secondaryUntilRaw)}");
        sb.AppendLine($"graceDays={graceDays}");
        sb.AppendLine($"deprecationDays={deprecationDays}");

        foreach (var kid in keys.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            var expired = IsKidExpired(kid, now, out var expiresOn);
            var phase = ResolveKidPhase(kid, now, out var rolloutDate);
            sb.AppendLine($"kid={kid}|phase={phase.ToString().ToLowerInvariant()}|expired={expired.ToString().ToLowerInvariant()}|rollout={(rolloutDate.HasValue ? rolloutDate.Value.ToString("yyyy-MM-dd") : "none")}|expiresOn={(expiresOn.HasValue ? expiresOn.Value.ToString("yyyy-MM-dd") : "none")}");
        }

        return sb.ToString();
    }

    private static string ComputeChecksum(string payload)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(payload));
        // Take first 8 bytes (64-bit) — stronger checksum
        return Convert.ToHexString(hash[..8]).ToUpper();
    }

    /// <summary>
    /// Validate a Lightning SPP license key
    /// </summary>
    public static bool Validate(string key, out LicenseKey? license)
    {
        license = null;

        if (string.IsNullOrEmpty(key))
            return false;

        var parts = key.Split('-', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 6 && string.Equals(parts[0], SignedPrefix, StringComparison.Ordinal))
        {
            var kid = parts[1];
            var tierSigned = parts[2] switch
            {
                "FREE" => LicenseTier.Free,
                "PAYGO" => LicenseTier.PayAsYouGo,
                "UNLTD" => LicenseTier.Unlimited,
                "ENTER" => LicenseTier.Enterprise,
                _ => (LicenseTier?)null
            };

            if (tierSigned == null)
                return false;

            if (!DateTime.TryParseExact(parts[3], "yyyyMMdd", null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var exp))
                return false;

            var segment = parts[4];
            var signature = parts[5];
            var payload = $"{parts[2]}|{parts[3]}|{segment}";

            var signingKeys = ResolveSigningKeys();
            if (!signingKeys.TryGetValue(kid, out var secretForKid))
                return false;

            if (!IsKidUsableForValidation(kid, DateTime.UtcNow))
                return false;

            var expectedSig = ComputeSignature(payload, secretForKid);

            if (!string.Equals(signature, expectedSig, StringComparison.OrdinalIgnoreCase))
                return false;

            license = new LicenseKey
            {
                Key = key,
                Tier = tierSigned.Value,
                IssueDate = DateTime.UtcNow,
                ExpirationDate = exp,
                IsActive = true,
                Features = GetFeaturesForTier(tierSigned.Value)
            };

            return true;
        }

        // Backward compatibility for earlier signed format without KID:
        // LSP2-{TIER}-{EXP}-{SEG}-{SIGNATURE}
        if (parts.Length == 5 && string.Equals(parts[0], SignedPrefix, StringComparison.Ordinal))
        {
            var tierSigned = parts[1] switch
            {
                "FREE" => LicenseTier.Free,
                "PAYGO" => LicenseTier.PayAsYouGo,
                "UNLTD" => LicenseTier.Unlimited,
                "ENTER" => LicenseTier.Enterprise,
                _ => (LicenseTier?)null
            };

            if (tierSigned == null)
                return false;

            if (!DateTime.TryParseExact(parts[2], "yyyyMMdd", null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var exp))
                return false;

            var segment = parts[3];
            var signature = parts[4];
            var payload = $"{parts[1]}|{parts[2]}|{segment}";

            var signingKeys = ResolveSigningKeys();
            var signatureValid = signingKeys
                .Where(kv => IsKidUsableForValidation(kv.Key, DateTime.UtcNow))
                .Any(kv => string.Equals(signature, ComputeSignature(payload, kv.Value), StringComparison.OrdinalIgnoreCase));

            if (!signatureValid)
                return false;

            license = new LicenseKey
            {
                Key = key,
                Tier = tierSigned.Value,
                IssueDate = DateTime.UtcNow,
                ExpirationDate = exp,
                IsActive = true,
                Features = GetFeaturesForTier(tierSigned.Value)
            };

            return true;
        }

        // The historical unsigned "LSPP-{TIER}-xxxx-xxxx-xxxx-xxxx" format is rejected
        // outright: LicenseKey.Generate() has never produced it (it always emits the
        // signed LSP2 format above), so no legitimate customer holds one, and accepting
        // it here would let anyone self-activate a free, unverified Enterprise license.
        return false;
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
    /// Export license to key-value storage format.
    /// </summary>
    public string ToStorageString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Key={Key}");
        sb.AppendLine($"Tier={Tier}");
        sb.AppendLine($"CustomerEmail={CustomerEmail}");
        sb.AppendLine($"CompanyName={CompanyName}");
        sb.AppendLine($"IssueDate={IssueDate:O}");
        sb.AppendLine($"ExpirationDate={ExpirationDate:O}");
        sb.AppendLine($"MaxDevices={MaxDevices}");
        sb.AppendLine($"MaxOperationsPerMonth={MaxOperationsPerMonth}");
        sb.AppendLine($"IsActive={IsActive}");
        sb.AppendLine($"Features={Convert.ToBase64String(Encoding.UTF8.GetBytes(Features))}");
        return sb.ToString();
    }

    /// <summary>
    /// Import license from key-value storage format.
    /// </summary>
    public static LicenseKey? FromStorageString(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var idx = line.IndexOf('=');
            if (idx <= 0 || idx >= line.Length - 1)
            {
                continue;
            }

            var key = line[..idx].Trim();
            var val = line[(idx + 1)..].Trim();
            values[key] = val;
        }

        if (!values.TryGetValue("Key", out var keyValue) || string.IsNullOrWhiteSpace(keyValue))
        {
            return null;
        }

        var tierText = values.TryGetValue("Tier", out var t) ? t : nameof(LicenseTier.Free);
        _ = Enum.TryParse<LicenseTier>(tierText, true, out var tier);

        var issueText = values.TryGetValue("IssueDate", out var i) ? i : DateTime.UtcNow.ToString("O");
        var expText = values.TryGetValue("ExpirationDate", out var e) ? e : DateTime.UtcNow.ToString("O");
        _ = DateTime.TryParse(issueText, out var issueDate);
        _ = DateTime.TryParse(expText, out var expirationDate);

        var maxDevicesText = values.TryGetValue("MaxDevices", out var md) ? md : "1";
        var maxOpsText = values.TryGetValue("MaxOperationsPerMonth", out var mo) ? mo : "1000";
        _ = int.TryParse(maxDevicesText, out var maxDevices);
        _ = long.TryParse(maxOpsText, out var maxOps);

        var isActiveText = values.TryGetValue("IsActive", out var ia) ? ia : "true";
        _ = bool.TryParse(isActiveText, out var isActive);

        var features = string.Empty;
        if (values.TryGetValue("Features", out var f) && !string.IsNullOrWhiteSpace(f))
        {
            try
            {
                features = Encoding.UTF8.GetString(Convert.FromBase64String(f));
            }
            catch
            {
                features = string.Empty;
            }
        }

        return new LicenseKey
        {
            Key = keyValue,
            Tier = tier,
            CustomerEmail = values.TryGetValue("CustomerEmail", out var c) ? c : string.Empty,
            CompanyName = values.TryGetValue("CompanyName", out var n) ? n : string.Empty,
            IssueDate = issueDate == default ? DateTime.UtcNow : issueDate,
            ExpirationDate = expirationDate == default ? DateTime.UtcNow.AddDays(365) : expirationDate,
            MaxDevices = maxDevices,
            MaxOperationsPerMonth = maxOps,
            IsActive = isActive,
            Features = string.IsNullOrWhiteSpace(features) ? GetFeaturesForTier(tier) : features
        };
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
