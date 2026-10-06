using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using wwwmmm.Integrations;
using Xunit;

namespace LightningSPP.Tests;

// Mock responses exercise protocol guards only. Real upstream verification is recorded separately.
public class BridgeContractTests
{
    private static readonly BridgeConnection Connection = new(new Uri("https://services.example.test/prefix/"));

    [Theory]
    [InlineData("http://external.example.test")]
    [InlineData("https://user:password@example.test")]
    [InlineData("https://example.test?token=secret")]
    [InlineData("https://example.test#fragment")]
    [InlineData("file:///C:/service")]
    public void UnsafeBaseUrl_IsRejected(string uri) =>
        Assert.Throws<ArgumentException>(() => new OsClxNodeDbBridge(new(new Uri(uri))));

    [Fact]
    public async Task ExcelParse_UsesMultipartAndPreservesTruncationCount()
    {
        var dataRows = string.Join(",", Enumerable.Repeat("{\"Name\":\"fixture\"}", 100));
        using var bridge = new ClisonixExcelBridge(Connection, new Handler(async (request, _) =>
        {
            Assert.Equal("https://services.example.test/prefix/api/excel/parse", request.RequestUri!.AbsoluteUri);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.IsType<MultipartFormDataContent>(request.Content);
            Assert.Contains("fixture.xlsx", await request.Content!.ReadAsStringAsync());
            return Json($$"""{"filename":"fixture.xlsx","sheet":"Sheet","headers":["Name"],"rows":120,"data":[{{dataRows}}]}""");
        }));
        var result = await bridge.ParseAsync("fixture.xlsx", new byte[] { 1, 2, 3 });
        Assert.Equal(120, result.GetProperty("rows").GetInt32());
        Assert.Equal(100, result.GetProperty("data").GetArrayLength());
    }

    [Theory]
    [InlineData("../fixture.xlsx")]
    [InlineData("C:\\fixture.xlsx")]
    [InlineData("fixture.pdf")]
    [InlineData("fixture.csv")]
    [InlineData("fixture\n.xlsx")]
    public async Task UnsupportedOrUnsafeFilename_IsRejectedBeforeNetwork(string filename)
    {
        using var bridge = new ClisonixExcelBridge(Connection, NeverCalled());
        await Assert.ThrowsAsync<ArgumentException>(() => bridge.ParseAsync(filename, new byte[] { 1 }));
    }

    [Fact]
    public async Task OversizedUpload_IsRejectedBeforeNetwork()
    {
        using var bridge = new ClisonixExcelBridge(Connection, NeverCalled());
        await Assert.ThrowsAsync<ArgumentException>(() => bridge.ParseAsync("fixture.xlsx", new byte[10 * 1024 * 1024 + 1]));
    }

    [Theory]
    [InlineData("{\"filename\":\"fixture.xlsx\",\"valid\":false,\"error\":\"bad workbook\"}")]
    [InlineData("{\"filename\":\"fixture.xlsx\",\"valid\":true}")]
    public async Task Http200_DoesNotMakeInvalidWorkbookSuccessful(string response)
    {
        using var bridge = new ClisonixExcelBridge(Connection, Reply(Json(response)));
        await Assert.ThrowsAsync<BridgeException>(() => bridge.ValidateAsync("fixture.xlsx", new byte[] { 1 }));
    }

    [Theory]
    [InlineData("{\"filename\":\"fixture.xlsx\",\"sheet\":\"S\",\"headers\":[],\"rows\":\"wrong\",\"data\":[]}")]
    [InlineData("{\"filename\":\"fixture.xlsx\",\"sheet\":\"S\",\"headers\":[],\"rows\":10,\"data\":[]}")]
    public async Task ExcelParse_InvalidSchemaIsRejected(string response)
    {
        using var bridge = new ClisonixExcelBridge(Connection, Reply(Json(response)));
        await Assert.ThrowsAsync<BridgeException>(() => bridge.ParseAsync("fixture.xlsx", new byte[] { 1 }));
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(429)]
    [InlineData(503)]
    [InlineData(302)]
    public async Task UpstreamFailure_IsNotRetriedOrReportedAsSuccess(int status)
    {
        var calls = 0;
        using var bridge = new OsClxNodeDbBridge(Connection, new Handler((_, _) =>
        {
            calls++;
            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)status));
        }));
        var error = await Assert.ThrowsAsync<BridgeException>(() => bridge.IngestAsync("fixture-node", Telemetry()));
        Assert.Contains(status.ToString(), error.Message);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task NodeIngest_UsesCanonicalFieldsRoleAndHmacWithoutPromotingTrust()
    {
        using var bridge = new OsClxNodeDbBridge(Connection with { ApiKey = "test-key", HmacSecret = "test-secret" },
            new Handler(async (request, token) =>
            {
                Assert.Equal("/prefix/v1/ndb/nodes/fixture-node/telemetry", request.RequestUri!.AbsolutePath);
                Assert.Equal("operator", request.Headers.GetValues("X-CLX-Role").Single());
                Assert.Equal("test-key", request.Headers.GetValues("X-API-Key").Single());
                var timestamp = request.Headers.GetValues("X-CLX-Timestamp").Single();
                var signed = $"POST\n{request.RequestUri.AbsolutePath}\n{timestamp}";
                var expected = Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes("test-secret"), Encoding.UTF8.GetBytes(signed)));
                Assert.Equal(expected, request.Headers.GetValues("X-CLX-Signature").Single());
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
                Assert.Equal(0.25, body.RootElement.GetProperty("pressure").GetDouble());
                Assert.Equal(17, body.RootElement.GetProperty("latency_ms").GetDouble());
                Assert.False(body.RootElement.TryGetProperty("trust_level", out _));
                return Json("""{"type":"nodedb-telemetry-accepted","node_id":"fixture-node","node":{"node_id":"fixture-node","fabric_id":"test-fabric","trust_domain":"test-domain","cycle":{"pressure":0.25,"resonance":0.75,"memory":0.1,"momentum":0.2}}}""");
            }));
        await bridge.IngestAsync("fixture-node", Telemetry());
    }

    [Fact]
    public async Task NodeRead_UsesViewerAndValidatesNodeIdentity()
    {
        using var bridge = new OsClxNodeDbBridge(Connection, new Handler((request, _) =>
        {
            Assert.Equal("viewer", request.Headers.GetValues("X-CLX-Role").Single());
            return Task.FromResult(Json("""{"type":"nodedb-node","node":{"node_id":"other-node","cycle":{}}}"""));
        }));
        await Assert.ThrowsAsync<BridgeException>(() => bridge.ReadNodeAsync("fixture-node"));
    }

    [Fact]
    public async Task InvalidMetricsAndPathTraversal_AreRejectedBeforeNetwork()
    {
        using var bridge = new OsClxNodeDbBridge(Connection, NeverCalled());
        await Assert.ThrowsAsync<ArgumentException>(() => bridge.IngestAsync("fixture-node", Telemetry() with { Pressure = double.NaN }));
        await Assert.ThrowsAsync<ArgumentException>(() => bridge.IngestAsync("fixture-node", Telemetry() with { QueueDepth = -1 }));
        await Assert.ThrowsAsync<ArgumentException>(() => bridge.ReadNodeAsync("../admin"));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"pressure\":0.1}")]
    [InlineData("{\"unexpected\":true}")]
    public void MissingOrUnknownTelemetry_IsRejected(string json) =>
        Assert.Throws<JsonException>(() => OsClxNodeDbBridge.ParseTelemetry(json));

    [Fact]
    public async Task Cancellation_IsPropagated()
    {
        using var bridge = new OsClxNodeDbBridge(Connection, new Handler(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return Json("{}");
        }));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => bridge.ReadNodeAsync("fixture-node", cancellation.Token));
    }

    [Fact]
    public async Task HtmlInvalidJsonAndOversizedJson_AreRejected()
    {
        foreach (var content in new[]
        {
            new StringContent("<html>Login</html>", Encoding.UTF8, "text/html"),
            new StringContent("not JSON", Encoding.UTF8, "application/json"),
            new StringContent(new string('x', 2 * 1024 * 1024 + 1), Encoding.UTF8, "application/json")
        })
        {
            using var bridge = new OsClxNodeDbBridge(Connection, Reply(new(HttpStatusCode.OK) { Content = content }));
            await Assert.ThrowsAsync<BridgeException>(() => bridge.ReadNodeAsync("fixture-node"));
        }
    }

    [Fact]
    public async Task ExcelCore_RejectsNonWorkbookDespiteSuccessStatusAndXlsxContentType()
    {
        using var core = new ClisonixExcelCoreBridge(Connection, Reply(new(HttpStatusCode.OK)
        {
            Content = new StringContent("not XLSX", Encoding.UTF8, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
        }));
        await Assert.ThrowsAsync<BridgeException>(() => core.ExportLagterAsync());
    }

    [Fact]
    public async Task ExcelCore_AcceptsStructurallyValidWorkbook()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", """<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/></Types>""");
            WriteEntry(archive, "xl/workbook.xml", """<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheets><sheet name="Fixture"/></sheets></workbook>""");
            WriteEntry(archive, "xl/worksheets/sheet1.xml", """<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"/>""");
        }
        var content = new ByteArrayContent(stream.ToArray());
        content.Headers.ContentType = new("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        using var core = new ClisonixExcelCoreBridge(Connection, Reply(new(HttpStatusCode.OK) { Content = content }));
        Assert.Equal(stream.ToArray(), await core.ExportLagterAsync());
    }

    private static void WriteEntry(ZipArchive archive, string name, string value)
    {
        using var writer = new StreamWriter(archive.CreateEntry(name).Open());
        writer.Write(value);
    }

    [Theory]
    [InlineData("{\"filename\":\"fixture.xlsx\",\"valid\":true,\"sheets\":[]}")]
    [InlineData("{\"filename\":\"fixture.xlsx\",\"valid\":true,\"sheets\":[null]}")]
    [InlineData("{\"filename\":\"fixture.xlsx\",\"valid\":true,\"sheets\":[{\"name\":\"S\",\"rows\":-1,\"columns\":1}]}")]
    public async Task ExcelValidation_MalformedSheetDetailsCannotConfirmSuccess(string response)
    {
        using var bridge = new ClisonixExcelBridge(Connection, Reply(Json(response)));
        await Assert.ThrowsAsync<BridgeException>(() => bridge.ValidateAsync("fixture.xlsx", new byte[] { 1 }));
    }

    [Theory]
    [InlineData("{\"filename\":\"fixture.xlsx\",\"sheet\":\"\",\"headers\":[\"Name\"],\"rows\":0,\"data\":[]}")]
    [InlineData("{\"filename\":\"fixture.xlsx\",\"sheet\":\"S\",\"headers\":[1],\"rows\":0,\"data\":[]}")]
    [InlineData("{\"filename\":\"fixture.xlsx\",\"sheet\":\"S\",\"headers\":[\"Name\"],\"rows\":1,\"data\":[null]}")]
    [InlineData("{\"filename\":\"fixture.xlsx\",\"sheet\":\"S\",\"headers\":[\"Name\"],\"rows\":1,\"data\":[123]}")]
    [InlineData("{\"filename\":\"fixture.xlsx\",\"sheet\":\"S\",\"headers\":[\"Name\",\"Name\"],\"rows\":1,\"data\":[{\"Name\":\"overwritten\"}]}")]
    [InlineData("{\"filename\":\"fixture.xlsx\",\"sheet\":\"S\",\"headers\":[\"Name\",\"Count\"],\"rows\":1,\"data\":[{\"Name\":\"missing-column\"}]}")]
    public async Task ExcelParse_MalformedRowsAndHeadersAreRejected(string response)
    {
        using var bridge = new ClisonixExcelBridge(Connection, Reply(Json(response)));
        await Assert.ThrowsAsync<BridgeException>(() => bridge.ParseAsync("fixture.xlsx", new byte[] { 1 }));
    }

    [Theory]
    [InlineData("node_id", "different-node")]
    [InlineData("fabric_id", "different-fabric")]
    [InlineData("trust_domain", "different-domain")]
    [InlineData("pressure", "0.5")]
    [InlineData("resonance", "0.5")]
    [InlineData("memory", "0.5")]
    [InlineData("momentum", "0.5")]
    public async Task NodeIngest_MismatchedAcknowledgementIsRejected(string field, string value)
    {
        var node = new Dictionary<string, object>
        {
            ["node_id"] = "fixture-node", ["fabric_id"] = "test-fabric", ["trust_domain"] = "test-domain",
            ["cycle"] = new Dictionary<string, double> { ["pressure"] = 0.25, ["resonance"] = 0.75, ["memory"] = 0.1, ["momentum"] = 0.2 }
        };
        if (field is "pressure" or "resonance" or "memory" or "momentum")
            ((Dictionary<string, double>)node["cycle"])[field] = double.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        else node[field] = value;
        var json = JsonSerializer.Serialize(new { type = "nodedb-telemetry-accepted", node_id = "fixture-node", node });
        using var bridge = new OsClxNodeDbBridge(Connection, Reply(Json(json)));
        await Assert.ThrowsAsync<BridgeException>(() => bridge.IngestAsync("fixture-node", Telemetry()));
    }

    [Fact]
    public async Task StreamingResponseWithoutLength_IsStillBounded()
    {
        using var content = new StreamContent(new UnseekableStream(new MemoryStream(new byte[2 * 1024 * 1024 + 1])));
        content.Headers.ContentType = new("application/json");
        Assert.Null(content.Headers.ContentLength);
        using var bridge = new OsClxNodeDbBridge(Connection, Reply(new(HttpStatusCode.OK) { Content = content }));
        var exception = await Assert.ThrowsAsync<BridgeException>(() => bridge.ReadNodeAsync("fixture-node"));
        Assert.Contains("size limit", exception.Message);
    }

    [Fact]
    public async Task InterruptedResponse_IsAFailureWithoutLeakingDiagnostics()
    {
        using var content = new StreamContent(new UnseekableStream(new MemoryStream(), () => throw new IOException("secret-fixture-diagnostic")));
        content.Headers.ContentType = new("application/json");
        using var bridge = new OsClxNodeDbBridge(Connection, Reply(new(HttpStatusCode.OK) { Content = content }));
        var exception = await Assert.ThrowsAsync<BridgeException>(() => bridge.ReadNodeAsync("fixture-node"));
        Assert.DoesNotContain("secret-fixture-diagnostic", exception.ToString());
    }

    [Fact]
    public async Task CancellationDuringResponseRead_IsPropagated()
    {
        using var cancellation = new CancellationTokenSource();
        using var content = new StreamContent(new UnseekableStream(new MemoryStream(), () => cancellation.Cancel()));
        content.Headers.ContentType = new("application/json");
        using var bridge = new OsClxNodeDbBridge(Connection, Reply(new(HttpStatusCode.OK) { Content = content }));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => bridge.ReadNodeAsync("fixture-node", cancellation.Token));
    }

    [Fact]
    public async Task TransportTimeout_DoesNotConfirmWriteOrRetry()
    {
        var calls = 0;
        using var bridge = new OsClxNodeDbBridge(Connection, new Handler((_, _) =>
        {
            calls++;
            throw new TaskCanceledException();
        }));
        var exception = await Assert.ThrowsAsync<BridgeException>(() => bridge.IngestAsync("fixture-node", Telemetry()));
        Assert.Contains("outcome is unknown", exception.Message);
        Assert.Equal(1, calls);
    }

    private sealed class UnseekableStream(Stream source, Action? beforeRead = null) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            beforeRead?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            return await source.ReadAsync(buffer, cancellationToken);
        }
        public override int Read(byte[] buffer, int offset, int count) => source.Read(buffer, offset, count);
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) source.Dispose(); base.Dispose(disposing); }
    }

    private static NodeDbTelemetry Telemetry() => new()
    {
        FabricId = "test-fabric", TrustDomain = "test-domain", Pressure = 0.25, Resonance = 0.75,
        HealthScore = 0.9, Memory = 0.1, Momentum = 0.2, ErrorRate = 0, QueueDepth = 2, LatencyMs = 17
    };

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private static Handler Reply(HttpResponseMessage response) => new((_, _) => Task.FromResult(response));
    private static Handler NeverCalled() => new((_, _) => throw new InvalidOperationException("Unexpected network call."));

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            handle(request, cancellationToken);
    }
}
