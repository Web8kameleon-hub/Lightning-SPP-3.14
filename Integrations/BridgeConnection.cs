using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace wwwmmm.Integrations;

/// <summary>Explicit upstream configuration. Never use a GitHub token as a service credential.</summary>
public sealed record BridgeConnection(Uri BaseUri, string? ApiKey = null, string? HmacSecret = null);

public sealed class BridgeException(string message) : Exception(message);

internal sealed class BridgeTransport : IDisposable
{
    private const int MaxResponseBytes = 2 * 1024 * 1024;
    private readonly HttpClient _http;
    private readonly BridgeConnection _connection;

    internal BridgeTransport(BridgeConnection connection, HttpMessageHandler? handler = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var uri = connection.BaseUri;
        if (uri is null || !uri.IsAbsoluteUri ||
            (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)) ||
            uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new ArgumentException("Bridge URL must use HTTPS (or HTTP on loopback), without credentials, query or fragment.");
        if (connection.ApiKey?.Any(char.IsControl) == true)
            throw new ArgumentException("Invalid service credential.");
        _connection = connection;
        _http = new HttpClient(handler ?? new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false
        })
        {
            BaseAddress = new Uri(uri.AbsoluteUri.TrimEnd('/') + "/"),
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    internal async Task<JsonElement> SendAsync(HttpRequestMessage request, bool osClx,
        CancellationToken cancellationToken)
    {
        var bytes = await SendBytesAsync(request, osClx, "application/json", MaxResponseBytes, cancellationToken);
        try
        {
            using var document = JsonDocument.Parse(bytes);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new BridgeException("Upstream JSON must be an object.");
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            throw new BridgeException("Upstream returned invalid JSON.");
        }
    }

    internal async Task<byte[]> SendBytesAsync(HttpRequestMessage request, bool osClx,
        string mediaType, int limit, CancellationToken cancellationToken)
    {
        request.RequestUri = new Uri(_http.BaseAddress!, request.RequestUri!);
        request.Headers.Accept.ParseAdd(mediaType);
        request.Headers.Add("X-Request-ID", Guid.NewGuid().ToString("N"));
        if (!string.IsNullOrWhiteSpace(_connection.ApiKey))
        {
            if (osClx) request.Headers.Add("X-API-Key", _connection.ApiKey);
            else request.Headers.Authorization = new("Bearer", _connection.ApiKey);
        }
        if (osClx)
        {
            request.Headers.Add("X-CLX-Role", request.Method == HttpMethod.Get ? "viewer" : "operator");
            if (request.Method != HttpMethod.Get && !string.IsNullOrWhiteSpace(_connection.HmacSecret))
            {
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
                // Matches OS-CLX HMACGuard: method + path + UNIX timestamp, separated by newlines.
                var message = $"{request.Method.Method}\n{request.RequestUri.AbsolutePath}\n{timestamp}";
                var signature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(_connection.HmacSecret), Encoding.UTF8.GetBytes(message));
                request.Headers.Add("X-CLX-Timestamp", timestamp);
                request.Headers.Add("X-CLX-Signature", Convert.ToHexStringLower(signature));
            }
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (!response.IsSuccessStatusCode)
                throw new BridgeException($"Upstream returned HTTP {(int)response.StatusCode}. No success was confirmed.");
            if (!string.Equals(response.Content.Headers.ContentType?.MediaType, mediaType, StringComparison.OrdinalIgnoreCase))
                throw new BridgeException("Upstream returned an unexpected content type.");
            if (response.Content.Headers.ContentLength > limit)
                throw new BridgeException("Upstream response exceeds the configured size limit.");
            await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            int read;
            while ((read = await stream.ReadAsync(chunk, deadline.Token)) != 0)
            {
                if (buffer.Length + read > limit)
                    throw new BridgeException("Upstream response exceeds the configured size limit.");
                buffer.Write(chunk, 0, read);
            }
            return buffer.ToArray();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new BridgeException("Bridge request timed out. For writes, the upstream outcome is unknown; read back before retrying.");
        }
        catch (HttpRequestException)
        {
            throw new BridgeException("Could not communicate with the upstream service. For writes, read back before retrying.");
        }
        catch (IOException)
        {
            throw new BridgeException("Upstream response could not be fully read. No success was confirmed.");
        }
    }

    internal static bool Has(JsonElement json, string name, JsonValueKind kind) =>
        json.TryGetProperty(name, out var value) && value.ValueKind == kind;

    public void Dispose() => _http.Dispose();
}
