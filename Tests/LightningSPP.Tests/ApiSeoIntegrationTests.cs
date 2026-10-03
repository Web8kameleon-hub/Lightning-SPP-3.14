using System.Net;
using System.Net.Sockets;
using wwwmmm.Web;
using Xunit;

namespace LightningSPP.Tests;

public class ApiSeoIntegrationTests
{
    [Fact]
    public async Task PublicPages_RemainCrawlable_WhenGeneralRateLimitIsExhausted()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        using var cts = new CancellationTokenSource();
        var server = LicenseApiServer.RunAsync(port, cts.Token);
        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var client = new HttpClient(handler) { BaseAddress = new Uri($"http://localhost:{port}") };
        var previousTrustProxy = Environment.GetEnvironmentVariable("LSPP_TRUST_PROXY_HEADERS");

        try
        {
            // Use a dedicated client IP so exhausting the static rate-limit
            // dictionary does not affect other integration tests in this process.
            Environment.SetEnvironmentVariable("LSPP_TRUST_PROXY_HEADERS", "true");
            client.DefaultRequestHeaders.Add("X-Forwarded-For", "192.0.2.241");
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (true)
            {
                try
                {
                    using var health = await client.GetAsync("/api/health");
                    if (health.IsSuccessStatusCode)
                        break;
                }
                catch (HttpRequestException) when (DateTime.UtcNow < deadline) { }
                if (DateTime.UtcNow >= deadline)
                    throw new TimeoutException("API server did not start.");
                await Task.Delay(100);
            }

            using var dashboard = await client.GetAsync("/dashboard");
            Assert.Equal("noindex", dashboard.Headers.GetValues("X-Robots-Tag").Single());

            // Unknown routes retain abuse protection. Exhausting that budget must
            // not prevent crawlers from fetching public pages, assets or metadata.
            for (var i = 0; i < 31; i++)
            {
                using var unknown = await client.GetAsync("/unknown-seo-test-path");
            }
            using var limited = await client.GetAsync("/api/license/status");
            Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
            Assert.Equal("noindex", limited.Headers.GetValues("X-Robots-Tag").Single());

            foreach (var path in new[] { "/", "/features", "/technology", "/pricing", "/benchmarks", "/try", "/swagger", "/robots.txt", "/sitemap.xml", "/site.css", "/try.js", "/favicon.ico", "/openapi.json" })
            {
                using var response = await client.GetAsync(path);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }

            using var redirect = await client.GetAsync("/index.html");
            Assert.Equal(HttpStatusCode.MovedPermanently, redirect.StatusCode);
            Assert.Equal("/", redirect.Headers.Location?.OriginalString);

            using var headRequest = new HttpRequestMessage(HttpMethod.Head, "/features");
            using var head = await client.SendAsync(headRequest);
            Assert.Equal(HttpStatusCode.OK, head.StatusCode);
            Assert.True(head.Content.Headers.ContentLength > 0);
            Assert.Empty(await head.Content.ReadAsByteArrayAsync());
        }
        finally
        {
            Environment.SetEnvironmentVariable("LSPP_TRUST_PROXY_HEADERS", previousTrustProxy);
            cts.Cancel();
            await server;
        }
    }
}
