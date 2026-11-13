using wwwmmm.FluidEngine.Core.Contracts;
using wwwmmm.FluidEngine.Core.Context;

namespace wwwmmm.FluidEngine.Core.Data;

/// <summary>
/// Lexon CSV, validon rreshtat dhe i konverton në formatin e brendshëm të sistemit.
/// </summary>
public sealed class CsvDataStep : IFluidStep<string, string[][]>
{
    public string Name => "DataPipeline/CsvParse";

    public Task<string[][]> ExecuteAsync(string input, PipelineContext context, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new InvalidOperationException("CSV input is empty.");

        // Parseim i thjeshtë dhe i shpejtë (pa varësi të jashtme)
        var lines = input.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var result = new string[lines.Length][];

        for (int i = 0; i < lines.Length; i++)
        {
            result[i] = lines[i].Split(',').Select(x => x.Trim()).ToArray();
            
            if (result[i].Length == 0)
                throw new InvalidOperationException($"CSV line {i + 1} is malformed.");
        }

        context.RecordStep(Name, "CsvParser", 0, true);
        return Task.FromResult(result);
    }
}
