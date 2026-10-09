using System.Net.Http.Headers;
using System.Text.Json;

namespace wwwmmm.Web;

/// <summary>Production uses Stripe's API only. Tests inject a transport into this boundary.</summary>
internal sealed class StripeGateway(HttpClient? transport = null)
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(20) };
    internal async Task<JsonDocument> GetAsync(string path) => await SendAsync(HttpMethod.Get, path);
    internal async Task<JsonDocument> PostAsync(string path, Dictionary<string, string> form, string idempotencyKey) =>
        await SendAsync(HttpMethod.Post, path, form, idempotencyKey);
    private async Task<JsonDocument> SendAsync(HttpMethod method, string path, Dictionary<string, string>? form = null, string? idempotencyKey = null)
    {
        var key = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY");
        if (string.IsNullOrWhiteSpace(key) || (RuntimeSecurity.IsProduction && !key.StartsWith("sk_live_", StringComparison.Ordinal) && !key.StartsWith("rk_live_", StringComparison.Ordinal)))
            throw new InvalidOperationException("Stripe credentials are unavailable for this environment.");
        using var request = new HttpRequestMessage(method, "https://api.stripe.com/v1/" + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        if (idempotencyKey is not null) request.Headers.Add("Idempotency-Key", idempotencyKey);
        if (form is not null) request.Content = new FormUrlEncodedContent(form);
        using var response = await (transport ?? Client).SendAsync(request);
        response.EnsureSuccessStatusCode();
        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        if (RuntimeSecurity.IsProduction && document.RootElement.TryGetProperty("livemode", out var live) && !live.GetBoolean())
        { document.Dispose(); throw new InvalidOperationException("Test Stripe object cannot be used in production."); }
        return document;
    }
    internal static string? String(JsonElement value, string property) => value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(property, out var result) && result.ValueKind == JsonValueKind.String ? result.GetString() : null;
    internal static string? Reference(JsonElement value, string property)
    {
        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(property, out var reference)) return null;
        return reference.ValueKind == JsonValueKind.String ? reference.GetString() : String(reference, "id");
    }
}
