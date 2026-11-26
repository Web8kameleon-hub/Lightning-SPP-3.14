using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace wwwmmm.Crm;

/// <summary>
/// Input rules for CRM records. Every rule here rejects rather than repairs: a value that is
/// wrong is reported to the caller instead of being quietly rewritten into something plausible.
/// </summary>
public static partial class CrmValidation
{
    public const string ActiveStatus = "active";
    public const string InactiveStatus = "inactive";
    private const int MaxNameLength = 200;
    private const int MaxDetailLength = 2000;

    [GeneratedRegex("^[a-z0-9][a-z0-9_-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex CategoryPattern();

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Pattern();

    /// <summary>
    /// Returns the canonical mailbox form. Angle brackets, display names, multiple addresses
    /// and hosts without a dot are rejected because each one makes the identity ambiguous.
    /// </summary>
    public static string Email(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length is 0 or > 254)
            throw new ArgumentException("Enter an email address of at most 254 characters.");
        if (trimmed.Contains('<') || trimmed.Contains('>') || trimmed.Contains(',') || trimmed.Contains(';') ||
            trimmed.Any(char.IsWhiteSpace) || trimmed.Any(char.IsControl))
            throw new ArgumentException("Enter a single bare email address without a display name.");
        if (!System.Net.Mail.MailAddress.TryCreate(trimmed, out var address) ||
            !string.Equals(address.Address, trimmed, StringComparison.Ordinal) ||
            !address.Host.Contains('.'))
            throw new ArgumentException("Enter a valid email address with a dotted domain.");
        return address.Address.ToLowerInvariant();
    }

    public static string Name(string? value, string field)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length is 0 || trimmed.Length > MaxNameLength)
            throw new ArgumentException($"Enter a {field} of 1 to {MaxNameLength} characters.");
        if (trimmed.Any(char.IsControl))
            throw new ArgumentException($"The {field} contains control characters.");
        return trimmed;
    }

    public static string? OptionalText(string? value, string field, int maxLength = MaxNameLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new ArgumentException($"The {field} must be at most {maxLength} characters.");
        if (trimmed.Any(char.IsControl))
            throw new ArgumentException($"The {field} contains control characters.");
        return trimmed;
    }

    public static string Status(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        ActiveStatus => ActiveStatus,
        InactiveStatus => InactiveStatus,
        _ => throw new ArgumentException($"Status must be '{ActiveStatus}' or '{InactiveStatus}'.")
    };

    public static string Sha256(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (!Sha256Pattern().IsMatch(trimmed))
            throw new ArgumentException("Supply the lowercase hex SHA-256 of the exact file that was read.");
        return trimmed;
    }

    public static string? Category(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (!CategoryPattern().IsMatch(trimmed))
            throw new ArgumentException("Category must be 1-64 lowercase letters, digits, underscores or hyphens.");
        return trimmed;
    }

    public static string Detail(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length is 0 || trimmed.Length > MaxDetailLength)
            throw new ArgumentException($"Detail must be 1 to {MaxDetailLength} characters.");
        if (trimmed.Any(char.IsControl))
            throw new ArgumentException("Detail contains control characters.");
        return trimmed;
    }

    /// <summary>Stable record identifier: the prefix plus a hash of what identifies the record.</summary>
    internal static string Identity(string prefix, string naturalKey) =>
        prefix + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(naturalKey))).ToLowerInvariant()[..16];

    /// <summary>Random identifier for records whose only natural key is their own creation.</summary>
    internal static string Mint(string prefix) =>
        prefix + Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
}
