using System.Collections.Concurrent;
using System.Net;
using System.Text;
using LightningSPP.Licensing;

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
        var listener = new HttpListener();
        var inContainer = string.Equals(
            Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
            "true",
            StringComparison.OrdinalIgnoreCase);
        var bindHost = inContainer ? "+" : "localhost";
        listener.Prefixes.Add($"http://{bindHost}:{port}/");
        listener.Start();

        Console.WriteLine($"REST API + Dashboard running on http://localhost:{port}");
        Console.WriteLine("Endpoints: /api/health, /api/license/status, /api/license/issue, /api/license/activate");
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
        // CORS headers
        context.Response.Headers["Access-Control-Allow-Origin"] = "*";
        context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
        context.Response.Headers["Access-Control-Allow-Headers"] = "Authorization, Content-Type";

        // Pre-flight
        if (context.Request.HttpMethod == "OPTIONS")
        {
            context.Response.StatusCode = (int)HttpStatusCode.NoContent;
            context.Response.Close();
            return;
        }

        // Rate limiting
        var clientIp = context.Request.RemoteEndPoint?.Address?.ToString() ?? "unknown";
        if (IsRateLimited(clientIp))
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
                // Authorization: require LSPP_ADMIN_TOKEN environment variable
                var adminToken = Environment.GetEnvironmentVariable("LSPP_ADMIN_TOKEN");
                var authHeader = context.Request.Headers["Authorization"];
                var providedToken = authHeader?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true
                    ? authHeader[7..]
                    : null;
                if (string.IsNullOrWhiteSpace(adminToken) || !string.Equals(adminToken, providedToken, StringComparison.Ordinal))
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
