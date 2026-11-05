using System.Text.Json;
using wwwmmm.FluidEngine.Core.Context;
using wwwmmm.FluidEngine.Core.Data;
using wwwmmm.FluidEngine.Core.Registry;

// Hosts only pipelines that are wired here; the registry lists all known ones.
// Binds to loopback by default (see openapi.fluid.json: http://localhost:13080).
var builder = WebApplication.CreateBuilder(args);
if (builder.Configuration["urls"] is null && Environment.GetEnvironmentVariable("ASPNETCORE_URLS") is null)
    builder.WebHost.UseUrls("http://127.0.0.1:13080");
var app = builder.Build();

var hosted = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "JsonDataStep" };

app.MapGet("/api/fluid/pipelines", () => Results.Ok(
    PipelineRegistry.All.Select(p => new { p.Name, p.Category, p.InputType, p.OutputType, hosted = hosted.Contains(p.Name) })));

app.MapPost("/api/fluid/execute", async (JsonElement body, CancellationToken ct) =>
{
    var name = body.TryGetProperty("pipelineName", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
    var input = body.TryGetProperty("inputData", out var i) && i.ValueKind == JsonValueKind.String ? i.GetString() : null;
    if (name is null || input is null)
        return Results.BadRequest(new { error = "pipelineName and inputData (strings) are required" });
    if (!hosted.Contains(name))
        return Results.BadRequest(new { error = "pipeline_not_hosted", pipelineName = name });

    var ctx = new PipelineContext();
    try
    {
        var output = await new JsonDataStep(false).ExecuteAsync(input, ctx, ct);
        return Results.Ok(new { pipelineName = "JsonDataStep", output, traceId = ctx.TraceId, steps = ctx.StepTraces });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message, traceId = ctx.TraceId });
    }
});

app.Run();
