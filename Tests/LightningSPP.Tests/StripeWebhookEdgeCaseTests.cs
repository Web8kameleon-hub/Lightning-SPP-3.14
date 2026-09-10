using wwwmmm.Web;
using Xunit;

namespace LightningSPP.Tests;

public class StripeWebhookEdgeCaseTests
{
    [Theory]
    [InlineData("", "signature")]
    [InlineData("{}", "")]
    [InlineData(" ", " ")]
    public void Verifier_RejectsMissingPayloadOrSignature(string payload, string signature)
    {
        var valid = StripeBilling.TryVerifyWebhook(
            payload, signature, out var stripeEvent, out var destination, out var error);

        Assert.False(valid);
        Assert.Null(stripeEvent);
        Assert.Equal(string.Empty, destination);
        Assert.Contains("Missing", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Verifier_FailsClosedWhenNoWebhookSecretsExist()
    {
        var names = new[]
        {
            "STRIPE_WEBHOOK_SECRET",
            "STRIPE_WEBHOOK_SECRET_SNAPSHOT",
            "STRIPE_WEBHOOK_SECRET_THIN"
        };
        var previous = names.ToDictionary(name => name, Environment.GetEnvironmentVariable);
        try
        {
            foreach (var name in names)
                Environment.SetEnvironmentVariable(name, null);

            var valid = StripeBilling.TryVerifyWebhook(
                """{"id":"evt_no_secret","object":"event"}""",
                "t=1,v1=invalid",
                out _, out _, out var error);

            Assert.False(valid);
            Assert.Contains("No Stripe webhook secret", error, StringComparison.Ordinal);
        }
        finally
        {
            foreach (var item in previous)
                Environment.SetEnvironmentVariable(item.Key, item.Value);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void ReplayGuard_RejectsBlankEventIds(string eventId)
    {
        Assert.False(StripeBilling.TryMarkEventProcessed(eventId));
    }
}
