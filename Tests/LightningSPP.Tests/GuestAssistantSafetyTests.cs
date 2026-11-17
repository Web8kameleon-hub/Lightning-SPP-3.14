using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using wwwmmm.Web;
using wwwmmm.Workflow;
using Xunit;

namespace LightningSPP.Tests;

[CollectionDefinition("Guest assistant safety", DisableParallelization = true)]
public sealed class GuestAssistantSafetyCollection;

[Collection("Guest assistant safety")]
public sealed class GuestAssistantSafetyTests
{
    [Fact]
    public async Task PublicAccessIsOptInAndUnconfirmedDestinationDoesNotProcessQuestion()
    {
        string[] names = ["LSPP_DATA_DIR", "LSPP_GUEST_ASSISTANT_ENABLED", "LSPP_MODEL_AUTODISCOVER", "LSPP_MODEL_ROUTES_FILE"];
        var old = names.ToDictionary(name => name, Environment.GetEnvironmentVariable);
        var root = Path.Combine(Path.GetTempPath(), "spp-guest-safety-" + Guid.NewGuid().ToString("N"));
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        using var cancellation = new CancellationTokenSource(); Task server = Task.CompletedTask;
        try
        {
            Environment.SetEnvironmentVariable("LSPP_DATA_DIR", root);
            Environment.SetEnvironmentVariable("LSPP_GUEST_ASSISTANT_ENABLED", null);
            Environment.SetEnvironmentVariable("LSPP_MODEL_AUTODISCOVER", "off");
            Environment.SetEnvironmentVariable("LSPP_MODEL_ROUTES_FILE", null); ModelRoutes.ClearCache();
            server = LicenseApiServer.RunAsync(port, cancellation.Token);
            using var handler = new HttpClientHandler { CookieContainer = new CookieContainer() };
            using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:" + port) };
            client.DefaultRequestHeaders.Add("Origin", client.BaseAddress.GetLeftPart(UriPartial.Authority));
            using (var disabled = await client.GetAsync("/api/assistant")) Assert.Equal(HttpStatusCode.ServiceUnavailable, disabled.StatusCode);
            Environment.SetEnvironmentVariable("LSPP_GUEST_ASSISTANT_ENABLED", "true");
            var manifest = await client.GetFromJsonAsync<JsonElement>("/api/assistant");
            Assert.Equal("clisonix", manifest.GetProperty("channel").GetString());
            client.DefaultRequestHeaders.Add("X-CSRF-Token", manifest.GetProperty("csrfToken").GetString());
            using var denied = await client.PostAsJsonAsync("/api/assistant/conversation", new { goal = "Do not send this fixture anywhere" });
            Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
            using var changed = await client.PostAsJsonAsync("/api/assistant/conversation", new { goal = "Do not send this fixture anywhere", backend = "local" });
            Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        }
        finally
        {
            cancellation.Cancel(); await server;
            foreach (var item in old) Environment.SetEnvironmentVariable(item.Key, item.Value);
            ModelRoutes.ClearCache();
        }
    }
}
