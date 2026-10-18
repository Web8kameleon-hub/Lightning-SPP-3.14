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
