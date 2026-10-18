using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace wwwmmm.Workflow;

public sealed class WorkflowRunner(ToolRegistry registry)
{
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> Active = new();
    private static readonly SemaphoreSlim Workers = new(Math.Clamp(int.TryParse(Environment.GetEnvironmentVariable("LSPP_WORKFLOW_WORKERS"), out var workers) ? workers : 2, 1, 32));
    internal static string RuntimeHash(ToolRegistry registry) => WorkflowJson.Hash(new
    { contracts = registry.Contracts, assembly = WorkflowJson.Hash(File.ReadAllBytes(typeof(WorkflowRunner).Assembly.Location)), runtime = Environment.Version.ToString() });
    private static string JobPath(WorkspaceScope workspace, string id)
    { if (!Guid.TryParseExact(id, "N", out _)) throw new ArgumentException("Invalid job ID."); return Path.Combine(workspace.StateDirectory, "jobs", id + ".json"); }
    public async Task<WorkflowJob> CreateAsync(WorkspaceScope workspace, string owner, WorkflowPlan plan, IReadOnlySet<string> permissions, CancellationToken token)
    {
        _ = registry.Validate(plan, permissions);
        var job = new WorkflowJob(Guid.NewGuid().ToString("N"), owner, plan, WorkflowJson.Hash(plan), RuntimeHash(registry), "READY",
            plan.Steps.Select(s => new StepRecord(s.Id, "PENDING")).ToArray(), DateTimeOffset.UtcNow);
        await Save(workspace, job, token); return job;
    }
    public async Task<WorkflowJob> GetAsync(WorkspaceScope workspace, string owner, string id, CancellationToken token)
    {
        var path = JobPath(workspace, id);
        if (!File.Exists(path)) throw new FileNotFoundException("Job not found.");
        var job = JsonSerializer.Deserialize<WorkflowJob>(await File.ReadAllBytesAsync(path, token), WorkflowJson.Options) ?? throw new IOException("Job snapshot invalid.");
        if (job.Owner != owner) throw new UnauthorizedAccessException("Job belongs to another owner.");
        if (job.PlanHash != WorkflowJson.Hash(job.Plan)) throw new IOException("Persisted plan integrity failure.");
        if (job.Steps.Any(s => s.Result is not null && s.ResultHash != WorkflowJson.Hash(s.Result))) throw new IOException("Persisted result integrity failure.");
        return job;
    }
    public bool Cancel(WorkspaceScope workspace, string id)
    {
        var path = JobPath(workspace, id);
        if (!Active.TryGetValue(path, out var cancellation)) return false;
        cancellation.Cancel(); return true;
    }
    public async Task<WorkflowJob> RunAsync(WorkspaceScope workspace, string owner, string id, IReadOnlySet<string> permissions, CancellationToken token, Action? validateAccess = null)
    {
        var job = await GetAsync(workspace, owner, id, token); var ordered = registry.Validate(job.Plan, permissions);
        if (job.RuntimeHash != RuntimeHash(registry)) throw new InvalidOperationException("Runtime changed. Review a new plan before execution.");
        if (job.State == "SUCCEEDED") return job;
        if (!await Workers.WaitAsync(0, token)) throw new InvalidOperationException("Workflow capacity busy. Retry the persisted job later.");
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        var key = JobPath(workspace, id);
        if (!Active.TryAdd(key, cancellation)) { Workers.Release(); throw new InvalidOperationException("Job is already running."); }
        try
        {
            // Cross-process ownership: only one worker can execute this job snapshot.
            await using var lease = new FileStream(key + ".lease", FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
            job = await GetAsync(workspace, owner, id, cancellation.Token);
            var records = job.Steps.ToDictionary(s => s.Id, StringComparer.Ordinal);
            foreach (var step in ordered)
            {
                if (records[step.Id].State == "SUCCEEDED") continue;
                if ((records[step.Id].State is "RUNNING" or "REVIEW_REQUIRED") && !registry.Get(step.Tool).Contract.ReplaySafe)
                { records[step.Id] = new(step.Id, "REVIEW_REQUIRED", Error: "Interrupted external operation must be reconciled before retry."); continue; }
                if (step.DependsOn.Any(d => records[d].State != "SUCCEEDED"))
                { records[step.Id] = new(step.Id, "BLOCKED", Error: "A dependency did not succeed."); continue; }
                cancellation.Token.ThrowIfCancellationRequested();
                records[step.Id] = new(step.Id, "RUNNING");
                job = job with { State = "RUNNING", Steps = job.Plan.Steps.Select(s => records[s.Id]).ToArray(), Updated = DateTimeOffset.UtcNow };
                await Save(workspace, job, cancellation.Token);
                try
                {
                    validateAccess?.Invoke();
                    var arguments = Resolve(step.Arguments, step.DependsOn, records);
                    var sources = step.DependsOn.SelectMany(d => records[d].Result!.Sources).Distinct().ToArray();
                    var result = await registry.Get(step.Tool).ExecuteAsync(arguments, new(workspace, id, step.Id, permissions, sources, owner), cancellation.Token);
                    if (System.Text.Encoding.UTF8.GetByteCount(result.Data.GetRawText()) + records.Values.Where(r => r.Result is not null).Sum(r => (long)System.Text.Encoding.UTF8.GetByteCount(r.Result!.Data.GetRawText())) > 16 * 1024 * 1024)
                        throw new IOException("Job evidence budget exceeded; split the work into smaller checkpointed batches.");
                    records[step.Id] = new(step.Id, "SUCCEEDED", result, ResultHash: WorkflowJson.Hash(result));
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { records[step.Id] = new(step.Id, "CANCELED"); throw; }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                { records[step.Id] = new(step.Id, registry.Get(step.Tool).Contract.ReplaySafe ? "FAILED" : "REVIEW_REQUIRED", Error: ex.GetType().Name + ": operation did not complete; reconcile any external side effect before retry."); }
                job = job with { Steps = job.Plan.Steps.Select(s => records[s.Id]).ToArray(), Updated = DateTimeOffset.UtcNow };
                await Save(workspace, job, CancellationToken.None);
            }
            job = job with { State = records.Values.All(r => r.State == "SUCCEEDED") ? "SUCCEEDED" : "NEEDS_REVIEW", Steps = job.Plan.Steps.Select(s => records[s.Id]).ToArray(), Updated = DateTimeOffset.UtcNow };
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        { job = job with { State = "CANCELED", Steps = job.Steps.Select(s => s.State == "RUNNING" ? s with { State = registry.Get(job.Plan.Steps.Single(p => p.Id == s.Id).Tool).Contract.ReplaySafe ? "CANCELED" : "REVIEW_REQUIRED", Error = "Interrupted operation; reconcile external side effects before retry." } : s).ToArray(), Updated = DateTimeOffset.UtcNow }; }
        finally { Active.TryRemove(key, out _); Workers.Release(); }
        await Save(workspace, job, CancellationToken.None); return job;
    }
    private static Task Save(WorkspaceScope workspace, WorkflowJob job, CancellationToken token) => workspace.SaveJsonAsync("jobs", job.Id, job, token);
    private static JsonElement Resolve(JsonElement arguments, string[] dependencies, Dictionary<string, StepRecord> records)
    {
        JsonNode? Walk(JsonNode? node)
        {
            if (node is JsonObject obj && obj.ContainsKey("$step"))
            {
                if (obj.Count != 2 || !obj.ContainsKey("pointer")) throw new ArgumentException("Result reference requires $step and pointer.");
                var id = obj["$step"]!.GetValue<string>();
                if (!dependencies.Contains(id) || records[id].Result is null) throw new ArgumentException("Result references require a successful declared dependency.");
                var value = JsonNode.Parse(records[id].Result!.Data.GetRawText());
                var pointer = obj["pointer"]!.GetValue<string>();
                if (pointer.Length > 0 && !pointer.StartsWith('/')) throw new ArgumentException("JSON pointer required.");
                foreach (var part in pointer.Split('/').Skip(1))
                {
                    var name = part.Replace("~1", "/").Replace("~0", "~");
                    value = value is JsonArray array ? array[int.Parse(name, System.Globalization.CultureInfo.InvariantCulture)] : value?[name];
                    if (value is null) throw new ArgumentException("Result pointer not found.");
                }
                return value?.DeepClone();
            }
            if (node is JsonObject map) { foreach (var key in map.Select(p => p.Key).ToArray()) map[key] = Walk(map[key]); }
            else if (node is JsonArray list) { for (var i = 0; i < list.Count; i++) list[i] = Walk(list[i]); }
            return node;
        }
        return JsonSerializer.SerializeToElement(Walk(JsonNode.Parse(arguments.GetRawText())), WorkflowJson.Options);
    }
}
