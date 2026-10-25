namespace wwwmmm.FluidEngine.Core.Registry;

public sealed record PipelineDescriptor(
    string Name,
    string Category,
    string InputType,
    string OutputType,
    string Description,
    bool RequiresBinaryInput);

public static class PipelineRegistry
{
    public static IReadOnlyList<PipelineDescriptor> All { get; } = new[]
    {
        new PipelineDescriptor("BitPipeline",     "Binary", "byte[]", "byte[]", "Brotli + framing (Magic+Version+Length)", true),
        new PipelineDescriptor("BitUnpacker",     "Binary", "byte[]", "byte[]", "Framed Brotli decoder with validation",   true),
        new PipelineDescriptor("JsonDataStep",    "Data",   "string", "string", "JSON validation and pretty-print",       false),
        new PipelineDescriptor("CsvDataStep",     "Data",   "string", "string[][]", "CSV parse and validation",           false),
        new PipelineDescriptor("VisionPipeline",  "Vision", "byte[]", "byte[]", "SkiaSharp image transform",               true),
        new PipelineDescriptor("FlowPipeline",    "Flow",   "string", "string", "Content-based routing",                   false),
        new PipelineDescriptor("RoutinePipeline", "Routine","int",    "string", "Iterative computation",                   false),
        new PipelineDescriptor("StigmaMemoryFilmPipeline", "Memory", "byte[]", "Guid", "Store bytes in FluidLayer",       true),
        new PipelineDescriptor("SeltenDataPipeline", "Data", "int[]", "int[]", "Outlier detection (2σ)",                  false),
        new PipelineDescriptor("StandardDataMetaknowPipeline", "Cognitive", "string", "string", "Metadata extraction",    false),
        new PipelineDescriptor("SuitabilityPipeline", "Cognitive", "string", "bool", "Pattern-based suitability check",    false)
    };

    public static PipelineDescriptor Get(string name) =>
        All.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"Unknown pipeline: {name}");
}
