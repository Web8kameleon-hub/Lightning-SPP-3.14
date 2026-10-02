using System.Security.Cryptography;
using System.Net;
using System.Net.Sockets;
using System.Text;
using LightningSPP.Licensing;
using wwwmmm.Services;

namespace wwwmmm.Web;

internal static class LicenseApiServer
{
    internal const int MaxBodyBytes = 1024 * 1024;

    private static string GetClientIp(HttpListenerRequest request)
    {
        var address = request.RemoteEndPoint?.Address;
        var trustProxy = bool.TryParse(Environment.GetEnvironmentVariable("LSPP_TRUST_PROXY_HEADERS"), out var trusted) && trusted;
        // Only a loopback reverse proxy may supply the client address; never trust CF/XFF from clients.
        if (trustProxy && address != null && IPAddress.IsLoopback(address) &&
            IPAddress.TryParse(request.Headers["X-Real-IP"], out var forwarded))
            return forwarded.ToString();
        return address?.ToString() ?? "unknown";
    }

    public static async Task RunAsync(int port, CancellationToken cancellationToken = default)
    {
        RuntimeSecurity.ValidateProduction();
        using var listener = new HttpListener();
        var limiter = new RequestRateLimiter();
        var slots = new SemaphoreSlim(64);

        var inContainer = string.Equals(
            Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
            "true",
            StringComparison.OrdinalIgnoreCase);
        var bindHost = inContainer ? "+" : "localhost";
        listener.Prefixes.Add($"http://{bindHost}:{port}/");
        listener.Start();

        Console.WriteLine($"REST API + Dashboard running on http://localhost:{port}");
        Console.WriteLine("Endpoints: /api/health, /api/metrics, /api/alerts, /api/stigma/schema, /api/license/status, /api/license/issue, /api/license/activate, /api/license/signing-status, /api/stripe/webhook");
        Console.WriteLine("Dashboard: /dashboard, Landing page: /");
        InternalAuditLogger.Info($"Server started on port {port}");

        using var reg = cancellationToken.Register(() =>
        {
            if (listener.IsListening)
            {
                listener.Stop();
            }
        });

        while (listener.IsListening)
        {
            HttpListenerContext? ctx = null;
            try
            {
                ctx = await listener.GetContextAsync();
                if (!slots.Wait(0))
                {
                    await TextAsync(ctx, HttpStatusCode.ServiceUnavailable, "ok=false\nerror=Server busy");
                    continue;
                }
                _ = HandleSafelyAsync(ctx, limiter, slots);
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }

        InternalAuditLogger.Info("Server stopped");
    }

    private static async Task HandleSafelyAsync(HttpListenerContext context, RequestRateLimiter limiter, SemaphoreSlim slots)
    {
        try { await HandleRequestAsync(context, limiter); }
        catch (Exception) { context.Response.Abort(); }
        finally { slots.Release(); }
    }

    private static async Task HandleRequestAsync(HttpListenerContext context, RequestRateLimiter limiter)
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers["Cache-Control"] = "no-store";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        // Browser access is same-origin. Do not grant cross-origin access to licensing APIs.
        if (context.Request.HttpMethod == "OPTIONS")
        {
            await TextAsync(context, HttpStatusCode.MethodNotAllowed, "ok=false\nerror=Method not allowed");
            return;
        }
        if (context.Request.ContentLength64 > MaxBodyBytes)
        {
            await TextAsync(context, HttpStatusCode.RequestEntityTooLarge, "ok=false\nerror=Request too large");
            return;
        }

        // Rate limiting
        var clientIp = GetClientIp(context.Request);
        if (limiter.IsLimited(clientIp, DateTimeOffset.UtcNow))
        {
            context.Response.Headers["Retry-After"] = "60";
            await TextAsync(context, HttpStatusCode.TooManyRequests, "ok=false\nerror=Rate limit exceeded");
            return;
        }

        var path = context.Request.Url?.AbsolutePath?.TrimEnd('/') ?? "/";
        if (string.IsNullOrWhiteSpace(path))
        {
            path = "/";
        }

        try
        {
            if (path == "/" || path == "/index.html")
            {
                await ServeStaticAsync(context, "landing.html", "text/html; charset=utf-8");
                return;
            }

            if (path == "/dashboard")
            {
                await ServeStaticAsync(context, "dashboard.html", "text/html; charset=utf-8");
                return;
            }

            if (path == "/swagger")
            {
                await ServeStaticAsync(context, "swagger.html", "text/html; charset=utf-8");
                return;
            }

            if (path == "/openapi.json")
            {
                await ServeStaticAsync(context, "openapi.json", "application/json; charset=utf-8");
                return;
            }

            if (path == "/api/health")
            {
                await TextAsync(context, HttpStatusCode.OK, $"ok=true\nservice=LightningSPP\ntimestamp={DateTime.UtcNow:O}");
                return;
            }

            if (path == "/api/metrics")
            {
                await TextAsync(context, HttpStatusCode.OK, LightningMetrics.ToText());
                return;
            }

            if (path == "/api/alerts")
            {
                await TextAsync(context, HttpStatusCode.OK, LightningMetrics.AlertsToText());
                return;
            }

            if (path == "/api/stigma/schema")
            {
                var payload = StigmaSchema.ToJson();
                var buffer = Encoding.UTF8.GetBytes(payload);
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.ContentLength64 = buffer.LongLength;
                await context.Response.OutputStream.WriteAsync(buffer);
                context.Response.Close();
                return;
            }

            if (path == "/api/stripe/webhook" && context.Request.HttpMethod == "POST")
            {
                var payload = await ReadBodyAsync(context.Request);
                var signature = context.Request.Headers["Stripe-Signature"] ?? string.Empty;

                if (!StripeBilling.TryVerifyWebhook(
                        payload,
                        signature,
                        out var stripeEvent,
                        out var destination,
                        out var verificationError))
                {
                    InternalAuditLogger.Error($"Stripe webhook rejected: {verificationError}");
                    await TextAsync(context, HttpStatusCode.BadRequest, "ok=false\nerror=Invalid webhook signature");
                    return;
                }

                if (stripeEvent is null)
                {
                    await TextAsync(context, HttpStatusCode.BadRequest, "ok=false\nerror=Missing Stripe event");
                    return;
                }
                var outcome = await StripeFulfillment.ProcessAsync(stripeEvent);
                await TextAsync(context, outcome.Status, outcome.Message);
                return;
            }

            if (path == "/api/billing/license" && context.Request.HttpMethod == "GET")
            {
                if (!IsAdminAuthorized(context.Request))
                {
                    await TextAsync(context, HttpStatusCode.Unauthorized, "ok=false\nerror=Unauthorized");
                    return;
                }
                var paymentId = context.Request.QueryString["paymentId"];
                if (string.IsNullOrWhiteSpace(paymentId)) throw new ArgumentException("Payment ID required.");
                var receipt = StripeFulfillment.ReadReceipt(paymentId);
                await TextAsync(context, receipt == null ? HttpStatusCode.NotFound : HttpStatusCode.OK,
                    receipt == null ? "ok=false\nerror=License not fulfilled" : $"ok=true\nkey={receipt.LicenseKey}\nexpires={receipt.ExpiresUtc:O}");
                return;
            }

            if (path == "/api/license/status")
            {
                var license = LicenseManager.GetActiveLicense();
                var statusText = string.Join('\n',
                    $"hasLicense={license != null}",
                    $"valid={license?.IsValid() ?? false}",
                    $"tier={license?.Tier.ToString() ?? "Free"}",
                    $"usageThisMonth={LicenseManager.GetUsageThisMonth()}");
                await TextAsync(context, HttpStatusCode.OK, statusText);
                return;
            }

            if (path == "/api/license/activate" && context.Request.HttpMethod == "POST")
            {
                if (!IsAdminAuthorized(context.Request))
                {
                    await TextAsync(context, HttpStatusCode.Unauthorized, "ok=false\nerror=Unauthorized");
                    return;
                }
                var form = await ReadFormAsync(context.Request);
                var key = ValueFromQueryOrForm(context.Request, form, "key");
                if (string.IsNullOrWhiteSpace(key))
                {
                    await TextAsync(context, HttpStatusCode.BadRequest, "ok=false\nerror=Missing key");
                    return;
                }

                var ok = LicenseManager.ActivateLicense(key);
                await TextAsync(context, ok ? HttpStatusCode.OK : HttpStatusCode.BadRequest, $"ok={ok.ToString().ToLowerInvariant()}");
                return;
            }

            if (path == "/api/license/issue" && context.Request.HttpMethod == "POST")
            {
                if (!IsAdminAuthorized(context.Request))
                {
                    context.Response.Headers["WWW-Authenticate"] = "Bearer";
                    await TextAsync(context, HttpStatusCode.Unauthorized, "ok=false\nerror=Unauthorized");
                    return;
                }

                var form = await ReadFormAsync(context.Request);
                var email = ValueFromQueryOrForm(context.Request, form, "email");
                if (string.IsNullOrWhiteSpace(email) || !System.Net.Mail.MailAddress.TryCreate(email, out _))
                    throw new ArgumentException("Valid customer email required.");
                var company = ValueFromQueryOrForm(context.Request, form, "company") ?? string.Empty;
                var tier = ValueFromQueryOrForm(context.Request, form, "tier") ?? "free";
                var devices = ParseInt(ValueFromQueryOrForm(context.Request, form, "devices"), 1);
                var validityDays = ParseInt(ValueFromQueryOrForm(context.Request, form, "validityDays"), 365);
                var internalSecurity = ParseBool(ValueFromQueryOrForm(context.Request, form, "useInternalQuantumSecurity"), false);

                var result = LicenseManager.CreateLicenseKey(
                    email,
                    company,
                    tier,
                    devices,
                    validityDays,
                    internalSecurity,
                    LicenseManager.PaymentMode.Manual);

                var issuedText = string.Join('\n',
                    "ok=true",
                    $"key={result.Key}",
                    $"tier={result.Tier}",
                    $"expires={result.ExpirationDate:O}");
                await TextAsync(context, HttpStatusCode.OK, issuedText);
                return;
            }

            if (path == "/api/license/signing-status" && context.Request.HttpMethod == "GET")
            {
                if (!IsAdminAuthorized(context.Request))
                {
                    context.Response.Headers["WWW-Authenticate"] = "Bearer";
                    await TextAsync(context, HttpStatusCode.Unauthorized, "ok=false\nerror=Unauthorized");
                    return;
                }

                await TextAsync(context, HttpStatusCode.OK, LicenseKey.GetSigningStatusText());
                return;
            }

            await TextAsync(context, HttpStatusCode.NotFound, "ok=false\nerror=Not found");
        }
        catch (RequestBodyTooLargeException)
        {
            await TextAsync(context, HttpStatusCode.RequestEntityTooLarge, "ok=false\nerror=Request too large");
        }
        catch (OperationCanceledException)
        {
            await TextAsync(context, HttpStatusCode.RequestTimeout, "ok=false\nerror=Request timed out");
        }
        catch (ArgumentException)
        {
            await TextAsync(context, HttpStatusCode.BadRequest, "ok=false\nerror=Invalid input");
        }
        catch (Exception ex)
        {
            InternalAuditLogger.Error($"API error on {path}: {ex.Message}");
            await TextAsync(context, HttpStatusCode.InternalServerError, "ok=false\nerror=Internal server error");
        }
    }

    private static async Task ServeStaticAsync(HttpListenerContext context, string fileName, string contentType)
    {
        // Prevent path traversal — allow only simple file names (no slashes or dots outside extension)
        var safeName = Path.GetFileName(fileName);
        if (!string.Equals(safeName, fileName, StringComparison.Ordinal) || fileName.Contains('.', StringComparison.Ordinal) && Path.GetDirectoryName(fileName) != string.Empty)
        {
            await TextAsync(context, HttpStatusCode.BadRequest, "ok=false\nerror=Invalid file name");
            return;
        }

        var wwwroot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Web", "wwwroot"));
        var fullPath = Path.GetFullPath(Path.Combine(wwwroot, safeName));
        if (!fullPath.StartsWith(wwwroot, StringComparison.OrdinalIgnoreCase))
        {
            await TextAsync(context, HttpStatusCode.Forbidden, "ok=false\nerror=Forbidden");
            return;
        }

        if (!File.Exists(fullPath))
        {
            // Fallback for local dev runs from repository root.
            var devRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Web", "wwwroot"));
            fullPath = Path.GetFullPath(Path.Combine(devRoot, safeName));
            if (!fullPath.StartsWith(devRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
            {
                await TextAsync(context, HttpStatusCode.NotFound, $"ok=false\nerror=Missing static file: {safeName}");
                return;
            }
        }

        if (!File.Exists(fullPath))
        {
            await TextAsync(context, HttpStatusCode.NotFound, $"ok=false\nerror=Missing static file: {safeName}");
            return;
        }

        var bytes = await File.ReadAllBytesAsync(fullPath);
        context.Response.StatusCode = (int)HttpStatusCode.OK;
        context.Response.ContentType = contentType;
        context.Response.ContentLength64 = bytes.LongLength;
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }

    private sealed class RequestBodyTooLargeException : Exception { }

    private static async Task<string> ReadBodyAsync(HttpListenerRequest request)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var body = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await request.InputStream.ReadAsync(buffer, timeout.Token)) > 0)
        {
            if (body.Length + count > MaxBodyBytes) throw new RequestBodyTooLargeException();
            body.Write(buffer, 0, count);
        }
        return Encoding.UTF8.GetString(body.ToArray());
    }

    private static async Task<Dictionary<string, string>> ReadFormAsync(HttpListenerRequest request)
    {
        var form = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var body = await ReadBodyAsync(request);
        if (string.IsNullOrWhiteSpace(body))
        {
            return form;
        }

        var pairs = body.Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var pair in pairs)
        {
            var idx = pair.IndexOf('=');
            if (idx <= 0)
            {
                continue;
            }

            var key = WebUtility.UrlDecode(pair[..idx]);
            var value = WebUtility.UrlDecode(pair[(idx + 1)..]);
            if (!string.IsNullOrWhiteSpace(key))
            {
                form[key] = value ?? string.Empty;
            }
        }

        return form;
    }

    private static string? ValueFromQueryOrForm(HttpListenerRequest request, Dictionary<string, string> form, string name)
    {
        var queryValue = request.QueryString[name];
        if (!string.IsNullOrWhiteSpace(queryValue))
        {
            return queryValue;
        }

        return form.TryGetValue(name, out var formValue) ? formValue : null;
    }

    private static int ParseInt(string? value, int fallback)
    {
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static bool ParseBool(string? value, bool fallback)
    {
        return bool.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static bool IsAdminAuthorized(HttpListenerRequest request)
    {
        var adminToken = Environment.GetEnvironmentVariable("LSPP_ADMIN_TOKEN");
        if (string.IsNullOrWhiteSpace(adminToken))
            return false;

        var authHeader = request.Headers["Authorization"];
        var providedToken = authHeader?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true
            ? authHeader[7..]
            : null;

        return providedToken != null && RuntimeSecurity.SecretEquals(adminToken, providedToken);
    }

    private static async Task TextAsync(HttpListenerContext context, HttpStatusCode statusCode, string payload)
    {
        var buffer = Encoding.UTF8.GetBytes(payload);
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "text/plain; charset=utf-8";
        context.Response.ContentLength64 = buffer.LongLength;
        await context.Response.OutputStream.WriteAsync(buffer);
        context.Response.Close();
    }
}
