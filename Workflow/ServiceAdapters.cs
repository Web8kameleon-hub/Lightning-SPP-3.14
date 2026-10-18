using System.Net.Http.Headers;
using System.Text.Json;

namespace wwwmmm.Workflow;

public sealed record ReadOnlyServiceAdapter(string Id, string Description, string Endpoint, string Path,
    string[] Arguments, string? ApiKeyEnvironment = null, bool Remote = true);

/// <summary>Operator-configured read-only HTTP tools. Users and model output cannot select arbitrary hosts or credential names.</summary>
internal static class ServiceAdapters
{
    internal static IEnumerable<IWorkflowTool> FromEnvironment()
    {
        var file = Environment.GetEnvironmentVariable("LSPP_TOOL_ADAPTERS_FILE");
        if (string.IsNullOrWhiteSpace(file)) return [];
        if (!Path.IsPathFullyQualified(file) || new FileInfo(file).Length > 1024 * 1024) throw new ArgumentException("Bounded operator adapter configuration required.");
        return Create(JsonSerializer.Deserialize<ReadOnlyServiceAdapter[]>(File.ReadAllBytes(file), WorkflowJson.Options) ?? throw new ArgumentException("Adapter configuration array required."));
    }
    internal static IEnumerable<IWorkflowTool> Create(IEnumerable<ReadOnlyServiceAdapter> adapters, HttpClient? transport = null)
    {
        var client = transport ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(30) };
        foreach (var adapter in adapters)
        {
            if (!Uri.TryCreate(adapter.Endpoint, UriKind.Absolute, out var root) || root.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(root.UserInfo) || !string.IsNullOrEmpty(root.Query) || !string.IsNullOrEmpty(root.Fragment) ||
                adapter.Path.StartsWith('/') || adapter.Path.Contains("..") || adapter.Path.Contains(':') || adapter.Path.Contains('?') || adapter.Path.Contains('#') || adapter.Arguments.Distinct().Count() != adapter.Arguments.Length)
                throw new ArgumentException("Invalid read-only service adapter configuration.");
            if (adapter.ApiKeyEnvironment is not null && root.Scheme != "https" && !root.IsLoopback) throw new ArgumentException("Authenticated remote service requires TLS.");
            yield return new DelegateTool(new(adapter.Id, adapter.Description, adapter.Remote ? ["services.read", "network.remote"] : ["services.read"], adapter.Arguments, [], Dependency: "Configured read-only HTTP service"), async (args, context, token) =>
            {
                var query = adapter.Arguments.Select(key =>
                {
                    var value = WorkflowJson.Text(args, key); if (value.Length > 1024) throw new ArgumentException("Adapter query argument exceeds budget.");
                    return Uri.EscapeDataString(key) + "=" + Uri.EscapeDataString(value);
                });
                var url = adapter.Endpoint.TrimEnd('/') + "/" + adapter.Path + (adapter.Arguments.Length > 0 ? "?" + string.Join('&', query) : "");
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (adapter.ApiKeyEnvironment is not null)
                {
                    var secret = Environment.GetEnvironmentVariable(adapter.ApiKeyEnvironment);
                    if (string.IsNullOrWhiteSpace(secret)) throw new InvalidOperationException("Service credential unavailable.");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
                }
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromSeconds(30));
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token); response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token); using var bytes = new MemoryStream(); var buffer = new byte[8192]; int read;
                while ((read = await stream.ReadAsync(buffer, deadline.Token)) != 0)
                { if (bytes.Length + read > context.Workspace.MaxFileBytes) throw new IOException("Service response exceeds resource budget."); await bytes.WriteAsync(buffer.AsMemory(0, read), deadline.Token); }
                using var json = JsonDocument.Parse(bytes.ToArray()); var hash = WorkflowJson.Hash(bytes.ToArray());
                return new ToolResult(WorkflowJson.Value(new { service = adapter.Id, retrievedUtc = DateTimeOffset.UtcNow, response = json.RootElement, responseHash = hash }), [new("service:" + adapter.Id, hash)], "UPSTREAM_RESPONSE_REQUIRES_REVIEW");
            });
        }
    }
}
