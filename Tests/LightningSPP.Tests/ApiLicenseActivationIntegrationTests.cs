using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using LightningSPP.Licensing;
using wwwmmm.Web;
using Xunit;

namespace LightningSPP.Tests;

/// <summary>
/// Edge-case coverage for the self-service activation fix: activation must succeed for any
/// valid, cryptographically signed license key without an admin token, while issuance and
/// signing-status remain admin-protected. See Web/LicenseApiServer.cs.
/// </summary>
public class ApiLicenseActivationIntegrationTests
{
    private static readonly object EnvLock = new();

    [Fact]
    public async Task ActivateEndpoint_EdgeCases_NoAdminTokenRequired_IssueStillProtected()
    {
        lock (EnvLock)
        {
            // Serialize with other tests that mutate the same static environment variables.
        }

        var oldAdmin = Environment.GetEnvironmentVariable("LSPP_ADMIN_TOKEN");
        var oldKeys = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEYS");
        var oldActive = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_ACTIVE_KID");
        var oldSingle = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY");
        var oldStripeSecret = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY");
        var oldStripePublishable = Environment.GetEnvironmentVariable("STRIPE_PUBLISHABLE_KEY");

        const string adminToken = "edge-case-admin-token";
        var port = GetFreePort();
        using var cts = new CancellationTokenSource();

        try
        {
            Environment.SetEnvironmentVariable("LSPP_ADMIN_TOKEN", adminToken);
            Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEYS", "k1=activation-edge-secret");
            Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_ACTIVE_KID", "k1");
            Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY", null);
            Environment.SetEnvironmentVariable("STRIPE_SECRET_KEY", "sk_test_edge_case");
            Environment.SetEnvironmentVariable("STRIPE_PUBLISHABLE_KEY", "pk_test_edge_case");

            var validKey = LicenseKey.Generate(LicenseKey.LicenseTier.Free, "edge@case.test", "Edge Case Co").Key;

            var serverTask = LicenseApiServer.RunAsync(port, cts.Token);
            await WaitUntilHealthyAsync(port, TimeSpan.FromSeconds(5));

            using var client = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}") };

            // 1. Valid signed key, NO Authorization header at all -> must succeed (the fix).
            var activateOk = await client.PostAsync("/api/license/activate",
                new FormUrlEncodedContent(new Dictionary<string, string> { ["key"] = validKey }));
            Assert.Equal(HttpStatusCode.OK, activateOk.StatusCode);
            Assert.Contains("ok=true", await activateOk.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

            // 2. Garbage/invalid key, no token -> rejected as bad input, not as an auth bypass.
            var activateGarbage = await client.PostAsync("/api/license/activate",
                new FormUrlEncodedContent(new Dictionary<string, string> { ["key"] = "not-a-real-license-key" }));
            Assert.Equal(HttpStatusCode.BadRequest, activateGarbage.StatusCode);

            // 3. Missing "key" field entirely -> 400, not a 500 or silent success.
            var activateMissing = await client.PostAsync("/api/license/activate",
                new FormUrlEncodedContent(new Dictionary<string, string>()));
            Assert.Equal(HttpStatusCode.BadRequest, activateMissing.StatusCode);
            Assert.Contains("Missing key", await activateMissing.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

            // 4. Empty-string key -> treated the same as missing, not accepted.
            var activateEmpty = await client.PostAsync("/api/license/activate",
                new FormUrlEncodedContent(new Dictionary<string, string> { ["key"] = "" }));
            Assert.Equal(HttpStatusCode.BadRequest, activateEmpty.StatusCode);

            // 5. Issuance must remain admin-protected: no token -> 401.
            var issueNoToken = await client.PostAsync("/api/license/issue",
                new FormUrlEncodedContent(new Dictionary<string, string> { ["email"] = "a@b.com", ["tier"] = "free" }));
            Assert.Equal(HttpStatusCode.Unauthorized, issueNoToken.StatusCode);

            // 6. Issuance with a wrong token -> still 401.
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "wrong-token");
            var issueWrongToken = await client.PostAsync("/api/license/issue",
                new FormUrlEncodedContent(new Dictionary<string, string> { ["email"] = "a@b.com", ["tier"] = "free" }));
            Assert.Equal(HttpStatusCode.Unauthorized, issueWrongToken.StatusCode);

            // 7. Issuance with the correct admin token -> succeeds and returns a usable key.
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);
            var issueOk = await client.PostAsync("/api/license/issue",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["email"] = "edge@case.test",
                    ["company"] = "Edge Case Co",
                    ["tier"] = "free"
                }));
            Assert.Equal(HttpStatusCode.OK, issueOk.StatusCode);
            var issuedBody = await issueOk.Content.ReadAsStringAsync();
            Assert.Contains("ok=true", issuedBody, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("key=", issuedBody, StringComparison.OrdinalIgnoreCase);

            cts.Cancel();
            await serverTask;
        }
        finally
        {
            Environment.SetEnvironmentVariable("LSPP_ADMIN_TOKEN", oldAdmin);
            Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEYS", oldKeys);
            Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_ACTIVE_KID", oldActive);
            Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY", oldSingle);
            Environment.SetEnvironmentVariable("STRIPE_SECRET_KEY", oldStripeSecret);
            Environment.SetEnvironmentVariable("STRIPE_PUBLISHABLE_KEY", oldStripePublishable);
        }
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task WaitUntilHealthyAsync(int port, TimeSpan timeout)
    {
        using var client = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}") };
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await client.GetAsync("/api/health");
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch
            {
                // Server not ready yet.
            }

            await Task.Delay(100);
        }

        throw new TimeoutException("API server did not become healthy in time.");
    }
}
