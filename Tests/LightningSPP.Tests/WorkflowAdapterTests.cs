using System.Net;
using System.Text;
using wwwmmm.Web;
using wwwmmm.Workflow;
using Xunit;

namespace LightningSPP.Tests;

public sealed class WorkflowAdapterTests
{
    private static WorkspaceScope Scope() => new(Path.Combine(Path.GetTempPath(),"lightning-adapter-test-"+Guid.NewGuid().ToString("N")));
    [Fact]
    public async Task ConfiguredServiceUsesOnlyGetAndEscapedQueryAndRequiresPermissions()
    {
        var transport = new CaptureHandler("{\"actualTestFixture\":true}");
        var tool = Assert.Single(ServiceAdapters.Create([new("service.test", "Test-only read adapter", "https://service.example.test", "records", ["query"])],new(transport)));
        var registry = new ToolRegistry([tool]); var arguments = WorkflowJson.Value(new {query="x&other=value"});
        var plan = new WorkflowPlan("Read",[new("read",tool.Contract.Id,arguments,[])]);
        Assert.Throws<UnauthorizedAccessException>(()=>registry.Validate(plan,new HashSet<string>())); Assert.Null(transport.Request);
        var permissions = new HashSet<string>(["services.read","network.remote"]); _=registry.Validate(plan,permissions);
        var result = await tool.ExecuteAsync(arguments,new(Scope(),"test","read",permissions),default);
        Assert.Equal(HttpMethod.Get,transport.Request!.Method); Assert.Equal("?query=x%26other%3Dvalue",transport.Request.RequestUri!.Query);
        Assert.Equal("UPSTREAM_RESPONSE_REQUIRES_REVIEW",result.Verification);
    }
    [Fact]
    public async Task StripeReadIsBoundToAuthenticatedOwnerAndRejectsCrossCustomerResponse()
    {
        var old=Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY"); Environment.SetEnvironmentVariable("STRIPE_SECRET_KEY","sk_test_fixture_get_only");
        try
        {
            var handler=new CaptureHandler("{\"data\":[{\"id\":\"in_fixture\",\"customer\":\"cus_other\"}],\"has_more\":false}");
            var tool=Assert.Single(StripeWorkspaceTools.Create(new(new HttpClient(handler)),_=>"cus_owner"));
            var context=new ToolContext(Scope(),"fixture","read",new HashSet<string>(["billing.read"]),Owner:Guid.NewGuid().ToString("N"));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>tool.ExecuteAsync(WorkflowJson.Value(new{}),context,default));
            Assert.Equal(HttpMethod.Get,handler.Request!.Method); Assert.Contains("customer=cus_owner",handler.Request.RequestUri!.Query);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>tool.ExecuteAsync(WorkflowJson.Value(new{}),context with{Owner=null},default));
        }
        finally {Environment.SetEnvironmentVariable("STRIPE_SECRET_KEY",old);}
    }
    [Fact]
    public async Task StripeSnapshotKeepsMinorUnitsAndDoesNotClaimBankReconciliation()
    {
        var old=Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY"); Environment.SetEnvironmentVariable("STRIPE_SECRET_KEY","sk_test_fixture_get_only");
        try
        {
            var handler=new CaptureHandler("{\"data\":[{\"id\":\"in_fixture\",\"customer\":\"cus_owner\",\"currency\":\"eur\",\"status\":\"paid\",\"total\":2379,\"amount_paid\":2379,\"amount_remaining\":0,\"created\":1791552066}],\"has_more\":false}");
            var tool=Assert.Single(StripeWorkspaceTools.Create(new(new HttpClient(handler)),_=>"cus_owner"));
            var result=await tool.ExecuteAsync(WorkflowJson.Value(new{}),new(Scope(),"fixture","read",new HashSet<string>(["billing.read"]),Owner:Guid.NewGuid().ToString("N")),default);
            var invoice=result.Data.GetProperty("invoices")[0]; Assert.Equal(2379,invoice.GetProperty("paidMinorUnits").GetInt64()); Assert.Equal("NOT_PERFORMED",invoice.GetProperty("bankReconciliation").GetString());
        }
        finally {Environment.SetEnvironmentVariable("STRIPE_SECRET_KEY",old);}
    }
    private sealed class CaptureHandler(string json):HttpMessageHandler
    {
        internal HttpRequestMessage? Request;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {Request=request;return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(json,Encoding.UTF8,"application/json")});}
    }
}
