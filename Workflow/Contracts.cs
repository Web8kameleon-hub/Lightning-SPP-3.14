using System.Text.Json;
using System.Text.RegularExpressions;

namespace wwwmmm.Workflow;

public sealed record ToolContract(string Id, string Description, string[] Permissions, string[] RequiredArguments,
    string[] OptionalArguments, bool ReplaySafe = true, string? Dependency = null);
public sealed record WorkflowStep(string Id, string Tool, JsonElement Arguments, string[] DependsOn);
public sealed record WorkflowPlan(string Goal, WorkflowStep[] Steps);
public sealed record SourceReference(string Path, string Sha256, int? Line = null, int? Page = null);
public sealed record ToolResult(JsonElement Data, SourceReference[] Sources, string Verification = "MEASURED");
public sealed record StepRecord(string Id, string State, ToolResult? Result = null, string? Error = null, string? ResultHash = null);
public sealed record WorkflowJob(string Id, string Owner, WorkflowPlan Plan, string PlanHash, string RuntimeHash,
    string State, StepRecord[] Steps, DateTimeOffset Updated);
public sealed record ToolContext(WorkspaceScope Workspace, string JobId, string StepId, IReadOnlySet<string> Permissions, IReadOnlyList<SourceReference>? DependencySources = null, string? Owner = null);
public interface IWorkflowTool
{
    ToolContract Contract { get; }
    Task<ToolResult> ExecuteAsync(JsonElement arguments, ToolContext context, CancellationToken cancellationToken);
}
public sealed class ToolRegistry
{
    private readonly Dictionary<string, IWorkflowTool> tools = new(StringComparer.Ordinal);
    public IEnumerable<ToolContract> Contracts => tools.Values.Select(t => t.Contract);
    public ToolRegistry(IEnumerable<IWorkflowTool> tools)
    {
        foreach (var tool in tools)
            if (!Regex.IsMatch(tool.Contract.Id, "^[a-z][a-z0-9.-]+$", RegexOptions.CultureInvariant) || !this.tools.TryAdd(tool.Contract.Id, tool))
                throw new ArgumentException("Invalid or duplicate tool ID.");
    }
    public IWorkflowTool Get(string id) => tools.GetValueOrDefault(id) ?? throw new ArgumentException("Unknown tool: " + id);
    public WorkflowStep[] Validate(WorkflowPlan plan, IReadOnlySet<string> permissions)
    {
        if (string.IsNullOrWhiteSpace(plan.Goal) || plan.Goal.Length > 16_000 || plan.Steps is null || plan.Steps.Length is < 1 or > 256)
            throw new ArgumentException("Supply a goal and 1–256 steps per batch; split larger work into checkpointed batches.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var step in plan.Steps)
        {
            if (!Regex.IsMatch(step.Id ?? "", "^[A-Za-z0-9_-]{1,64}$") || !ids.Add(step.Id!)) throw new ArgumentException("Invalid or duplicate step ID.");
            var contract = Get(step.Tool).Contract;
            if (contract.Permissions.Any(p => !permissions.Contains(p))) throw new UnauthorizedAccessException("Tool permission denied: " + step.Tool);
            if (step.Arguments.ValueKind != JsonValueKind.Object || step.DependsOn is null) throw new ArgumentException("Arguments must be an object and dependencies an array.");
            foreach (var name in contract.RequiredArguments)
                if (!step.Arguments.TryGetProperty(name, out _)) throw new ArgumentException("Missing argument: " + name);
            if (step.Arguments.EnumerateObject().Any(p => !contract.RequiredArguments.Contains(p.Name) && !contract.OptionalArguments.Contains(p.Name))) throw new ArgumentException("Unknown tool argument.");
        }
        var ordered = new List<WorkflowStep>();
        while (ordered.Count < plan.Steps.Length)
        {
            var ready = plan.Steps.Where(s => !ordered.Any(o => o.Id == s.Id) && s.DependsOn.All(d => ordered.Any(o => o.Id == d))).ToArray();
            if (ready.Length == 0) throw new ArgumentException("Missing dependency or cyclic plan.");
            ordered.AddRange(ready);
        }
        return ordered.ToArray();
    }
}
internal static class WorkflowJson
{
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    internal static JsonElement Value<T>(T value) => JsonSerializer.SerializeToElement(value, Options);
    internal static string Text(JsonElement arguments, string key) => arguments.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
        ? value.GetString()! : throw new ArgumentException("Nonempty string required: " + key);
    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
    internal static string Hash(byte[] bytes) => Hash(bytes.AsSpan());
    internal static string Hash<T>(T value) => Hash(JsonSerializer.SerializeToUtf8Bytes(value, Options));
}
