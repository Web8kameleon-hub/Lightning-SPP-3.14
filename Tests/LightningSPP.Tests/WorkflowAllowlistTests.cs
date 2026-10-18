using wwwmmm.Web;
using Xunit;

namespace LightningSPP.Tests;

public sealed class WorkflowAllowlistTests
{
    private static CustomerStore.User User(string email, bool verified = true) =>
        new(Guid.NewGuid().ToString("N"), CustomerStore.NormalizeEmail(email), "hash", verified, 1, "active");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ProductionWithoutAnExplicitListDeniesEveryone(string? list) =>
        Assert.False(WorkflowApi.WorkspaceAllowed(User("owner@example.test"), list, production: true));

    [Fact]
    public void ProductionAllowsOnlyListedVerifiedEmailsCaseInsensitively()
    {
        const string list = "Owner@Example.test, second@example.test";
        Assert.True(WorkflowApi.WorkspaceAllowed(User("owner@example.test"), list, production: true));
        Assert.True(WorkflowApi.WorkspaceAllowed(User("SECOND@example.test"), list, production: true));
        Assert.False(WorkflowApi.WorkspaceAllowed(User("stranger@example.test"), list, production: true));
        Assert.False(WorkflowApi.WorkspaceAllowed(User("owner@example.test", verified: false), list, production: true));
        Assert.False(WorkflowApi.WorkspaceAllowed(null, list, production: true));
    }

    [Fact]
    public void WildcardOpensToVerifiedAccountsAndMalformedEntriesNeverMatch()
    {
        Assert.True(WorkflowApi.WorkspaceAllowed(User("anyone@example.test"), "*", production: true));
        Assert.False(WorkflowApi.WorkspaceAllowed(User("anyone@example.test", verified: false), "*", production: true));
        Assert.False(WorkflowApi.WorkspaceAllowed(User("owner@example.test"), "not-an-email, owner@", production: true));
    }

    [Fact]
    public void DevelopmentWithoutAListKeepsSignedInAccess() =>
        Assert.True(WorkflowApi.WorkspaceAllowed(User("dev@example.test"), null, production: false));
}

public sealed class AssistantScreenContextTests
{
    private static System.Text.Json.JsonElement Json(string raw) => System.Text.Json.JsonDocument.Parse(raw).RootElement;

    [Fact]
    public void SharedScreenIsLabelledUntrustedAndBounded()
    {
        var text = new string('x', 20000);
        var value = System.Text.Json.JsonSerializer.SerializeToElement(WorkflowApi.ScreenContext(Json("{\"page\":\"/try\",\"title\":\"Try\",\"text\":\"" + text + "\"}")));
        Assert.Equal("visible-page:/try", value.GetProperty("source").GetString());
        Assert.Equal("UNTRUSTED_USER_SCREEN_DATA", value.GetProperty("trust").GetString());
        Assert.Equal(12000, value.GetProperty("text").GetString()!.Length);
    }

    [Theory]
    [InlineData("{\"page\":\"https://evil.example/\",\"text\":\"a\"}")]
    [InlineData("{\"page\":\"//evil.example\",\"text\":\"a\"}")]
    [InlineData("{\"page\":\"/t\\u0007ry\",\"text\":\"a\"}")]
    [InlineData("{\"page\":\"/try\",\"text\":\"   \"}")]
    public void ForeignPagesAndEmptyScreensAreRejected(string raw) =>
        Assert.Throws<ArgumentException>(() => WorkflowApi.ScreenContext(Json(raw)));
}
