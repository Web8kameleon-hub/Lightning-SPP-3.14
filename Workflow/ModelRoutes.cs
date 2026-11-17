using System.Text.Json;

namespace wwwmmm.Workflow;

/// <summary>
/// Decides which model routes this instance may use. An operator file always wins; otherwise a
/// loopback Ollama runtime is discovered over real HTTP. Nothing here reaches a non-loopback host
/// and no credential is invented, so an unconfigured instance reports "no model" instead of
/// pretending to infer. Probe results are cached so per-request resolution stays cheap.
/// </summary>
public static class ModelRoutes
{
    public const string LocalRouteId = "local";
    public const string OperatorFileSource = "operator-file";
    public const string LoopbackSource = "loopback-ollama";
    public const string NoSource = "none";
    private const string DefaultOllamaEndpoint = "http://127.0.0.1:11434";
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(30);
    private static readonly HttpClient Probe = new(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false }) { Timeout = TimeSpan.FromSeconds(2) };
    private static readonly object Gate = new();
    private static (DateTimeOffset ResolvedAt, ModelRoute[] Routes, string Source) cached = (DateTimeOffset.MinValue, [], NoSource);

    /// <summary>Operator file, else loopback discovery, else an empty route set with its reason.</summary>
    public static (ModelRoute[] Routes, string Source) Resolve()
    {
        lock (Gate)
        {
            if (DateTimeOffset.UtcNow - cached.ResolvedAt < CacheLifetime) return (cached.Routes, cached.Source);
            var file = Environment.GetEnvironmentVariable("LSPP_MODEL_ROUTES_FILE");
            if (!string.IsNullOrWhiteSpace(file))
            {
                cached = (DateTimeOffset.UtcNow, FromOperatorFile(file), OperatorFileSource);
                return (cached.Routes, cached.Source);
            }
            var discovered = DiscoverLoopbackOllama();
            cached = (DateTimeOffset.UtcNow, discovered, discovered.Length == 0 ? NoSource : LoopbackSource);
            return (cached.Routes, cached.Source);
        }
    }

    /// <summary>Drops the cached probe so the next call reflects the current environment.</summary>
    internal static void ClearCache() { lock (Gate) cached = (DateTimeOffset.MinValue, [], NoSource); }

    private static ModelRoute[] FromOperatorFile(string file)
    {
        if (!Path.IsPathFullyQualified(file))
            throw new ArgumentException("LSPP_MODEL_ROUTES_FILE must be an absolute path to an operator-owned JSON array.");
        if (!File.Exists(file))
            throw new ArgumentException("LSPP_MODEL_ROUTES_FILE names no existing file: " + file);
        var info = new FileInfo(file);
        if (info.Length > 1024 * 1024)
            throw new ArgumentException("LSPP_MODEL_ROUTES_FILE must stay under 1 MiB.");
        return JsonSerializer.Deserialize<ModelRoute[]>(File.ReadAllBytes(file), WorkflowJson.Options)
            ?? throw new ArgumentException("Model route array required.");
    }

    /// <summary>Reads the local runtime's own catalogue over HTTP; a model is never assumed from its name.</summary>
    private static ModelRoute[] DiscoverLoopbackOllama()
    {
        if (string.Equals(Environment.GetEnvironmentVariable("LSPP_MODEL_AUTODISCOVER"), "off", StringComparison.OrdinalIgnoreCase))
            return [];
        var endpointText = Environment.GetEnvironmentVariable("LSPP_OLLAMA_ENDPOINT") ?? DefaultOllamaEndpoint;
        if (!Uri.TryCreate(endpointText, UriKind.Absolute, out var endpoint) || endpoint.Scheme is not ("http" or "https") ||
            !endpoint.IsLoopback || !string.IsNullOrEmpty(endpoint.UserInfo) || !string.IsNullOrEmpty(endpoint.Query) || !string.IsNullOrEmpty(endpoint.Fragment))
            throw new ArgumentException("LSPP_OLLAMA_ENDPOINT must be a loopback http(s) endpoint without credentials, query or fragment.");
        var baseUrl = endpointText.TrimEnd('/');
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            using var response = Probe.GetAsync(baseUrl + "/api/tags", HttpCompletionOption.ResponseHeadersRead, deadline.Token).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode) return [];
            using var stream = response.Content.ReadAsStreamAsync(deadline.Token).GetAwaiter().GetResult();
            using var data = new MemoryStream(); var buffer = new byte[8192]; int read;
            while ((read = stream.ReadAsync(buffer, deadline.Token).AsTask().GetAwaiter().GetResult()) != 0)
            { if (data.Length + read > 262144) throw new IOException("Model discovery response exceeds its budget."); data.Write(buffer, 0, read); }
            using var document = JsonDocument.Parse(data.ToArray());
            if (!document.RootElement.TryGetProperty("models", out var models) || models.ValueKind != JsonValueKind.Array) return [];
            var catalog = models.EnumerateArray()
                .Where(m => m.ValueKind == JsonValueKind.Object)
                .Select(m => (Name: WorkflowJson.Text(m, "name"), Capabilities: Capabilities(m)))
                .Where(m => !string.IsNullOrWhiteSpace(m.Name))
                .ToArray();
            var model = Preferred(catalog) ?? catalog.FirstOrDefault(m => m.Capabilities.Contains("completion")).Name ?? catalog.FirstOrDefault().Name;
            return string.IsNullOrWhiteSpace(model) ? [] : [new ModelRoute(LocalRouteId, baseUrl, model, "ollama", null, false)];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or JsonException or IOException)
        {
            // A stopped runtime is a normal state: report no local model rather than a phantom route.
            return [];
        }
    }

    private static string? Preferred((string Name, string[] Capabilities)[] catalog)
    {
        var list = Environment.GetEnvironmentVariable("LSPP_MODEL_PREFER");
        if (string.IsNullOrWhiteSpace(list)) return null;
        foreach (var candidate in list.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (catalog.Any(m => string.Equals(m.Name, candidate, StringComparison.Ordinal))) return candidate;
        return null;
    }

    private static string[] Capabilities(JsonElement model) =>
        model.TryGetProperty("capabilities", out var values) && values.ValueKind == JsonValueKind.Array
            ? values.EnumerateArray().Select(v => v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "").Where(v => v.Length > 0).ToArray()
            : [];
}
