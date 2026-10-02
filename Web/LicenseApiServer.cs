using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using LightningSPP.Licensing;
using wwwmmm.Services;

namespace wwwmmm.Web;

internal static class LicenseApiServer
{
    // Rate limiting: max requests per window per IP
    private const int RateLimitMaxRequests = 30;
    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromMinutes(1);
    private static readonly ConcurrentDictionary<string, (int Count, DateTime WindowStart)> _rateLimits = new();

    private static bool IsRateLimited(string ip)
    {
        var now = DateTime.UtcNow;
        var entry = _rateLimits.GetOrAdd(ip, _ => (0, now));
        if (now - entry.WindowStart > RateLimitWindow)
            entry = (0, now);
        entry = (entry.Count + 1, entry.WindowStart);
        _rateLimits[ip] = entry;
        return entry.Count > RateLimitMaxRequests;
    }

    public static async Task RunAsync(int port, CancellationToken cancellationToken = default)
    {
        using var listener = new HttpListener();
        var inContainer = string.Equals(
            Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
            "true",
            StringComparison.OrdinalIgnoreCase);
        var bindHost = Environment.GetEnvironmentVariable("LSPP_BIND_HOST") ?? (inContainer ? "+" : "localhost");
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
                _ = Task.Run(() => HandleRequestAsync(ctx), cancellationToken);
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

    private static async Task HandleRequestAsync(HttpListenerContext context)
    {
        ApplyCorsHeaders(context);
        context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
        context.Response.Headers["Access-Control-Allow-Headers"] = "Authorization, Content-Type, Stripe-Signature";

        // Pre-flight
        if (context.Request.HttpMethod == "OPTIONS")
        {
            context.Response.StatusCode = (int)HttpStatusCode.NoContent;
            context.Response.Close();
            return;
        }

        // Rate limiting
        var clientIp = ResolveClientIp(context.Request);
        if (context.Request.Url?.AbsolutePath != "/api/health" && IsRateLimited(clientIp))
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
            if (path == "/site.css" || path == "/dashboard.js")
            {
                await ServeStaticAsync(context, path[1..], path.EndsWith(".css") ? "text/css; charset=utf-8" : "text/javascript; charset=utf-8");
                return;
            }

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

            if (path == "/features" || path == "/technology" || path == "/pricing" || path == "/benchmarks")
            {
                await ServeStaticAsync(context, path[1..] + ".html", "text/html; charset=utf-8");
                return;
            }

            if (path == "/robots.txt")
            {
                await ServeStaticAsync(context, "robots.txt", "text/plain; charset=utf-8");
                return;
            }

            if (path == "/sitemap.xml")
            {
                await ServeStaticAsync(context, "sitemap.xml", "application/xml; charset=utf-8");
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
                using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
                var payload = await reader.ReadToEndAsync();
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

                if (stripeEvent is null || !StripeBilling.TryMarkEventProcessed(stripeEvent.Id))
                {
                    await TextAsync(context, HttpStatusCode.OK, "ok=true\nduplicate=true");
                    return;
                }

                InternalAuditLogger.Info(
                    $"Stripe webhook verified: destination={destination}, type={stripeEvent.Type}, id={stripeEvent.Id}");
                await TextAsync(
                    context,
                    HttpStatusCode.OK,
                    $"ok=true\ndestination={destination}\neventType={stripeEvent.Type}");
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
                // Self-service: any customer with a valid, signed license key may activate it.
                // LicenseManager.ActivateLicense cryptographically validates the key itself,
                // so no admin credential is required here (unlike /issue and /signing-status).
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
                var email = ValueFromQueryOrForm(context.Request, form, "email") ?? "demo@example.com";
                var company = ValueFromQueryOrForm(context.Request, form, "company") ?? "Demo Company";
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
                    internalSecurity);

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

    private static async Task<Dictionary<string, string>> ReadFormAsync(HttpListenerRequest request)
    {
        var form = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
        var body = await reader.ReadToEndAsync();
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

        if (string.IsNullOrEmpty(providedToken))
            return false;

        var expected = Encoding.UTF8.GetBytes(adminToken);
        var provided = Encoding.UTF8.GetBytes(providedToken);
        return expected.Length == provided.Length &&
               CryptographicOperations.FixedTimeEquals(expected, provided);
    }

    private static string ResolveClientIp(HttpListenerRequest request)
    {
        var remote = request.RemoteEndPoint?.Address;
        var trustProxy = string.Equals(
            Environment.GetEnvironmentVariable("LSPP_TRUST_PROXY_HEADERS"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        if (trustProxy && remote is not null && IPAddress.IsLoopback(remote))
        {
            var forwarded = request.Headers["X-Forwarded-For"]?.Split(',')[0].Trim();
            if (IPAddress.TryParse(forwarded, out var forwardedIp))
                return forwardedIp.ToString();
        }

        return remote?.ToString() ?? "unknown";
    }

    private static void ApplyCorsHeaders(HttpListenerContext context)
    {
        var origin = context.Request.Headers["Origin"];
        var configured = Environment.GetEnvironmentVariable("LSPP_ALLOWED_ORIGINS");
        if (string.IsNullOrWhiteSpace(origin) || string.IsNullOrWhiteSpace(configured))
            return;

        var allowed = configured.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!allowed.Contains(origin, StringComparer.OrdinalIgnoreCase))
            return;

        context.Response.Headers["Access-Control-Allow-Origin"] = origin;
        context.Response.Headers["Vary"] = "Origin";
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
