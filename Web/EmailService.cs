using System.Net;
using System.Net.Mail;

namespace wwwmmm.Web;

/// <summary>
/// Minimal SMTP email sender for license-key fulfillment. Uses the built-in
/// System.Net.Mail client so no additional NuGet dependency is required.
/// Configuration is read entirely from environment variables; if any
/// required variable is missing, sending is skipped and the caller is told
/// why so it can log/alert instead of silently losing a paid order.
/// </summary>
internal static class EmailService
{
    public static async Task<(bool Sent, string Error)> TrySendLicenseKeyEmailAsync(
        string toEmail,
        string tier,
        string licenseKey,
        DateTime expirationUtc)
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
            return (false, "SMTP is not configured (missing SMTP_HOST/SMTP_USER/SMTP_PASSWORD/SMTP_FROM).");
        }

        if (string.IsNullOrWhiteSpace(toEmail))
        {
            return (false, "Recipient email address is missing.");
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
