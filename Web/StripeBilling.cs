using System.Collections.Concurrent;
using Stripe;
using Stripe.Checkout;

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
                stripeEvent = EventUtility.ConstructEvent(
                    payload,
                    signature,
                    candidate.Secret,
                    tolerance: 300,
                    throwOnApiVersionMismatch: false);
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

    /// <summary>
    /// Extracts the fields needed to fulfil a completed Checkout Session (issue + email a
    /// license key). Returns false for any other event type, or for a session that is not
    /// actually paid yet (e.g. still awaiting an asynchronous payment method).
    /// </summary>
    public static bool TryGetCheckoutFulfillment(
        Event stripeEvent,
        out string email,
        out string tier,
        out string sessionId)
    {
        email = string.Empty;
        tier = string.Empty;
        sessionId = string.Empty;

        if (!string.Equals(stripeEvent.Type, "checkout.session.completed", StringComparison.Ordinal))
            return false;

        if (stripeEvent.Data?.Object is not Session session)
            return false;

        if (!string.Equals(session.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase))
            return false;

        var resolvedEmail = session.CustomerDetails?.Email ?? session.CustomerEmail;
        if (string.IsNullOrWhiteSpace(resolvedEmail))
            return false;

        if (session.Metadata is null || !session.Metadata.TryGetValue("tier", out var resolvedTier) ||
            string.IsNullOrWhiteSpace(resolvedTier))
            return false;

        email = resolvedEmail;
        tier = resolvedTier;
        sessionId = session.Id ?? string.Empty;
        return true;
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
