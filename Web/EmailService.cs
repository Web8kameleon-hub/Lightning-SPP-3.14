using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mail;

namespace wwwmmm.Web;

/// <summary>
/// Email sender for license-key fulfillment. Prefers the Resend HTTP API
/// (officially recommended by Resend over SMTP, and avoids known MIME
/// quirks in the legacy System.Net.Mail.SmtpClient) when a Resend API key
/// is configured, falling back to plain SMTP for other providers.
/// Configuration is read entirely from environment variables; if nothing
/// is configured, sending is skipped and the caller is told why so it can
/// log/alert instead of silently losing a paid order.
/// </summary>
internal static class EmailService
{
    private static readonly HttpClient HttpClient = new() { BaseAddress = new Uri("https://api.resend.com/") };

    public static async Task<(bool Sent, string Error)> TrySendLicenseKeyEmailAsync(
        string toEmail,
        string tier,
        string licenseKey,
        DateTime expirationUtc)
        => await TrySendEmailAsync(toEmail, "Your Lightning SPP license key", BuildBody(tier, licenseKey, expirationUtc));

    internal static async Task<(bool Sent, string Error)> TrySendEmailAsync(string toEmail, string subject, string text)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            return (false, "Recipient email address is missing.");
        }

        // CustomerStore uses case-insensitive, uppercase identity keys. Never send that
        // storage representation to a provider that compares its test-recipient allowlist
        // case-sensitively. Delivery uses the same case-insensitive address policy.
        toEmail = DeliveryAddress(toEmail);

        var smtpHost = Environment.GetEnvironmentVariable("SMTP_HOST");
        var isResend = string.Equals(smtpHost, "smtp.resend.com", StringComparison.OrdinalIgnoreCase);
        var resendApiKey = Environment.GetEnvironmentVariable("RESEND_API_KEY")
            ?? (isResend ? Environment.GetEnvironmentVariable("SMTP_PASSWORD") : null);

        if (!string.IsNullOrWhiteSpace(resendApiKey))
        {
            return await SendViaResendApiAsync(resendApiKey, toEmail, subject, text);
        }

        return await SendViaSmtpAsync(toEmail, subject, text);
    }

    internal static string DeliveryAddress(string address) => address.Trim().ToLowerInvariant();

    private static async Task<(bool Sent, string Error)> SendViaResendApiAsync(
        string apiKey, string toEmail, string subject, string text)
    {
        var from = Environment.GetEnvironmentVariable("SMTP_FROM");
        if (string.IsNullOrWhiteSpace(from)) return (false, "Sender address is not configured.");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "emails");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = JsonContent.Create(new
            {
                from,
                to = new[] { toEmail },
                subject,
                text
            });

            using var response = await HttpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                return (true, string.Empty);
            }

            return (false, $"Email provider rejected the request ({(int)response.StatusCode}).");
        }
        catch (Exception ex)
        {
            return (false, ex.GetType().Name);
        }
    }

    private static async Task<(bool Sent, string Error)> SendViaSmtpAsync(
        string toEmail, string subject, string text)
    {
        var host = Environment.GetEnvironmentVariable("SMTP_HOST");
        var user = Environment.GetEnvironmentVariable("SMTP_USER");
        var password = Environment.GetEnvironmentVariable("SMTP_PASSWORD");
        var from = Environment.GetEnvironmentVariable("SMTP_FROM");

        if (string.IsNullOrWhiteSpace(host) ||
            string.IsNullOrWhiteSpace(user) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(from))
        {
            return (false, "Email is not configured (missing RESEND_API_KEY, or SMTP_HOST/SMTP_USER/SMTP_PASSWORD/SMTP_FROM).");
        }

        var portText = Environment.GetEnvironmentVariable("SMTP_PORT");
        var port = int.TryParse(portText, out var parsedPort) ? parsedPort : 587;
        var useSslText = Environment.GetEnvironmentVariable("SMTP_USE_SSL");
        var useSsl = !bool.TryParse(useSslText, out var parsedSsl) || parsedSsl;
        if (RuntimeSecurity.IsProduction && !useSsl) return (false, "Production SMTP requires TLS.");

        try
        {
            using var client = new SmtpClient(host, port)
            {
                EnableSsl = useSsl,
                Credentials = new NetworkCredential(user, password)
            };

            using var message = new MailMessage(from, toEmail)
            {
                Subject = subject,
                Body = text,
                IsBodyHtml = false
            };

            await client.SendMailAsync(message);
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, ex.GetType().Name);
        }
    }

    private static string BuildBody(string tier, string licenseKey, DateTime expirationUtc) => string.Join(
        "\n",
        "Thank you for purchasing Lightning SPP.",
        "",
        $"Plan: {tier}",
        $"License key: {licenseKey}",
        $"Valid until: {expirationUtc:yyyy-MM-dd}",
        "",
        "To activate:",
        "1. Sign in to your verified customer account: https://scanprocessprint.com/account",
        "2. Open your paid entitlement and select Activate.",
        "3. Your customer dashboard records activation and provides the signed license key.",
        "",
        "Need help? Reply to this email or contact clisonix@pm.me.",
        "",
        "— Lightning SPP");
}
