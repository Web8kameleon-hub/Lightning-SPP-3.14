using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using wwwmmm.Web;
using Xunit;

namespace LightningSPP.Tests;

/// <summary>Local HTTP, SMTP and Longo integration fixtures; no production credentials or email.</summary>
public sealed class AccountFlowTests : IClassFixture<LongoFixture>
{
    [Fact]
    public async Task SignupVerificationLoginAndResetKeepSecretsHashedAndExistingLicenseStable()
    {
        await using var mailbox = new LocalMailbox();
        var names = new[] { "LSPP_DATA_DIR", "LSPP_TRUST_PROXY_HEADERS", "LSPP_LICENSE_SIGNING_KEY", "RESEND_API_KEY", "SMTP_HOST", "SMTP_PORT", "SMTP_USER", "SMTP_PASSWORD", "SMTP_FROM", "SMTP_USE_SSL" };
        var old = names.ToDictionary(n => n, Environment.GetEnvironmentVariable);
        var directory = Path.Combine(Path.GetTempPath(), "lightning-account-flow-" + Guid.NewGuid().ToString("N"));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        Task server = Task.CompletedTask;
        const string password = "test-only-starting-password-123!";
        const string replacement = "test-only-replacement-password-456!";
        try
        {
            Environment.SetEnvironmentVariable("LSPP_DATA_DIR", directory);
            Environment.SetEnvironmentVariable("LSPP_TRUST_PROXY_HEADERS", "true");
            Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY", "test-only-account-flow-signing-secret-32-plus-bytes");
            Environment.SetEnvironmentVariable("RESEND_API_KEY", null);
            Environment.SetEnvironmentVariable("SMTP_HOST", "127.0.0.1");
            Environment.SetEnvironmentVariable("SMTP_PORT", mailbox.Port.ToString());
            Environment.SetEnvironmentVariable("SMTP_USER", "test-fixture");
            Environment.SetEnvironmentVariable("SMTP_PASSWORD", "test-only-smtp-fixture");
            Environment.SetEnvironmentVariable("SMTP_FROM", "sender@example.test");
            Environment.SetEnvironmentVariable("SMTP_USE_SSL", "false");
            var probe = new TcpListener(IPAddress.Loopback, 0); probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
            server = LicenseApiServer.RunAsync(port, cancellation.Token);
            using var handler = new HttpClientHandler { CookieContainer = new CookieContainer() };
            using var client = new HttpClient(handler) { BaseAddress = new Uri($"http://localhost:{port}") };
            client.DefaultRequestHeaders.Add("Origin", client.BaseAddress.GetLeftPart(UriPartial.Authority));
            client.DefaultRequestHeaders.Add("X-Forwarded-For", "192.0.2.246");
            async Task Csrf()
            {
                var value = await client.GetFromJsonAsync<JsonElement>("/api/account/csrf", cancellation.Token);
                client.DefaultRequestHeaders.Remove("X-CSRF-Token");
                client.DefaultRequestHeaders.Add("X-CSRF-Token", value.GetProperty("csrfToken").GetString());
            }
            async Task<HttpResponseMessage> Post(string route, object body) =>
                await client.PostAsJsonAsync("/api/account/" + route, body, cancellation.Token);
            await Csrf();
            using (var signup = await Post("register", new { email = " Buyer+Trial@Example.Test ", password }))
                Assert.Equal(HttpStatusCode.OK, signup.StatusCode);
            var verificationEmail = await mailbox.NextAsync(cancellation.Token);
            Assert.Contains("RCPT TO:<buyer+trial@example.test>", verificationEmail);
            var verifyToken = LinkToken(verificationEmail, "verify");
            var store = CustomerAccounts.Store;
            var user = store.FindUser("BUYER+TRIAL@EXAMPLE.TEST", true)!;
            Assert.Equal("buyer+trial@example.test", user.Email);
            Assert.False(user.EmailVerified);
            var hash = Convert.FromBase64String(user.PasswordHash);
            Assert.Equal(1, hash[0]); // Identity V3 PBKDF2 format.
            Assert.Equal(600_000u, System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(hash.AsSpan(5, 4)));
            var grant = new CustomerStore.PaidGrant("in_flow_fixture", "evt_flow_fixture", "cus_flow_fixture", "sub_flow_fixture", user.Email, "PayAsYouGo", DateTimeOffset.UtcNow.AddDays(30), "active");
            var entitlement = store.CommitPaid(grant, () => LightningSPP.Licensing.LicenseKey.Generate(
                LightningSPP.Licensing.LicenseKey.LicenseTier.PayAsYouGo, user.Email, "", 30).Key);
            using (var wrong = await Post("login", new { email = user.Email, password = "wrong" }))
            {
                Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
                Assert.DoesNotContain("email_not_verified", await wrong.Content.ReadAsStringAsync(cancellation.Token));
            }
            using (var unverified = await Post("login", new { email = "BUYER+TRIAL@EXAMPLE.TEST", password }))
            {
                Assert.Equal(HttpStatusCode.Unauthorized, unverified.StatusCode);
                var body = await unverified.Content.ReadFromJsonAsync<JsonElement>(cancellation.Token);
                Assert.Equal("email_not_verified", body.GetProperty("code").GetString());
                Assert.Equal("UNVERIFIED", body.GetProperty("accountState").GetString());
                Assert.False(unverified.Headers.TryGetValues("Set-Cookie", out var cookies) && cookies.Any(c => c.StartsWith("LightningCustomer=")));
            }
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account", cancellation.Token)).StatusCode);
            Assert.Empty(store.Entitlements(user.Id));
            using (var verified = await Post("verify", new { token = verifyToken })) Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
            using (var replay = await Post("verify", new { token = verifyToken })) Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
            foreach (var email in new[] { "BUYER+TRIAL@EXAMPLE.TEST", "Buyer+Trial@Example.Test", " buyer+trial@example.test " })
            {
                using var login = await Post("login", new { email, password });
                Assert.Equal(HttpStatusCode.OK, login.StatusCode);
                var account = await client.GetFromJsonAsync<JsonElement>("/api/account", cancellation.Token);
                Assert.DoesNotContain("passwordHash", account.GetRawText(), StringComparison.OrdinalIgnoreCase);
                Assert.Equal("VERIFIED", account.GetProperty("accountState").GetString());
                Assert.Equal(entitlement.Id, account.GetProperty("entitlements")[0].GetProperty("id").GetString());
                client.DefaultRequestHeaders.Remove("X-CSRF-Token");
                client.DefaultRequestHeaders.Add("X-CSRF-Token", account.GetProperty("csrfToken").GetString());
                using var logout = await Post("logout", new { }); Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
                await Csrf();
            }
            using (var login = await Post("login", new { email = user.Email, password })) Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            var signedIn = await client.GetFromJsonAsync<JsonElement>("/api/account", cancellation.Token);
            client.DefaultRequestHeaders.Remove("X-CSRF-Token");
            client.DefaultRequestHeaders.Add("X-CSRF-Token", signedIn.GetProperty("csrfToken").GetString());
            using var requestedReset = await Post("request-reset", new { email = " BUYER+TRIAL@EXAMPLE.TEST " });
            Assert.Equal(HttpStatusCode.OK, requestedReset.StatusCode);
            var recoveryResponse = await requestedReset.Content.ReadAsStringAsync(cancellation.Token);
            var resetEmail = await mailbox.NextAsync(cancellation.Token);
            Assert.DoesNotContain(password, resetEmail);
            Assert.DoesNotContain(replacement, resetEmail);
            var resetToken = LinkToken(resetEmail, "reset");
            using (var wrongPurpose = await Post("verify", new { token = resetToken })) Assert.Equal(HttpStatusCode.BadRequest, wrongPurpose.StatusCode);
            using (var reset = await Post("reset", new { token = resetToken, password = replacement })) Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account", cancellation.Token)).StatusCode);
            await Csrf();
            using (var replay = await Post("reset", new { token = resetToken, password = replacement })) Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
            using (var wrong = await Post("login", new { email = user.Email, password })) Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
            using (var correct = await Post("login", new { email = user.Email, password = replacement })) Assert.Equal(HttpStatusCode.OK, correct.StatusCode);
            Assert.Equal(entitlement.LicenseKey, Assert.Single(store.Entitlements(user.Id)).LicenseKey);
            Assert.Single(store.ReadState().Receipts);
            var durable = JsonSerializer.Serialize(store.ReadState());
            Assert.DoesNotContain(password, durable); Assert.DoesNotContain(replacement, durable);
            Assert.DoesNotContain(resetToken, durable); Assert.DoesNotContain(verifyToken, durable);

            // A refused transport never fabricates successful delivery or grants account access.
            var unavailable = new TcpListener(IPAddress.Loopback, 0); unavailable.Start();
            var closedPort = ((IPEndPoint)unavailable.LocalEndpoint).Port; unavailable.Stop();
            Environment.SetEnvironmentVariable("SMTP_PORT", closedPort.ToString());
            // A different customer gets its own request budget; keep production limits intact.
            client.DefaultRequestHeaders.Remove("X-Forwarded-For");
            client.DefaultRequestHeaders.Add("X-Forwarded-For", "192.0.2.247");
            await Csrf();
            using (var failedEmail = await Post("register", new { email = "another@different.example", password }))
            {
                Assert.Equal(HttpStatusCode.ServiceUnavailable, failedEmail.StatusCode);
                var body = await failedEmail.Content.ReadFromJsonAsync<JsonElement>(cancellation.Token);
                Assert.Equal("verification_email_unavailable", body.GetProperty("code").GetString());
            }
            Assert.False(store.FindUser("ANOTHER@DIFFERENT.EXAMPLE", true)!.EmailVerified);
            using (var unverified = await Post("login", new { email = "ANOTHER@DIFFERENT.EXAMPLE", password }))
                Assert.Equal(HttpStatusCode.Unauthorized, unverified.StatusCode);
            using (var unknown = await Post("request-reset", new { email = "unknown@third.example" }))
            {
                Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
                Assert.Equal(recoveryResponse, await unknown.Content.ReadAsStringAsync(cancellation.Token));
            }
            Assert.Single(store.ReadState().Entitlements);
            if (Environment.GetEnvironmentVariable("SPP_ACCOUNT_BROWSER") == "1")
            {
                var root = AppContext.BaseDirectory;
                while (!File.Exists(Path.Combine(root, "wwwmmm.csproj"))) root = Directory.GetParent(root)!.FullName;
                var launch = new System.Diagnostics.ProcessStartInfo("node") { UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = root };
                launch.ArgumentList.Add("tools/Test-AccountBrowser.mjs");
                launch.Environment["SPP_ACCOUNT_BROWSER_BACKEND"] = client.BaseAddress.ToString();
                using var browser = System.Diagnostics.Process.Start(launch)!;
                var stdout = browser.StandardOutput.ReadToEndAsync(cancellation.Token);
                var stderr = browser.StandardError.ReadToEndAsync(cancellation.Token);
                await browser.WaitForExitAsync(cancellation.Token);
                Assert.True(browser.ExitCode == 0, await stdout + await stderr);
            }
        }
        finally
        {
            cancellation.Cancel(); await server;
            foreach (var pair in old) Environment.SetEnvironmentVariable(pair.Key, pair.Value);
        }
    }

    private static string LinkToken(string email, string purpose)
    {
        var decoded = Regex.Replace(email.Replace("=\r\n", ""), "=([0-9A-Fa-f]{2})",
            match => ((char)Convert.ToByte(match.Groups[1].Value, 16)).ToString());
        var token = Regex.Match(decoded, "#" + purpose + "=([A-Fa-f0-9]{64})");
        Assert.True(token.Success, "Test mailbox did not receive the expected single-use link.");
        return token.Groups[1].Value;
    }

    private sealed class LocalMailbox : IAsyncDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource cancel = new();
        private readonly Channel<string> messages = Channel.CreateUnbounded<string>();
        private readonly Task loop;
        internal int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
        internal LocalMailbox() { listener.Start(); loop = ServeAsync(); }
        internal ValueTask<string> NextAsync(CancellationToken token) => messages.Reader.ReadAsync(token);
        private async Task ServeAsync()
        {
            while (!cancel.IsCancellationRequested)
            {
                using var socket = await listener.AcceptTcpClientAsync(cancel.Token);
                using var stream = socket.GetStream(); using var reader = new StreamReader(stream);
                await using var writer = new StreamWriter(stream) { AutoFlush = true, NewLine = "\r\n" };
                var message = new StringBuilder(); await writer.WriteLineAsync("220 localhost fixture");
                while (await reader.ReadLineAsync(cancel.Token) is { } line)
                {
                    message.AppendLine(line);
                    if (line.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase)) await writer.WriteLineAsync("250-localhost\r\n250 OK");
                    else if (line == "DATA")
                    {
                        await writer.WriteLineAsync("354 End with dot");
                        while (await reader.ReadLineAsync(cancel.Token) is { } data && data != ".") message.Append(data).Append("\r\n");
                        await messages.Writer.WriteAsync(message.ToString(), cancel.Token);
                        await writer.WriteLineAsync("250 Test fixture accepted");
                    }
                    else if (line == "QUIT") { await writer.WriteLineAsync("221 Bye"); break; }
                    else await writer.WriteLineAsync("250 OK");
                }
            }
        }
        public async ValueTask DisposeAsync()
        {
            cancel.Cancel(); listener.Stop();
            try { await loop; } catch (OperationCanceledException) { }
            cancel.Dispose();
        }
    }
}
