using System.Collections.Concurrent;
using Stripe;

namespace wwwmmm.Web;

internal static class StripeBilling
{
    private static readonly string[] WebhookSecretVariables =
    [
        "STRIPE_WEBHOOK_SECRET",
        "STRIPE_WEBHOOK_SECRET_SNAPSHOT",
        "STRIPE_WEBHOOK_SECRET_THIN"
    ];

    private static readonly ConcurrentDictionary<string, DateTime> ProcessedEvents =
        new(StringComparer.Ordinal);

    public static bool EnsureConfigured(out string message)
    {
        var secretKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY");
        var publishableKey = Environment.GetEnvironmentVariable("STRIPE_PUBLISHABLE_KEY");

        if (string.IsNullOrWhiteSpace(secretKey))
        {
            message = "Missing STRIPE_SECRET_KEY in environment.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(publishableKey))
        {
            message = "Missing STRIPE_PUBLISHABLE_KEY in environment.";
            return false;
        }

        StripeConfiguration.ApiKey = secretKey;
        message = "Stripe environment keys loaded.";
        return true;
    }

    public static bool TryVerifyWebhook(
        string payload,
        string signature,
        out Event? stripeEvent,
        out string matchedDestination,
        out string error)
    {
        stripeEvent = null;
        matchedDestination = string.Empty;
        error = "Webhook signature verification failed.";

        if (string.IsNullOrWhiteSpace(payload) || string.IsNullOrWhiteSpace(signature))
        {
            error = "Missing Stripe payload or Stripe-Signature header.";
            return false;
        }

        var secrets = ResolveWebhookSecrets();
        if (secrets.Count == 0)
        {
            error = "No Stripe webhook secret is configured.";
            return false;
        }

        foreach (var candidate in secrets)
        {
            try
            {
                stripeEvent = EventUtility.ConstructEvent(\n                    payload,\n                    signature,\n                    candidate.Secret,\n                    tolerance: 300,\n                    throwOnApiVersionMismatch: false);
                matchedDestination = candidate.Destination;
                error = string.Empty;
                return true;
            }
            catch (StripeException)
            {
                // Try the next configured destination without logging secret material.
            }
        }

        return false;
    }

    public static bool TryMarkEventProcessed(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
            return false;

        var cutoff = DateTime.UtcNow.AddDays(-1);
        foreach (var entry in ProcessedEvents)
        {
            if (entry.Value < cutoff)
                ProcessedEvents.TryRemove(entry.Key, out _);
        }

        return ProcessedEvents.TryAdd(eventId, DateTime.UtcNow);
    }

    private static List<(string Destination, string Secret)> ResolveWebhookSecrets()
    {
        var resolved = new List<(string Destination, string Secret)>();
        foreach (var variable in WebhookSecretVariables)
        {
            var secret = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrWhiteSpace(secret) &&
                !resolved.Any(item => string.Equals(item.Secret, secret, StringComparison.Ordinal)))
            {
                resolved.Add((DestinationName(variable), secret.Trim()));
            }
        }

        return resolved;
    }

    private static string DestinationName(string variable) => variable switch
    {
        "STRIPE_WEBHOOK_SECRET_SNAPSHOT" => "snapshot",
        "STRIPE_WEBHOOK_SECRET_THIN" => "thin",
        _ => "legacy"
    };
}
