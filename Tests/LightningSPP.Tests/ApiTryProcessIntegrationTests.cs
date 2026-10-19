using System.Net;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using wwwmmm.Configuration;
using wwwmmm.Documents;
using wwwmmm.Services;
using wwwmmm.Web;
using Xunit;

namespace LightningSPP.Tests;

/// <summary>
/// Edge-case coverage for the public, unauthenticated /api/try/process demo endpoint.
/// All scenarios run against a single server instance in one test method because the
/// endpoint's rate limiter is keyed by client IP in a static, process-wide dictionary;
/// splitting this across multiple test methods from the same loopback IP would make
/// the rate-limit assertion (and any test that runs after it) order-dependent and flaky.
/// </summary>
public class ApiTryProcessIntegrationTests
{
    [Fact]
    public async Task TryProcessEndpoint_EdgeCases_ThenRateLimitTrips()
    {
        var port = GetFreePort();
        using var cts = new CancellationTokenSource();
        var serverTask = LicenseApiServer.RunAsync(port, cts.Token);
        await WaitUntilHealthyAsync(port, TimeSpan.FromSeconds(5));

        using var client = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}") };

        // 1. Valid upload, no profile header -> defaults to lightning.
        var r1 = await PostImageAsync(client, RealTestImage.Png(), "image/png", profile: null);
        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
        var body1 = await r1.Content.ReadAsStringAsync();
        Assert.Contains("\"ok\": true", body1);
        Assert.Contains("\"profile\": \"lightning\"", body1);
        Assert.Contains("\"afterImageBase64\"", body1);
        using var json = JsonDocument.Parse(body1);
        Assert.True(json.RootElement.GetProperty("compressedBytes").GetInt32() > 0,
            "A successful demo must include actual processing output, not only the scanned preview.");
        var scan = await ServiceFactory.CreateScanService().ScanAsync(RealTestImage.Png(), ScanMode.Lightning, quality: 300);
        var processed = await ServiceFactory.CreateProcessingService().ProcessAsync(scan.Data,
            new ProcessingOptions(ProcessingEngine.Hybrid, UseAI: false, UseClisonix: false,
                OptimizationLevel: 3, UseStigmaMemory: false));
        Assert.True(processed.Success);
        Assert.Equal(processed.ProcessedData,
            Convert.FromBase64String(json.RootElement.GetProperty("outputArtifactBase64").GetString()!));
        Assert.Equal(".br", json.RootElement.GetProperty("outputArtifactFileExtension").GetString());
        Assert.Equal("application/octet-stream", json.RootElement.GetProperty("outputArtifactMimeType").GetString());
        var after = Convert.FromBase64String(json.RootElement.GetProperty("afterImageBase64").GetString()!);
        Assert.Equal(TryDocumentProcessorTests.Restore(processed.ProcessedData), after);
        using var decoded = SkiaSharp.SKBitmap.Decode(after);
        Assert.NotNull(decoded);
        Assert.Equal(after.Length, json.RootElement.GetProperty("afterBytes").GetInt32());
        Assert.Equal(after.Length, json.RootElement.GetProperty("compressedBytes").GetInt32());

        // 2. Mixed ZIP import returns original bytes and an honest inventory.
        using var archiveInput = new MemoryStream();
        using (var inputZip = new ZipArchive(archiveInput, ZipArchiveMode.Create, true))
        {
            using var entry = inputZip.CreateEntry("src/main.py").Open();
            entry.Write("print('inventory only')"u8);
        }
        var r2 = await PostImageAsync(client, archiveInput.ToArray(), "application/zip", null, "/api/try/archive", "structural", "pdf");
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
        Assert.Equal("application/zip", r2.Content.Headers.ContentType!.MediaType);
        using (var imported = new ZipArchive(new MemoryStream(await r2.Content.ReadAsByteArrayAsync())))
        {
            Assert.NotNull(imported.GetEntry("originals/src/main.py"));
            using var inventory = JsonDocument.Parse(imported.GetEntry("report.json")!.Open());
            Assert.False(inventory.RootElement.GetProperty("codeExecuted").GetBoolean());
            Assert.Equal("INSPECTED", inventory.RootElement.GetProperty("files")[0].GetProperty("analysis").GetProperty("status").GetString());
            Assert.Equal("code", inventory.RootElement.GetProperty("files")[0].GetProperty("category").GetString());
            Assert.Equal("archive-pdf-processing", inventory.RootElement.GetProperty("operation").GetString());
            Assert.Equal("NOT_REQUESTED_FOR_TYPE", inventory.RootElement.GetProperty("files")[0].GetProperty("documentProcessing").GetProperty("status").GetString());
        }

        // ZIP export shares the same processing path and request budget.
        var package = await PostImageAsync(client, RealTestImage.Png(), "image/png", "lightning", "/api/try/package");
        Assert.Equal(HttpStatusCode.OK, package.StatusCode);
        Assert.Equal("application/zip", package.Content.Headers.ContentType!.MediaType);
        Assert.Contains("no-store", package.Headers.CacheControl!.ToString());
        using (var zip = new ZipArchive(new MemoryStream(await package.Content.ReadAsByteArrayAsync())))
        {
            Assert.Equal(new[] { "preview.png", "processed.png", "report.json" }, zip.Entries.Select(e => e.FullName).Order().ToArray());
            using var report = JsonDocument.Parse(zip.GetEntry("report.json")!.Open());
            Assert.Equal("NOT_PERFORMED", report.RootElement.GetProperty("classification").GetProperty("status").GetString());
            foreach (var file in report.RootElement.GetProperty("files").EnumerateArray())
            {
                using var bytes = new MemoryStream();
                await zip.GetEntry(file.GetProperty("path").GetString()!)!.Open().CopyToAsync(bytes);
                Assert.Equal(file.GetProperty("sha256").GetString(), Convert.ToHexStringLower(SHA256.HashData(bytes.ToArray())));
            }
        }

        // 3. Invalid ZIP is rejected without exposing internal errors.
        var r3 = await PostImageAsync(client, RandomBytes(1024), "application/zip", profile: null, endpoint: "/api/try/archive");
        Assert.Equal(HttpStatusCode.BadRequest, r3.StatusCode);

        // 4. PDF MIME is accepted, but an invalid PDF is rejected by the real PDF validator
        // before native Poppler tools are invoked.
        var r4 = await PostImageAsync(client, "invalid pdf"u8.ToArray(), "application/pdf", profile: null);
        Assert.Equal(HttpStatusCode.BadRequest, r4.StatusCode);
        var invalidBody = await r4.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Unsupported file type", invalidBody);
        Assert.DoesNotContain("outputPdfBase64", invalidBody);
        Assert.DoesNotContain("Exception", invalidBody);

        // 5. Empty body -> 400, not a 500 or hang.
        var r5 = await PostImageAsync(client, Array.Empty<byte>(), "image/png", profile: null, endpoint: "/api/try/package");
        Assert.Equal(HttpStatusCode.BadRequest, r5.StatusCode);

        // 6. Oversized body (> 8 MB) -> 400, rejected before full processing.
        var oversized = RandomBytes(8 * 1024 * 1024 + 1024);
        var r6 = await PostImageAsync(client, oversized, "image/png", profile: null);
        Assert.Equal(HttpStatusCode.BadRequest, r6.StatusCode);

        // Keep exactly ten requests before the rate-limit assertion, including native coverage.
        var fillerCount = 3;
        if (Environment.GetEnvironmentVariable("SPP_TEST_NATIVE_DOCUMENTS") == "1")
        {
            var pdf = new PdfDocumentService().CreateFromImages([RealTestImage.Png(), RealTestImage.Png()]);
            var pdfResponse = await PostImageAsync(client, pdf, "application/pdf", profile: null);
            Assert.Equal(HttpStatusCode.OK, pdfResponse.StatusCode);
            using var pdfJson = JsonDocument.Parse(await pdfResponse.Content.ReadAsStringAsync());
            var root = pdfJson.RootElement;
            Assert.Equal("pdf", root.GetProperty("inputType").GetString());
            Assert.Equal(1, root.GetProperty("page").GetInt32());
            Assert.Equal(2, root.GetProperty("totalPages").GetInt32());
            Assert.Equal("application/pdf", root.GetProperty("outputMimeType").GetString());
            Assert.Equal(".pdf", root.GetProperty("outputFileExtension").GetString());
            var output = Convert.FromBase64String(root.GetProperty("outputPdfBase64").GetString()!);
            Assert.True(output.AsSpan(0, 5).SequenceEqual("%PDF-"u8));
            Assert.Equal(output.Length, root.GetProperty("afterBytes").GetInt32());
            Assert.Equal(output.Length, root.GetProperty("compressedBytes").GetInt32());
            Assert.Equal(pdf.Length, root.GetProperty("originalBytes").GetInt32());
            Assert.Equal(Math.Round(100.0 * (1 - (double)output.Length / pdf.Length), 1),
                root.GetProperty("compressionPercent").GetDouble());
            Assert.Equal(2, (await new PdfDocumentService().RenderPageAsync(output, 2)).TotalPages);
            using var preview = SkiaSharp.SKBitmap.Decode(Convert.FromBase64String(root.GetProperty("afterImageBase64").GetString()!));
            Assert.NotNull(preview);

            var tooMany = await PostImageAsync(client, TryDocumentProcessorTests.CreatePdf(33), "application/pdf", profile: null);
            Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
            Assert.Contains("32 pages", await tooMany.Content.ReadAsStringAsync());
            Assert.DoesNotContain("outputPdfBase64", await tooMany.Content.ReadAsStringAsync());
            var broken = await PostImageAsync(client, "%PDF-1.7\nbroken fixture"u8.ToArray(), "application/pdf", profile: null);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, broken.StatusCode);
            var brokenBody = await broken.Content.ReadAsStringAsync();
            Assert.DoesNotContain("outputPdfBase64", brokenBody);
            Assert.DoesNotContain("exit code", brokenBody);
            Assert.DoesNotContain("spp-pdf-", brokenBody);
            fillerCount -= 3;
        }
        for (var i = 0; i < fillerCount; i++)
        {
            var filler = await PostImageAsync(client, RealTestImage.Png(), "image/png", profile: null);
            Assert.Equal(HttpStatusCode.OK, filler.StatusCode);
        }

        // 7. The 11th request from this IP within the window must be rate-limited.
        var r11 = await PostImageAsync(client, RealTestImage.Png(), "image/png", profile: null, endpoint: "/api/try/package");
        Assert.Equal(HttpStatusCode.TooManyRequests, r11.StatusCode);

        cts.Cancel();
        await serverTask;
    }

    private static Task<HttpResponseMessage> PostImageAsync(HttpClient client, byte[] bytes, string contentType, string? profile, string endpoint = "/api/try/process", string? analysis = null, string? documents = null)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };
        if (profile is not null)
        {
            request.Headers.Add("X-Stigma-Profile", profile);
        }

        if (analysis is not null) request.Headers.Add("X-Archive-Analysis", analysis);
        if (documents is not null) request.Headers.Add("X-Archive-Documents", documents);
        return client.SendAsync(request);
    }

    private static byte[] RandomBytes(int size)
    {
        var bytes = new byte[size];
        Random.Shared.NextBytes(bytes);
        return bytes;
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task WaitUntilHealthyAsync(int port, TimeSpan timeout)
    {
        using var client = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}") };
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await client.GetAsync("/api/health");
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch
            {
                // Server not ready yet.
            }

            await Task.Delay(100);
        }

        throw new TimeoutException("API server did not become healthy in time.");
    }
}
