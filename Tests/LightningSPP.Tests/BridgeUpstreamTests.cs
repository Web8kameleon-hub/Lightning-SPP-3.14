using wwwmmm.Integrations;
using Xunit;

namespace LightningSPP.Tests;

// These tests call real services. They require isolated loopback deployments and never target production.
public class BridgeUpstreamTests
{
    [IsolatedBridgeFact]
    public async Task ExcelParsing_PreservesZeroAndFalseCells()
    {
        using var excel = new ClisonixExcelBridge(Local("SPP_EXCEL_SERVICE_URL"));
        var workbook = ExcelTestWorkbook.Create([0, false, "Count"], [0, false, 0]);
        var parsed = await excel.ParseAsync("zeros.xlsx", workbook);
        Assert.Equal(1, parsed.GetProperty("rows").GetInt32());
        var row = parsed.GetProperty("data")[0];
        Assert.Equal(0, row.GetProperty("0").GetInt32());
        Assert.False(row.GetProperty("False").GetBoolean());
        Assert.Equal(0, row.GetProperty("Count").GetInt32());
    }

    [IsolatedBridgeFact]
    public async Task ExcelParsing_DuplicateHeadersPreserveEveryColumn()
    {
        using var excel = new ClisonixExcelBridge(Local("SPP_EXCEL_SERVICE_URL"));
        var workbook = ExcelTestWorkbook.Create(["Name", "Name", "Name_2"], ["first", "second", "third"]);
        var parsed = await excel.ParseAsync("duplicates.xlsx", workbook);
        var row = parsed.GetProperty("data")[0];
        Assert.Equal("first", row.GetProperty("Name").GetString());
        Assert.Equal("second", row.GetProperty("Name_3").GetString());
        Assert.Equal("third", row.GetProperty("Name_2").GetString());
    }

    [IsolatedBridgeFact]
    public async Task ExcelParsing_RealPreviewIsLimitedWithoutLosingTotalCount()
    {
        using var excel = new ClisonixExcelBridge(Local("SPP_EXCEL_SERVICE_URL"));
        var rows = new[] { new object[] { "Count" } }.Concat(Enumerable.Range(0, 120).Select(index => new object[] { index })).ToArray();
        var parsed = await excel.ParseAsync("preview.xlsx", ExcelTestWorkbook.Create(rows));
        Assert.Equal(120, parsed.GetProperty("rows").GetInt32());
        Assert.Equal(100, parsed.GetProperty("data").GetArrayLength());
        Assert.Equal(0, parsed.GetProperty("data")[0].GetProperty("Count").GetInt32());
        Assert.Equal(99, parsed.GetProperty("data")[99].GetProperty("Count").GetInt32());
    }

    [IsolatedBridgeFact]
    public async Task ExcelValidation_RealServiceRejectsInvalidWorkbook()
    {
        using var excel = new ClisonixExcelBridge(Local("SPP_EXCEL_SERVICE_URL"));
        await Assert.ThrowsAsync<BridgeException>(() => excel.ValidateAsync("invalid.xlsx", "not a workbook"u8.ToArray()));
    }

    [IsolatedBridgeFact]
    public async Task ExcelParsing_RealServiceRejectsInvalidWorkbook()
    {
        using var excel = new ClisonixExcelBridge(Local("SPP_EXCEL_SERVICE_URL"));
        var exception = await Assert.ThrowsAsync<BridgeException>(() => excel.ParseAsync("invalid.xlsx", "not a workbook"u8.ToArray()));
        Assert.Contains("500", exception.Message);
    }

    [IsolatedBridgeFact]
    public async Task ExcelCoreWorkbook_IsValidatedAndParsedByExcelService()
    {
        using var core = new ClisonixExcelCoreBridge(Local("SPP_EXCEL_CORE_URL"));
        using var excel = new ClisonixExcelBridge(Local("SPP_EXCEL_SERVICE_URL"));
        var workbook = await core.ExportLagterAsync();
        var validation = await excel.ValidateAsync("lagter-export.xlsx", workbook);
        Assert.True(validation.GetProperty("valid").GetBoolean());
        Assert.Equal(5, validation.GetProperty("sheets").GetArrayLength());
        var parsed = await excel.ParseAsync("lagter-export.xlsx", workbook);
        Assert.Equal("Overview", parsed.GetProperty("sheet").GetString());
        Assert.True(parsed.GetProperty("rows").GetInt32() > 0);
        Assert.True(parsed.GetProperty("data").GetArrayLength() > 0);
    }

    [IsolatedBridgeFact]
    public async Task NodeDbTestFixture_IsIngestedWithAuthAndHmacThenReadBack()
    {
        var connection = Local("SPP_OSCLX_URL") with
        {
            ApiKey = Environment.GetEnvironmentVariable("SPP_OSCLX_API_KEY"),
            HmacSecret = Environment.GetEnvironmentVariable("SPP_OSCLX_HMAC_SECRET")
        };
        Assert.False(string.IsNullOrEmpty(connection.ApiKey));
        Assert.False(string.IsNullOrEmpty(connection.HmacSecret));
        using var nodeDb = new OsClxNodeDbBridge(connection);
        var nodeId = "spp-contract-fixture-" + Guid.NewGuid().ToString("N");
        // Explicit test fixture values, not measurements or production evidence.
        var fixture = new NodeDbTelemetry
        {
            FabricId = "bridge-contract-test", TrustDomain = "isolated-local-test", Pressure = 0.25,
            Resonance = 0.75, HealthScore = 0.9, Memory = 0.1, Momentum = 0.2,
            ErrorRate = 0, QueueDepth = 2, LatencyMs = 17, Capabilities = ["excel-parse"], Role = "edge"
        };
        await nodeDb.IngestAsync(nodeId, fixture);
        var result = await nodeDb.ReadNodeAsync(nodeId);
        var node = result.GetProperty("node");
        Assert.Equal(fixture.FabricId, node.GetProperty("fabric_id").GetString());
        Assert.Equal(fixture.TrustDomain, node.GetProperty("trust_domain").GetString());
        Assert.Equal(fixture.Pressure, node.GetProperty("cycle").GetProperty("pressure").GetDouble());
        Assert.Equal(fixture.Resonance, node.GetProperty("cycle").GetProperty("resonance").GetDouble());
        using var wrongKey = new OsClxNodeDbBridge(connection with { ApiKey = "incorrect-test-key" });
        var unauthorized = await Assert.ThrowsAsync<BridgeException>(() => wrongKey.ReadNodeAsync(nodeId));
        Assert.Contains("401", unauthorized.Message);
        using var wrongSignature = new OsClxNodeDbBridge(connection with { HmacSecret = "incorrect-test-hmac" });
        var invalidSignature = await Assert.ThrowsAsync<BridgeException>(() => wrongSignature.IngestAsync(nodeId, fixture));
        Assert.Contains("401", invalidSignature.Message);
    }

    private static BridgeConnection Local(string name)
    {
        var url = Environment.GetEnvironmentVariable(name);
        Assert.False(string.IsNullOrWhiteSpace(url));
        var uri = new Uri(url!);
        Assert.True(uri.IsLoopback, "Upstream verification must use isolated loopback services.");
        return new(uri);
    }
}

public sealed class IsolatedBridgeFactAttribute : FactAttribute
{
    public IsolatedBridgeFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("SPP_BRIDGE_TEST_ALLOW_WRITES") != "isolated-loopback")
            Skip = "Real upstream services are not configured. Set SPP_BRIDGE_TEST_ALLOW_WRITES=isolated-loopback only for isolated local test services.";
    }
}
