# Lightning SPP — Functions Reference

**Namespace:** `wwwmmm`  
**NuGet:** `LightningSPP 3.14.0`  

**Platform:** .NET 10.0 / C# 13  
**Architecture:** CLX/WWWMMM/XLC/NDB/Stigma pipeline

---

## 1. Architecture Overview

Lightning SPP (Scan–Process–Print) is a physical resonance processing platform.  
The full pipeline:


[Scanner] → [NDB Node] → [TideWave] → [WWWMMM Mesh] → [Stigma Gate] → [Printer]
```

| Symbol          | Meaning                                                                         |
| --------------- | ------------------------------------------------------------------------------- |
| **WWWMMM**      | World-Wide Wave-Mesh Multi-Modal — 6-dimensional physical resonance fingerprint |
| **XLC**         | Resonance Codec (XLCCodec / XLCKey / XLCLock / LayerBuilder / LayerStack)       |
| **CLX**         | Clisonix Lightweight eXtensions — compression + AI runtime                      |
| **NDB**         | NodeDB — binary resonance node store (`blake2b` ID, `>ddi` binary format)       |
| **Stigma**      | Behavioral profile gate (LIGHTNING / NORMAL / CONSERVATIVE)                     |
| **TideWave**    | Wave propagation mechanism (GaussianSharpen + Contrast)                         |
| **Resonance**   | DetectEdges + GaussianSharpen(3.0f)                                             |
| **NanoDecibel** | `20 × log10(score) × 10⁹` — 10⁹× precision vs standard decibel                  |

---

## 2. SDK — `SDK/Lightning314.cs`

Core processing engines based on CLX/CLI.I/XCL Brotli pipelines.

### `Lightning314.Instant(byte[] data) → Task<byte[]>`

- **Engine:** CLX — Brotli `CompressionLevel.Fastest`
- **Target latency:** < 1 ms
- **Use case:** Real-time medical devices, IoT sensors, embedded systems
- Real Brotli stream compression — no simulation

### `Lightning314.UltraFast(byte[] data) → Task<byte[]>`

- **Engine:** CLI.I — Brotli `CompressionLevel.Optimal`
- **Target latency:** < 5 ms
- **Use case:** Mobile apps, web APIs, general-purpose pipelines
- Balanced speed/quality

### `Lightning314.Lightning(byte[] data) → Task<byte[]>`

- **Engine:** XCL — Brotli `CompressionLevel.SmallestSize` + `MemoryPool<byte>`
- **Target latency:** < 10 ms
- **Use case:** Desktop apps, complex processing, archive quality

### `Lightning314.Stream(Func<byte[],Task<byte[]>> pipeline, byte[] data) → Task<byte[]>`

- Chains multiple processing steps as a pipeline delegate

### `Lightning314.Parallel(byte[][] dataSet, Func<byte[],Task<byte[]>> pipeline) → Task<byte[][]>`

- Runs processing in parallel across a dataset

---

## 3. Universal SDK — `SDK/UniversalSDK.cs`

Device-profile-aware processing with per-device Brotli tuning.

### `ProcessAsync(byte[] data, DeviceType device) → Task<byte[]>`

Dispatches to the appropriate profile based on `DeviceType`.

| Method                     | Device    | Engine                | Brotli Level              | Max Memory | CPU Threads |
| -------------------------- | --------- | --------------------- | ------------------------- | ---------- | ----------- |
| `MedicalOptimizedProcess`  | Medical   | Resonance NanoDecibel | SmallestSize + pattern    | 512 MB     | 4           |
| `DesktopOptimizedProcess`  | Desktop   | XCL                   | SmallestSize + MemoryPool | unlimited  | 8           |
| `CloudOptimizedProcess`    | Cloud     | XCL                   | SmallestSize + MemoryPool | unlimited  | 16          |
| `MobileOptimizedProcess`   | Mobile    | CLI.I                 | Optimal                   | 256 MB     | 2           |
| `WebOptimizedProcess`      | Web       | CLI.I                 | Optimal                   | 128 MB     | 4           |
| `UniversalProcess`         | Universal | CLI.I                 | Optimal                   | —          | —           |
| `IoTOptimizedProcess`      | IoT       | CLX                   | Fastest                   | 64 KB      | 1           |
| `EmbeddedOptimizedProcess` | Embedded  | CLX                   | Fastest                   | 32 KB      | 1           |

### `RealTimeProcessAsync(byte[] data) → Task<byte[]>`

- Ultra-low latency — CLX Fastest in direct MemoryStream (no device overhead)

### Device Profiles (static `DeviceProfile` records)

```csharp
UniversalSDK.DeviceProfile.Medical  // MaxMemory=512MB, MaxCpu=4
UniversalSDK.DeviceProfile.Mobile   // MaxMemory=256MB, MaxCpu=2
UniversalSDK.DeviceProfile.IoT      // MaxMemory=64KB,  MaxCpu=1
UniversalSDK.DeviceProfile.Embedded // MaxMemory=32KB,  MaxCpu=1
UniversalSDK.DeviceProfile.Desktop  // MaxMemory=0(unlimited), MaxCpu=8
UniversalSDK.DeviceProfile.Web      // MaxMemory=128MB, MaxCpu=4
UniversalSDK.DeviceProfile.Cloud    // MaxMemory=0(unlimited), MaxCpu=16
```

---

## 4. AI Processor — `Services/Implementation/RealAIProcessor.cs`

### `ClxProcessAsync(byte[] data) → Task<byte[]>`

- CLX engine: Brotli Fastest

### `CliIProcessAsync(byte[] data) → Task<byte[]>`

- CLI.I engine: Brotli Optimal

### `XclProcessAsync(byte[] data) → Task<byte[]>`

- XCL engine: Brotli SmallestSize + MemoryPool

### `HybridProcessAsync(byte[] data) → Task<byte[]>`

- Combines CLX + XCL stages sequentially

### `AutoSelectEngineAsync(byte[] data) → Task<byte[]>`

- Size-based auto-routing:
  - `< 1 KB` → CLX
  - `< 100 KB` → CLI.I
  - `< 1 MB` → XCL
  - `≥ 1 MB` → Hybrid

### `MLOptimizeAsync(byte[] data) → Task<byte[]>`

- XOR pattern optimization: `data[i] ^= 0x01` where `data[i] == data[i-1]`
- Removes consecutive duplicate bytes for better compression ratios

### `CompressWithBrotliAsync(byte[] data, CompressionLevel level) → Task<byte[]>`

- Generic Brotli compression helper used by all other methods

---

## 5. Print Service — `Services/Implementation/RealPrintService.cs`

### `PrintAsync(byte[] data, PrintOptions options, CancellationToken) → Task<PrintResult>`

- Routes to Stigma or quality-based file output
- Output directory: `%TEMP%\LightningSPP\`
- File naming: `output_{quality}_{timestamp}.dat`

### `StigmaPrintAsync(byte[] data, CancellationToken) → Task<PrintResult>`

- NanoDecibel-precision output (quality=2400)
- Uses `ApplyResonanceEnhancementAsync` + Brotli SmallestSize

### `ApplyResonanceEnhancementAsync(byte[] data) → Task<byte[]>`

- Byte-level smoothing: `data[i] = (data[i] + data[i+1]) / 2`
- Increases signal coherence for high-precision output

### `CompressWithMaxQualityAsync(byte[] data) → Task<byte[]>`

- Brotli SmallestSize — highest fidelity compression

### `GetPrintedFiles() → string[]`

- Returns list of all output files, ordered by creation date (newest first)

### `ClearPrintQueue()`

- Deletes all output files in the temp directory

### `CleanupOldFiles(int olderThanDays = 7)`

- Deletes output files older than the specified number of days
- Call periodically to prevent temp directory accumulation

---

## 6. Image Processor — `Services/Implementation/RealImageProcessor.cs`

### TideWave Enhancement

- `GaussianSharpen(1.2f)` + `Contrast(1.12f)` via SixLabors.ImageSharp

### Resonance Enhancement

- `DetectEdges()` + `GaussianSharpen(3.0f)`

### NanoDecibel Mode

- Resonance pipeline with quality=2400

---

## 7. Scan Service — `Services/Implementation/RealScanService.cs`

### `ScanAsync(ScanOptions options) → Task<ScanResult>`

- Reads files from disk or byte input
- Returns raw binary data ready for pipeline processing

---

## 8. Lightning Pipeline — `Services/LightningPipeline.cs`

### `ExecuteAsync(byte[] input, PipelineOptions options) → Task<PipelineResult>`

- Orchestrates the full pipeline:  
  `Scan → AI Process → (optional Stigma) → Print`
- Selects engine automatically based on profile

---

## 9. Licensing — `Licensing/`

### `LicenseKey.GenerateKeyString(LicenseKeyInfo info) → string`

- Format: `LSPP-{TIER}-{SEG1}-{SEG2}-{SEG3}-{CHECKSUM}`
- Checksum: first 8 bytes (64-bit) of SHA-256 of payload

### `LicenseKey.Validate(string keyString) → LicenseValidationResult`

- Verifies checksum + tier + expiry

### `LicenseManager.LoadAsync() → Task<LicenseInfo>`

- Reads `license.json` from app directory

### `LicenseManager.ActivateAsync(string key) → Task<ActivationResult>`

- Activates license locally

---

## 10. REST API — `Web/LicenseApiServer.cs`

Run with: `LicenseApiServer.RunAsync(port)`

| Endpoint                | Method | Auth Required                               | Description                    |
| ----------------------- | ------ | ------------------------------------------- | ------------------------------ |
| `/api/license/issue`    | POST   | ✅ Bearer token (`LSPP_ADMIN_TOKEN` env var) | Issue a new license key        |
| `/api/license/activate` | POST   | ❌                                           | Activate an existing key       |
| `/api/license/validate` | GET    | ❌                                           | Validate a key                 |
| `/api/status`           | GET    | ❌                                           | Server status                  |
| `/*` (static)           | GET    | ❌                                           | Serve HTML from `Web/wwwroot/` |

**Issuing a license (`/api/license/issue`):**

```
Authorization: Bearer <LSPP_ADMIN_TOKEN>
Content-Type: application/x-www-form-urlencoded

email=user@company.com&tier=pro&company=ACME&devices=5&validityDays=365
```

---

## 11. Stigma Profile System

Stigma gates control quality/speed behavior based on NanoDecibel thresholds:

| Profile      | Threshold (NdB) | Aggressive | Batch Size | Cache TTL |
| ------------ | --------------- | ---------- | ---------- | --------- |
| LIGHTNING    | −10 NdB         | ✅ Yes      | 256        | 10 s      |
| NORMAL       | −40 NdB         | ❌ No       | 64         | 30 s      |
| CONSERVATIVE | −80 NdB         | ❌ No       | 16         | 60 s      |

**NanoDecibel formula:**  
$$\text{NdB} = 20 \times \log_{10}(\text{score}) \times 10^9$$

---

## 12. CLX/XLC Compression Map

| Symbol | Compression Level           | Use Case                 |
| ------ | --------------------------- | ------------------------ |
| CLX    | `Fastest`                   | IoT, Embedded, Real-time |
| CLI.I  | `Optimal`                   | Mobile, Web, General     |
| XCL    | `SmallestSize` + MemoryPool | Desktop, Cloud, Archive  |

---

## 13. WWWMMM Resonance Profile (from Clisonix Ecosystem)

WWWMMM is the 6-dimensional physical resonance fingerprint used to identify XLC symbols:

| Dimension | Field          | Description                 |
| --------- | -------------- | --------------------------- |
| W1        | `w1_amplitude` | 4 amplitude samples         |
| W2        | `w2_phase`     | 4 phase samples             |
| W3        | `w3_coherence` | 4 coherence samples         |
| M1        | `m1_frequency` | 4 frequency samples         |
| M2        | `m2_harmonic`  | 4 harmonic samples          |
| M3        | `m3_jitter_ns` | 4 nanosecond jitter samples |

Total: **24 floats** per fingerprint → `to_vector()` → cosine similarity matching.

---

## 14. NDB Node Format (NodeDB)

Binary node structure used by the pipeline:

```
ID: blake2b(input_text:timestamp_ns, digest_size=16).hexdigest()
Data: struct.pack(">ddi", ndb_value, db_value, ts_ns & 0x7FFFFFFF)
```

---

*Patent pending: WWWMMM-NDB-STIGMA — DPMA Germany + WIPO (filed 2026-05-09)*  
*Lightning SPP © Web8kameleon-hub — Licensed under LightningSPP License System*
