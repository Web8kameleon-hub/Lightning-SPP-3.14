using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using wwwmmm.Web;
using Xunit;

namespace LightningSPP.Tests;

/// <summary>Exercises schema-1 records in both historical and future normalized representations.</summary>
public sealed class EmailCompatibilityTests : IClassFixture<LongoFixture>
{
    [Theory]
    [InlineData("LEGACY+PAID@EXAMPLE.TEST", "legacy+paid@example.test")]
    [InlineData("legacy+paid@example.test", "LEGACY+PAID@EXAMPLE.TEST")]
    [InlineData("Legacy+Paid@Example.Test", "legacy+paid@example.test")]
    public async Task BothReaderVersionsFindExistingAccountsAndLinkTheirPaidLicense(string userEmail, string paidEmail)
    {
        const string password = "test-only-compatible-password-123!";
        var directory = Path.Combine(Path.GetTempPath(), "lightning-email-compat-" + Guid.NewGuid().ToString("N"));
        var store = new CustomerStore(directory);
        var user = store.Register("legacy+paid@example.test", password);
        var entitlement = store.CommitPaid(new("in_compat", "evt_compat", "cus_compat", "sub_compat", user.Email,
            "PayAsYouGo", DateTimeOffset.UtcNow.AddDays(30), "active"), () => "test-only-stable-license");
        // Seed a historical/future casing variant only in isolated Longo test state.
        var state = store.ReadState();
        state.Users[user.Id] = user with { Email = userEmail };
        state.Customers[entitlement.CustomerId] = state.Customers[entitlement.CustomerId] with { Email = paidEmail };
        using var client = new HttpClient();
        var url = Environment.GetEnvironmentVariable("LONGO_URL")!.TrimEnd('/') + "/v1/lightning/state/" + CustomerStore.Hash(store.DirectoryPath);
        var envelope = await client.GetFromJsonAsync<JsonElement>(url);
        using var request = new HttpRequestMessage(HttpMethod.Put, url) { Content = JsonContent.Create(new { data = state }, options: new JsonSerializerOptions(JsonSerializerDefaults.Web)) };
        request.Headers.TryAddWithoutValidation("If-Match", envelope.GetProperty("revision").GetInt64().ToString());
        using var saved = await client.SendAsync(request); Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var reopened = new CustomerStore(directory);
        Assert.Equal(user.Id, reopened.Register("LEGACY+PAID@EXAMPLE.TEST", "another-test-password-456!").Id);
        Assert.Single(reopened.ReadState().Users);
        var recorded = reopened.FindUser("legacy+paid@example.test", true)!;
        Assert.True(reopened.ConsumeToken(reopened.CreateToken(recorded, "verify", TimeSpan.FromHours(1)), "verify"));
        foreach (var email in new[] { "legacy+paid@example.test", "LEGACY+PAID@EXAMPLE.TEST", " Legacy+Paid@Example.Test " })
            Assert.Equal(user.Id, reopened.Login(email, password)!.Id);
        Assert.Equal(entitlement.LicenseKey, Assert.Single(reopened.Entitlements(user.Id)).LicenseKey);
        Assert.Single(reopened.ReadState().Receipts);
        Assert.True(WorkflowApi.WorkspaceAllowed(reopened.FindUser(user.Id), "LEGACY+PAID@EXAMPLE.TEST", production: true));
    }
}
