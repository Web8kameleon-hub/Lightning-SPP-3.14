using System.Text.Json;
using wwwmmm.FluidEngine.Core.Contracts;
using wwwmmm.FluidEngine.Core.Context;

namespace wwwmmm.FluidEngine.Core.Data;

/// <summary>
/// Validon JSON dhe e kthen si string të formatuar (pretty-print) ose minified.
/// Nëse JSON është i pavlefshëm, hedh exception (Never Fake).
/// </summary>
public sealed class JsonDataStep : IFluidStep<string, string>
{
    private readonly bool _prettyPrint;

    public JsonDataStep(bool prettyPrint = true) => _prettyPrint = prettyPrint;
    public string Name => "DataPipeline/JsonValidate";

    public Task<string> ExecuteAsync(string input, PipelineContext context, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(input);
            var options = new JsonSerializerOptions { WriteIndented = _prettyPrint };
            var result = JsonSerializer.Serialize(doc.RootElement, options);
            
            context.RecordStep(Name, "System.Text.Json", 0, true);
            return Task.FromResult(result);
        }
        catch (JsonException ex)
        {
            context.RecordStep(Name, "System.Text.Json", 0, false);
            throw new InvalidOperationException("Invalid JSON payload.", ex);
        }
    }
}
