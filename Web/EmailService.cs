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
    {
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            return (false, "Recipient email address is missing.");
        }

        var smtpHost = Environment.GetEnvironmentVariable("SMTP_HOST");
        var isResend = string.Equals(smtpHost, "smtp.resend.com", StringComparison.OrdinalIgnoreCase);
        var resendApiKey = Environment.GetEnvironmentVariable("RESEND_API_KEY")
            ?? (isResend ? Environment.GetEnvironmentVariable("SMTP_PASSWORD") : null);

        if (!string.IsNullOrWhiteSpace(resendApiKey))
        {
            return await SendViaResendApiAsync(resendApiKey, toEmail, tier, licenseKey, expirationUtc);
        }

        return await SendViaSmtpAsync(toEmail, tier, licenseKey, expirationUtc);
    }

    private static async Task<(bool Sent, string Error)> SendViaResendApiAsync(
        string apiKey, string toEmail, string tier, string licenseKey, DateTime expirationUtc)
    {
        var from = Environment.GetEnvironmentVariable("SMTP_FROM") ?? "Lightning SPP <onboarding@resend.dev>";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "emails");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = JsonContent.Create(new
            {
                from,
                to = new[] { toEmail },
                subject = "Your Lightning SPP license key",
                text = BuildBody(tier, licenseKey, expirationUtc)
            });

            using var response = await HttpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return (true, string.Empty);
            }

            return (false, $"Resend API returned {(int)response.StatusCode}: {body}");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static async Task<(bool Sent, string Error)> SendViaSmtpAsync(
        string toEmail, string tier, string licenseKey, DateTime expirationUtc)
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

        try
        {
            using var client = new SmtpClient(host, port)
            {
                EnableSsl = useSsl,
                Credentials = new NetworkCredential(user, password)
            };

            using var message = new MailMessage(from, toEmail)
            {
                Subject = "Your Lightning SPP license key",
                Body = BuildBody(tier, licenseKey, expirationUtc),
                IsBodyHtml = false
            };

            await client.SendMailAsync(message);
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
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
        "1. Open your workspace dashboard: https://scanprocessprint.com/dashboard",
        "2. Paste the license key under \"Activate a license\".",
        "3. Click \"Activate license\". No administrator access code is needed.",
        "",
        "Need help? Reply to this email or contact clisonix@pm.me.",
        "",
        "— Lightning SPP");
}
