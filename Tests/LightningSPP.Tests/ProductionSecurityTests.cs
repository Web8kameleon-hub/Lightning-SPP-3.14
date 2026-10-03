using System.Net;
using System.Net.Sockets;
using System.Text;
using LightningSPP.Licensing;
using wwwmmm.Web;
using Xunit;

namespace LightningSPP.Tests;

public class ProductionSecurityTests
{
    [Fact]
    public void UnsignedLegacyLicenseCannotGrantEnterpriseAccess()
    {
        Assert.False(LicenseKey.Validate("LSPP-ENTER-A-B-C-D", out _));
    }

    [Fact]
    public void MissingProductionSecretsFailClosed()
    {
        var oldEnv = Environment.GetEnvironmentVariable("LSPP_ENVIRONMENT");
        var oldToken = Environment.GetEnvironmentVariable("LSPP_ADMIN_TOKEN");
        try
        {
            Environment.SetEnvironmentVariable("LSPP_ENVIRONMENT", "Production");
            Environment.SetEnvironmentVariable("LSPP_ADMIN_TOKEN", null);
            Assert.Throws<InvalidOperationException>(RuntimeSecurity.ValidateProduction);
        }
        finally
        {
            Environment.SetEnvironmentVariable("LSPP_ENVIRONMENT", oldEnv);
            Environment.SetEnvironmentVariable("LSPP_ADMIN_TOKEN", oldToken);
        }
    }

    [Fact]
    public void RateLimitIsAtomicUnderConcurrentRequestsAndExpires()
    {
        var limiter = new RequestRateLimiter();
        var now = DateTimeOffset.UtcNow;
        var allowed = 0;
        Parallel.For(0, 300, _ => { if (!limiter.IsLimited("127.0.0.1", now)) Interlocked.Increment(ref allowed); });
        Assert.Equal(120, allowed);
        Assert.False(limiter.IsLimited("127.0.0.1", now.AddMinutes(1)));
    }

    [Fact]
    public void FulfillmentReceiptSurvivesReloadAndDoesNotMintTwice()
    {
        var old = Environment.GetEnvironmentVariable("LSPP_DATA_DIR");
        var dir = Path.Combine(Path.GetTempPath(), "lspp-receipt-" + Guid.NewGuid().ToString("N"));
        try
        {
            Environment.SetEnvironmentVariable("LSPP_DATA_DIR", dir);
            var receipt = new StripeFulfillment.Receipt("cs_test_receipt", "evt_test", "buyer@example.test", "test-fixture", DateTime.UtcNow);
            StripeFulfillment.StoreOnce(receipt.PaymentId, () => receipt);
            Assert.Equal(receipt, StripeFulfillment.ReadReceipt(receipt.PaymentId));
            Assert.Equal(receipt, StripeFulfillment.StoreOnce(receipt.PaymentId, () => throw new Exception("Must not issue twice")));
        }
        finally
        {
            Environment.SetEnvironmentVariable("LSPP_DATA_DIR", old);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task ActivationIsSelfServiceAndBodyLimitsApplyToChunkedRequests()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        using var cancel = new CancellationTokenSource();
        var server = LicenseApiServer.RunAsync(port, cancel.Token);
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}") };
            // Self-service: activation needs no admin token (LicenseManager.ActivateLicense
            // cryptographically validates the key itself), but a cryptographically invalid
            // key must still be rejected.
            using var activation = await client.PostAsync("/api/license/activate", new StringContent("key=LSPP-ENTER-A-B-C-D"));
            Assert.Equal(HttpStatusCode.BadRequest, activation.StatusCode);
            using var oversized = new HttpRequestMessage(HttpMethod.Post, "/api/stripe/webhook");
            oversized.Headers.TransferEncodingChunked = true;
            oversized.Content = new StreamContent(new MemoryStream(new byte[LicenseApiServer.MaxBodyBytes + 1]));
            using var result = await client.SendAsync(oversized);
            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, result.StatusCode);
        }
        finally { cancel.Cancel(); await server; }
    }
}
