using System.Text;
using System.Text.Json;
using wwwmmm.Workflow;
using wwwmmm.Web;
using Xunit;

namespace LightningSPP.Tests;

public sealed class WorkflowRecoveryTests
{
    private static WorkspaceScope Scope() => new(Path.Combine(Path.GetTempPath(), "lightning-recovery-" + Guid.NewGuid().ToString("N")));
    private static HashSet<string> Permissions => new(["files.read", "reports.write"]);
    private static WorkflowPlan Plan(string tool, object arguments) => new("Recovery test", [new("step",tool,JsonSerializer.SerializeToElement(arguments),[])]);
    [Fact]
    public async Task CancellationIsDurableAndReplaySafeWorkCanResume()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var calls = 0;
        var tool = new DelegateTool(new("test.read", "Test-only cancellation fixture", [], [], []), async (_, _, token) =>
        { if (++calls == 1) { started.SetResult(); await Task.Delay(Timeout.Infinite, token); } return new(WorkflowJson.Value(new {read=true}),[]); });
        var runner = new WorkflowRunner(new([tool])); var scope = Scope(); var job = await runner.CreateAsync(scope,"owner",Plan("test.read",new {}),Permissions,default);
        var run = runner.RunAsync(scope,"owner",job.Id,Permissions,default); await started.Task;
        Assert.True(runner.Cancel(scope,job.Id)); var canceled = await run;
        Assert.Equal("CANCELED",canceled.State); Assert.Equal("CANCELED",canceled.Steps[0].State);
        Assert.Equal("SUCCEEDED",(await runner.RunAsync(scope,"owner",job.Id,Permissions,default)).State);
    }
    [Fact]
    public async Task UncertainExternalOperationIsNeverAutomaticallyRepeated()
    {
        var calls = 0;
        var tool = new DelegateTool(new("test.external", "Test-only side effect fixture", [], [], [], ReplaySafe:false), (_,_,_)=>{ calls++; throw new IOException("Test-only uncertain submission"); });
        var registry = new ToolRegistry([tool]); var scope = Scope(); var runner = new WorkflowRunner(registry);
        var job = await runner.CreateAsync(scope,"owner",Plan("test.external",new {}),Permissions,default);
        var failed = await runner.RunAsync(scope,"owner",job.Id,Permissions,default); Assert.Equal("REVIEW_REQUIRED",failed.Steps[0].State);
        await runner.RunAsync(scope,"owner",job.Id,Permissions,default); Assert.Equal(1,calls);
    }
    [Fact]
    public async Task AlteredPersistedResultsFailIntegrityCheck()
    {
        var scope = Scope(); var registry = WorkflowApi.Registry(new([])); var runner = new WorkflowRunner(registry);
        var saved = await runner.CreateAsync(scope,"owner",Plan("files.list",new {}),Permissions,default);
        var job = await runner.RunAsync(scope,"owner",saved.Id,Permissions,default);
        var changed = job with { Steps = [job.Steps[0] with {Result = new(WorkflowJson.Value(new {invented=true}),[])}] };
        await scope.SaveJsonAsync("jobs",job.Id,changed,default);
        await Assert.ThrowsAsync<IOException>(()=>runner.GetAsync(scope,"owner",job.Id,default));
    }
    [Fact]
    public async Task RealPdfReportIncludesSourceEvidenceAndCanBeParsedWhenPopplerIsEnabled()
    {
        var scope = Scope(); var registry = WorkflowApi.Registry(new([])); var source = new SourceReference("fixture-input.txt",new string('a',64),Line:3);
        var report = await registry.Get("reports.export").ExecuteAsync(WorkflowJson.Value(new {format="pdf",data=new {finding="actual report writer test"}}),new(scope,"pdfjob","report",Permissions,[source]),default);
        var path = report.Data.GetProperty("path").GetString()!; var bytes = await File.ReadAllBytesAsync(scope.Resolve(path));
        Assert.True(bytes.AsSpan(0,5).SequenceEqual("%PDF-"u8)); Assert.Contains("%%EOF",Encoding.ASCII.GetString(bytes)); Assert.Equal(source,Assert.Single(report.Sources));
        if(Environment.GetEnvironmentVariable("SPP_TEST_NATIVE_DOCUMENTS")=="1")
        {
            var result = await registry.Get("documents.pdf-text").ExecuteAsync(WorkflowJson.Value(new {path}),new(scope,"pdfjob","extract",Permissions),default);
            Assert.Contains("actual report writer test",result.Data.GetRawText()); Assert.False(result.Data.GetProperty("requiresOcr").GetBoolean());
        }
    }
    [Fact]
    public async Task CsvPreservesQuotedFieldsAndRejectsMalformedRows()
    {
        var scope = Scope(); var registry = WorkflowApi.Registry(new([]));
        var path = await scope.ImportAsync("bank.csv",Encoding.UTF8.GetBytes("id;description;amount\r\np1;\"quoted;description\";19.99\r\n"),default);
        var result = await registry.Get("tables.read-csv").ExecuteAsync(WorkflowJson.Value(new {path,delimiter=";"}),new(scope,"csvjob","csv",Permissions),default);
        Assert.Equal("quoted;description",result.Data.GetProperty("rows")[0].GetProperty("description").GetString());
        Assert.Contains("RAW_FIELDS",result.Data.GetProperty("interpretation").GetString());
        var malformed = await scope.ImportAsync("bad.csv",Encoding.UTF8.GetBytes("a,b\n1,2,3"),default);
        await Assert.ThrowsAsync<ArgumentException>(()=>registry.Get("tables.read-csv").ExecuteAsync(WorkflowJson.Value(new {path=malformed,delimiter=","}),new(scope,"csvjob","csv",Permissions),default));
    }
    [Theory]
    [InlineData(false,"REVISION_MISMATCH")]
    [InlineData(true,"DOCUMENTED_PASS_REQUIRES_REVIEW")]
    public async Task RequirementsTraceChecksVersionsAndReadsActualEvidence(bool current,string expected)
    {
        var scope = Scope(); var evidence = await scope.ImportAsync("test-result.txt",Encoding.UTF8.GetBytes("Test-only evidence"),default);
        var input = new {requirements=new[]{new{id="r1",revision="2"}},tests=new[]{new{id="t1",requirementId="r1",requirementRevision=current?"2":"1",passed=true,sourcePath=evidence}}};
        var path = await scope.ImportAsync("trace.json",JsonSerializer.SerializeToUtf8Bytes(input),default);
        var result = await WorkflowApi.Registry(new([])).Get("requirements.trace").ExecuteAsync(WorkflowJson.Value(new{path}),new(scope,"tracejob","trace",Permissions),default);
        Assert.Equal(expected,result.Data.GetProperty("findings")[0].GetProperty("state").GetString()); Assert.False(result.Data.GetProperty("physicalInspectionPerformed").GetBoolean());
    }
}
