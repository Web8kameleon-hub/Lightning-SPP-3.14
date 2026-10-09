using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LightningSPP.Licensing;
using Stripe;
using File = System.IO.File;
using System.Text.Json.Serialization;

namespace wwwmmm.Web;

// Payment confirmation is fetched from Stripe, never inferred from browser input.
internal static class StripeFulfillment
{
    internal sealed record Plan(string Tier, int ValidityDays, string Mode);
    internal sealed record Receipt(string PaymentId, string EventId, string Email, string LicenseKey, DateTime ExpiresUtc);
    internal sealed record Outcome(HttpStatusCode Status, string Message);
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(20) };

    internal static Dictionary<string, Plan> LoadPlans()
    {
        var raw = Environment.GetEnvironmentVariable("LSPP_STRIPE_PLANS");
        if (string.IsNullOrWhiteSpace(raw)) throw new InvalidOperationException("LSPP_STRIPE_PLANS is not configured.");
        var plans = JsonSerializer.Deserialize(raw, BillingJsonContext.Default.DictionaryStringPlan) ?? new();
        if (plans.Count == 0 || plans.Any(p => !p.Key.StartsWith("price_", StringComparison.Ordinal) ||
            p.Value == null || !Enum.TryParse<LicenseKey.LicenseTier>(p.Value.Tier, true, out var tier) ||
            !Enum.IsDefined(tier) || p.Value.ValidityDays is < 1 or > 3660 ||
            p.Value.Mode is not ("payment" or "subscription")))
            throw new InvalidOperationException("Stripe plan mappings are missing or invalid.");
        return plans;
    }

    internal static Task<Outcome> ProcessAsync(Event stripeEvent) =>
        new PaidFulfillment(CustomerAccounts.Store, new StripeGateway()).ProcessAsync(stripeEvent);

    private static string? String(JsonElement value, string property) =>
        value.TryGetProperty(property, out var result) && result.ValueKind == JsonValueKind.String ? result.GetString() : null;

    private static async Task<JsonDocument> FetchAsync(string path)
    {
        var key = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY");
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("STRIPE_SECRET_KEY is missing.");
        if (RuntimeSecurity.IsProduction && !key.StartsWith("sk_live_", StringComparison.Ordinal) && !key.StartsWith("rk_live_", StringComparison.Ordinal))
            throw new InvalidOperationException("Production requires live Stripe credentials.");
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.stripe.com/v1/" + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        using var response = await Client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static string ReceiptPath(string paymentId)
    {
        if (string.IsNullOrWhiteSpace(paymentId)) throw new ArgumentException("Payment ID required.");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(paymentId)));
        var dir = Path.Combine(RuntimeSecurity.DataDirectory, "payments");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, hash + ".json");
    }

    internal static Receipt? ReadReceipt(string paymentId)
    {
        var path = ReceiptPath(paymentId);
        return File.Exists(path) ? JsonSerializer.Deserialize(File.ReadAllText(path), BillingJsonContext.Default.Receipt) : null;
    }

    internal static Receipt StoreOnce(string paymentId, Func<Receipt> create)
    {
        var path = ReceiptPath(paymentId);
        // Cross-process lock. Concurrent delivery returns a retryable error rather than minting twice.
        using var gate = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        if (File.Exists(path)) return JsonSerializer.Deserialize(File.ReadAllText(path), BillingJsonContext.Default.Receipt)
            ?? throw new IOException("Stored receipt is invalid.");
        var receipt = create();
        var temp = path + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(receipt, BillingJsonContext.Default.Receipt);
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }
        File.Move(temp, path, overwrite: false);
        return receipt;
    }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(Dictionary<string, StripeFulfillment.Plan>))]
[JsonSerializable(typeof(StripeFulfillment.Receipt))]
internal partial class BillingJsonContext : JsonSerializerContext { }
