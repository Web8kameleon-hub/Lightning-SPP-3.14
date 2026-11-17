using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Buffers.Binary;
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
    public async Task BinaryRoutesProcessRealBytesAndRejectUnsafeResourceRequests()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        using var cancellation = new CancellationTokenSource();
        var server = LicenseApiServer.RunAsync(port, cancellation.Token);
        try
        {
            using var client = IsolatedHttpClient.Create(port, 79);
            using (var method = await client.GetAsync("/api/fluid/bitpipeline")) Assert.Equal(HttpStatusCode.MethodNotAllowed, method.StatusCode);
            using (var missing = await client.PostAsJsonAsync("/api/fluid/missing", new { inputData = "" })) Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            var bytes = "real binary round-trip fixture"u8.ToArray();
            using var packed = await client.PostAsJsonAsync("/api/fluid/bitpipeline", new { inputData = Convert.ToBase64String(bytes) });
            Assert.Equal(HttpStatusCode.OK, packed.StatusCode);
            var frame = (await packed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("output").GetString();
            using var restored = await client.PostAsJsonAsync("/api/fluid/bitunpacker", new { inputData = frame });
            Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
            Assert.Equal(bytes, Convert.FromBase64String((await restored.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("output").GetString()!));
            var excessive = new byte[] { 0x43, 0x4c, 1, 0, 0, 0, 0 };
            BinaryPrimitives.WriteInt32LittleEndian(excessive.AsSpan(3), 8 * 1024 * 1024 + 1);
            using (var rejected = await client.PostAsJsonAsync("/api/fluid/bitunpacker", new { inputData = Convert.ToBase64String(excessive) }))
                Assert.Equal(HttpStatusCode.RequestEntityTooLarge, rejected.StatusCode);
            using var tooLarge = await client.PostAsync("/api/fluid/bitpipeline", new StringContent(new string('x', 1024 * 1024 + 1)));
            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, tooLarge.StatusCode);
        }
        finally { cancellation.Cancel(); await server; }
    }

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
            using var client = IsolatedHttpClient.Create(port, 80, handler.CookieContainer);
            client.DefaultRequestHeaders.Add("Origin", client.BaseAddress!.GetLeftPart(UriPartial.Authority));
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
