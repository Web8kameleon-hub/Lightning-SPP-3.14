using System.Text.Json;
using System.Text.Json.Nodes;

namespace wwwmmm.FluidEngine.Core.Registry;

public static class OpenApiGenerator
{
    public static string Generate()
    {
        var paths = new JsonObject();
        var schemas = new JsonObject();

        foreach (var p in PipelineRegistry.All.Where(p => p.Name is "BitPipeline" or "BitUnpacker"))
        {
            schemas[p.Name] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["inputType"] = new JsonObject { ["type"] = "string", ["const"] = p.InputType },
                    ["outputType"] = new JsonObject { ["type"] = "string", ["const"] = p.OutputType }
                }
            };

            paths[$"/api/fluid/{p.Name}"] = new JsonObject
            {
                ["post"] = new JsonObject
                {
                    ["summary"] = p.Description,
                    ["tags"] = new JsonArray(p.Category),
                    ["operationId"] = $"execute_{p.Name}",
                    ["responses"] = new JsonObject
                    {
                        ["200"] = new JsonObject { ["description"] = "Success" },
                        ["400"] = new JsonObject { ["description"] = "Never Fake: input invalid" }
                    }
                }
            };
        }

        var doc = new JsonObject
        {
            ["openapi"] = "3.1.0",
            ["info"] = new JsonObject
            {
                ["title"] = "wwwmmm Fluid Engine API",
                ["version"] = "3.19.0",
                ["description"] = "HTTP operations implemented by FluidApiHandler. Other registry entries are library-only."
            },
            ["paths"] = paths,
            ["components"] = new JsonObject { ["schemas"] = schemas }
        };

        return doc.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }
}
