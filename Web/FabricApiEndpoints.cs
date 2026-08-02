using System.Net;
using System.Text;
using System.Text.Json;
using wwwmmm.PublicAPI;
using wwwmmm.Services;

namespace wwwmmm.Web;

/// <summary>
/// Kloud Fabric bridge for Lightning SPP.
/// Exposes the Scan-Process-Print pipeline as JSON endpoints under /api/v1/*
/// so the Kloud sovereign fabric (9999 / nanogrid / NodeDB) can orchestrate it
/// as a first-class node. Kept isolated from the licensing/dashboard surface.
///
/// JSON is written with Utf8JsonWriter (no reflection) because the project
/// disables reflection-based serialization for AOT/trimming compatibility.
/// </summary>
internal static class FabricApiEndpoints
{
    private const string NodeService = "lightning-spp";
    private const string NodeType = "scan-process-print";

    /// <summary>
    /// Attempts to handle a fabric request. Returns true if the path was a
    /// fabric endpoint (handled), false to let the caller continue routing.
    /// </summary>
    public static async Task<bool> TryHandleAsync(HttpListenerContext context, string path)
    {
        switch (path)
        {
            case "/api/v1/fabric/status":
                await FabricStatusAsync(context);
                return true;

            case "/api/v1/scan" when context.Request.HttpMethod == "POST":
                await ScanAsync(context);
                return true;

            case "/api/v1/pipeline" when context.Request.HttpMethod == "POST":
                await PipelineAsync(context);
                return true;

            default:
                return false;
        }
    }

    private static async Task FabricStatusAsync(HttpListenerContext context)
    {
        await WriteJsonAsync(context, HttpStatusCode.OK, writer =>
        {
            writer.WriteString("service", NodeService);
            writer.WriteString("type", NodeType);
            writer.WriteString("status", "up");
            writer.WriteString("version", "3.14");

            WriteStringArray(writer, "capabilities", new[] { "scan", "process", "print", "pipeline" });
            WriteStringArray(writer, "engines", new[] { "CLX", "CLI_I", "XCL", "Clisonic", "Hybrid" });
            WriteStringArray(writer, "scanModes", new[] { "Lightning", "TideWave", "Resonance", "NanoDecibel" });
            WriteStringArray(writer, "printQuality", new[] { "Draft", "Normal", "High", "Stigma" });

            writer.WriteString("timestampUtc", DateTime.UtcNow.ToString("O"));
        });
    }

    private static async Task ScanAsync(HttpListenerContext context)
    {
        var body = await ReadJsonAsync(context.Request);
        if (body is null)
        {
            await ErrorAsync(context, HttpStatusCode.BadRequest, "Invalid JSON body");
            return;
        }

        var source = GetString(body.Value, "source");
        if (string.IsNullOrWhiteSpace(source))
        {
            await ErrorAsync(context, HttpStatusCode.BadRequest, "Missing 'source'");
            return;
        }

        var mode = (GetString(body.Value, "mode") ?? "lightning").ToLowerInvariant();

        try
        {
            var result = mode is "hq" or "highquality" or "nanodecibel"
                ? await LightningAPI.HighQualityScanAsync(source)
                : await LightningAPI.QuickScanAsync(source);

            var status = result.Success ? HttpStatusCode.OK : HttpStatusCode.UnprocessableEntity;
            await WriteJsonAsync(context, status, writer =>
            {
                writer.WriteBoolean("ok", result.Success);
                writer.WriteString("service", NodeService);
                writer.WriteNumber("bytes", result.Data?.LongLength ?? 0);

                writer.WriteStartObject("metadata");
                writer.WriteString("technology", result.Metadata.Technology);
                writer.WriteNumber("resolution", result.Metadata.Resolution);
                writer.WriteString("format", result.Metadata.Format);
                writer.WriteNumber("processingTimeMs", result.Metadata.ProcessingTimeMs);
                writer.WriteString("timestamp", result.Metadata.Timestamp.ToString("O"));
                writer.WriteEndObject();

                WriteNullableString(writer, "error", result.Error);
            });
        }
        catch (Exception ex)
        {
            await ErrorAsync(context, HttpStatusCode.InternalServerError, ex.Message);
        }
    }

    private static async Task PipelineAsync(HttpListenerContext context)
    {
        var body = await ReadJsonAsync(context.Request);
        if (body is null)
        {
            await ErrorAsync(context, HttpStatusCode.BadRequest, "Invalid JSON body");
            return;
        }

        var source = GetString(body.Value, "source");
        if (string.IsNullOrWhiteSpace(source))
        {
            await ErrorAsync(context, HttpStatusCode.BadRequest, "Missing 'source'");
            return;
        }

        var shouldPrint = GetBool(body.Value, "shouldPrint", defaultValue: true);

        try
        {
            var request = new PipelineRequest(source, ShouldPrint: shouldPrint);
            var result = await LightningAPI.ExecuteCustomPipelineAsync(request);

            var status = result.Success ? HttpStatusCode.OK : HttpStatusCode.UnprocessableEntity;
            await WriteJsonAsync(context, status, writer =>
            {
                writer.WriteBoolean("ok", result.Success);
                writer.WriteString("service", NodeService);
                writer.WriteNumber("totalTimeMs", result.TotalTimeMs);

                writer.WriteStartArray("steps");
                foreach (var step in result.Steps)
                {
                    writer.WriteStringValue(step);
                }
                writer.WriteEndArray();

                WriteNullableString(writer, "error", result.Error);
            });
        }
        catch (Exception ex)
        {
            await ErrorAsync(context, HttpStatusCode.InternalServerError, ex.Message);
        }
    }

    // ---- JSON helpers (reflection-free) ----------------------------------

    private static async Task<JsonElement?> ReadJsonAsync(HttpListenerRequest request)
    {
        try
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8);
            var text = await reader.ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            using var doc = JsonDocument.Parse(text);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? GetString(JsonElement obj, string name)
        => obj.ValueKind == JsonValueKind.Object
           && obj.TryGetProperty(name, out var v)
           && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static bool GetBool(JsonElement obj, string name, bool defaultValue)
    {
        if (obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var v))
        {
            if (v.ValueKind == JsonValueKind.True) return true;
            if (v.ValueKind == JsonValueKind.False) return false;
        }
        return defaultValue;
    }

    private static void WriteStringArray(Utf8JsonWriter writer, string name, string[] values)
    {
        writer.WriteStartArray(name);
        foreach (var value in values)
        {
            writer.WriteStringValue(value);
        }
        writer.WriteEndArray();
    }

    private static void WriteNullableString(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is null)
        {
            writer.WriteNull(name);
        }
        else
        {
            writer.WriteString(name, value);
        }
    }

    private static async Task ErrorAsync(HttpListenerContext context, HttpStatusCode status, string message)
    {
        await WriteJsonAsync(context, status, writer =>
        {
            writer.WriteBoolean("ok", false);
            writer.WriteString("error", message);
        });
    }

    private static async Task WriteJsonAsync(HttpListenerContext context, HttpStatusCode status, Action<Utf8JsonWriter> writeBody)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writeBody(writer);
            writer.WriteEndObject();
        }

        var bytes = buffer.ToArray();
        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength64 = bytes.LongLength;
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }
}
