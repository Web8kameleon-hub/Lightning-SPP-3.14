# 🔧 Troubleshooting Guide

Common issues and their solutions for Lightning SPP 3.14.

---

## Table of Contents

- [Installation Issues](#installation-issues)
- [License Issues](#license-issues)
- [Performance Issues](#performance-issues)
- [Build/Compilation Errors](#buildcompilation-errors)
- [Runtime Errors](#runtime-errors)
- [Docker Issues](#docker-issues)
- [Integration Issues](#integration-issues)

---

## Installation Issues

### NuGet Package Not Found

**Symptom:**
```
error NU1101: Unable to find package 'LightningSPP'
```

**Solutions:**

1. **Check NuGet source:**
   ```bash
   dotnet nuget list source
   ```
   Ensure `nuget.org` is listed.

2. **Clear NuGet cache:**
   ```bash
   dotnet nuget locals all --clear
   dotnet restore
   ```

3. **Specify source explicitly:**
   ```bash
   dotnet add package LightningSPP --source https://api.nuget.org/v3/index.json
   ```

### Visual Studio Extension Won't Install

**Symptom:**
```
This extension is not compatible with your version of Visual Studio
```

**Solutions:**

1. **Check VS version**: Lightning SPP requires **Visual Studio 2022 17.8+** or **Visual Studio 2026**

2. **Update Visual Studio**:
   - Tools → Get Tools and Features
   - Update to latest version

3. **Install prerequisites**:
   - .NET 10.0 SDK
   - .NET desktop development workload

### Docker Image Pull Fails

**Symptom:**
```
Error response from daemon: manifest for abagmbh/lightning-spp:3.14 not found
```

**Solutions:**

1. **Check Docker Hub**: Verify image exists at https://hub.docker.com/r/abagmbh/lightning-spp

2. **Try without tag:**
   ```bash
   docker pull abagmbh/lightning-spp:latest
   ```

3. **Login to Docker Hub** (if private repository):
   ```bash
   docker login
   ```

---

## License Issues

### "License validation failed"

**Symptom:**
```
❌ Invalid license key format.
```

**Solutions:**

1. **Check key format**: Must be `LSPP-TIER-XXXX-XXXX-XXXX-XXXX`
   
   ✅ Correct: `LSPP-UNLTD-3F4D-6235-618C-7E23`  
   ❌ Wrong: `LSPP-UNLTD-3F4D` (too short)  
   ❌ Wrong: `lspp-unltd-3f4d-6235-618c-7e23` (lowercase)

2. **Copy key exactly** (avoid typos):
   - Don't add extra spaces
   - Don't remove dashes
   - Case-sensitive for checksum segments

3. **Verify from original email/purchase receipt**

### "License expired"

**Symptom:**
```
Status: ✗ EXPIRED
```

**Solutions:**

1. **Check expiration date:**
   ```bash
   wwwmmm license
   ```

2. **Renew license** at https://aba-gmbh.de/lightning-spp

3. **Temporary FREE tier fallback:**
   - System automatically falls back to FREE tier (1,000 ops/month)
   - Your data is safe, just features are limited

### "License file corrupted"

**Symptom:**
```
Error reading license from C:\Users\...\AppData\Roaming\LightningSPP\license.json
```

**Solutions:**

1. **Delete corrupted file:**
   ```powershell
   Remove-Item "$env:APPDATA\LightningSPP\license.json"
   ```

2. **Re-activate:**
   ```bash
   wwwmmm activate YOUR-LICENSE-KEY
   ```

### "Operation limit reached"

**Symptom:**
```
❌ Monthly operation limit (1,000) reached.
```

**Solutions:**

1. **Check tier limits:**
   ```bash
   wwwmmm license
   ```

2. **Upgrade tier:**
   - FREE → UNLIMITED: €99.99/year
   - Contact clisonix@pm.me

3. **Wait for billing cycle reset** (1st of month)

4. **Use multiple devices** (if tier allows)

---

## Performance Issues

### Processing is slower than advertised

**Symptom:**
```
Expected: < 10ms
Actual: 500ms
```

**Diagnostic Steps:**

1. **Check if using SIMULATED mode:**
   ```csharp
   // Switch to REAL mode
   LightningAPI.UseRealServices();
   ```

2. **Verify speed mode:**
   ```csharp
   // Use fastest mode
   var result = await Lightning314.Instant(data); // < 1ms target
   ```

3. **Check data size:**
   - Large files (>10MB) take longer
   - Solution: Use streaming or chunking

4. **Profile performance:**
   ```bash
   wwwmmm speed  # Run built-in benchmark
   ```

**Expected Results:**
```
Instant:    < 1ms   ✓
UltraFast:  < 5ms   ✓
Lightning:  < 10ms  ✓
```

### High memory usage

**Symptom:**
```
OutOfMemoryException
```

**Solutions:**

1. **Use device profile with memory limits:**
   ```csharp
   var instance = UniversalSDK.ForDevice(DeviceProfile.IoT);
   instance.MemoryLimitMB = 128; // Constrain memory
   ```

2. **Process in chunks:**
   ```csharp
   var chunks = SplitIntoChunks(data, chunkSize: 1024 * 1024); // 1MB chunks
   foreach (var chunk in chunks)
   {
       await Lightning314.Lightning(chunk);
   }
   ```

3. **Dispose resources:**
   ```csharp
   using var stream = new MemoryStream(data);
   var result = await LightningAPI.QuickScanAsync(stream);
   // Stream disposed automatically
   ```

### Batch processing slower than sequential

**Symptom:**
```
Batch (10 items): 500ms
Sequential (10 items): 200ms
```

**Cause:** Too much parallelism overwhelming system.

**Solutions:**

1. **Limit concurrency:**
   ```csharp
   await Lightning314.Parallel(
       items, 
       maxConcurrency: 4  // Instead of default 10
   );
   ```

2. **Match to CPU cores:**
   ```csharp
   int cores = Environment.ProcessorCount;
   maxConcurrency: cores - 1  // Leave 1 core for OS
   ```

---

## Build/Compilation Errors

### CS0234: The type or namespace name does not exist

**Symptom:**
```
error CS0234: The type or namespace name 'LightningSPP' does not exist
```

**Solutions:**

1. **Add using statement:**
   ```csharp
   using LightningSPP;
   using LightningSPP.Licensing;
   ```

2. **Restore NuGet packages:**
   ```bash
   dotnet restore
   ```

3. **Clean and rebuild:**
   ```bash
   dotnet clean
   dotnet build
   ```

### IL2026/IL3050: Trimming warnings with PublishAot

**Symptom:**
```
warning IL2026: Using member which has 'RequiresUnreferencedCodeAttribute'
warning IL3050: Using member which has 'RequiresDynamicCodeAttribute'
```

**Cause:** AOT compilation with JSON serialization.

**Solution:** Lightning SPP 3.14 already uses JSON Source Generators (fixed in latest version). Update to latest:

```bash
dotnet add package LightningSPP --version 3.14.0
```

### NETSDK1045: The current .NET SDK does not support targeting .NET 10.0

**Symptom:**
```
error NETSDK1045: The current .NET SDK does not support '.NET 10.0'
```

**Solution:**

1. **Install .NET 10 SDK**: https://dotnet.microsoft.com/download/dotnet/10.0

2. **Verify installation:**
   ```bash
   dotnet --version
   # Should output 10.0.x
   ```

---

## Runtime Errors

### NullReferenceException in LightningAPI

**Symptom:**
```
System.NullReferenceException: Object reference not set to an instance
```

**Common Causes:**

1. **Services not initialized:**
   ```csharp
   // Initialize BEFORE use
   LightningAPI.UseRealServices();
   
   // THEN use
   var result = await LightningAPI.QuickScanAsync(data);
   ```

2. **Null data passed:**
   ```csharp
   byte[]? data = null;
   // This will fail:
   await LightningAPI.QuickScanAsync(data);
   
   // Fix: Check for null
   if (data != null)
       await LightningAPI.QuickScanAsync(data);
   ```

### FileNotFoundException for SixLabors.ImageSharp

**Symptom:**
```
Could not load file or assembly 'SixLabors.ImageSharp, Version=3.1.5'
```

**Solutions:**

1. **Restore dependencies:**
   ```bash
   dotnet restore
   ```

2. **Explicit package reference:**
   ```xml
   <PackageReference Include="SixLabors.ImageSharp" Version="3.1.5" />
   ```

3. **Clear bin/obj:**
   ```bash
   Remove-Item -Recurse bin, obj
   dotnet build
   ```

### PlatformNotSupportedException on Linux/Mac

**Symptom:**
```
System.PlatformNotSupportedException: Operation is not supported on this platform
```

**Cause:** Some Windows-specific APIs not available on Linux/Mac.

**Solution:** Use device profiles:

```csharp
// Detect OS and use appropriate profile
var instance = OperatingSystem.IsWindows() 
    ? UniversalSDK.ForDevice(DeviceProfile.Desktop)
    : UniversalSDK.ForDevice(DeviceProfile.Cloud); // Linux-friendly

await instance.ProcessAsync(data);
```

---

## Docker Issues

### Container exits immediately

**Symptom:**
```bash
docker run abagmbh/lightning-spp:3.14
# Container exits with code 0
```

**Cause:** No command specified.

**Solution:** Specify a command:

```bash
# Run demo
docker run --rm abagmbh/lightning-spp:3.14 demo

# Run tests
docker run --rm abagmbh/lightning-spp:3.14 test

# Keep container running
docker run -it abagmbh/lightning-spp:3.14 /bin/bash
```

### "Permission denied" errors in container

**Symptom:**
```
Permission denied: '/app/data/output.dat'
```

**Solutions:**

1. **Use volume with proper permissions:**
   ```bash
   mkdir ./data
   chmod 777 ./data
   docker run -v ./data:/app/data abagmbh/lightning-spp:3.14
   ```

2. **Run as specific user:**
   ```bash
   docker run --user $(id -u):$(id -g) abagmbh/lightning-spp:3.14
   ```

### Container out of memory

**Symptom:**
```
Killed (out of memory)
```

**Solutions:**

1. **Increase memory limit:**
   ```bash
   docker run --memory="2g" abagmbh/lightning-spp:3.14
   ```

2. **Use IoT profile for constrained environments:**
   ```csharp
   var instance = UniversalSDK.ForDevice(DeviceProfile.IoT);
   instance.MemoryLimitMB = 512;
   ```

---

## Integration Issues

### Cannot integrate with Blazor WebAssembly

**Symptom:**
```
WASM: System.NotSupportedException: Operation is not supported in Blazor WASM
```

**Cause:** Some operations (file I/O, native libraries) don't work in browser.

**Solution:** Use backend API instead:

**Blazor WASM → Backend API:**
```csharp
// In Blazor WASM:
var response = await Http.PostAsync("/api/process", content);

// In Backend API:
[HttpPost("process")]
public async Task<IActionResult> Process([FromBody] byte[] data)
{
    var result = await LightningAPI.QuickScanAsync(data);
    return Ok(result);
}
```

### Python integration fails

**Symptom:**
```python
ModuleNotFoundError: No module named 'pythonnet'
```

**Solution:** Use REST API or Docker instead of direct .NET interop:

**Option 1: REST API**
```python
import requests

response = requests.post(
    'http://localhost:5000/api/scan',
    files={'file': open('image.jpg', 'rb')}
)
result = response.json()
```

**Option 2: Docker**
```python
import subprocess

result = subprocess.run([
    'docker', 'run', '--rm',
    '-v', f'{os.getcwd()}:/data',
    'abagmbh/lightning-spp:3.14',
    'scan', '/data/image.jpg'
], capture_output=True)
```

### "Access denied" with Azure Functions

**Symptom:**
```
Access to the path '/tmp/LightningSPP' is denied
```

**Solution:** Use in-memory processing (no file I/O):

```csharp
// Don't use file-based services in serverless
LightningAPI.Configure(new LightningConfiguration
{
    UseRealScanService = true,
    UseRealProcessingService = true,
    UseRealPrintService = false  // No disk access in serverless
});
```

---

## Getting Help

If none of these solutions work:

### 1. Check Version

```bash
dotnet list package | grep LightningSPP
# Should show: LightningSPP 3.14.0 or higher
```

### 2. Enable Diagnostic Logging

```csharp
LightningAPI.EnableLogging(LogLevel.Debug);
```

### 3. Collect Information

- Lightning SPP version
- .NET version (`dotnet --version`)
- OS and version (`uname -a` or `winver`)
- Full error message and stack trace
- Minimal reproducible code example

### 4. Report Issue

**GitHub Issues**: https://github.com/Web8kameleon-hub/Lightning-SPP-3.14/issues

**Include:**
- ✅ Version information
- ✅ Error message
- ✅ Reproducible example
- ✅ Expected vs actual behavior

### 5. Contact Support

| Tier | Support |
|------|---------|
| FREE | GitHub Issues |
| UNLIMITED | clisonix@pm.me (24h response) |
| ENTERPRISE | Dedicated support (4h SLA) |

---

**Still stuck?**

📧 clisonix@pm.me  
💬 https://github.com/Web8kameleon-hub/Lightning-SPP-3.14/discussions

---

*© 2025-2026 ABA GmbH. All rights reserved.*
