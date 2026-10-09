using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using wwwmmm.Web;
using Xunit;

namespace LightningSPP.Tests;

/// <summary>Runs the extracted, real Java Longo engine against isolated test data. No fake persistence.</summary>
public sealed class LongoFixture : IDisposable
{
    private readonly string? oldUrl = Environment.GetEnvironmentVariable("LONGO_URL");
    private readonly string classes;
    private readonly string storage;
    private Process process = null!;
    public string Url { get; }
    public LongoFixture()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "wwwmmm.csproj"))) root = Directory.GetParent(root)?.FullName ?? throw new IOException("Repository not found.");
        var temp = Path.Combine(Path.GetTempPath(), "lightning-real-longo-" + Guid.NewGuid().ToString("N"));
        classes = Path.Combine(temp, "classes"); Directory.CreateDirectory(classes); storage = Path.Combine(temp, "longo.bin");
        var compiler = new ProcessStartInfo("javac") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var arg in new[] { "--add-modules", "jdk.httpserver", "-d", classes }) compiler.ArgumentList.Add(arg);
        foreach (var source in Directory.GetFiles(Path.Combine(root, "Persistence", "Longo", "src", "main", "java"), "*.java", SearchOption.AllDirectories)) compiler.ArgumentList.Add(source);
        using var build = Process.Start(compiler) ?? throw new IOException("javac unavailable.");
        var output = build.StandardOutput.ReadToEndAsync(); var error = build.StandardError.ReadToEndAsync();
        if (!build.WaitForExit(60_000)) { build.Kill(true); throw new IOException("Longo compilation timed out."); }
        if (build.ExitCode != 0) throw new IOException("Longo compilation failed: " + error.GetAwaiter().GetResult());
        var socket = new TcpListener(IPAddress.Loopback, 0); socket.Start(); var port = ((IPEndPoint)socket.LocalEndpoint).Port; socket.Stop();
        Url = "http://127.0.0.1:" + port;
        Start(); Environment.SetEnvironmentVariable("LONGO_URL", Url);
    }
    private void Start()
    {
        var launch = new ProcessStartInfo("java") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var arg in new[] { "--add-modules", "jdk.httpserver", "-cp", classes, "com.clisonix.longo.LongoServer" }) launch.ArgumentList.Add(arg);
        launch.Environment["LONGO_HOST"] = "127.0.0.1"; launch.Environment["LONGO_PORT"] = new Uri(Url).Port.ToString(); launch.Environment["LONGO_STORAGE_PATH"] = storage;
        process = Process.Start(launch) ?? throw new IOException("Longo could not start.");
        process.BeginOutputReadLine(); process.BeginErrorReadLine();
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
        for (var i = 0; i < 100; i++)
        {
            if (process.HasExited) throw new IOException("Longo exited during startup.");
            try { if (client.GetAsync(Url + "/health").GetAwaiter().GetResult().IsSuccessStatusCode) return; } catch (HttpRequestException) { }
            Thread.Sleep(50);
        }
        throw new IOException("Real Longo health check timed out.");
    }
    public void Restart() { process.Kill(true); process.WaitForExit(); process.Dispose(); Start(); }
    public void Dispose() { if (!process.HasExited) { process.Kill(true); process.WaitForExit(); } process.Dispose(); Environment.SetEnvironmentVariable("LONGO_URL", oldUrl); }
}

public sealed class LongoPersistenceTests(LongoFixture fixture) : IClassFixture<LongoFixture>
{
    [Fact]
    public async Task StaleWriterCannotOverwriteDurableStateAfterServiceRestart()
    {
        using var client = new HttpClient(); var url = fixture.Url + "/v1/lightning/state/cas-test";
        async Task<HttpResponseMessage> Write(int revision, string value)
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, url);
            request.Headers.TryAddWithoutValidation("If-Match", revision.ToString()); request.Content = JsonContent.Create(new { data = new { value } });
            return await client.SendAsync(request);
        }
        using var first = await Write(0, "committed"); Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var conflict = await Write(0, "lost-update"); Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        fixture.Restart();
        var recovered = await client.GetFromJsonAsync<JsonElement>(url);
        Assert.Equal(1, recovered.GetProperty("revision").GetInt32()); Assert.Equal("committed", recovered.GetProperty("data").GetProperty("value").GetString());
    }
    [Fact]
    public void CustomerIdentitySessionsAndReceiptSurviveEngineRestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); var store = new CustomerStore(directory);
        var user = store.Register("restart@example.test", "durable test password 123!");
        Assert.True(store.ConsumeToken(store.CreateToken(user, "verify", TimeSpan.FromHours(1)), "verify")); user = store.FindUser(user.Id)!;
        var token = store.StartSession(user);
        var grant = new CustomerStore.PaidGrant("in_restart", "evt_restart", "cus_restart", "sub_restart", user.Email, "PayAsYouGo", DateTimeOffset.UtcNow.AddDays(30), "active");
        var license = store.CommitPaid(grant, () => "test-only-key").LicenseKey;
        fixture.Restart(); var reopened = new CustomerStore(directory);
        Assert.True(reopened.ValidSession(user.Id, token)); Assert.Equal(user.Id, reopened.Login(user.Email, "durable test password 123!")!.Id);
        Assert.Equal(license, reopened.CommitPaid(grant, () => throw new Exception("Must not reissue")).LicenseKey);
    }
}
