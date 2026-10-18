using System.Text.Json;
using wwwmmm.Web;

namespace wwwmmm.Workflow;

internal static class WorkflowCommands
{
    internal static async Task<int> RunAsync(string[] args, CancellationToken token)
    {
        try
        {
            var models = ModelGateway.FromEnvironment(); var registry = WorkflowApi.Registry(models);
            var permissions = new HashSet<string>(["files.read", "reports.write", "model.invoke"], StringComparer.Ordinal);
            if (args.Contains("--allow-remote")) permissions.Add("network.remote");
            if (args.Contains("--allow-print")) { permissions.Add("devices.print"); permissions.Add("devices.read"); }
            if (args.Contains("--allow-services")) permissions.Add("services.read");
            object result;
            switch (args.ElementAtOrDefault(1))
            {
                case "tools": result = new { tools = registry.Contracts, models = models.Routes }; break;
                case "discover" when args.Length >= 3: result = await models.DiscoverAsync(args[2], permissions, token); break;
                case "ask" when args.Length >= 4:
                    var reply = await models.ChatAsync(args[2], [new("user", args[3])], permissions, token);
                    result = new { reply.Text, reply.Identity, verification = "MODEL_CLAIM_REQUIRES_REVIEW" }; break;
                case "propose" when args.Length >= 5:
                    var proposedScope = new WorkspaceScope(args[2]);
                    var listing = await registry.Get("files.list").ExecuteAsync(WorkflowJson.Value(new {}), new(proposedScope,"planning","files",permissions),token);
                    var proposed = await models.ProposeAsync(args[3], args[4], registry, permissions, listing.Data.GetRawText(), token);
                    result = new { plan = proposed, state = "PROPOSED_REQUIRES_REVIEW" }; break;
                case "run" when args.Length >= 4:
                    var scope = new WorkspaceScope(args[2]);
                    if (new FileInfo(args[3]).Length > 1024 * 1024) throw new ArgumentException("Plan exceeds resource budget.");
                    var plan = JsonSerializer.Deserialize<WorkflowPlan>(await File.ReadAllBytesAsync(args[3], token), WorkflowJson.Options) ?? throw new ArgumentException("Plan required.");
                    var runner = new WorkflowRunner(registry); var job = await runner.CreateAsync(scope, "local-operator", plan, permissions, token);
                    result = await runner.RunAsync(scope, "local-operator", job.Id, permissions, token); break;
                case "resume" when args.Length >= 4:
                    result = await new WorkflowRunner(registry).RunAsync(new(args[2]), "local-operator", args[3], permissions, token); break;
                default:
                    Console.Error.WriteLine("workflow tools | discover <route> | ask <route> <prompt> | propose <root> <route> <goal> | run <root> <plan.json> | resume <root> <job-id> [--allow-remote]"); return 2;
            }
            Console.WriteLine(JsonSerializer.Serialize(result, WorkflowJson.Options));
            return result is WorkflowJob completed && completed.State != "SUCCEEDED" ? 1 : 0;
        }
        catch (OperationCanceledException) { Console.Error.WriteLine("Workflow canceled. Inspect saved job state before resuming."); return 130; }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or InvalidOperationException or HttpRequestException or JsonException)
        { Console.Error.WriteLine("Workflow unavailable: " + ex.GetType().Name + ". Check configuration and input; no success is implied."); return 1; }
    }
}
