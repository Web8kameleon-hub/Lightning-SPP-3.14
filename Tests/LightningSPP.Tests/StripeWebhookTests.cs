using System.Security.Cryptography;
using System.Text;
using wwwmmm.Web;
using Xunit;

namespace LightningSPP.Tests;

public class StripeWebhookTests
{
    [Fact]
    public void WebhookVerifier_AcceptsSnapshotDestination_AndRejectsReplay()
    {
        const string secret = "whsec_test_snapshot";
        var oldLegacy = Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET");
        var oldSnapshot = Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET_SNAPSHOT");
        var oldThin = Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET_THIN");

        try
        {
            Environment.SetEnvironmentVariable("STRIPE_WEBHOOK_SECRET", null);
            Environment.SetEnvironmentVariable("STRIPE_WEBHOOK_SECRET_SNAPSHOT", secret);
            Environment.SetEnvironmentVariable("STRIPE_WEBHOOK_SECRET_THIN", null);

            var eventId = $"evt_{Guid.NewGuid():N}";
            var payload = "{\"id\":\"" + eventId + "\",\"object\":\"event\",\"type\":\"customer.updated\",\"data\":{\"object\":{}}}";
            var signature = CreateSignature(payload, secret);

            var valid = StripeBilling.TryVerifyWebhook(
                payload,
                signature,
                out var stripeEvent,
                out var destination,
                out var error);

            Assert.True(valid, error);
            Assert.NotNull(stripeEvent);
            Assert.Equal("snapshot", destination);
            Assert.True(StripeBilling.TryMarkEventProcessed(eventId));
            Assert.False(StripeBilling.TryMarkEventProcessed(eventId));
        }
        finally
        {
            Environment.SetEnvironmentVariable("STRIPE_WEBHOOK_SECRET", oldLegacy);
            Environment.SetEnvironmentVariable("STRIPE_WEBHOOK_SECRET_SNAPSHOT", oldSnapshot);
            Environment.SetEnvironmentVariable("STRIPE_WEBHOOK_SECRET_THIN", oldThin);
        }
    }

    [Fact]
    public void WebhookVerifier_RejectsInvalidSignature()
    {
        Environment.SetEnvironmentVariable("STRIPE_WEBHOOK_SECRET", "whsec_correct");
        try
        {
            const string payload = """{"id":"evt_invalid","object":"event","type":"customer.updated","data":{"object":{}}}""";
            var signature = CreateSignature(payload, "whsec_wrong");

            Assert.False(StripeBilling.TryVerifyWebhook(
                payload,
                signature,
                out _,
                out _,
                out _));
        }
        finally
        {
            Environment.SetEnvironmentVariable("STRIPE_WEBHOOK_SECRET", null);
        }
    }
    private static string CreateSignature(string payload, string secret)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signedPayload = $"{timestamp}.{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var digest = Convert.ToHexString(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload))).ToLowerInvariant();
        return $"t={timestamp},v1={digest}";
    }
}
