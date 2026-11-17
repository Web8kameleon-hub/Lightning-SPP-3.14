using System.Net;
using System.Net.Sockets;
using wwwmmm.Web;
using Xunit;

namespace LightningSPP.Tests;

[CollectionDefinition("Admin summary environment", DisableParallelization = true)]
public sealed class AdminSummaryEnvironmentCollection;

[Collection("Admin summary environment")]
public sealed class ApiAdminSummaryIntegrationTests(LongoFixture fixture) : IClassFixture<LongoFixture>
{
    [Fact]
    public async Task AdminSummaryRequiresBearerAndReturnsOnlyAggregateCounts()
    {
        var oldDataDirectory = Environment.GetEnvironmentVariable("LSPP_DATA_DIR");
        var oldAdminToken = Environment.GetEnvironmentVariable("LSPP_ADMIN_TOKEN");
        var directory = Path.Combine(Path.GetTempPath(), "lightning-admin-summary-" + Guid.NewGuid().ToString("N"));
        const string adminToken = "test-admin-summary-token";
        var port = GetFreePort();
        using var cancellation = new CancellationTokenSource();

        try
        {
            Assert.StartsWith("http://127.0.0.1:", fixture.Url, StringComparison.Ordinal);
            Environment.SetEnvironmentVariable("LSPP_DATA_DIR", directory);
            Environment.SetEnvironmentVariable("LSPP_ADMIN_TOKEN", adminToken);
            var server = LicenseApiServer.RunAsync(port, cancellation.Token);
            try
            {
                await WaitUntilHealthyAsync(port, TimeSpan.FromSeconds(5));
                using var client = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}") };

                using var denied = await client.GetAsync("/api/admin/summary");
                Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
                using var wrongMethod = await client.PostAsync("/api/admin/summary", content: null);
                Assert.Equal(HttpStatusCode.MethodNotAllowed, wrongMethod.StatusCode);

                var store = CustomerAccounts.Store;
                var user = store.Register("admin-summary-fixture@example.test", "admin summary fixture password 123!");
                Assert.True(store.ConsumeToken(store.CreateToken(user, "verify", TimeSpan.FromHours(1)), "verify"));

                client.DefaultRequestHeaders.Authorization = new("Bearer", adminToken);
                using var authorized = await client.GetAsync("/api/admin/summary");
                Assert.Equal(HttpStatusCode.OK, authorized.StatusCode);
                var summary = await authorized.Content.ReadAsStringAsync();
                Assert.Contains("totalUsers=1", summary, StringComparison.Ordinal);
                Assert.Contains("verifiedActiveUsers=1", summary, StringComparison.Ordinal);
                Assert.Contains("apiRequests=", summary, StringComparison.Ordinal);
                Assert.DoesNotContain(user.Email, summary, StringComparison.Ordinal);

                var publicMetrics = await client.GetStringAsync("/api/metrics");
                Assert.DoesNotContain("apiRequests=", publicMetrics, StringComparison.Ordinal);
            }
            finally
            {
                cancellation.Cancel();
                await server;
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("LSPP_DATA_DIR", oldDataDirectory);
            Environment.SetEnvironmentVariable("LSPP_ADMIN_TOKEN", oldAdminToken);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static async Task WaitUntilHealthyAsync(int port, TimeSpan timeout)
    {
        using var client = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}") };
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var response = await client.GetAsync("/api/health");
                if (response.IsSuccessStatusCode) return;
            }
            catch (HttpRequestException) { }
            await Task.Delay(50);
        }
        throw new TimeoutException("API server did not become healthy in time.");
    }
}
