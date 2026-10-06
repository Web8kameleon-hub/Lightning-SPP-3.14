# 📝 Migration Guide: Upgrading to Lightning SPP 3.14

This guide helps you migrate from previous versions or integrate Lightning SPP into existing applications.

---

## Table of Contents

- [From 2.x to 3.x](#from-2x-to-3x)
- [From Legacy Systems](#from-legacy-systems)
- [New Projects](#new-projects)
- [Breaking Changes](#breaking-changes)
- [Deprecated Features](#deprecated-features)

---

## From 2.x to 3.x

### Major Changes

**1. Licensing System Added**

Lightning SPP 3.x introduces a professional licensing system:

```csharp
// OLD (2.x): No licensing
using LightningSPP;
var result = await LightningAPI.QuickScanAsync(data);

// NEW (3.x): Optional licensing for production
using LightningSPP;
using LightningSPP.Licensing;

// Activate license (production only)
LicenseManager.ActivateLicense("LSPP-UNLTD-XXXX-XXXX-XXXX-XXXX");

// Use normally (FREE tier works without activation)
var result = await LightningAPI.QuickScanAsync(data);
```

**2. REAL vs SIMULATED Processing**

```csharp
// OLD (2.x): Always REAL
// No configuration needed

// NEW (3.x): Choose mode
LightningAPI.UseRealServices();      // Production (default)
LightningAPI.UseSimulatedServices(); // Testing
```

**3. Universal SDK Enhancements**

```csharp
// OLD (2.x): Single API
var result = await Lightning.ProcessAsync(data);

// NEW (3.x): Device-specific optimization
var instance = UniversalSDK.ForDevice(DeviceProfile.Medical);
var result = await instance.ProcessAsync(data);
```

### Step-by-Step Migration

**Step 1: Update NuGet Package**

```bash
# Remove old version
dotnet remove package LightningSPP --version 2.x

# Add new version
dotnet add package LightningSPP --version 3.14.0
```

**Step 2: Update Using Statements**

```csharp
// Add new namespace for licensing
using LightningSPP.Licensing;
```

**Step 3: Configure Services (Optional)**

```csharp
// At application startup
public void ConfigureServices(IServiceCollection services)
{
    // Configure Lightning SPP
    LightningAPI.UseRealServices(); // Or UseSimulatedServices() for testing

    // Optional: Activate license
    if (Environment.GetEnvironmentVariable("LIGHTNING_LICENSE_KEY") is string key)
    {
        LicenseManager.ActivateLicense(key);
    }
}
```

**Step 4: Update Code (Minimal Changes)**

Most code continues to work without changes:

```csharp
// This still works in 3.x!
var result = await LightningAPI.QuickScanAsync(imageData);
var processedData = await LightningAPI.AIProcessAsync(result.Data);
await LightningAPI.StigmaPrintAsync(processedData.Data);
```

**Step 5: Test Thoroughly**

```csharp
// Run existing tests
dotnet test

// Run new speed benchmarks
dotnet run -- speed

// Test REAL vs SIMULATED
dotnet run -- real
```

---

## From Legacy Systems

### Replacing ImageSharp Direct Usage

**Before (direct ImageSharp):**
```csharp
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

using var image = Image.Load(filePath);
image.Mutate(x => x
    .DetectEdges()
    .GaussianSharpen()
    .Contrast(1.5f)
);
var bytes = ImageToBytes(image);
```

**After (Lightning SPP):**
```csharp
using LightningSPP;

var imageBytes = File.ReadAllBytes(filePath);
var result = await LightningAPI.HighQualityScanAsync(
    imageBytes,
    ScanMode.Resonance // Includes edge detection + sharpening
);
```

### Replacing Custom ML Code

**Before (Microsoft.ML direct):**
```csharp
using Microsoft.ML;

var mlContext = new MLContext();
var model = mlContext.Model.Load("model.zip", out var schema);
var predictionEngine = mlContext.Model.CreatePredictionEngine<Input, Output>(model);
var prediction = predictionEngine.Predict(input);
```

**After (Lightning SPP):**
```csharp
using LightningSPP;

var result = await LightningAPI.AIProcessAsync(
    data,
    ProcessingEngine.Clisonix // AI-powered optimization
);
```

### Replacing Custom Print Pipeline

**Before (custom pipeline):**
```csharp
async Task ProcessDocument(string filePath)
{
    // Step 1: Load
    var bytes = await File.ReadAllBytesAsync(filePath);

    // Step 2: Process
    var processed = await CustomImageProcessor(bytes);

    // Step 3: Compress
    var compressed = await CompressData(processed);

    // Step 4: Save
    await File.WriteAllBytesAsync(outputPath, compressed);
}
```

**After (Lightning SPP):**
```csharp
async Task ProcessDocument(string filePath)
{
    var result = await LightningAPI.ExecuteLightningPipelineAsync(
        source: filePath,
        scanMode: ScanMode.Lightning,
        processingEngine: ProcessingEngine.Hybrid,
        printQuality: PrintQuality.High
    );
    // Done in one call!
}
```

---

## New Projects

### Starting from Scratch

**1. Create Project**
```bash
dotnet new console -n MyLightningApp
cd MyLightningApp
```

**2. Add Lightning SPP**
```bash
dotnet add package LightningSPP
```

**3. Write Code**
```csharp
using LightningSPP;

// Simple example
var data = File.ReadAllBytes("image.jpg");
var result = await Lightning314.Instant(data); // < 1ms
Console.WriteLine($"Processed in {result.TimeMs}ms");
```

**4. Run**
```bash
dotnet run
```

### Recommended Project Structure

```
MyLightningApp/
├── Program.cs              # Entry point
├── Services/
│   ├── DocumentService.cs  # Business logic
│   └── LightningConfig.cs  # Lightning SPP configuration
├── Models/
│   └── DocumentModel.cs    # Data models
├── appsettings.json        # Configuration (license key, etc.)
└── MyLightningApp.csproj
```

**appsettings.json:**
```json
{
  "LightningSPP": {
    "LicenseKey": "LSPP-UNLTD-XXXX-XXXX-XXXX-XXXX",
    "Mode": "Real",
    "LogLevel": "Information"
  }
}
```

**LightningConfig.cs:**
```csharp
public static class LightningConfig
{
    public static void Initialize(IConfiguration config)
    {
        var settings = config.GetSection("LightningSPP");

        // Activate license
        var licenseKey = settings["LicenseKey"];
        if (!string.IsNullOrEmpty(licenseKey))
        {
            LicenseManager.ActivateLicense(licenseKey);
        }

        // Configure mode
        var mode = settings["Mode"];
        if (mode == "Real")
            LightningAPI.UseRealServices();
        else
            LightningAPI.UseSimulatedServices();
    }
}
```

---

## Breaking Changes

### 3.0.0 → 3.14.0

**No breaking changes!** Lightning SPP 3.14 is fully backward compatible with 3.0.x.

### 2.x → 3.x

**1. Namespace Changes**

| Old (2.x) | New (3.x) |
|-----------|-----------|
| `Lightning.Core` | `LightningSPP` |
| `Lightning.SDK` | `LightningSPP.SDK` |
| N/A | `LightningSPP.Licensing` (NEW) |

**Migration:**
```csharp
// OLD
using Lightning.Core;
using Lightning.SDK;

// NEW
using LightningSPP;
using LightningSPP.SDK;
using LightningSPP.Licensing; // If using licensing
```

**2. Configuration API**

| Old (2.x) | New (3.x) |
|-----------|-----------|
| `Lightning.Configure(...)` | `LightningAPI.Configure(...)` |
| N/A | `LightningAPI.UseRealServices()` (NEW) |
| N/A | `LightningAPI.UseSimulatedServices()` (NEW) |

**3. Removed Methods**

These methods were removed in 3.x:

```csharp
// REMOVED in 3.x:
Lightning.SetGlobalOptions(...)  // Use LightningConfiguration instead
Lightning.Initialize()           // Auto-initialized now
Lightning.Dispose()              // No longer needed
```

**Replacement:**
```csharp
// OLD (2.x)
Lightning.SetGlobalOptions(new GlobalOptions { ... });
Lightning.Initialize();

// NEW (3.x)
LightningAPI.Configure(new LightningConfiguration
{
    UseRealScanService = true,
    UseRealProcessingService = true,
    UseRealPrintService = true
});
```

---

## Deprecated Features

### Still Supported (but not recommended)

**1. Direct Service Instantiation**

```csharp
// DEPRECATED (still works):
var scanService = new FastScanService();
var result = await scanService.ScanAsync(...);

// RECOMMENDED:
var result = await LightningAPI.QuickScanAsync(...);
```

**2. Manual Pipeline Construction**

```csharp
// DEPRECATED (still works):
var pipeline = new LightningPipeline(scanService, processService, printService);
var result = await pipeline.ExecuteAsync(...);

// RECOMMENDED:
var result = await LightningAPI.ExecuteLightningPipelineAsync(...);
```

**3. Hard-coded Configuration**

```csharp
// DEPRECATED:
var config = new LightningConfiguration
{
    UseRealScanService = true,
    // ... hard-coded values
};

// RECOMMENDED:
// Load from appsettings.json or environment variables
var licenseKey = Environment.GetEnvironmentVariable("LIGHTNING_LICENSE_KEY");
LicenseManager.ActivateLicense(licenseKey);
```

---

## Rollback Plan

If you encounter issues, you can temporarily rollback:

**Step 1: Downgrade Package**
```bash
dotnet add package LightningSPP --version 2.9.0
```

**Step 2: Remove New Code**
```csharp
// Remove licensing code
// using LightningSPP.Licensing; // DELETE
// LicenseManager.ActivateLicense(...); // DELETE
```

**Step 3: Revert Namespace Changes**
```csharp
// Revert to old namespaces
using Lightning.Core;
using Lightning.SDK;
```

**Important:** Report issues at https://github.com/Web8kameleon-hub/Lightning-SPP-3.14/issues

---

## Migration Checklist

- [ ] Backup existing code
- [ ] Update NuGet package to 3.14.0
- [ ] Update using statements
- [ ] Configure REAL/SIMULATED mode
- [ ] (Optional) Activate production license
- [ ] Run existing tests
- [ ] Run new speed benchmarks
- [ ] Update CI/CD pipelines
- [ ] Update documentation
- [ ] Train team on new features
- [ ] Monitor production deployment

---

## Getting Help

**Migration Support:**

- **Community**: GitHub Discussions
- **Email**: clisonix@pm.me
- **Priority Support**: Available for UNLIMITED and ENTERPRISE customers

**Resources:**

- [Full Changelog](https://github.com/Web8kameleon-hub/Lightning-SPP-3.14/releases)
- [API Documentation](https://github.com/Web8kameleon-hub/Lightning-SPP-3.14/wiki/API-Documentation)
- [Examples](https://github.com/Web8kameleon-hub/Lightning-SPP-3.14/tree/main/Examples)

---

*© 2025-2026 ABA GmbH. All rights reserved.*
