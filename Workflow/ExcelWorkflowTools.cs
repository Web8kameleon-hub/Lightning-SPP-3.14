using wwwmmm.Integrations;

namespace wwwmmm.Workflow;

internal static class ExcelWorkflowTools
{
    internal static IEnumerable<IWorkflowTool> FromEnvironment() => Create(
        ReadConnection("SPP_EXCEL_CORE_URL", "SPP_EXCEL_CORE_API_KEY"),
        ReadConnection("SPP_EXCEL_SERVICE_URL", "SPP_EXCEL_SERVICE_API_KEY"));

    internal static IEnumerable<IWorkflowTool> Create(
        BridgeConnection? coreConnection,
        BridgeConnection? serviceConnection,
        Func<BridgeConnection, ClisonixExcelCoreBridge>? createCore = null,
        Func<BridgeConnection, ClisonixExcelBridge>? createService = null)
    {
        var tools = new List<IWorkflowTool>();
        if (serviceConnection is not null)
        {
            var permissions = RequiredPermissions(serviceConnection);
            tools.Add(new DelegateTool(
                new("excel.validate", "Validate an imported .xlsx workbook with the configured Clisonix Excel service.", permissions, ["path"], [], Dependency: "Clisonix Excel service"),
                async (arguments, context, token) =>
                {
                    var input = await ReadWorkbookAsync(arguments, context, token);
                    using var bridge = (createService ?? (connection => new ClisonixExcelBridge(connection)))(serviceConnection);
                    var result = await bridge.ValidateAsync(input.Filename, input.Bytes, token);
                    return new ToolResult(result, [input.Source], "UPSTREAM_RESPONSE_REQUIRES_REVIEW");
                }));
            tools.Add(new DelegateTool(
                new("excel.parse", "Parse an imported .xlsx workbook with the configured Clisonix Excel service; upstream previews are limited to 100 rows.", permissions, ["path"], [], Dependency: "Clisonix Excel service"),
                async (arguments, context, token) =>
                {
                    var input = await ReadWorkbookAsync(arguments, context, token);
                    using var bridge = (createService ?? (connection => new ClisonixExcelBridge(connection)))(serviceConnection);
                    var result = await bridge.ParseAsync(input.Filename, input.Bytes, token);
                    return new ToolResult(result, [input.Source], "UPSTREAM_RESPONSE_REQUIRES_REVIEW");
                }));
        }

        if (coreConnection is not null)
        {
            var permissions = RequiredPermissions(coreConnection);
            tools.Add(new DelegateTool(
                new("excel.core.export-lagter", "Download the actual LAGTER .xlsx workbook from the configured Clisonix Excel Core.", permissions, [], [], Dependency: "Clisonix Excel Core"),
                async (_, context, token) =>
                {
                    using var bridge = (createCore ?? (connection => new ClisonixExcelCoreBridge(connection)))(coreConnection);
                    var bytes = await bridge.ExportLagterAsync(token);
                    var hash = WorkflowJson.Hash(bytes);
                    var relative = $"reports/{context.JobId}-{context.StepId}.xlsx";
                    var path = context.Workspace.Resolve(relative, mustExist: false);
                    if (File.Exists(path))
                    {
                        if (WorkflowJson.Hash(await File.ReadAllBytesAsync(path, token)) != hash)
                            throw new IOException("Existing Excel export differs; refusing to overwrite it.");
                    }
                    else
                    {
                        await WorkspaceScope.AtomicWriteAsync(path, bytes, token);
                    }
                    return new ToolResult(WorkflowJson.Value(new { path = relative, bytes = bytes.Length, sha256 = hash, format = "xlsx" }),
                        [new SourceReference("service:clisonix-excel-core", hash)], "UPSTREAM_RESPONSE_REQUIRES_REVIEW");
                }));
        }

        return tools;
    }

    private static string[] RequiredPermissions(BridgeConnection connection) => ["services.read"];

    private static async Task<(string Filename, byte[] Bytes, SourceReference Source)> ReadWorkbookAsync(
        System.Text.Json.JsonElement arguments, ToolContext context, CancellationToken token)
    {
        var input = await context.Workspace.ReadAsync(WorkflowJson.Text(arguments, "path"), token);
        var filename = Path.GetFileName(input.Source.Path);
        if (!filename.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Clisonix Excel tools require an imported .xlsx workbook.");
        return (filename, input.Bytes, input.Source);
    }

    private static BridgeConnection? ReadConnection(string urlName, string keyName)
    {
        var value = Environment.GetEnvironmentVariable(urlName);
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http") ||
            (uri.Scheme == "http" && !uri.IsLoopback) || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new ArgumentException($"{urlName} must be HTTPS, or loopback HTTP, without credentials, query or fragment.");
        if (!uri.IsLoopback && Environment.GetEnvironmentVariable("LSPP_WORKFLOW_ALLOW_REMOTE_SERVICES") != "true") return null;
        return new BridgeConnection(uri, Environment.GetEnvironmentVariable(keyName));
    }
}
