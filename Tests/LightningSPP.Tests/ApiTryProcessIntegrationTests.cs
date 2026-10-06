using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using wwwmmm.Web;
using Xunit;

namespace LightningSPP.Tests;

/// <summary>
/// Edge-case coverage for the public, unauthenticated /api/try/process demo endpoint.
/// All scenarios run against a single server instance in one test method because the
/// endpoint's rate limiter is keyed by client IP in a static, process-wide dictionary;
/// splitting this across multiple test methods from the same loopback IP would make
/// the rate-limit assertion (and any test that runs after it) order-dependent and flaky.
/// </summary>
public class ApiTryProcessIntegrationTests
{
    [Fact]
    public async Task TryProcessEndpoint_EdgeCases_ThenRateLimitTrips()
    {
        var port = GetFreePort();
        using var cts = new CancellationTokenSource();
        var serverTask = LicenseApiServer.RunAsync(port, cts.Token);
        await WaitUntilHealthyAsync(port, TimeSpan.FromSeconds(5));

        using var client = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}") };

        // 1. Valid upload, no profile header -> defaults to lightning.
        var r1 = await PostImageAsync(client, RealTestImage.Png(), "image/png", profile: null);
        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
        var body1 = await r1.Content.ReadAsStringAsync();
        Assert.Contains("\"ok\": true", body1);
        Assert.Contains("\"profile\": \"lightning\"", body1);
        Assert.Contains("\"afterImageBase64\"", body1);

        // 2. Valid upload, explicit profile -> echoed back.
        var r2 = await PostImageAsync(client, RealTestImage.Png(), "image/png", profile: "nanodecibel");
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
        Assert.Contains("\"profile\": \"nanodecibel\"", await r2.Content.ReadAsStringAsync());

        // 3. Unknown profile header -> 400, not silently defaulted.
        var r3 = await PostImageAsync(client, RandomBytes(1024), "image/png", profile: "not-a-real-profile");
        Assert.Equal(HttpStatusCode.BadRequest, r3.StatusCode);

        // 4. Unsupported content type (e.g. a PDF) -> 400, clear reason.
        var r4 = await PostImageAsync(client, RandomBytes(1024), "application/pdf", profile: null);
        Assert.Equal(HttpStatusCode.BadRequest, r4.StatusCode);
        Assert.Contains("Unsupported file type", await r4.Content.ReadAsStringAsync());

        // 5. Empty body -> 400, not a 500 or hang.
        var r5 = await PostImageAsync(client, Array.Empty<byte>(), "image/png", profile: null);
        Assert.Equal(HttpStatusCode.BadRequest, r5.StatusCode);

        // 6. Oversized body (> 8 MB) -> 400, rejected before full processing.
        var oversized = RandomBytes(8 * 1024 * 1024 + 1024);
        var r6 = await PostImageAsync(client, oversized, "image/png", profile: null);
        Assert.Equal(HttpStatusCode.BadRequest, r6.StatusCode);

        // Calls so far: 6. Issue 4 more harmless valid calls to reach the limit of 10.
        for (var i = 0; i < 4; i++)
        {
            var filler = await PostImageAsync(client, RealTestImage.Png(), "image/png", profile: null);
            Assert.Equal(HttpStatusCode.OK, filler.StatusCode);
        }

        // 7. The 11th request from this IP within the window must be rate-limited.
        var r11 = await PostImageAsync(client, RealTestImage.Png(), "image/png", profile: null);
        Assert.Equal(HttpStatusCode.TooManyRequests, r11.StatusCode);

        cts.Cancel();
        await serverTask;
    }

    private static Task<HttpResponseMessage> PostImageAsync(HttpClient client, byte[] bytes, string contentType, string? profile)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/try/process") { Content = content };
        if (profile is not null)
        {
            request.Headers.Add("X-Stigma-Profile", profile);
        }

        return client.SendAsync(request);
    }

    private static byte[] RandomBytes(int size)
    {
        var bytes = new byte[size];
        Random.Shared.NextBytes(bytes);
        return bytes;
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
