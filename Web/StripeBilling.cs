using Stripe;

namespace wwwmmm.Web;

internal static class StripeBilling
{
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
}
