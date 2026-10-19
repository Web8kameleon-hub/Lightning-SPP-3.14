using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using wwwmmm.Web;
using Xunit;

namespace LightningSPP.Tests;

/// <summary>Real HttpListener + cookie/CSRF middleware + real isolated Longo engine. Fixture accounts never enter production.</summary>
public sealed class WorkflowApiTests : IClassFixture<LongoFixture>
{
    [Fact]
    public async Task CustomerWorkspaceRequiresCsrfAndKeepsDocumentsJobsAndReportsPrivate()
    {
        var names = new[] { "LSPP_DATA_DIR", "LSPP_TRUST_PROXY_HEADERS", "LSPP_MODEL_ROUTES_FILE", "LSPP_WORKFLOW_ALLOWED_EMAILS" };
        var old = names.ToDictionary(n => n, Environment.GetEnvironmentVariable);
        var directory = Path.Combine(Path.GetTempPath(), "lightning-workflow-api-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("LSPP_DATA_DIR", directory); Environment.SetEnvironmentVariable("LSPP_TRUST_PROXY_HEADERS", "true"); Environment.SetEnvironmentVariable("LSPP_MODEL_ROUTES_FILE", null);
        var socket = new TcpListener(IPAddress.Loopback, 0); socket.Start(); var port = ((IPEndPoint)socket.LocalEndpoint).Port; socket.Stop();
        using var cancel = new CancellationTokenSource(); var server = LicenseApiServer.RunAsync(port, cancel.Token);
        using var handler = new HttpClientHandler { CookieContainer = new CookieContainer() };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:" + port) };
        client.DefaultRequestHeaders.Add("Origin", client.BaseAddress.GetLeftPart(UriPartial.Authority)); client.DefaultRequestHeaders.Add("X-Forwarded-For", "192.0.2.245");
        const string password = "test-only-workspace-password-123!";
        try
        {
            using (var signedOut = await client.GetAsync("/api/workflow"))
            {
                Assert.Equal(HttpStatusCode.Forbidden, signedOut.StatusCode); // Keep the legacy HTTP contract.
                Assert.Equal("authentication_required", (await signedOut.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
            }
            var store = CustomerAccounts.Store; var user = store.Register("workspace-fixture@example.test", password);
            Assert.True(store.ConsumeToken(store.CreateToken(user, "verify", TimeSpan.FromHours(1)), "verify"));
            var anonymous = await client.GetFromJsonAsync<JsonElement>("/api/account/csrf"); client.DefaultRequestHeaders.Add("X-CSRF-Token", anonymous.GetProperty("csrfToken").GetString());
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/account/login", new { email = user.Email, password })).StatusCode);
            Environment.SetEnvironmentVariable("LSPP_WORKFLOW_ALLOWED_EMAILS", "different-fixture@example.test");
            using (var forbidden = await client.GetAsync("/api/workflow"))
            {
                Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
                Assert.Equal("workspace_access_denied", (await forbidden.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
            }
            Environment.SetEnvironmentVariable("LSPP_WORKFLOW_ALLOWED_EMAILS", user.Email);
            var manifest = await client.GetFromJsonAsync<JsonElement>("/api/workflow"); Assert.True(manifest.GetProperty("tools").GetArrayLength() >= 10);
            client.DefaultRequestHeaders.Remove("X-CSRF-Token");
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/workflow/import", new { filename = "test.txt", base64 = "eA==" })).StatusCode);
            client.DefaultRequestHeaders.Add("X-CSRF-Token", manifest.GetProperty("csrfToken").GetString());
            var response = await client.PostAsJsonAsync("/api/workflow/import", new { filename = "source.txt", base64 = Convert.ToBase64String("real test-only input"u8.ToArray()) });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode); var path = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("path").GetString();
            var plan = new { goal = "Read my imported source", steps = new[] { new { id = "read", tool = "files.read", arguments = new { path }, dependsOn = Array.Empty<string>() } } };
            var saved = await client.PostAsJsonAsync("/api/workflow/plan", new { plan }); Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
            var id = (await saved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
            var executed = await client.PostAsJsonAsync("/api/workflow/run", new { id }); Assert.Equal(HttpStatusCode.OK, executed.StatusCode);
            Assert.Equal("SUCCEEDED", (await executed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("state").GetString());
            Environment.SetEnvironmentVariable("LSPP_WORKFLOW_ALLOWED_EMAILS", null);
            // A second valid customer's session still cannot name the first customer's job or file.
            var other = store.Register("other-workspace-fixture@example.test", password); Assert.True(store.ConsumeToken(store.CreateToken(other, "verify", TimeSpan.FromHours(1)), "verify"));
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/account/logout", new { })).StatusCode);
            var csrf = await client.GetFromJsonAsync<JsonElement>("/api/account/csrf"); client.DefaultRequestHeaders.Remove("X-CSRF-Token"); client.DefaultRequestHeaders.Add("X-CSRF-Token", csrf.GetProperty("csrfToken").GetString());
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/account/login", new { email = other.Email, password })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/workflow/job?id=" + id)).StatusCode);
            var otherManifest = await client.GetFromJsonAsync<JsonElement>("/api/workflow"); client.DefaultRequestHeaders.Remove("X-CSRF-Token"); client.DefaultRequestHeaders.Add("X-CSRF-Token", otherManifest.GetProperty("csrfToken").GetString());
            var otherJob = await client.PostAsJsonAsync("/api/workflow/plan", new { plan }); var otherId = (await otherJob.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
            var denied = await client.PostAsJsonAsync("/api/workflow/run", new { id = otherId }); var deniedJob = await denied.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("FAILED", deniedJob.GetProperty("steps")[0].GetProperty("state").GetString());
            Assert.DoesNotContain("real test-only input", deniedJob.GetRawText());
            if (Environment.GetEnvironmentVariable("SPP_WORKFLOW_BROWSER") == "1")
            {
                var root = AppContext.BaseDirectory;
                while (!File.Exists(Path.Combine(root, "wwwmmm.csproj"))) root = Directory.GetParent(root)!.FullName;
                var launch = new System.Diagnostics.ProcessStartInfo("node") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = root };
                launch.ArgumentList.Add("tools/Test-WorkflowBrowser.mjs"); launch.Environment["SPP_WORKFLOW_BROWSER_BACKEND"] = client.BaseAddress.ToString();
                using var browser = System.Diagnostics.Process.Start(launch)!;
                var stdout = browser.StandardOutput.ReadToEndAsync(); var stderr = browser.StandardError.ReadToEndAsync();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
                try { await browser.WaitForExitAsync(timeout.Token); } catch { browser.Kill(true); throw; }
                Assert.True(browser.ExitCode == 0, await stdout + "\n" + await stderr);
            }
        }
        finally { cancel.Cancel(); await server; foreach (var pair in old) Environment.SetEnvironmentVariable(pair.Key, pair.Value); }
    }
}
