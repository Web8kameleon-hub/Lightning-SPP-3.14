using System.Collections.Concurrent;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using static wwwmmm.Web.StripeGateway;

namespace wwwmmm.Web;

/// <summary>Framework cookie authentication/antiforgery adapted to the existing HttpListener transport.</summary>
internal static class CustomerAccounts
{
    private const string Scheme = "LightningCustomer";
    private static readonly ConcurrentDictionary<string, Lazy<ServiceProvider>> Providers = new(StringComparer.Ordinal);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    internal static bool Enabled => !RuntimeSecurity.IsProduction || Environment.GetEnvironmentVariable("LSPP_CUSTOMER_ACCOUNTS_ENABLED") == "true";
    private static ServiceProvider Provider => Providers.GetOrAdd(Path.GetFullPath(RuntimeSecurity.DataDirectory), path => new(() => CreateServices(path))).Value;
    internal static CustomerStore Store => Provider.GetRequiredService<CustomerStore>();
    internal static string PublicOrigin => (Environment.GetEnvironmentVariable("LSPP_PUBLIC_ORIGIN") ?? "https://scanprocessprint.com").TrimEnd('/');
    private static ServiceProvider CreateServices(string path)
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddWebEncoders();
        var store = new CustomerStore(path); services.AddSingleton(store);
        services.AddDataProtection().SetApplicationName("LightningSPP.Customers.v1").PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(store.DirectoryPath, "keys")));
        services.AddAuthentication(Scheme).AddCookie(Scheme, options =>
        {
            options.Cookie.Name = RuntimeSecurity.IsProduction ? "__Host-LightningCustomer" : "LightningCustomer";
            options.Cookie.Path = "/"; options.Cookie.HttpOnly = true; options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = RuntimeSecurity.IsProduction ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromHours(8); options.SlidingExpiration = false;
            options.Events.OnValidatePrincipal = context =>
            {
                var user = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                var session = context.Principal?.FindFirstValue("session");
                if (user is null || session is null || !store.ValidSession(user, session)) context.RejectPrincipal();
                return Task.CompletedTask;
            };
        });
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-Token";
            options.Cookie.Name = RuntimeSecurity.IsProduction ? "__Host-LightningCsrf" : "LightningCsrf";
            options.Cookie.Path = "/"; options.Cookie.HttpOnly = true; options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = RuntimeSecurity.IsProduction ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
        });
        return services.BuildServiceProvider();
    }
    internal static async Task<(string Owner, string Csrf, string Session)> AuthorizeWorkspaceAsync(HttpListenerContext transport, bool mutation)
    {
        if (!Enabled) throw new InvalidOperationException("Customer accounts are unavailable.");
        using var scope = Provider.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Method = transport.Request.HttpMethod;
        context.Request.Scheme = RuntimeSecurity.IsProduction ? "https" : transport.Request.Url!.Scheme;
        context.Request.Host = new HostString(transport.Request.Url!.Authority);
        context.Request.Path = transport.Request.Url.AbsolutePath;
        foreach (var key in transport.Request.Headers.AllKeys) if (key is not null) context.Request.Headers[key] = transport.Request.Headers[key];
        var result = await context.AuthenticateAsync(Scheme);
        var owner = result.Succeeded ? result.Principal?.FindFirstValue(ClaimTypes.NameIdentifier) : null;
        if (owner is null) throw new UnauthorizedAccessException("Sign in to access your workspace.");
        context.User = result.Principal!;
        var antiforgery = scope.ServiceProvider.GetRequiredService<IAntiforgery>();
        if (mutation)
        {
            var expected = RuntimeSecurity.IsProduction ? PublicOrigin : transport.Request.Url.GetLeftPart(UriPartial.Authority);
            if (!string.Equals(transport.Request.Headers["Origin"], expected, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(transport.Request.ContentType?.Split(';')[0], "application/json", StringComparison.OrdinalIgnoreCase))
                throw new AntiforgeryValidationException("Same-origin JSON required.");
            await antiforgery.ValidateRequestAsync(context);
        }
        var csrf = antiforgery.GetAndStoreTokens(context).RequestToken!;
        foreach (var cookie in context.Response.Headers.SetCookie) if (cookie is not null) transport.Response.Headers.Add("Set-Cookie", cookie);
        return (owner, csrf, result.Principal!.FindFirstValue("session")!);
    }
    internal static async Task HandleAsync(HttpListenerContext transport, string clientIp)
    {
        transport.Response.Headers["Cache-Control"] = "no-store";
        transport.Response.Headers["X-Content-Type-Options"] = "nosniff";
        if (!Enabled) { await Reply(transport, null, 503, new { error = "Customer accounts are not enabled on this instance." }); return; }
        using var scope = Provider.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Method = transport.Request.HttpMethod;
        context.Request.Scheme = RuntimeSecurity.IsProduction ? "https" : transport.Request.Url!.Scheme;
        context.Request.Host = new HostString(transport.Request.Url!.Authority);
        context.Request.Path = transport.Request.Url.AbsolutePath;
        foreach (var key in transport.Request.Headers.AllKeys) if (key is not null) context.Request.Headers[key] = transport.Request.Headers[key];
        var result = await context.AuthenticateAsync(Scheme);
        if (result.Succeeded) context.User = result.Principal!;
        var antiforgery = scope.ServiceProvider.GetRequiredService<IAntiforgery>();
        var route = transport.Request.Url.AbsolutePath["/api/account".Length..].TrimEnd('/');
        var store = scope.ServiceProvider.GetRequiredService<CustomerStore>();
        try
        {
            if (transport.Request.HttpMethod == "GET" && route == "/csrf")
            { await Reply(transport, context, 200, new { csrfToken = antiforgery.GetAndStoreTokens(context).RequestToken }); return; }
            if (transport.Request.HttpMethod == "GET" && route == "")
            {
                var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userId is null) { await Reply(transport, context, 401, new { error = "Sign in to view your account." }); return; }
                var user = store.FindUser(userId)!;
                var entitlements = store.Entitlements(userId).Select(e => new { e.Id, e.Tier, e.Expires, e.SubscriptionStatus, e.EmailState,
                    state = State(e, store.Now), licenseState = LicenseState(e, store.Now), activationRecorded = e.Activated, e.Activated });
                // Free PDF demo remains free: no commercial product access is fabricated.
                var availablePlans = new List<object>();
                try { foreach (var plan in StripeFulfillment.LoadPlans()) availablePlans.Add(new { priceId = plan.Key, tier = plan.Value.Tier, mode = plan.Value.Mode }); }
                catch (InvalidOperationException) { /* No purchase control when mappings are unavailable. */ }
                await Reply(transport, context, 200, new { userId, email = CustomerStore.NormalizeEmail(user.Email), emailVerified = user.EmailVerified, accountState = "VERIFIED",
                    csrfToken = antiforgery.GetAndStoreTokens(context).RequestToken, entitlements, plans = availablePlans,
                    canPurchase = store.CanPurchase(userId), pdfDemoRoute = "/pdf" }); return;
            }
            if (transport.Request.HttpMethod != "POST") { await Reply(transport, context, 405, new { error = "Unsupported account method." }); return; }
            var expectedOrigin = RuntimeSecurity.IsProduction ? PublicOrigin : transport.Request.Url.GetLeftPart(UriPartial.Authority);
            if (!Uri.TryCreate(expectedOrigin, UriKind.Absolute, out var origin) || (RuntimeSecurity.IsProduction && origin.Scheme != "https") ||
                !string.Equals(transport.Request.Headers["Origin"], expectedOrigin, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(transport.Request.ContentType?.Split(';')[0], "application/json", StringComparison.OrdinalIgnoreCase))
            { await Reply(transport, context, 403, new { error = "Same-origin JSON request required." }); return; }
            await antiforgery.ValidateRequestAsync(context);
            using var body = new MemoryStream(); var buffer = new byte[4096]; int read;
            while ((read = await transport.Request.InputStream.ReadAsync(buffer)) > 0)
            { if (body.Length + read > 16 * 1024) { await Reply(transport, context, 413, new { error = "Account request too large." }); return; } body.Write(buffer, 0, read); }
            using var document = JsonDocument.Parse(body.ToArray()); var data = document.RootElement;
            if (data.ValueKind != JsonValueKind.Object) throw new ArgumentException("JSON object required.");
            string Field(string name) => String(data, name) ?? "";
            var email = Field("email"); var password = Field("password");
            if (route is "/register" or "/login" or "/request-reset" or "/request-verification")
            {
                if (store.RateLimited("auth-ip:" + clientIp, 20, TimeSpan.FromMinutes(15)) ||
                    store.RateLimited("auth-email:" + route + ":" + email.Trim().ToUpperInvariant(), route == "/login" ? 10 : 3, TimeSpan.FromMinutes(15)))
                { await Reply(transport, context, 429, new { error = "Too many account requests. Try again later." }); return; }
            }
            if (route == "/register")
            {
                var user = store.Register(email, password);
                if (!user.EmailVerified && !await AccountEmail(store, user, "verify"))
                { await Reply(transport, context, 503, new { code = "verification_email_unavailable", nextAction = "request-verification", error = "Verification email could not be sent. Your account remains unverified. Request another verification email later or contact support; do not pay again." }); return; }
                await Reply(transport, context, 200, new { message = "If your account needs verification, check your email before signing in." }); return;
            }
            if (route == "/login")
            {
                if (password.Length > 128) throw new ArgumentException("Invalid credentials.");
                var user = store.CheckPassword(email, password);
                if (user is null) { await Reply(transport, context, 401, new { code = "invalid_credentials", error = "Email or password is incorrect. Use account recovery if you forgot your password." }); return; }
                if (!user.EmailVerified)
                { await Reply(transport, context, 401, new { code = "email_not_verified", accountState = "UNVERIFIED", nextAction = "request-verification", error = "Your email is not verified. Open your verification email or request a new link in Account recovery below." }); return; }
                var session = store.StartSession(user);
                await context.SignInAsync(Scheme, new ClaimsPrincipal(new ClaimsIdentity(new[] {
                    new Claim(ClaimTypes.NameIdentifier, user.Id), new Claim("session", session) }, Scheme)),
                    new AuthenticationProperties { IsPersistent = false, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8) });
                await Reply(transport, context, 200, new { message = "Signed in." }); return;
            }
            if (route is "/request-reset" or "/request-verification")
            {
                CustomerStore.User? user = null; try { user = store.FindUser(CustomerStore.NormalizeEmail(email), true); } catch (ArgumentException) { }
                // Uniform response does not reveal whether an account exists or its verification state.
                if (user is not null && user.Status == "ACTIVE" && (route == "/request-reset" || !user.EmailVerified))
                    if (!await AccountEmail(store, user, route == "/request-reset" ? "reset" : "verify")) InternalAuditLogger.Error("Account email unavailable; token can be replaced by a later request.");
                await Reply(transport, context, 200, new { message = "If the account is eligible, check your inbox. If no email arrives, request another later." }); return;
            }
            if (route is "/verify" or "/reset")
            {
                var ok = store.ConsumeToken(Field("token"), route == "/verify" ? "verify" : "reset", password);
                await Reply(transport, context, ok ? 200 : 400, new { message = ok ?
                    route == "/verify" ? "Email verified. Sign in with your password." :
                    "Password reset. Sign in with your new password. If your email is still unverified, request a new verification link in Account recovery." :
                    "The link is invalid, expired or already used. Request a new link in Account recovery." }); return;
            }
            var ownerId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (ownerId is null) { await Reply(transport, context, 401, new { error = "Sign in first." }); return; }
            if (route == "/logout")
            {
                store.EndSession(context.User.FindFirstValue("session")!); await context.SignOutAsync(Scheme);
                await Reply(transport, context, 200, new { message = "Signed out." }); return;
            }
            if (route is "/license" or "/activate" or "/retry-email")
            {
                var entitlement = store.Entitlements(ownerId, Field("entitlementId")).SingleOrDefault();
                if (entitlement is null) { await Reply(transport, context, 404, new { error = "Entitlement not found." }); return; }
                if (route == "/license")
                { await Reply(transport, context, 200, new { licenseKey = entitlement.LicenseKey, expires = entitlement.Expires }); return; }
                if (route == "/activate")
                {
                    var ok = store.Activate(ownerId, entitlement.Id, Field("deviceId"));
                    await Reply(transport, context, ok ? 200 : 409, new { message = ok ? "Signed license validated and device activation recorded." : "Activation denied: invalid license, expired entitlement, unavailable subscription or device limit." }); return;
                }
                var accepted = await new PaidFulfillment(store, new StripeGateway()).NotifyAsync(entitlement.Id);
                await Reply(transport, context, accepted ? 200 : 503, new { message = accepted ? "Email provider accepted the notification." : "Email delivery requires another retry; your license is retained." }); return;
            }
            if (route == "/checkout")
            {
                var plans = StripeFulfillment.LoadPlans(); var priceId = Field("priceId");
                if (!plans.TryGetValue(priceId, out var plan)) throw new ArgumentException("Unsupported price.");
                var user = store.FindUser(ownerId)!; var nonce = store.CheckoutNonce(ownerId, priceId); var gateway = new StripeGateway();
                var customerId = store.CustomerId(ownerId);
                if (customerId is null)
                {
                    using var customer = await gateway.PostAsync("customers", new() { ["email"] = user.Email, ["metadata[lightning_user_id]"] = ownerId }, "lightning-customer-" + ownerId);
                    customerId = String(customer.RootElement, "id") ?? throw new InvalidOperationException("Stripe customer creation returned no ID.");
                    store.LinkCustomer(ownerId, customerId);
                }
                // Check Stripe as well as local state before offering another paid subscription.
                using var subscriptions = await gateway.GetAsync("subscriptions?customer=" + Uri.EscapeDataString(customerId) + "&status=all&limit=100");
                if (subscriptions.RootElement.GetProperty("has_more").GetBoolean() || subscriptions.RootElement.GetProperty("data").EnumerateArray().Any(s => String(s, "status") is "active" or "trialing" or "past_due" or "incomplete" or "unpaid" or "paused"))
                    throw new InvalidOperationException("A subscription already exists; use billing management.");
                using var session = await gateway.PostAsync("checkout/sessions", new() { ["mode"] = plan.Mode, ["customer"] = customerId,
                    ["client_reference_id"] = ownerId, ["metadata[lightning_user_id]"] = ownerId, ["line_items[0][price]"] = priceId,
                    ["line_items[0][quantity]"] = "1", ["success_url"] = PublicOrigin + "/dashboard?purchase=success", ["cancel_url"] = PublicOrigin + "/account" }, "lightning-checkout-" + nonce);
                store.RecordCheckout(ownerId, nonce, String(session.RootElement, "id") ?? throw new InvalidOperationException("Stripe session ID missing."));
                await Reply(transport, context, 200, new { url = StripeUrl(session.RootElement, "checkout.stripe.com") }); return;
            }
            if (route == "/billing")
            {
                var customerId = store.CustomerId(ownerId) ?? throw new InvalidOperationException("No Stripe customer is linked to this account.");
                using var portal = await new StripeGateway().PostAsync("billing_portal/sessions", new() { ["customer"] = customerId, ["return_url"] = PublicOrigin + "/account" }, "lightning-portal-" + Guid.NewGuid().ToString("N"));
                await Reply(transport, context, 200, new { url = StripeUrl(portal.RootElement, "billing.stripe.com") }); return;
            }
            await Reply(transport, context, 404, new { error = "Account action not found." });
        }
        catch (AntiforgeryValidationException) { await Reply(transport, context, 403, new { error = "Refresh the account page before submitting this request." }); }
        catch (ArgumentException ex) { await Reply(transport, context, 400, new { error = ex.Message }); }
        catch (JsonException) { await Reply(transport, context, 400, new { error = "Invalid account request." }); }
        catch (Exception ex)
        {
            InternalAuditLogger.Error($"Customer action failed: {ex.GetType().Name}");
            await Reply(transport, context, 503, new { error = "Account action unavailable. No payment success is implied. Retry later or contact support." });
        }
    }
    internal static string State(CustomerStore.Entitlement e, long now) => e.SubscriptionStatus switch
    { "past_due" or "unpaid" => "PAST_DUE", "canceled" => "CANCELED", _ => e.Expires <= now ? "EXPIRED" :
        !LightningSPP.Licensing.LicenseKey.Validate(e.LicenseKey, out var license) || license is null || !license.IsValid() ? "FULFILLMENT_FAILED" :
        e.SubscriptionStatus is not ("active" or "paid") ? "PROVISIONING" : e.Activated ? "ACTIVE" : "PAYMENT_CONFIRMED" };
    internal static string LicenseState(CustomerStore.Entitlement e, long now) =>
        State(e, now) is "ACTIVE" or "PAYMENT_CONFIRMED" ? "ACTIVE" : State(e, now);
    private static string StripeUrl(JsonElement value, string hostname)
    {
        var url = String(value, "url");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != hostname) throw new InvalidOperationException("Unexpected Stripe redirect.");
        return url!;
    }
    private static async Task<bool> AccountEmail(CustomerStore store, CustomerStore.User user, string purpose)
    {
        var token = store.CreateToken(user, purpose, purpose == "reset" ? TimeSpan.FromHours(1) : TimeSpan.FromHours(24));
        var link = PublicOrigin + "/account#" + purpose + "=" + Uri.EscapeDataString(token);
        var result = await EmailService.TrySendEmailAsync(user.Email, purpose == "reset" ? "Reset your Lightning SPP password" : "Verify your Lightning SPP email",
            "Open this single-use link to " + (purpose == "reset" ? "reset your password" : "verify your email") + ":\n" + link + "\nIf you did not request this, ignore this email.");
        return result.Sent;
    }
    private static async Task Reply(HttpListenerContext transport, HttpContext? context, int status, object data)
    {
        if (context is not null)
            foreach (var cookie in context.Response.Headers.SetCookie) if (cookie is not null) transport.Response.Headers.Add("Set-Cookie", cookie);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(data, Json);
        transport.Response.StatusCode = status; transport.Response.ContentType = "application/json; charset=utf-8";
        transport.Response.ContentLength64 = bytes.Length; await transport.Response.OutputStream.WriteAsync(bytes); transport.Response.Close();
    }
}
