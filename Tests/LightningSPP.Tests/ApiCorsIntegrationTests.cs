using System.Net;
using System.Net.Sockets;
using wwwmmm.Web;
using Xunit;

namespace LightningSPP.Tests;

public class ApiCorsIntegrationTests
{
    [Fact]
    public async Task Cors_ReflectsOnlyExplicitlyAllowedOrigins()
    {
        var previous = Environment.GetEnvironmentVariable("LSPP_ALLOWED_ORIGINS");
        var port = GetFreePort();
        using var cts = new CancellationTokenSource();

        try
        {
            Environment.SetEnvironmentVariable(
                "LSPP_ALLOWED_ORIGINS",
                "https://scanprocessprint.com,https://www.scanprocessprint.com");

            var serverTask = LicenseApiServer.RunAsync(port, cts.Token);
            await WaitUntilHealthyAsync(port, TimeSpan.FromSeconds(5));

            using var client = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}") };

            using var allowedRequest = new HttpRequestMessage(HttpMethod.Get, "/api/health");
            allowedRequest.Headers.Add("Origin", "https://scanprocessprint.com");
            var allowedResponse = await client.SendAsync(allowedRequest);
            Assert.Equal("https://scanprocessprint.com", allowedResponse.Headers.GetValues("Access-Control-Allow-Origin").Single());

            using var deniedRequest = new HttpRequestMessage(HttpMethod.Get, "/api/health");
            deniedRequest.Headers.Add("Origin", "https://attacker.example");
            var deniedResponse = await client.SendAsync(deniedRequest);
            Assert.False(deniedResponse.Headers.Contains("Access-Control-Allow-Origin"));

            cts.Cancel();
            await serverTask;
        }
        finally
        {
            Environment.SetEnvironmentVariable("LSPP_ALLOWED_ORIGINS", previous);
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
                if ((await client.GetAsync("/api/health")).IsSuccessStatusCode)
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
