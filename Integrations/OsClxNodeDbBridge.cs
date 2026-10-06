using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace wwwmmm.Integrations;

/// <summary>Caller supplies measured signals; omitted signals never silently become zero.</summary>
public sealed record NodeDbTelemetry
{
    [JsonPropertyName("fabric_id")] public required string FabricId { get; init; }
    [JsonPropertyName("trust_domain")] public required string TrustDomain { get; init; }
    [JsonPropertyName("pressure")] public required double Pressure { get; init; }
    [JsonPropertyName("resonance")] public required double Resonance { get; init; }
    [JsonPropertyName("health_score")] public required double HealthScore { get; init; }
    [JsonPropertyName("memory")] public required double Memory { get; init; }
    [JsonPropertyName("momentum")] public required double Momentum { get; init; }
    [JsonPropertyName("error_rate")] public required double ErrorRate { get; init; }
    [JsonPropertyName("queue_depth")] public required double QueueDepth { get; init; }
    [JsonPropertyName("latency_ms")] public required double LatencyMs { get; init; }
    [JsonPropertyName("role")] public string? Role { get; init; }
    [JsonPropertyName("capabilities")] public string[]? Capabilities { get; init; }
    [JsonPropertyName("affinity_tags")] public string[]? AffinityTags { get; init; }
    [JsonPropertyName("preferred_task_types")] public string[]? PreferredTaskTypes { get; init; }
    [JsonPropertyName("burst_allowed")] public bool? BurstAllowed { get; init; }

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(FabricId) || string.IsNullOrWhiteSpace(TrustDomain) ||
            FabricId.Length > 128 || TrustDomain.Length > 128 || FabricId.Any(char.IsControl) || TrustDomain.Any(char.IsControl))
            throw new ArgumentException("fabric_id and trust_domain are required.");
        if (Role is not null && (Role.Length > 128 || Role.Any(char.IsControl)))
            throw new ArgumentException("Invalid node role.");
        foreach (var tags in new[] { Capabilities, AffinityTags, PreferredTaskTypes })
            if (tags is not null && (tags.Length > 64 || tags.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 128 || x.Any(char.IsControl))))
                throw new ArgumentException("Telemetry tag lists must contain at most 64 nonempty values of up to 128 characters.");
        foreach (var value in new[] { Pressure, Resonance, HealthScore, Memory, Momentum, ErrorRate })
            if (!double.IsFinite(value) || value < 0 || value > 1)
                throw new ArgumentException("Normalized telemetry signals must be finite numbers in [0, 1].");
        if (!double.IsFinite(QueueDepth) || QueueDepth < 0 || !double.IsFinite(LatencyMs) || LatencyMs < 0)
            throw new ArgumentException("queue_depth and latency_ms must be finite, nonnegative measurements.");
    }
}

public sealed class OsClxNodeDbBridge : IDisposable
{
    private readonly BridgeTransport _transport;
    private static readonly JsonSerializerOptions TelemetryJson = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public OsClxNodeDbBridge(BridgeConnection connection) : this(connection, null) { }
    internal OsClxNodeDbBridge(BridgeConnection connection, HttpMessageHandler? handler) =>
        _transport = new(connection, handler);

    public async Task<JsonElement> ReadNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        ValidateNodeId(nodeId);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"v1/ndb/nodes/{nodeId}");
        var result = await _transport.SendAsync(request, true, cancellationToken);
        ValidateNodeResponse(result, "nodedb-node", nodeId);
        return result;
    }

    /// <summary>Writes explicitly supplied telemetry once; no automatic retries or promotion of trust.</summary>
    public async Task<JsonElement> IngestAsync(string nodeId, NodeDbTelemetry telemetry,
        CancellationToken cancellationToken = default)
    {
        ValidateNodeId(nodeId);
        ArgumentNullException.ThrowIfNull(telemetry);
        telemetry.Validate();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"v1/ndb/nodes/{nodeId}/telemetry")
        {
            Content = JsonContent.Create(telemetry, options: TelemetryJson)
        };
        var result = await _transport.SendAsync(request, true, cancellationToken);
        ValidateNodeResponse(result, "nodedb-telemetry-accepted", nodeId);
        if (!result.TryGetProperty("node_id", out var accepted) || accepted.ValueKind != JsonValueKind.String || accepted.GetString() != nodeId)
            throw new BridgeException("NodeDB did not acknowledge the requested node.");
        var node = result.GetProperty("node");
        if (!BridgeTransport.Has(node, "fabric_id", JsonValueKind.String) || node.GetProperty("fabric_id").GetString() != telemetry.FabricId ||
            !BridgeTransport.Has(node, "trust_domain", JsonValueKind.String) || node.GetProperty("trust_domain").GetString() != telemetry.TrustDomain)
            throw new BridgeException("NodeDB acknowledged a different fabric or trust domain.");
        var cycle = node.GetProperty("cycle");
        foreach (var signal in new[] { ("pressure", telemetry.Pressure), ("resonance", telemetry.Resonance), ("memory", telemetry.Memory), ("momentum", telemetry.Momentum) })
            if (cycle.GetProperty(signal.Item1).GetDouble() != signal.Item2)
                throw new BridgeException("NodeDB did not acknowledge the supplied telemetry signals.");
        return result;
    }

    public static NodeDbTelemetry ParseTelemetry(string json) =>
        JsonSerializer.Deserialize<NodeDbTelemetry>(json, TelemetryJson) ?? throw new JsonException("Telemetry must be an object.");

    private static void ValidateNodeId(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId) || nodeId.Length > 128 ||
            nodeId.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')))
            throw new ArgumentException("Node ID must contain 1–128 ASCII letters, digits, hyphens or underscores.", nameof(nodeId));
    }

    private static void ValidateNodeResponse(JsonElement json, string type, string nodeId)
    {
        if (!BridgeTransport.Has(json, "type", JsonValueKind.String) || json.GetProperty("type").GetString() != type ||
            !BridgeTransport.Has(json, "node", JsonValueKind.Object) ||
            !BridgeTransport.Has(json.GetProperty("node"), "node_id", JsonValueKind.String) ||
            json.GetProperty("node").GetProperty("node_id").GetString() != nodeId ||
            !BridgeTransport.Has(json.GetProperty("node"), "cycle", JsonValueKind.Object))
            throw new BridgeException("NodeDB returned an invalid or mismatched node response.");
        var cycle = json.GetProperty("node").GetProperty("cycle");
        foreach (var field in new[] { "pressure", "resonance", "memory", "momentum" })
            if (!BridgeTransport.Has(cycle, field, JsonValueKind.Number) ||
                !cycle.GetProperty(field).TryGetDouble(out var value) || !double.IsFinite(value) || value < 0 || value > 1)
                throw new BridgeException("NodeDB returned an invalid cycle signal.");
    }

    public void Dispose() => _transport.Dispose();
}
