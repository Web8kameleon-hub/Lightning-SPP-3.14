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

    internal static async Task<Outcome> ProcessAsync(Event stripeEvent)
    {
        if (stripeEvent.Type is not ("checkout.session.completed" or "checkout.session.async_payment_succeeded" or "invoice.paid"))
            return new(HttpStatusCode.OK, "ok=true\nignored=true\nreason=Event does not grant a license");
        try
        {
            var plans = LoadPlans();
            if (RuntimeSecurity.IsProduction && !stripeEvent.Livemode)
                return new(HttpStatusCode.BadRequest, "ok=false\nerror=Test payments cannot grant production licenses");
            var id = stripeEvent.Data?.Object switch
            {
                Stripe.Checkout.Session session => session.Id,
                Invoice invoice => invoice.Id,
                _ => null
            };
            if (string.IsNullOrWhiteSpace(id))
                return new(HttpStatusCode.BadRequest, "ok=false\nerror=Missing payment object");

            var isInvoice = stripeEvent.Type == "invoice.paid";
            using var payment = await FetchAsync(isInvoice ? $"invoices/{Uri.EscapeDataString(id)}" :
                $"checkout/sessions/{Uri.EscapeDataString(id)}?expand[]=line_items");
            var data = payment.RootElement;
            if (RuntimeSecurity.IsProduction && (!data.TryGetProperty("livemode", out var live) || !live.GetBoolean()))
                return new(HttpStatusCode.BadRequest, "ok=false\nerror=Test payments cannot grant production licenses");
            if (!isInvoice && String(data, "mode") == "subscription")
                return new(HttpStatusCode.OK, "ok=true\nignored=true\nreason=Subscription licenses are granted by invoice.paid");
            if (isInvoice ? String(data, "status") != "paid" : String(data, "payment_status") != "paid")
                return new(HttpStatusCode.OK, "ok=true\nfulfilled=false\nreason=Payment not confirmed");
            var lines = data.GetProperty(isInvoice ? "lines" : "line_items");
            if (lines.TryGetProperty("has_more", out var more) && more.GetBoolean())
                throw new InvalidOperationException("Paginated purchases are not supported; no license was issued.");
            var items = lines.GetProperty("data").EnumerateArray().ToArray();
            if (items.Length != 1 || !items[0].TryGetProperty("quantity", out var quantity) || quantity.GetInt32() != 1)
                throw new InvalidOperationException("Expected exactly one license plan with quantity one.");
            var item = items[0];
            var priceId = item.TryGetProperty("price", out var legacyPrice) && legacyPrice.ValueKind == JsonValueKind.Object
                ? String(legacyPrice, "id")
                : item.GetProperty("pricing").GetProperty("price_details").GetProperty("price").GetString();
            if (priceId == null || !plans.TryGetValue(priceId, out var plan) || plan.Mode != (isInvoice ? "subscription" : "payment"))
                throw new InvalidOperationException("Paid price has no matching license plan.");
            var email = isInvoice ? String(data, "customer_email") :
                data.TryGetProperty("customer_details", out var details) && details.ValueKind == JsonValueKind.Object ? String(details, "email") : null;
            if (string.IsNullOrWhiteSpace(email)) throw new InvalidOperationException("Paid customer email is missing.");
            var expiry = isInvoice
                ? DateTimeOffset.FromUnixTimeSeconds(item.GetProperty("period").GetProperty("end").GetInt64()).UtcDateTime
                : DateTimeOffset.FromUnixTimeSeconds(data.GetProperty("created").GetInt64()).UtcDateTime.AddDays(plan.ValidityDays);
            if (expiry <= DateTime.UtcNow)
                return new(HttpStatusCode.OK, "ok=true\nfulfilled=false\nreason=Paid entitlement period has expired");
            var receipt = StoreOnce(id, () =>
            {
                var days = Math.Max(1, (int)Math.Ceiling((expiry.Date - DateTime.UtcNow.Date).TotalDays));
                var key = LicenseKey.Generate(Enum.Parse<LicenseKey.LicenseTier>(plan.Tier, true), email, "", days);
                return new Receipt(id, stripeEvent.Id, email, key.Key, key.ExpirationDate.Date);
            });
            InternalAuditLogger.Info($"Payment fulfilled: payment={id}, event={stripeEvent.Id}");
            return new(HttpStatusCode.OK, "ok=true\nfulfilled=true");
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or HttpRequestException or
            TaskCanceledException or JsonException or KeyNotFoundException or ArgumentException)
        {
            // Never acknowledge an uncommitted purchase; Stripe can retry after configuration/storage recovery.
            InternalAuditLogger.Error($"Stripe fulfillment failed: {ex.GetType().Name}");
            return new(HttpStatusCode.ServiceUnavailable, "ok=false\nerror=Payment fulfillment unavailable; retry required");
        }
    }

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
