using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using LightningSPP.Licensing;
using wwwmmm.Configuration;
using wwwmmm.Services;

namespace wwwmmm.Web;

internal static class LicenseApiServer
{
    // Upper bound on any request body this server will buffer into memory, enforced
    // while streaming (not just via the often-absent/spoofable Content-Length header),
    // so a chunked-transfer request can't exhaust memory before a size check runs.
    internal const int MaxBodyBytes = 1024 * 1024;

    // Rate limiting: max requests per window per IP
    private const int RateLimitMaxRequests = 30;
    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromMinutes(1);
    private static readonly ConcurrentDictionary<string, (int Count, DateTime WindowStart)> _rateLimits = new();

    // A tighter, dedicated limit for /api/try/process: it runs real CPU-bound image
    // processing for anonymous, unauthenticated visitors, so it must not share the
    // general 30/min budget that covers lightweight endpoints like /api/health.
    private const int TryRateLimitMaxRequests = 10;
    private static readonly TimeSpan TryRateLimitWindow = TimeSpan.FromMinutes(5);
    private static readonly ConcurrentDictionary<string, (int Count, DateTime WindowStart)> _tryRateLimits = new();
    private const long TryMaxUploadBytes = 8 * 1024 * 1024; // 8 MB

    private static readonly HashSet<string> AllowedTryContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/jpg", "image/bmp", "image/tiff", "image/gif"
    };

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

    private static bool IsTryRateLimited(string ip)
    {
        var now = DateTime.UtcNow;
        var entry = _tryRateLimits.GetOrAdd(ip, _ => (0, now));
        if (now - entry.WindowStart > TryRateLimitWindow)
            entry = (0, now);
        entry = (entry.Count + 1, entry.WindowStart);
        _tryRateLimits[ip] = entry;
        return entry.Count > TryRateLimitMaxRequests;
    }

    public static async Task RunAsync(int port, CancellationToken cancellationToken = default)
    {
        RuntimeSecurity.ValidateProduction();
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
        context.Response.Headers["Access-Control-Allow-Headers"] = "Authorization, Content-Type, Stripe-Signature, X-Stigma-Profile";

        // Pre-flight
        if (context.Request.HttpMethod == "OPTIONS")
        {
            context.Response.StatusCode = (int)HttpStatusCode.NoContent;
            context.Response.Close();
            return;
        }

        var path = context.Request.Url?.AbsolutePath?.TrimEnd('/') ?? "/";
        if (string.IsNullOrWhiteSpace(path))
        {
            path = "/";
        }

        if (path == "/dashboard" || path.StartsWith("/api/", StringComparison.Ordinal))
        {
            context.Response.Headers["X-Robots-Tag"] = "noindex";
        }

        // Public pages and their assets must remain crawlable without consuming
        // the API budget. Unknown paths still have the general abuse limit.
        var isPublicRead = (context.Request.HttpMethod is "GET" or "HEAD") && path is
            "/" or "/index.html" or "/features" or "/technology" or "/try" or
            "/pricing" or "/benchmarks" or "/swagger" or "/openapi.json" or
            "/robots.txt" or "/sitemap.xml" or "/site.css" or "/try.js" or
            "/favicon.ico" or "/favicon-16x16.png" or "/favicon-32x32.png" or
            "/apple-touch-icon.png" or "/site.webmanifest";

        // Rate limiting
        var clientIp = ResolveClientIp(context.Request);
        if (!isPublicRead && path != "/api/health" && IsRateLimited(clientIp))
        {
            context.Response.Headers["Retry-After"] = "60";
            await TextAsync(context, HttpStatusCode.TooManyRequests, "ok=false\nerror=Rate limit exceeded");
            return;
        }

        try
        {
            if (path == "/index.html" && context.Request.HttpMethod is "GET" or "HEAD")
            {
                context.Response.StatusCode = (int)HttpStatusCode.MovedPermanently;
                context.Response.RedirectLocation = "/";
                context.Response.Close();
                return;
            }
            if (path == "/site.css" || path == "/dashboard.js" || path == "/try.js")
            {
                var jsOrCss = path.EndsWith(".css") ? "text/css; charset=utf-8" : "text/javascript; charset=utf-8";
                await ServeStaticAsync(context, path[1..], jsOrCss);
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

            if (path == "/try")
            {
                await ServeStaticAsync(context, "try.html", "text/html; charset=utf-8");
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

            if (path == "/favicon.ico")
            {
                await ServeStaticAsync(context, "favicon.ico", "image/x-icon");
                return;
            }

            if (path is "/favicon-16x16.png" or "/favicon-32x32.png" or "/favicon-48x48.png"
                or "/apple-touch-icon.png" or "/android-chrome-192x192.png" or "/android-chrome-512x512.png")
            {
                await ServeStaticAsync(context, path[1..], "image/png");
                return;
            }

            if (path == "/site.webmanifest")
            {
                await ServeStaticAsync(context, "site.webmanifest", "application/manifest+json; charset=utf-8");
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

                if (stripeEvent is null || !StripeBilling.TryMarkEventProcessed(stripeEvent.Id))
                {
                    await TextAsync(context, HttpStatusCode.OK, "ok=true\nduplicate=true");
                    return;
                }

                InternalAuditLogger.Info(
                    $"Stripe webhook verified: destination={destination}, type={stripeEvent.Type}, id={stripeEvent.Id}");

                if (StripeBilling.TryGetCheckoutFulfillment(stripeEvent, out var customerEmail, out var purchasedTier, out var sessionId))
                {
                    await FulfillCheckoutAsync(customerEmail, purchasedTier, sessionId);
                }

                await TextAsync(
                    context,
                    HttpStatusCode.OK,
                    $"ok=true\ndestination={destination}\neventType={stripeEvent.Type}");
                return;
            }

            if (path == "/api/try/process" && context.Request.HttpMethod == "POST")
            {
                await HandleTryProcessAsync(context, clientIp);
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
        catch (RequestBodyTooLargeException)
        {
            await TextAsync(context, HttpStatusCode.RequestEntityTooLarge, "ok=false\nerror=Request too large");
        }
        catch (Exception ex)
        {
            InternalAuditLogger.Error($"API error on {path}: {ex.Message}");
            await TextAsync(context, HttpStatusCode.InternalServerError, "ok=false\nerror=Internal server error");
        }
    }

    /// <summary>
    /// Issues a license key for a paid Checkout Session and emails it to the customer.
    /// Deliberately swallows and logs all failures: the webhook must still return 200 to
    /// Stripe (the event is already marked processed, so Stripe will not retry), and a
    /// failure here must surface as an audit-log alert for manual follow-up rather than
    /// as an HTTP error that could be misread as "payment rejected".
    /// </summary>
    private static async Task FulfillCheckoutAsync(string email, string tier, string sessionId)
    {
        try
        {
            var license = LicenseManager.CreateLicenseKey(
                email,
                company: email,
                tier,
                devices: 1,
                validityDays: tier.Equals("paygo", StringComparison.OrdinalIgnoreCase) ? 30 : 365,
                useInternalQuantumSecurity: false);

            InternalAuditLogger.Info(
                $"Stripe checkout fulfilled: session={sessionId}, tier={tier}, email={email}, expires={license.ExpirationDate:yyyy-MM-dd}");

            var (sent, emailError) = await EmailService.TrySendLicenseKeyEmailAsync(
                email, tier, license.Key, license.ExpirationDate);

            if (!sent)
            {
                InternalAuditLogger.Error(
                    $"Stripe checkout fulfilled but license email FAILED to send: session={sessionId}, email={email}, error={emailError}. Manual delivery required.");
            }
        }
        catch (Exception ex)
        {
            InternalAuditLogger.Error(
                $"Stripe checkout fulfillment FAILED: session={sessionId}, tier={tier}, email={email}, error={ex.Message}. Manual license issuance required.");
        }
    }

    /// <summary>
    /// Public, unauthenticated demo: runs one real Scan + Process job on an uploaded image
    /// and returns the processed preview plus per-job timing, so a visitor can see Lightning
    /// SPP work without installing anything. Deliberately image-only: this codebase has no
    /// PDF renderer, so a PDF upload would otherwise be visualized as raw-byte noise instead
    /// of real page content, which would be misleading.
    /// </summary>
    private static async Task HandleTryProcessAsync(HttpListenerContext context, string clientIp)
    {
        if (IsTryRateLimited(clientIp))
        {
            context.Response.Headers["Retry-After"] = "300";
            await JsonAsync(context, HttpStatusCode.TooManyRequests,
                "{\"ok\":false,\"error\":\"Too many demo requests from this address. Please wait a few minutes and try again.\"}");
            return;
        }

        var contentType = (context.Request.ContentType ?? string.Empty).Split(';')[0].Trim();
        if (!AllowedTryContentTypes.Contains(contentType))
        {
            await JsonAsync(context, HttpStatusCode.BadRequest,
                "{\"ok\":false,\"error\":\"Unsupported file type. Upload a JPEG, PNG, BMP, TIFF or GIF image.\"}");
            return;
        }

        if (context.Request.ContentLength64 <= 0 || context.Request.ContentLength64 > TryMaxUploadBytes)
        {
            // Drain (not just reject) the oversized body before closing the response.
            // Responding and closing the connection while the client is still mid-upload
            // can reset the TCP connection under it, surfacing as a client-side
            // SocketException instead of the intended clean 400 - observed intermittently
            // in CI. Draining up to a bounded cap avoids that without trusting an
            // unbounded, possibly-spoofed Content-Length header.
            await DrainRequestBodyAsync(context.Request, TryMaxUploadBytes * 2);
            await JsonAsync(context, HttpStatusCode.BadRequest,
                "{\"ok\":false,\"error\":\"File must be larger than 0 bytes and no more than 8 MB.\"}");
            return;
        }

        if (!TryParseScanMode(context.Request.Headers["X-Stigma-Profile"], out var scanMode, out var profileName))
        {
            await JsonAsync(context, HttpStatusCode.BadRequest,
                "{\"ok\":false,\"error\":\"Unknown profile. Use lightning, tidewave, resonance or nanodecibel.\"}");
            return;
        }

        byte[] originalBytes;
        await using (var buffer = new MemoryStream())
        {
            await context.Request.InputStream.CopyToAsync(buffer);
            originalBytes = buffer.ToArray();
        }

        if (originalBytes.Length == 0 || originalBytes.Length > TryMaxUploadBytes)
        {
            await JsonAsync(context, HttpStatusCode.BadRequest,
                "{\"ok\":false,\"error\":\"File must be larger than 0 bytes and no more than 8 MB.\"}");
            return;
        }

        try
        {
            var scanService = ServiceFactory.CreateScanService();
            var processingService = ServiceFactory.CreateProcessingService();

            var scanResult = await scanService.ScanAsync(originalBytes, scanMode, quality: 300);
            if (!scanResult.Success)
            {
                await JsonAsync(context, HttpStatusCode.UnprocessableEntity,
                    $"{{\"ok\":false,\"error\":{JsonEscape(scanResult.Error ?? "The file could not be read as an image.")}}}");
                return;
            }

            var processResult = await processingService.ProcessAsync(
                scanResult.Data,
                new ProcessingOptions(ProcessingEngine.Hybrid, UseAI: true, UseClisonix: true, OptimizationLevel: 3, UseStigmaMemory: true));

            var compressedBytes = processResult.Success ? processResult.ProcessedData.Length : 0;
            var compressionPercent = processResult.Success && scanResult.Data.Length > 0
                ? Math.Round(100.0 * (1 - (double)compressedBytes / scanResult.Data.Length), 1)
                : 0.0;

            LightningMetrics.RecordScan(scanResult.Metadata.ProcessingTimeMs);
            if (processResult.Success)
            {
                LightningMetrics.RecordProcess(processResult.Metadata.ProcessingTimeMs);
            }

            var afterBase64 = Convert.ToBase64String(scanResult.Data);
            var totalMs = scanResult.Metadata.ProcessingTimeMs + (processResult.Success ? processResult.Metadata.ProcessingTimeMs : 0);

            var json = $$"""
                {
                  "ok": true,
                  "profile": "{{profileName}}",
                  "scanTimeMs": {{scanResult.Metadata.ProcessingTimeMs}},
                  "processTimeMs": {{(processResult.Success ? processResult.Metadata.ProcessingTimeMs : 0)}},
                  "totalTimeMs": {{totalMs}},
                  "engine": {{JsonEscape(processResult.Success ? processResult.Metadata.Engine : "none")}},
                  "originalBytes": {{originalBytes.Length}},
                  "afterBytes": {{scanResult.Data.Length}},
                  "compressedBytes": {{compressedBytes}},
                  "compressionPercent": {{compressionPercent.ToString(System.Globalization.CultureInfo.InvariantCulture)}},
                  "afterImageBase64": "{{afterBase64}}"
                }
                """;

            InternalAuditLogger.Info(
                $"Try-it demo processed: ip={clientIp}, profile={profileName}, originalBytes={originalBytes.Length}, totalTimeMs={totalMs}");

            await JsonAsync(context, HttpStatusCode.OK, json);
        }
        catch (Exception ex)
        {
            InternalAuditLogger.Error($"Try-it demo failed: ip={clientIp}, error={ex.Message}");
            await JsonAsync(context, HttpStatusCode.InternalServerError,
                "{\"ok\":false,\"error\":\"Processing failed unexpectedly. Please try a different file.\"}");
        }
    }

    private static bool TryParseScanMode(string? header, out ScanMode mode, out string name)
    {
        var value = string.IsNullOrWhiteSpace(header) ? "lightning" : header.Trim().ToLowerInvariant();
        switch (value)
        {
            case "lightning":
                mode = ScanMode.Lightning;
                name = "lightning";
                return true;
            case "tidewave":
                mode = ScanMode.TideWave;
                name = "tidewave";
                return true;
            case "resonance":
                mode = ScanMode.Resonance;
                name = "resonance";
                return true;
            case "nanodecibel":
                mode = ScanMode.NanoDecibel;
                name = "nanodecibel";
                return true;
            default:
                mode = ScanMode.Lightning;
                name = string.Empty;
                return false;
        }
    }

    private static string JsonEscape(string value) =>
        "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", string.Empty) + "\"";

    private static async Task JsonAsync(HttpListenerContext context, HttpStatusCode statusCode, string json)
    {
        var buffer = Encoding.UTF8.GetBytes(json);
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength64 = buffer.LongLength;
        await context.Response.OutputStream.WriteAsync(buffer);
        context.Response.Close();
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
        if (context.Request.HttpMethod != "HEAD")
        {
            await context.Response.OutputStream.WriteAsync(bytes);
        }
        context.Response.Close();
    }

    private sealed class RequestBodyTooLargeException : Exception { }

    /// <summary>
    /// Reads and discards up to <paramref name="maxBytesToDrain"/> bytes of a request body
    /// that is being rejected, so the response can close cleanly without resetting the
    /// underlying TCP connection out from under a client that is still uploading. Best
    /// effort: any failure here must not prevent the caller from still sending its response.
    /// </summary>
    private static async Task DrainRequestBodyAsync(HttpListenerRequest request, long maxBytesToDrain)
    {
        try
        {
            var buffer = new byte[8192];
            long total = 0;
            int count;
            while (total < maxBytesToDrain && (count = await request.InputStream.ReadAsync(buffer)) > 0)
            {
                total += count;
            }
        }
        catch
        {
            // Ignore - this is a best-effort drain before an error response.
        }
    }

    /// <summary>
    /// Reads a request body up to <see cref="MaxBodyBytes"/>, enforced while streaming
    /// (not just via Content-Length, which a chunked-transfer request can omit or lie about).
    /// </summary>
    private static async Task<string> ReadBodyAsync(HttpListenerRequest request)
    {
        await using var body = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await request.InputStream.ReadAsync(buffer)) > 0)
        {
            if (body.Length + count > MaxBodyBytes)
            {
                throw new RequestBodyTooLargeException();
            }

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
