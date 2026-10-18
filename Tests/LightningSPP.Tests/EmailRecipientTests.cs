using wwwmmm.Web;
using Xunit;
using System.Net;
using System.Net.Sockets;

namespace LightningSPP.Tests;

public class EmailRecipientTests
{
    [Theory]
    [InlineData("DEALSJONA@GMAIL.COM", "dealsjona@gmail.com")]
    [InlineData(" Buyer+Paid@EXAMPLE.TEST ", "buyer+paid@example.test")]
    public void IdentityNormalizationDoesNotLeakIntoDeliveryAllowlist(string stored, string expected)
    {
        Assert.Equal(expected, EmailService.DeliveryAddress(stored));
        Assert.Equal(CustomerStore.NormalizeEmail(stored.Trim()), CustomerStore.NormalizeEmail(expected));
    }

    [Fact]
    public async Task ActualSmtpEnvelopeUsesDeliveryAddressFromUppercaseCustomerIdentity()
    {
        // A local SMTP fixture checks the real outbound transport, not inbox delivery.
        var names = new[] { "RESEND_API_KEY", "SMTP_HOST", "SMTP_PORT", "SMTP_USER", "SMTP_PASSWORD", "SMTP_FROM", "SMTP_USE_SSL" };
        var previous = names.ToDictionary(n => n, Environment.GetEnvironmentVariable);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        string? recipient = null;
        var transport = Task.Run(async () =>
        {
            using var socket = await listener.AcceptTcpClientAsync(timeout.Token);
            using var stream = socket.GetStream();
            using var reader = new StreamReader(stream);
            await using var writer = new StreamWriter(stream) { AutoFlush = true, NewLine = "\r\n" };
            await writer.WriteLineAsync("220 localhost test fixture");
            while (await reader.ReadLineAsync(timeout.Token) is { } line)
            {
                if (line.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase))
                    await writer.WriteLineAsync("250-localhost\r\n250 OK");
                else if (line.StartsWith("RCPT TO:", StringComparison.OrdinalIgnoreCase))
                { recipient = line; await writer.WriteLineAsync("250 OK"); }
                else if (line == "DATA")
                {
                    await writer.WriteLineAsync("354 End with dot");
                    while (await reader.ReadLineAsync(timeout.Token) is { } data && data != ".") { }
                    await writer.WriteLineAsync("250 Accepted by test fixture");
                }
                else if (line == "QUIT") { await writer.WriteLineAsync("221 Bye"); break; }
                else await writer.WriteLineAsync("250 OK");
            }
        }, timeout.Token);
        try
        {
            Environment.SetEnvironmentVariable("RESEND_API_KEY", null);
            Environment.SetEnvironmentVariable("SMTP_HOST", "127.0.0.1");
            Environment.SetEnvironmentVariable("SMTP_PORT", ((IPEndPoint)listener.LocalEndpoint).Port.ToString());
            Environment.SetEnvironmentVariable("SMTP_USER", "fixture");
            Environment.SetEnvironmentVariable("SMTP_PASSWORD", "test-only-fixture");
            Environment.SetEnvironmentVariable("SMTP_FROM", "sender@example.test");
            Environment.SetEnvironmentVariable("SMTP_USE_SSL", "false");
            var result = await EmailService.TrySendEmailAsync("BUYER@EXAMPLE.TEST", "Test fixture", "No production email.");
            Assert.True(result.Sent, result.Error);
            await transport.WaitAsync(timeout.Token);
            Assert.Equal("RCPT TO:<buyer@example.test>", recipient, ignoreCase: false);
        }
        finally
        {
            timeout.Cancel(); listener.Stop();
            foreach (var pair in previous) Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            try { await transport; } catch (OperationCanceledException) { }
        }
    }
}
