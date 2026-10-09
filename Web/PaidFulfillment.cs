using System.Net;
using System.Text.Json;
using LightningSPP.Licensing;
using Stripe;
using static wwwmmm.Web.StripeGateway;

namespace wwwmmm.Web;

/// <summary>Canonical paid-object reconciliation; event delivery IDs never mint entitlements.</summary>
internal sealed class PaidFulfillment(CustomerStore store, StripeGateway gateway,
    Func<string, string, string, DateTime, Task<(bool Sent, string Error)>>? notification = null)
{
    internal async Task<StripeFulfillment.Outcome> ProcessAsync(Event stripeEvent)
    {
        if (stripeEvent.Type is not ("checkout.session.completed" or "checkout.session.async_payment_succeeded" or "invoice.paid" or
            "customer.subscription.updated" or "customer.subscription.deleted" or "invoice.payment_failed"))
            return new(HttpStatusCode.OK, "ok=true\nignored=true");
        try
        {
            if (RuntimeSecurity.IsProduction && !stripeEvent.Livemode)
                return new(HttpStatusCode.BadRequest, "ok=false\nerror=Test event rejected");
            var id = stripeEvent.Data?.Object switch
            {
                Stripe.Checkout.Session session => session.Id, Invoice invoice => invoice.Id,
                Subscription subscription => subscription.Id, _ => null
            };
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(stripeEvent.Id))
                return new(HttpStatusCode.BadRequest, "ok=false\nerror=Missing payment object or event ID");
            if (stripeEvent.Type.StartsWith("customer.subscription.", StringComparison.Ordinal))
            {
                using var subscription = await gateway.GetAsync("subscriptions/" + Uri.EscapeDataString(id));
                store.SubscriptionState(id, Reference(subscription.RootElement, "customer") ?? "", String(subscription.RootElement, "status") ?? "unknown");
                return new(HttpStatusCode.OK, "ok=true\nreconciled=true");
            }
            var isInvoice = stripeEvent.Type.StartsWith("invoice.", StringComparison.Ordinal);
            using var payment = await gateway.GetAsync(isInvoice ? "invoices/" + Uri.EscapeDataString(id) :
                "checkout/sessions/" + Uri.EscapeDataString(id) + "?expand[]=line_items");
            var data = payment.RootElement;
            if (String(data, "id") != id || (RuntimeSecurity.IsProduction && (!data.TryGetProperty("livemode", out var live) || !live.GetBoolean())))
                throw new InvalidOperationException("Stripe returned an unexpected payment object.");
            if (!isInvoice && String(data, "mode") == "subscription")
                return new(HttpStatusCode.OK, "ok=true\nignored=true\nreason=Subscription licenses are granted by invoice.paid");
            var customerId = Reference(data, "customer") ?? throw new InvalidOperationException("Paid customer ID is missing.");
            string? subscriptionId = Reference(data, "subscription");
            if (subscriptionId is null && data.TryGetProperty("parent", out var parent) && parent.ValueKind == JsonValueKind.Object &&
                parent.TryGetProperty("subscription_details", out var subscriptionDetails)) subscriptionId = Reference(subscriptionDetails, "subscription");
            string subscriptionStatus = "paid";
            if (isInvoice)
            {
                if (subscriptionId is null) throw new InvalidOperationException("Invoice has no subscription reference.");
                using var subscription = await gateway.GetAsync("subscriptions/" + Uri.EscapeDataString(subscriptionId));
                if (Reference(subscription.RootElement, "customer") != customerId) throw new InvalidOperationException("Invoice/subscription customer mismatch.");
                subscriptionStatus = String(subscription.RootElement, "status") ?? "unknown";
                store.SubscriptionState(subscriptionId, customerId, subscriptionStatus);
            }
            if (stripeEvent.Type == "invoice.payment_failed" || (isInvoice ? String(data, "status") != "paid" : String(data, "payment_status") != "paid"))
                return new(HttpStatusCode.OK, "ok=true\nfulfilled=false\nreason=Payment not confirmed");
            var plans = StripeFulfillment.LoadPlans();
            var lines = data.GetProperty(isInvoice ? "lines" : "line_items");
            if (lines.TryGetProperty("has_more", out var more) && more.GetBoolean()) throw new InvalidOperationException("Paginated purchases unsupported.");
            var items = lines.GetProperty("data").EnumerateArray().ToArray();
            if (items.Length != 1 || !items[0].TryGetProperty("quantity", out var quantity) || quantity.GetInt32() != 1)
                throw new InvalidOperationException("Expected one configured license plan with quantity one.");
            var item = items[0];
            var priceId = item.TryGetProperty("price", out var legacyPrice) && legacyPrice.ValueKind == JsonValueKind.Object
                ? String(legacyPrice, "id") : item.GetProperty("pricing").GetProperty("price_details").GetProperty("price").GetString();
            if (priceId is null || !plans.TryGetValue(priceId, out var plan) || plan.Mode != (isInvoice ? "subscription" : "payment"))
                throw new InvalidOperationException("Paid price has no matching license plan.");
            var email = isInvoice ? String(data, "customer_email") : data.TryGetProperty("customer_details", out var details) ? String(details, "email") : null;
            if (string.IsNullOrWhiteSpace(email)) throw new InvalidOperationException("Paid email is missing.");
            var expiry = isInvoice ? DateTimeOffset.FromUnixTimeSeconds(item.GetProperty("period").GetProperty("end").GetInt64()) :
                DateTimeOffset.FromUnixTimeSeconds(data.GetProperty("created").GetInt64()).AddDays(plan.ValidityDays);
            if (expiry.ToUnixTimeSeconds() <= store.Now) return new(HttpStatusCode.OK, "ok=true\nfulfilled=false\nreason=Paid entitlement period expired");
            var entitlement = store.CommitPaid(new(id, stripeEvent.Id, customerId, subscriptionId, email, plan.Tier, expiry, subscriptionStatus), () =>
            {
                // Preserve any durable receipt issued by the earlier implementation.
                var existing = StripeFulfillment.ReadReceipt(id);
                if (existing is not null)
                {
                    if (CustomerStore.NormalizeEmail(existing.Email) != CustomerStore.NormalizeEmail(email) ||
                        !LicenseKey.Validate(existing.LicenseKey, out var recorded) || recorded is null)
                        throw new InvalidOperationException("Legacy receipt cannot be safely reconciled.");
                    return existing.LicenseKey;
                }
                var days = Math.Max(1, (int)Math.Ceiling((expiry.UtcDateTime.Date - DateTime.UtcNow.Date).TotalDays));
                return LicenseKey.Generate(Enum.Parse<LicenseKey.LicenseTier>(plan.Tier, true), email, "", days).Key;
            });
            InternalAuditLogger.Info($"Paid entitlement persisted: payment={id}, event={stripeEvent.Id}");
            var sent = await NotifyAsync(entitlement.Id);
            return sent ? new(HttpStatusCode.OK, "ok=true\nfulfilled=true\nnotification=provider_accepted") :
                new(HttpStatusCode.ServiceUnavailable, "ok=false\nfulfilled=true\nnotification=retry_required");
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or HttpRequestException or TaskCanceledException or
            JsonException or KeyNotFoundException or ArgumentException or FormatException or OverflowException)
        {
            InternalAuditLogger.Error($"Stripe reconciliation failed: {ex.GetType().Name}");
            return new(HttpStatusCode.ServiceUnavailable, "ok=false\nerror=Fulfillment unavailable; retry required");
        }
    }
    internal async Task<bool> NotifyAsync(string entitlementId)
    {
        var entitlement = store.GetEntitlement(entitlementId) ?? throw new InvalidOperationException("Entitlement is missing.");
        if (entitlement.EmailState == "PROVIDER_ACCEPTED") return true;
        if (!store.LeaseNotification(entitlementId)) return false;
        var accepted = false;
        try
        {
            var result = await (notification ?? EmailService.TrySendLicenseKeyEmailAsync)(store.NotificationEmail(entitlementId),
                entitlement.Tier, entitlement.LicenseKey, DateTimeOffset.FromUnixTimeSeconds(entitlement.Expires).UtcDateTime);
            accepted = result.Sent;
            if (!accepted) InternalAuditLogger.Error("Entitlement notification failed; durable license retained for retry.");
        }
        catch (Exception ex) { InternalAuditLogger.Error($"Entitlement notification transport failed: {ex.GetType().Name}"); }
        finally { store.NotificationResult(entitlementId, accepted, entitlement.LicenseKey); }
        return accepted;
    }
}
