using System.Net;
using System.Net.Sockets;
using System.Text;
using wwwmmm.Web;
using Xunit;

namespace LightningSPP.Tests;

public class ApiSigningStatusIntegrationTests
{
    [Fact]
    public async Task SigningStatusEndpoint_RequiresAuth_AndReturnsStatusWhenAuthorized()
    {
        var oldAdmin = Environment.GetEnvironmentVariable("LSPP_ADMIN_TOKEN");
        var oldKeys = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEYS");
        var oldActive = Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_ACTIVE_KID");

        var token = "test-admin-token-123";
        var port = GetFreePort();
        using var cts = new CancellationTokenSource();

        try
        {
            Environment.SetEnvironmentVariable("LSPP_ADMIN_TOKEN", token);
            Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEYS", "k1=alpha-secret;k2=beta-secret");
            Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_ACTIVE_KID", "k2");

            var serverTask = LicenseApiServer.RunAsync(port, cts.Token);
            await WaitUntilHealthyAsync(port, TimeSpan.FromSeconds(5));

            using var client = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}") };

            // Unauthorized request
            var unauthorized = await client.GetAsync("/api/license/signing-status");
            Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

            // Authorized request
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var authorized = await client.GetAsync("/api/license/signing-status");
            Assert.Equal(HttpStatusCode.OK, authorized.StatusCode);

            var payload = await authorized.Content.ReadAsStringAsync();
            Assert.Contains("ok=true", payload, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("activeKid=k2", payload, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("loadedKids=", payload, StringComparison.OrdinalIgnoreCase);

            cts.Cancel();
            await serverTask;
        }
        finally
        {
            Environment.SetEnvironmentVariable("LSPP_ADMIN_TOKEN", oldAdmin);
            Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEYS", oldKeys);
            Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_ACTIVE_KID", oldActive);
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
