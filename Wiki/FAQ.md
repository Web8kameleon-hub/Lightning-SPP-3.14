# ❓ Frequently Asked Questions (FAQ)

## General Questions

### What is Lightning SPP?

Lightning SPP (Scan-Process-Print) is a professional-grade document processing system designed for ultra-fast scanning, processing, and printing operations. It supports deployment on any device from medical scanners to simple mobile phones.

### Is Lightning SPP open source?

The core library is released under the **MIT License**, which means you can:
- ✅ Use it in commercial projects
- ✅ Modify and distribute
- ✅ Use it for free in development

However, **production use** of advanced features requires an active license key.

### What's the difference between FREE and PAID tiers?

| Feature | FREE | PAID (Unlimited) |
|---------|------|------------------|
| Operations/month | 1,000 | Unlimited |
| Devices | 1 | 10 |
| Basic Features (TideWave, CLX) | ✅ | ✅ |
| AI Features (Clisonic AI) | ❌ | ✅ |
| Premium Features (Stigma, NanoDecibel) | ❌ | ✅ |
| Support | Community | Priority Email |
| Price | Free | €99.99/year |

---

## Installation & Setup

### How do I install Lightning SPP?

**Option 1: NuGet Package**
```bash
dotnet add package LightningSPP
```

**Option 2: Visual Studio Extension**
- Download from VS Marketplace
- Tools → Extensions → Search "Lightning SPP"

**Option 3: Docker**
```bash
docker pull abagmbh/lightning-spp:3.14
```

### What are the system requirements?

**Minimum:**
- .NET 10.0 Runtime
- 512 MB RAM
- 50 MB disk space

**Recommended:**
- .NET 10.0 SDK
- 2 GB RAM
- 200 MB disk space (with cache)

**Supported Platforms:**
- Windows 10/11 (x64, ARM64)
- Linux (x64, ARM64)
- macOS 12+ (x64, ARM64)
- iOS 14+ (ARM64)
- Android 8+ (ARM64)

### Can I use Lightning SPP on Raspberry Pi?

**Yes!** Lightning SPP supports ARM64 architecture. Use the IoT device profile:

```csharp
var instance = UniversalSDK.ForDevice(DeviceProfile.IoT);
await instance.ProcessAsync(data);
```

---

## Licensing

### How do I activate a license?

```bash
# CLI
wwwmmm activate LSPP-UNLTD-XXXX-XXXX-XXXX-XXXX

# Code
using LightningSPP.Licensing;
LicenseManager.ActivateLicense("LSPP-UNLTD-XXXX-XXXX-XXXX-XXXX");
```

License is saved to: `%APPDATA%\LightningSPP\license.json`

### Where do I buy a license?

**Official Store**: https://aba-gmbh.de/lightning-spp

**Contact**: clisonix@pm.me

### Can I transfer my license to another device?

**YES**, but you must **deactivate** it first on the old device:

```bash
wwwmmm deactivate
```

Then activate on the new device. Each license allows a specific number of concurrent devices (see your tier).

### What happens when my license expires?

- You'll receive email reminders **30 days** and **7 days** before expiration
- After expiration, advanced features revert to FREE tier limits
- Your data and configurations remain intact
- You can renew at any time to restore full access

### Do you offer academic/non-profit discounts?

**Yes!**
- **Academic**: 50% discount (proof of .edu email required)
- **Non-Profit**: 30% discount (proof of 501(c)(3) or equivalent required)

Email clisonix@pm.me with documentation.

---

## Technical Questions

### What is "REAL processing" vs "SIMULATED"?

**SIMULATED Mode** (default for testing):
- Uses `Task.Delay()` to simulate processing time
- No actual image manipulation or AI
- Fast for unit testing
- Returns fake data

**REAL Mode** (production):
- Uses **SixLabors.ImageSharp** for actual image processing
- Uses **Microsoft.ML** for real machine learning
- Uses **Brotli** for compression
- Writes actual files to disk
- Returns real processed data

Switch modes:
```csharp
LightningAPI.UseRealServices();      // Production
LightningAPI.UseSimulatedServices(); // Testing
```

### How fast is Lightning SPP really?

**Benchmark Results** (100KB image):

| Mode | Target | Actual | Status |
|------|--------|--------|--------|
| Instant | < 1ms | 0.3ms | ✅ 3x faster |
| UltraFast | < 5ms | 3.7ms | ✅ 1.3ms margin |
| Lightning | < 10ms | 8.2ms | ✅ 1.8ms margin |

Real-world performance depends on:
- Data size
- Processing mode (Lightning/TideWave/Resonance/NanoDecibel)
- Hardware capabilities
- Network latency (cloud deployments)

### Does Lightning SPP support batch processing?

**Yes!** Batch processing is optimized with parallelism:

```csharp
var sources = new[] { "doc1.pdf", "doc2.pdf", "doc3.pdf" };
var results = await LightningAPI.BatchProcessAsync(sources);

// Or use parallel API
var results = await Lightning314.Parallel(
    sources, 
    maxConcurrency: 10
);
```

### Can I process PDF files?

Lightning SPP processes **byte arrays** and **streams**. For PDFs:

1. **Use a PDF library** to extract images/pages (e.g., PdfSharp, iTextSharp)
2. **Convert to byte array**
3. **Process with Lightning SPP**

Example:
```csharp
// Extract PDF page as image
var pdfBytes = File.ReadAllBytes("document.pdf");
var imageBytes = ConvertPdfPageToImage(pdfBytes, pageNumber: 0);

// Process with Lightning SPP
var result = await LightningAPI.QuickScanAsync(imageBytes);
```

### Does it work with DICOM medical images?

**Yes, with integration!** Use a DICOM library (e.g., fo-dicom):

```csharp
using FellowOakDicom.Imaging;

// Load DICOM
var dicomFile = DicomFile.Open("scan.dcm");
var dicomImage = new DicomImage(dicomFile.Dataset);

// Convert to byte array
var imageBytes = dicomImage.RenderImage().AsBytes();

// Process with Lightning SPP (NanoDecibel mode for medical quality)
var instance = UniversalSDK.ForDevice(DeviceProfile.Medical);
var result = await instance.ProcessAsync(
    imageBytes, 
    ScanMode.NanoDecibel
);
```

---

## Performance & Optimization

### Why is my processing slower than expected?

**Common causes:**

1. **Using SIMULATED mode in production**
   - Solution: `LightningAPI.UseRealServices()`

2. **Network latency (cloud deployments)**
   - Solution: Deploy closer to data source, use CDN

3. **Large file sizes without chunking**
   - Solution: Use streaming API or chunking

4. **Wrong device profile**
   - Solution: Match profile to actual hardware

5. **Insufficient memory**
   - Solution: Use IoT/Embedded profiles with memory limits

### How do I optimize for mobile devices?

Use the **Mobile device profile** with battery optimization:

```csharp
var instance = UniversalSDK.ForDevice(DeviceProfile.Mobile);
instance.BatteryOptimized = true;
instance.MaxConcurrency = 2; // Limit parallelism

var result = await instance.ProcessAsync(data);
```

### Can I run Lightning SPP serverless (Azure Functions, AWS Lambda)?

**Yes!** Lightning SPP is stateless and perfect for serverless:

**Azure Functions:**
```csharp
[FunctionName("ProcessDocument")]
public static async Task<IActionResult> Run(
    [HttpTrigger(AuthorizationLevel.Function, "post")] 
    HttpRequest req)
{
    var data = await ReadBodyAsync(req);
    var result = await Lightning314.UltraFast(data);
    return new OkObjectResult(result);
}
```

**AWS Lambda:**
```csharp
public async Task<APIGatewayProxyResponse> FunctionHandler(
    APIGatewayProxyRequest request, 
    ILambdaContext context)
{
    var data = Convert.FromBase64String(request.Body);
    var result = await Lightning314.Lightning(data);
    
    return new APIGatewayProxyResponse
    {
        StatusCode = 200,
        Body = JsonSerializer.Serialize(result)
    };
}
```

---

## Compliance & Security

### Is Lightning SPP HIPAA compliant?

**Yes**, when configured correctly:

1. ✅ **Data Encryption**: All data encrypted in transit (TLS 1.3)
2. ✅ **No Data Retention**: Lightning SPP doesn't store processed data
3. ✅ **Audit Logging**: Enable with `LightningAPI.EnableAuditLogging()`
4. ✅ **BAA Available**: Business Associate Agreement for healthcare customers

**Important**: You must sign a BAA for HIPAA compliance. Contact clisonix@pm.me.

### What about GDPR?

Lightning SPP is **GDPR compliant** by design:

- ✅ **No Personal Data Collection**: Processes data in-memory only
- ✅ **Data Minimization**: Only processes what you send
- ✅ **Right to Erasure**: No persistent storage (unless you configure it)
- ✅ **Data Portability**: Standard byte array I/O
- ✅ **Privacy by Design**: Configurable data retention policies

See [PRIVACY.md](../PRIVACY.md) for details.

### Is my data sent to external servers?

**NO!** Lightning SPP processes everything **locally** on your infrastructure:

- ❌ No cloud API calls (unless you deploy to cloud yourself)
- ❌ No telemetry by default
- ❌ No analytics tracking
- ✅ 100% on-premise processing

**License validation** requires internet for activation, but processing is fully offline.

---

## Troubleshooting

### "License validation failed" error

**Possible causes:**

1. **Expired license**
   - Solution: Renew at https://aba-gmbh.de/lightning-spp

2. **Invalid key format**
   - Solution: Copy key exactly as provided (including dashes)

3. **Corrupted license file**
   - Solution: Delete `%APPDATA%\LightningSPP\license.json` and re-activate

4. **System clock incorrect**
   - Solution: Sync system time with NTP server

### "Operation limit reached" message

You've hit your tier's monthly limit:

- **FREE tier**: 1,000 operations/month
- **PAY-AS-YOU-GO**: Check billing
- **UNLIMITED**: Should never happen (report bug if you see this)

**Solution**: Upgrade tier or wait for next billing cycle.

### Docker container won't start

**Check logs:**
```bash
docker logs <container-id>
```

**Common fixes:**

1. **Port conflict**
   ```bash
   docker run -p 8080:80 abagmbh/lightning-spp:3.14
   ```

2. **Memory limit too low**
   ```bash
   docker run --memory="512m" abagmbh/lightning-spp:3.14
   ```

3. **Missing volume mount**
   ```bash
   docker run -v /data:/app/data abagmbh/lightning-spp:3.14
   ```

---

## Integration

### How do I integrate with existing .NET applications?

**1. Add NuGet package:**
```bash
dotnet add package LightningSPP
```

**2. Add using statement:**
```csharp
using LightningSPP;
using LightningSPP.Licensing;
```

**3. Initialize once (at startup):**
```csharp
// Optional: Activate license
LicenseManager.ActivateLicense("LSPP-UNLTD-...");

// Configure services (REAL or SIMULATED)
LightningAPI.UseRealServices();
```

**4. Use anywhere in your code:**
```csharp
var result = await LightningAPI.QuickScanAsync(imageData);
```

### Can I use Lightning SPP with Blazor?

**Yes!** Both Blazor Server and Blazor WebAssembly:

**Blazor Server:**
```csharp
@inject ILightningSPP Lightning

<button @onclick="ProcessImage">Process</button>

@code {
    private async Task ProcessImage()
    {
        var result = await Lightning.QuickScanAsync(imageBytes);
        // Update UI with result
    }
}
```

**Blazor WebAssembly** (note: WASM has some limitations):
- REAL processing works with limitations
- Consider calling backend API instead for heavy processing

### Integration with Python/Node.js/Other languages?

Use the **REST API** wrapper (see Web API section below) or Docker container:

**Python Example:**
```python
import requests

response = requests.post(
    'http://localhost:5000/api/scan',
    files={'file': open('image.jpg', 'rb')},
    data={'mode': 'Lightning'}
)

result = response.json()
print(f"Processed in {result['timeMs']}ms")
```

---

## Pricing & Business

### Can I get a refund?

**Yes**, within **30 days** of purchase if:
- You haven't activated the license
- OR you've used < 100 operations

Email clisonix@pm.me with your license key.

### Do you offer volume discounts?

**Yes, for ENTERPRISE tier:**

| Locations | Discount |
|-----------|----------|
| 6-10 | 15% off |
| 11-25 | 25% off |
| 26+ | 35% off |

Contact clisonix@pm.me for custom quote.

### Can I pay monthly instead of annually?

**PAY-AS-YOU-GO** tier is monthly billing:
- €0.001/operation
- Minimum €9.99/month
- Unlimited operations

**UNLIMITED** tier is annual only (better value):
- €99.99/year = €8.33/month effective

### Do you accept cryptocurrency?

Currently **no**, but it's under consideration. We accept:
- Credit/Debit Cards (Visa, Mastercard, Amex)
- PayPal
- Bank Transfer (SEPA for EU customers)

---

## Development & Contributing

### Can I contribute to Lightning SPP?

**Yes!** We welcome contributions:

1. **Fork** the repository
2. **Create** a feature branch
3. **Submit** a pull request

See [CONTRIBUTING.md](Contributing.md) for guidelines.

### How do I report a bug?

**GitHub Issues**: https://github.com/Web8kameleon-hub/Lightning-SPP-3.14/issues

**Include:**
- Lightning SPP version
- .NET version
- Operating system
- Minimal reproducible example
- Expected vs actual behavior

### Where can I request features?

**GitHub Discussions**: https://github.com/Web8kameleon-hub/Lightning-SPP-3.14/discussions

Vote on existing requests or create new ones!

---

## Support

### How do I get support?

| Tier | Support Channel | Response Time |
|------|----------------|---------------|
| **FREE** | GitHub Issues, Community | Best effort |
| **PAY-AS-YOU-GO** | Email (clisonix@pm.me) | 48 hours |
| **UNLIMITED** | Priority Email | 24 hours |
| **ENTERPRISE** | Dedicated Support | 4 hours (SLA) |

### Is there a community forum?

**GitHub Discussions**: https://github.com/Web8kameleon-hub/Lightning-SPP-3.14/discussions

Join to:
- Ask questions
- Share use cases
- Get help from community
- Vote on features

---

**Still have questions?**

📧 Email: clisonix@pm.me  
💬 Discussions: https://github.com/Web8kameleon-hub/Lightning-SPP-3.14/discussions  
🐛 Issues: https://github.com/Web8kameleon-hub/Lightning-SPP-3.14/issues

---

*© 2025-2026 ABA GmbH. All rights reserved.*
