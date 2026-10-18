using System.Net;
using System.Text;
using System.Text.Json;
using wwwmmm.Workflow;
using wwwmmm.Web;
using Xunit;

namespace LightningSPP.Tests;

public sealed class WorkflowTests
{
    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);
    private static WorkspaceScope Scope() => new(Path.Combine(Path.GetTempPath(), "lightning-workflow-" + Guid.NewGuid().ToString("N")));
    private static HashSet<string> Permissions => new(["files.read", "reports.write", "model.invoke"]);
    private static ToolRegistry Registry => WorkflowApi.Registry(new ModelGateway([]));
    private static WorkflowStep Step(string id, string tool, object args, params string[] deps) => new(id, tool, Json(args), deps);
    private static async Task<ToolResult> Tool(string tool, object args, WorkspaceScope scope) => await Registry.Get(tool).ExecuteAsync(Json(args), new(scope, "testjob", "teststep", Permissions), CancellationToken.None);

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("nested/../../outside.txt")]
    [InlineData(".env")]
    [InlineData(".git/config")]
    [InlineData("private.key")]
    [InlineData(".lightning/jobs/x.json")]
    [InlineData("file.txt:secret")]
    [InlineData("/etc/passwd")]
    public void ProtectedPathsCannotEscapeOrReadCredentials(string path) => Assert.Throws<UnauthorizedAccessException>(() => Scope().Resolve(path, false));

    [Fact]
    public async Task InputsDeduplicateByActualBytesPreserveDistinctNamesAndRejectTraversal()
    {
        var scope = Scope(); var bytes = Encoding.UTF8.GetBytes("original\nactual content");
        var first = await scope.ImportAsync("invoice.txt", bytes, default);
        Assert.Equal(first, await scope.ImportAsync("invoice.txt", bytes, default));
        var second = await scope.ImportAsync("distinct.txt", bytes, default); Assert.NotEqual(first, second);
        var input = await scope.ReadAsync(first, default); Assert.Equal(bytes, input.Bytes);
        Assert.True(File.Exists(Path.Combine(scope.Root, ".lightning", "originals", input.Source.Sha256)));
        await File.WriteAllTextAsync(scope.Resolve(first), "changed");
        Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(scope.Root, ".lightning", "originals", input.Source.Sha256)));
        await Assert.ThrowsAnyAsync<Exception>(() => scope.ImportAsync("../escape.txt", bytes, default));
    }
    [Fact]
    public void LinkedSourceCannotBeRead()
    {
        var scope = Scope(); var other = Scope(); var link = Path.Combine(scope.Root, "linked");
        // Directory junctions on Windows require no developer-mode symlink privilege.
        if (OperatingSystem.IsWindows())
        {
            var start = new System.Diagnostics.ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
            start.ArgumentList.Add("/c"); start.ArgumentList.Add("mklink"); start.ArgumentList.Add("/J"); start.ArgumentList.Add(link); start.ArgumentList.Add(other.Root);
            using var process = System.Diagnostics.Process.Start(start)!; process.WaitForExit(); Assert.Equal(0, process.ExitCode);
        }
        else Directory.CreateSymbolicLink(link, other.Root);
        Assert.Throws<UnauthorizedAccessException>(() => scope.Resolve("linked", false));
    }
    [Fact]
    public void RegistryRejectsDuplicateToolsCyclesUnknownArgumentsAndMissingPermission()
    {
        var tool = Registry.Get("files.read"); Assert.Throws<ArgumentException>(() => new ToolRegistry([tool, tool]));
        Assert.Throws<ArgumentException>(() => Registry.Validate(new("test", [Step("a", "files.read", new { path = "x" }, "b"), Step("b", "files.read", new { path = "x" }, "a")]), Permissions));
        Assert.Throws<ArgumentException>(() => Registry.Validate(new("test", [Step("a", "files.read", new { path = "x", arbitrary = true })]), Permissions));
        Assert.Throws<UnauthorizedAccessException>(() => Registry.Validate(new("test", [Step("a", "files.read", new { path = "x" })]), new HashSet<string>()));
    }
    [Fact]
    public async Task RealFileToolsReturnLineAndHashEvidenceAndPatternFindingsRemainCandidates()
    {
        var scope = Scope(); var path = await scope.ImportAsync("code.cs", Encoding.UTF8.GetBytes("// mock in test\nreturn true;\n"), default);
        var result = await Tool("files.search", new { path, query = "return" }, scope);
        Assert.Equal(2, Assert.Single(result.Sources).Line);
        var audit = await Tool("code.audit-patterns", new { path, patterns = new[] { "mock", "return true" } }, scope);
        Assert.Equal("CANDIDATE", audit.Verification); Assert.False(audit.Data.GetProperty("semanticAuditPerformed").GetBoolean());
        Assert.Equal(2, audit.Data.GetProperty("findings").GetArrayLength());
        var equal = await Tool("files.compare", new { left = path, right = path }, scope); Assert.True(equal.Data.GetProperty("equalBytes").GetBoolean());
    }
    [Fact]
    public async Task DependencyResultReferencesPersistRealReportsAndDoNotRepeatCompletedSteps()
    {
        var scope = Scope(); var path = await scope.ImportAsync("input.txt", Encoding.UTF8.GetBytes("real text"), default);
        var runner = new WorkflowRunner(Registry);
        var plan = new WorkflowPlan("Read and report", [Step("read", "files.read", new { path }), Step("report", "reports.export", new Dictionary<string, object> { ["format"] = "json", ["data"] = new Dictionary<string, string> { ["$step"] = "read", ["pointer"] = "" } }, "read")]);
        var saved = await runner.CreateAsync(scope, "owner", plan, Permissions, default);
        var job = await runner.RunAsync(scope, "owner", saved.Id, Permissions, default); Assert.Equal("SUCCEEDED", job.State);
        var report = job.Steps.Single(s => s.Id == "report").Result!.Data.GetProperty("path").GetString()!;
        Assert.Contains("real text", await File.ReadAllTextAsync(scope.Resolve(report)));
        await File.WriteAllTextAsync(scope.Resolve(path), "changed input after completion");
        var duplicate = await runner.RunAsync(scope, "owner", saved.Id, Permissions, default);
        Assert.Equal(job.PlanHash, duplicate.PlanHash); Assert.Equal("real text", duplicate.Steps[0].Result!.Data.GetProperty("text").GetString());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => runner.GetAsync(scope, "different-owner", saved.Id, default));
    }
    [Fact]
    public async Task FailedInputBlocksDependantsButIndependentStepsStillRunAndCanResume()
    {
        var scope = Scope(); var runner = new WorkflowRunner(Registry);
        var job = await runner.CreateAsync(scope, "owner", new("Failure isolation", [Step("missing", "files.read", new { path = "missing.txt" }), Step("blocked", "files.read", new { path = "missing.txt" }, "missing"), Step("list", "files.list", new { })]), Permissions, default);
        var failed = await runner.RunAsync(scope, "owner", job.Id, Permissions, default);
        Assert.Equal("FAILED", failed.Steps[0].State); Assert.Equal("BLOCKED", failed.Steps[1].State); Assert.Equal("SUCCEEDED", failed.Steps[2].State);
        await File.WriteAllTextAsync(Path.Combine(scope.Root, "missing.txt"), "now available");
        Assert.Equal("SUCCEEDED", (await runner.RunAsync(scope, "owner", job.Id, Permissions, default)).State);
    }
    [Fact]
    public async Task CsvReportsNeutralizeFormulaInjectionAndKeepOriginalInputs()
    {
        var scope = Scope(); var result = await Tool("reports.export", new { format = "csv", data = new[] { new { description = "=cmd|' /C calc'!A0", amount = "19.99" } } }, scope);
        var text = await File.ReadAllTextAsync(scope.Resolve(result.Data.GetProperty("path").GetString()!));
        Assert.Contains("'=cmd", text);
    }
    [Fact]
    public async Task DecimalReconciliationSupportsConsolidatedPartialCreditAndRefundWithoutGuessingSigns()
    {
        var scope = Scope();
        var input = new { amountConvention = "positive_amount_explicit_kind", documents = new[] {
            new { id="a",kind="invoice",total=100.10m,currency="EUR",counterparty="customer" },
            new { id="b",kind="invoice",total=50m,currency="EUR",counterparty="customer" },
            new { id="c",kind="credit",total=10m,currency="EUR",counterparty="customer" } },
            transactions = new[] { new { id="p",kind="payment",amount=120.10m,currency="EUR",counterparty="customer" }, new { id="r",kind="refund",amount=5m,currency="EUR",counterparty="customer" } },
            allocations = new[] { new {documentId="a",transactionId="p",amount=90.10m,reviewed=true}, new {documentId="b",transactionId="p",amount=30m,reviewed=true}, new {documentId="b",transactionId="r",amount=5m,reviewed=true} },
            credits = new[] {new {creditId="c",invoiceId="a",amount=10m,reviewed=true}} };
        var path = await scope.ImportAsync("ledger.json", JsonSerializer.SerializeToUtf8Bytes(input), default);
        var result = await Tool("finance.reconcile", new { path }, scope); var docs = result.Data.GetProperty("documents");
        Assert.Equal("paid", docs[0].GetProperty("state").GetString()); Assert.Equal(0m, docs[0].GetProperty("remaining").GetDecimal());
        Assert.Equal("refunded", docs[1].GetProperty("state").GetString()); Assert.Equal(25m, docs[1].GetProperty("remaining").GetDecimal());
        Assert.False(result.Data.GetProperty("bankConnectionPerformed").GetBoolean());
    }
    [Fact]
    public async Task DomainRulesRequireExplicitUnitsAndVerifiedValuesAndNeverCertify()
    {
        var scope = Scope();
        var path = await scope.ImportAsync("observations.json", JsonSerializer.SerializeToUtf8Bytes(new {subjectId="fixture-only", observations=new[] {new {id="dimension",value=12,unit="mm",verified=true},new {id="uncertain",value=9,unit="mm",verified=false}}}), default);
        var rulesPath = await scope.ImportAsync("rules.json", JsonSerializer.SerializeToUtf8Bytes(new { protocolId="customer-test-rule",version="1",reviewOwner="test-reviewer",rules=new[] {new {field="dimension",unit="mm",min=1,max=10,severity="review"},new {field="uncertain",unit="mm",min=1,max=10,severity="review"}}}), default);
        var result = await Tool("review.rules", new { path, rulesPath }, scope); Assert.Equal("REVIEW_REQUIRED", result.Verification);
        Assert.Equal("REVIEW_ALERT", result.Data.GetProperty("findings")[0].GetProperty("state").GetString());
        Assert.Equal("NEEDS_REVIEW", result.Data.GetProperty("findings")[1].GetProperty("state").GetString());
        Assert.False(result.Data.GetProperty("medicalOrEngineeringCertification").GetBoolean());
    }
    [Fact]
    public async Task EconomicChangesAreMeasuredWithoutRecessionPrediction()
    {
        var scope = Scope(); var path = await scope.ImportAsync("series.json", JsonSerializer.SerializeToUtf8Bytes(new {series=new[] {new {id="orders",unit="count",points=new[] {new {date="2026-01-01",value=100,source="fixture-record-1"},new {date="2026-02-01",value=80,source="fixture-record-2"}}}}}), default);
        var result = await Tool("economy.indicators", new {path}, scope);
        Assert.Equal(-20m, result.Data.GetProperty("series")[0].GetProperty("changes")[0].GetProperty("percent").GetDecimal());
        Assert.Equal("NOT_ESTABLISHED", result.Data.GetProperty("recessionPrediction").GetString());
    }
    [Fact]
    public async Task NoConfiguredModelFailsInsteadOfReturningSyntheticAnswer()
    {
        var gateway = new ModelGateway([]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.ChatAsync("missing", [new("user", "question")], Permissions, default));
    }
    [Fact]
    public async Task RemoteRouteNeedsExplicitPermissionAndModelIdentityMustMatch()
    {
        var handler = new ModelFixture(); var gateway = new ModelGateway([new("fixture", "https://model.example.test", "actual-test-model", Remote:true)], new HttpClient(handler));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => gateway.DiscoverAsync("fixture", Permissions, default)); Assert.Equal(0,handler.Requests);
        var allowed = Permissions; allowed.Add("network.remote");
        var identity = await gateway.DiscoverAsync("fixture",allowed,default); Assert.Equal("fixture-digest",identity.Digest);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>gateway.ChatAsync("fixture",[new("user","test")],allowed,default));
    }
    private sealed class ModelFixture : HttpMessageHandler
    {
        internal int Requests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Requests++;
            var text = request.RequestUri!.AbsolutePath switch {
                "/api/tags" => "{\"models\":[{\"name\":\"actual-test-model\",\"digest\":\"fixture-digest\"}]}",
                "/api/show" => "{\"capabilities\":[\"completion\"]}",
                _ => "{\"model\":\"wrong-model\",\"done\":true,\"message\":{\"content\":\"test fixture only\"}}" };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {Content=new StringContent(text,Encoding.UTF8,"application/json")});
        }
    }
}
