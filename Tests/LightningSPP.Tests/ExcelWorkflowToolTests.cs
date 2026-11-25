using System.Net;
using System.Net.Http.Headers;
using System.Text;
using wwwmmm.Integrations;
using wwwmmm.Workflow;
using Xunit;

namespace LightningSPP.Tests;

[CollectionDefinition("Excel tool environment", DisableParallelization = true)]
public sealed class ExcelToolEnvironmentCollection;

[Collection("Excel tool environment")]
public sealed class ExcelWorkflowToolTests
{
    [Fact]
    public void ConfiguredBridgesAreRegisteredOnlyFromOperatorUrls()
    {
        using var coreUrl = new EnvironmentScope("SPP_EXCEL_CORE_URL", "http://127.0.0.1:4010/");
        using var serviceUrl = new EnvironmentScope("SPP_EXCEL_SERVICE_URL", "http://127.0.0.1:8002/");
        using var adapters = new EnvironmentScope("LSPP_TOOL_ADAPTERS_FILE", null);

        var ids = ServiceAdapters.FromEnvironment().Select(tool => tool.Contract.Id).ToArray();

        Assert.Contains("excel.core.export-lagter", ids);
        Assert.Contains("excel.parse", ids);
        Assert.Contains("excel.validate", ids);
    }

    [Fact]
    public void RemoteBridgesRequireAnExplicitServerOperatorGate()
    {
        using var coreUrl = new EnvironmentScope("SPP_EXCEL_CORE_URL", "https://core.example.test/");
        using var serviceUrl = new EnvironmentScope("SPP_EXCEL_SERVICE_URL", "https://excel.example.test/");
        using var remoteGate = new EnvironmentScope("LSPP_WORKFLOW_ALLOW_REMOTE_SERVICES", null);
        using var adapters = new EnvironmentScope("LSPP_TOOL_ADAPTERS_FILE", null);

        Assert.Empty(ServiceAdapters.FromEnvironment());
        using (new EnvironmentScope("LSPP_WORKFLOW_ALLOW_REMOTE_SERVICES", "true"))
        {
            var ids = ServiceAdapters.FromEnvironment().Select(tool => tool.Contract.Id).ToArray();
            Assert.Contains("excel.core.export-lagter", ids);
            Assert.Contains("excel.parse", ids);
            Assert.Contains("excel.validate", ids);
        }
    }

    [Fact]
    public async Task ParseUsesConfiguredExcelServiceAndRequiresServiceConsent()
    {
        var workspacePath = Path.Combine(Path.GetTempPath(), "spp-excel-tools-" + Guid.NewGuid().ToString("N"));
        var workspace = new WorkspaceScope(workspacePath);
        var workbook = ExcelTestWorkbook.Create(["Name"], ["fixture"]);
        try
        {
            var path = await workspace.ImportAsync("fixture.xlsx", workbook, default);
            var handler = new JsonHandler("""{"filename":"fixture.xlsx","sheet":"Sheet1","headers":["Name"],"rows":1,"data":[{"Name":"fixture"}]}""");
            var connection = new BridgeConnection(new Uri("https://excel.example.test/prefix/"), "fixture-api-token");
            var tool = ExcelWorkflowTools.Create(null, connection,
                createService: configured => new ClisonixExcelBridge(configured, handler))
                .Single(item => item.Contract.Id == "excel.parse");
            var registry = new ToolRegistry([tool]);
            var arguments = WorkflowJson.Value(new { path });
            var plan = new WorkflowPlan("Parse workbook", [new("parse", "excel.parse", arguments, [])]);
            Assert.Throws<UnauthorizedAccessException>(() => registry.Validate(plan, new HashSet<string>()));

            var permissions = new HashSet<string>(["services.read"]);
            _ = registry.Validate(plan, permissions);
            var result = await tool.ExecuteAsync(arguments, new(workspace, "job", "parse", permissions), default);

            Assert.Equal("fixture", result.Data.GetProperty("data")[0].GetProperty("Name").GetString());
            Assert.Equal("UPSTREAM_RESPONSE_REQUIRES_REVIEW", result.Verification);
            Assert.Equal(path, Assert.Single(result.Sources).Path);
            Assert.Equal(HttpMethod.Post, handler.Method);
            Assert.Equal("/prefix/api/excel/parse", handler.RequestPath);
            Assert.True(handler.SentBearerCredential);
        }
        finally
        {
            if (Directory.Exists(workspacePath)) Directory.Delete(workspacePath, true);
        }
    }

    [Fact]
    public async Task CoreExportIsSavedAsAnEvidenceBackedWorkspaceReport()
    {
        var workspacePath = Path.Combine(Path.GetTempPath(), "spp-excel-core-" + Guid.NewGuid().ToString("N"));
        var workspace = new WorkspaceScope(workspacePath);
        var workbook = ExcelTestWorkbook.Create(["Name"], ["fixture"]);
        try
        {
            var handler = new WorkbookHandler(workbook);
            var connection = new BridgeConnection(new Uri("https://core.example.test/prefix/"));
            var tool = Assert.Single(ExcelWorkflowTools.Create(connection, null,
                createCore: configured => new ClisonixExcelCoreBridge(configured, handler)));
            var permissions = new HashSet<string>(["services.read", "network.remote"]);
            var result = await tool.ExecuteAsync(WorkflowJson.Value(new { }), new(workspace, "job", "export", permissions), default);
            var relative = result.Data.GetProperty("path").GetString()!;

            Assert.StartsWith("reports/", relative, StringComparison.Ordinal);
            Assert.Equal(workbook, await File.ReadAllBytesAsync(workspace.Resolve(relative)));
            Assert.Equal(WorkflowJson.Hash(workbook), Assert.Single(result.Sources).Sha256);
            Assert.Equal("UPSTREAM_RESPONSE_REQUIRES_REVIEW", result.Verification);
            Assert.Equal(HttpMethod.Get, handler.Method);
            Assert.Equal("/prefix/api/lagter/v1/export", handler.RequestPath);
        }
        finally
        {
            if (Directory.Exists(workspacePath)) Directory.Delete(workspacePath, true);
        }
    }

    private sealed class EnvironmentScope : IDisposable
    {
        private readonly string name;
        private readonly string? previous;
        internal EnvironmentScope(string name, string? value)
        {
            this.name = name;
            previous = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }
        public void Dispose() => Environment.SetEnvironmentVariable(name, previous);
    }

    private sealed class JsonHandler(string json) : HttpMessageHandler
    {
        internal HttpMethod? Method { get; private set; }
        internal string? RequestPath { get; private set; }
        internal bool SentBearerCredential { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Method = request.Method;
            RequestPath = request.RequestUri!.AbsolutePath;
            SentBearerCredential = request.Headers.Authorization?.Scheme == "Bearer";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class WorkbookHandler(byte[] workbook) : HttpMessageHandler
    {
        internal HttpMethod? Method { get; private set; }
        internal string? RequestPath { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Method = request.Method;
            RequestPath = request.RequestUri!.AbsolutePath;
            var content = new ByteArrayContent(workbook);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
}
