namespace wwwmmm.Crm;

/// <summary>How CRM records reach the disk.</summary>
public enum CrmStorageMode
{
    /// <summary>Every record is written and flushed before the call returns.</summary>
    Durable = 0,

    /// <summary>
    /// Reserved value. Batched storage is not implemented and must not be selected.
    /// </summary>
    Batched = 1
}

/// <summary>
/// Chosen explicitly by the operator, never inferred from timing. <c>LSPP_CRM_STORAGE</c> selects
/// the mode and an unrecognised value is an error, so a typo cannot silently weaken durability.
/// </summary>
public sealed record CrmStoragePolicy(CrmStorageMode Mode, int BatchSize, int FlushMilliseconds, int MaxPending)
{
    public const string ModeVariable = "LSPP_CRM_STORAGE";
    public const string BatchSizeVariable = "LSPP_CRM_BATCH_SIZE";
    public const string FlushMillisecondsVariable = "LSPP_CRM_BATCH_MS";
    public const string MaxPendingVariable = "LSPP_CRM_BATCH_MAX";

    public static CrmStoragePolicy Default { get; } = new(CrmStorageMode.Durable, 64, 250, 4096);

    public static CrmStoragePolicy FromEnvironment()
    {
        var raw = Environment.GetEnvironmentVariable(ModeVariable);
        var mode = string.IsNullOrWhiteSpace(raw)
            ? CrmStorageMode.Durable
            : raw.Trim().ToLowerInvariant() switch
            {
                "durable" => CrmStorageMode.Durable,
                "batched" => throw new NotSupportedException("Batched CRM storage is not implemented; select durable."),
                _ => throw new InvalidOperationException($"{ModeVariable} must be 'durable'.")
            };

        if (new[] { BatchSizeVariable, FlushMillisecondsVariable, MaxPendingVariable }.Any(name =>
                !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name))))
            throw new NotSupportedException("CRM batching controls are not implemented and cannot affect durable storage.");
        return Default with { Mode = mode };
    }

    private static int ReadInt(string variable, int fallback, int minimum, int maximum)
    {
        var raw = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        if (!int.TryParse(raw.Trim(), System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var value) || value < minimum || value > maximum)
            throw new InvalidOperationException($"{variable} must be an integer from {minimum} to {maximum}; received '{raw}'.");
        return value;
    }
}
