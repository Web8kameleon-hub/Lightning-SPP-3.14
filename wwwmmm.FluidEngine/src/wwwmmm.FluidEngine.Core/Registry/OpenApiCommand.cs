using System.IO;
using wwwmmm.FluidEngine.Core.Registry;

namespace wwwmmm.FluidEngine.Core.Registry;

public static class OpenApiCommand
{
    public static int Run(string[] args)
    {
        var output = args.Length > 1 ? args[1] : "openapi.generated.json";
        var json = OpenApiGenerator.Generate();
        File.WriteAllText(output, json);
        Console.WriteLine($"Generated {PipelineRegistry.All.Count} pipelines -> {output}");
        return 0;
    }
}
