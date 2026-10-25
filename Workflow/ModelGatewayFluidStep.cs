using System.Diagnostics;
using wwwmmm.FluidEngine.Core.Contracts;
using wwwmmm.FluidEngine.Core.Context;

namespace wwwmmm.Workflow;

/// <summary>
/// Mbështjell ModelGateway ekzistues si një hap i FluidEngine.
/// NUK krijon motor të ri, thjesht e lidh "trurin" me pipeline-et.
/// </summary>
public sealed class ModelGatewayFluidStep : IFluidStep<string, string>
{
    private readonly ModelGateway _gateway;
    private readonly string _routeId;

    public ModelGatewayFluidStep(ModelGateway gateway, string routeId = "local")
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _routeId = routeId;
    }

    public string Name => "ModelGateway/AI";

    public async Task<string> ExecuteAsync(string input, PipelineContext context, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var permissions = new HashSet<string> { "model.invoke" };
            var messages = new[] { new ConversationMessage("user", input) };
            var (text, identity) = await _gateway.ChatAsync(_routeId, messages, permissions, ct);
            
            sw.Stop();
            context.RecordStep(Name, $"LLM:{identity.Model}", sw.Elapsed.TotalMilliseconds, true);
            return text;
        }
        catch (Exception ex)
        {
            sw.Stop();
            context.RecordStep(Name, "LLM:error", sw.Elapsed.TotalMilliseconds, false);
            throw new InvalidOperationException($"AI failed: {ex.Message}", ex);
        }
    }
}
