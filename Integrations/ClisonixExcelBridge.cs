using System.Net.Http.Headers;
using System.Text.Json;

namespace wwwmmm.Integrations;

/// <summary>Adapter for clisonix.com/services/excel/main.py, not the separate LAGTER reporting API.</summary>
public sealed class ClisonixExcelBridge : IDisposable
{
    private const int MaxUploadBytes = 10 * 1024 * 1024;
    private readonly BridgeTransport _transport;

    public ClisonixExcelBridge(BridgeConnection connection) : this(connection, null) { }
    internal ClisonixExcelBridge(BridgeConnection connection, HttpMessageHandler? handler) =>
        _transport = new(connection, handler);

    /// <summary>Returns upstream data verbatim. The service returns at most the first 100 data rows.</summary>
    public Task<JsonElement> ParseAsync(string filename, ReadOnlyMemory<byte> workbook,
        CancellationToken cancellationToken = default) => SendWorkbookAsync("parse", filename, workbook, cancellationToken);

    public Task<JsonElement> ValidateAsync(string filename, ReadOnlyMemory<byte> workbook,
        CancellationToken cancellationToken = default) => SendWorkbookAsync("validate", filename, workbook, cancellationToken);

    private async Task<JsonElement> SendWorkbookAsync(string operation, string filename,
        ReadOnlyMemory<byte> workbook, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(filename) || filename.Length > 255 || filename.Any(char.IsControl) ||
            filename.IndexOfAny(['/', '\\', '"']) >= 0 || !filename.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Supply a plain .xlsx filename without directory components.", nameof(filename));
        if (workbook.Length == 0 || workbook.Length > MaxUploadBytes)
            throw new ArgumentException("Workbook must contain 1 byte to 10 MiB.", nameof(workbook));
        cancellationToken.ThrowIfCancellationRequested();

        using var multipart = new MultipartFormDataContent();
        var file = new ByteArrayContent(workbook.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        multipart.Add(file, "file", filename);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/excel/{operation}") { Content = multipart };
        var result = await _transport.SendAsync(request, false, cancellationToken);
        if (!result.TryGetProperty("filename", out var actualName) || actualName.ValueKind != JsonValueKind.String || actualName.GetString() != filename)
            throw new BridgeException("Excel service returned an unexpected filename.");
        if (operation == "validate")
        {
            if (!result.TryGetProperty("valid", out var valid) || valid.ValueKind != JsonValueKind.True ||
                !BridgeTransport.Has(result, "sheets", JsonValueKind.Array) ||
                result.GetProperty("sheets").GetArrayLength() == 0 ||
                result.GetProperty("sheets").EnumerateArray().Any(sheet =>
                    sheet.ValueKind != JsonValueKind.Object || !HasName(sheet, "name") ||
                    !HasNonnegativeInteger(sheet, "rows") || !HasNonnegativeInteger(sheet, "columns")))
                throw new BridgeException("Excel service did not confirm a valid workbook.");
        }
        else if (!HasName(result, "sheet") ||
                 !BridgeTransport.Has(result, "headers", JsonValueKind.Array) ||
                 !HasUniqueHeaders(result.GetProperty("headers")) ||
                 !BridgeTransport.Has(result, "data", JsonValueKind.Array) ||
                 !HasMatchingRows(result.GetProperty("headers"), result.GetProperty("data")) ||
                 !result.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Number ||
                 !rows.TryGetInt32(out var count) || count < 0 ||
                 result.GetProperty("data").GetArrayLength() != Math.Min(count, 100))
            throw new BridgeException("Excel service returned an invalid parse result.");
        return result;
    }

    private static bool HasName(JsonElement json, string name) =>
        BridgeTransport.Has(json, name, JsonValueKind.String) && !string.IsNullOrWhiteSpace(json.GetProperty(name).GetString());

    private static bool HasNonnegativeInteger(JsonElement json, string name) =>
        BridgeTransport.Has(json, name, JsonValueKind.Number) && json.GetProperty(name).TryGetInt32(out var value) && value >= 0;

    private static bool HasUniqueHeaders(JsonElement headers)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var header in headers.EnumerateArray())
            if (header.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(header.GetString()) || !names.Add(header.GetString()!))
                return false;
        return true;
    }

    private static bool HasMatchingRows(JsonElement headers, JsonElement data)
    {
        var names = headers.EnumerateArray().Select(header => header.GetString()!).ToHashSet(StringComparer.Ordinal);
        foreach (var row in data.EnumerateArray())
            if (row.ValueKind != JsonValueKind.Object || row.EnumerateObject().Count() != names.Count ||
                row.EnumerateObject().Any(cell => !names.Contains(cell.Name)))
                return false;
        return true;
    }

    public void Dispose() => _transport.Dispose();
}
