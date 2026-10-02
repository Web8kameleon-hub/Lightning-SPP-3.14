using System.Security.Cryptography;
using System.Text;
using wwwmmm.Web;
using Xunit;

namespace LightningSPP.Tests;

/// <summary>
/// Edge-case coverage for Stripe Checkout fulfillment parsing (StripeBilling.TryGetCheckoutFulfillment),
/// which drives automatic license issuance + email after a real payment.
/// </summary>
public class StripeCheckoutFulfillmentTests
{
    private const string Secret = "whsec_test_fulfillment";

    [Fact]
    public void Fulfillment_PaidSessionWithEmailAndTier_IsRecognized()
    {
        WithWebhookSecret(() =>
        {
            var payload = BuildCheckoutEventPayload(
                eventId: $"evt_{Guid.NewGuid():N}",
                sessionId: "cs_test_paid_123",
                paymentStatus: "paid",
                email: "customer@example.com",
                tier: "unlimited");

            Assert.True(StripeBilling.TryVerifyWebhook(payload, Sign(payload), out var stripeEvent, out _, out var error), error);
            Assert.NotNull(stripeEvent);

            Assert.True(StripeBilling.TryGetCheckoutFulfillment(stripeEvent!, out var email, out var tier, out var sessionId));
            Assert.Equal("customer@example.com", email);
            Assert.Equal("unlimited", tier);
            Assert.Equal("cs_test_paid_123", sessionId);
        });
    }

    [Fact]
    public void Fulfillment_UnpaidSession_IsRejected()
    {
        WithWebhookSecret(() =>
        {
            var payload = BuildCheckoutEventPayload(
                eventId: $"evt_{Guid.NewGuid():N}",
                sessionId: "cs_test_unpaid",
                paymentStatus: "unpaid",
                email: "customer@example.com",
                tier: "unlimited");

            Assert.True(StripeBilling.TryVerifyWebhook(payload, Sign(payload), out var stripeEvent, out _, out _));
            Assert.False(StripeBilling.TryGetCheckoutFulfillment(stripeEvent!, out _, out _, out _));
        });
    }

    [Fact]
    public void Fulfillment_MissingTierMetadata_IsRejected()
    {
        WithWebhookSecret(() =>
        {
            var payload = BuildCheckoutEventPayload(
                eventId: $"evt_{Guid.NewGuid():N}",
                sessionId: "cs_test_no_tier",
                paymentStatus: "paid",
                email: "customer@example.com",
                tier: null);

            Assert.True(StripeBilling.TryVerifyWebhook(payload, Sign(payload), out var stripeEvent, out _, out _));
            Assert.False(StripeBilling.TryGetCheckoutFulfillment(stripeEvent!, out _, out _, out _));
        });
    }

    [Fact]
    public void Fulfillment_MissingEmail_IsRejected()
    {
        WithWebhookSecret(() =>
        {
            var payload = BuildCheckoutEventPayload(
                eventId: $"evt_{Guid.NewGuid():N}",
                sessionId: "cs_test_no_email",
                paymentStatus: "paid",
                email: null,
                tier: "paygo");

            Assert.True(StripeBilling.TryVerifyWebhook(payload, Sign(payload), out var stripeEvent, out _, out _));
            Assert.False(StripeBilling.TryGetCheckoutFulfillment(stripeEvent!, out _, out _, out _));
        });
    }

    [Fact]
    public void Fulfillment_UnrelatedEventType_IsRejected()
    {
        WithWebhookSecret(() =>
        {
            var eventId = $"evt_{Guid.NewGuid():N}";
            var payload = "{\"id\":\"" + eventId + "\",\"object\":\"event\",\"type\":\"customer.updated\",\"data\":{\"object\":{}}}";

            Assert.True(StripeBilling.TryVerifyWebhook(payload, Sign(payload), out var stripeEvent, out _, out _));
            Assert.False(StripeBilling.TryGetCheckoutFulfillment(stripeEvent!, out _, out _, out _));
        });
    }

    [Fact]
    public async Task EmailService_WithoutSmtpConfigured_FailsGracefullyWithReason()
    {
        var oldHost = Environment.GetEnvironmentVariable("SMTP_HOST");
        try
        {
            Environment.SetEnvironmentVariable("SMTP_HOST", null);

            var (sent, error) = await EmailService.TrySendLicenseKeyEmailAsync(
                "customer@example.com", "unlimited", "LSP2-k1-TEST-00000000-00000000-00000000", DateTime.UtcNow.AddYears(1));

            Assert.False(sent);
            Assert.Contains("SMTP", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SMTP_HOST", oldHost);
        }
    }

    private static void WithWebhookSecret(Action action)
    {
        var oldLegacy = Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET");
        var oldSnapshot = Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET_SNAPSHOT");
        var oldThin = Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET_THIN");

        try
        {
            Environment.SetEnvironmentVariable("STRIPE_WEBHOOK_SECRET", Secret);
            Environment.SetEnvironmentVariable("STRIPE_WEBHOOK_SECRET_SNAPSHOT", null);
            Environment.SetEnvironmentVariable("STRIPE_WEBHOOK_SECRET_THIN", null);
            action();
        }
        finally
        {
            Environment.SetEnvironmentVariable("STRIPE_WEBHOOK_SECRET", oldLegacy);
            Environment.SetEnvironmentVariable("STRIPE_WEBHOOK_SECRET_SNAPSHOT", oldSnapshot);
            Environment.SetEnvironmentVariable("STRIPE_WEBHOOK_SECRET_THIN", oldThin);
        }
    }

    private static string BuildCheckoutEventPayload(
        string eventId, string sessionId, string paymentStatus, string? email, string? tier)
    {
        var emailJson = email is null ? "null" : $"\"{email}\"";
        var metadataJson = tier is null ? "{}" : $$"""{"tier":"{{tier}}"}""";

        return $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "type": "checkout.session.completed",
          "data": {
            "object": {
              "id": "{{sessionId}}",
              "object": "checkout.session",
              "payment_status": "{{paymentStatus}}",
              "customer_details": { "email": {{emailJson}} },
              "metadata": {{metadataJson}}
            }
          }
        }
        """;
    }

    private static string Sign(string payload)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signedPayload = $"{timestamp}.{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        var digest = Convert.ToHexString(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload))).ToLowerInvariant();
        return $"t={timestamp},v1={digest}";
    }
}
