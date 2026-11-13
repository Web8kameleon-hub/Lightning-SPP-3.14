# Migration guide — moving to Lightning SPP 3.14

This page lists what changed and the code that actually exists in this
repository. Earlier revisions of this page documented APIs that are not present
in the codebase; those are listed in "Never existed" below so nobody codes
against them again.

---

## 1. What changed in 3.14

### 1.1 The simulated processing path was removed

There is no simulated mode. `LightningAPI.UseSimulatedServices()`,
`LightningConfiguration.FastSimulated` and the CLI command `simulated` do not
exist and do not compile. Any code that calls them must be deleted, not ported.

```csharp
// Wrong — does not compile in 3.14
LightningAPI.UseSimulatedServices();

// Correct
LightningAPI.UseRealServices();
```

### 1.2 License keys are asymmetric

New keys use the `LSP3` format, signed with ECDSA P-256 and verified with a
**public** key. Legacy `LSP2` HMAC keys still validate on an issuer that holds
the HMAC secret.

| Format | Verified with | Where it belongs |
| --- | --- | --- |
| `LSP3-{KID}-{TIER}-{EXP}-{SEG}-{SIG}` | `LSPP_LICENSE_PUBLIC_KEYS` | Customer device |
| `LSP2-{KID}-{TIER}-{EXP}-{SEG}-{SIG}` | `LSPP_LICENSE_SIGNING_KEYS` | Issuer only |

Rules that matter:

- Never copy the issuer secret to a customer device. That defeats the scheme.
- The unsigned historical `LSPP-…` format is **rejected**. If a key starts with
  `LSPP-`, it is not a licence this product issued.
- When `LSPP_LICENSE_PUBLIC_KEYS` is configured, the stored licence file is
  re-verified on every load and its stored tier/expiry are ignored in favour of
  the signed values.

### 1.3 API surface you can rely on

```csharp
using wwwmmm.Configuration;
using wwwmmm.PublicAPI;

LightningAPI.Configure(LightningConfiguration.RealProduction);
// or
LightningAPI.UseRealServices();

var scan       = await LightningAPI.QuickScanAsync("page.png");
var hiRes      = await LightningAPI.HighQualityScanAsync("page.png");
var processed  = await LightningAPI.HybridProcessAsync(scan.Data);
var artefact   = await LightningAPI.StigmaPrintAsync(processed.ProcessedData);
var pipeline   = await LightningAPI.ExecuteLightningPipelineAsync("page.png");

byte[] png = LightningAPI.EncodeOutput(scan.Data, OutputFormat.Png, quality: 90);
```

Every result carries `Success` and `Error`. A failed operation does **not**
throw; check the flag.

### 1.4 Licensing calls that exist

```csharp
using LightningSPP.Licensing;

LicenseKey.Validate(key, out var licence);        // verifies the signature
LicenseManager.ActivateLicense(key);              // validates, then stores
LicenseManager.HasValidLicense();
LicenseManager.GetLicenseTier();
```

Licence state is stored at `%APPDATA%\LightningSPP\license.json` on Windows and
`~/.config/LightningSPP/license.json` elsewhere.

---

## 2. Never existed — do not code against these

| Documented elsewhere | Reality |
| --- | --- |
| `LightningAPI.UseSimulatedServices()` | Removed; does not compile |
| CLI command `simulated` | Does not exist |
| `UniversalSDK.ForDevice(profile)` | The real entry point is `UniversalSDK.Initialize(profile)` |
| `HighQualityScanAsync(byte[] bytes, ScanMode mode)` | Signature is `HighQualityScanAsync(string source, CancellationToken)` |
| `ExecuteLightningPipelineAsync(source:, scanMode:, processingEngine:, printQuality:)` | The single-argument overload takes a path; use `PipelineRequest` with `ExecuteCustomPipelineAsync` for parameters |
| `LightningConfiguration { UseRealScanService = true, … }` | Those properties do not exist; use `LightningConfiguration.RealProduction` |
| Binding licence settings from `appsettings.json` | No configuration binder exists; read environment variables |
| `LIGHTNING_LICENSE_KEY` environment variable | Not read by the product. Activation is a CLI call or `LicenseManager.ActivateLicense` |
| "`Instant` completes in under 1 ms" | `Instant` is a method name, not a latency guarantee. Measure with `wwwmmm speed` |
| `Lightning.Core` / `Lightning.SDK` namespaces | The namespaces are `wwwmmm.*` and `LightningSPP.Licensing` |
| `dotnet run -- real` compares real vs simulated | No simulated baseline exists; the command exercises the real path only |

---

## 3. Replacing direct library use

### 3.1 Direct ImageSharp code

```csharp
// Before: direct ImageSharp pipeline
using var image = SixLabors.ImageSharp.Image.Load(path);
image.Mutate(x => x.DetectEdges().GaussianSharpen().Contrast(1.5f));
```

```csharp
// After: Lightning SPP pipeline (engine chosen by WWWMMM_IMAGE_ENGINE)
using wwwmmm.PublicAPI;

var scan = await LightningAPI.HighQualityScanAsync(path);
var processed = await LightningAPI.HybridProcessAsync(scan.Data);
await File.WriteAllBytesAsync("out.png",
    LightningAPI.EncodeOutput(processed.ProcessedData, OutputFormat.Png));
```

`WWWMMM_IMAGE_ENGINE` selects `Skia` (default) or `ImageSharp`; there is no
automatic fallback between them.

### 3.2 Custom ONNX code

Inference needs an explicit model and input shape. Without them the processor
does not invent a result.

```bash
LSPP_ONNX_MODEL_PATH=/models/document.onnx
LSPP_ONNX_INPUT_SHAPE=1x1x224x224
```

### 3.3 Custom document pipelines

```csharp
var request = new PipelineRequest(
    Source: "page.png",
    ScanMode: ScanMode.Resonance,
    Quality: 600,
    ProcessingEngine: ProcessingEngine.Hybrid,
    PrintQuality: PrintQuality.Stigma,
    Copies: 1);

var custom = await LightningAPI.ExecuteCustomPipelineAsync(request);
```

`WWWMMM_STIGMA_LEVEL` overrides these values. If the environment sets profile
`3`, a request for Resonance at 600 DPI will not be honoured — that is
documented behaviour, not a bug.

---

## 4. Upgrade checklist

- [ ] Remove every call to `UseSimulatedServices()` and every `simulated` command
- [ ] Replace `LSPP-…` test keys: they are rejected; issue `LSP3` keys
- [ ] Put only `LSPP_LICENSE_PUBLIC_KEYS` on devices
- [ ] Confirm `WWWMMM_STIGMA_LEVEL` is one of `1|2|3` — anything else fails at startup
- [ ] Confirm `WWWMMM_IMAGE_ENGINE` is `Skia` or `ImageSharp`
- [ ] Check that every `Success` flag is handled
- [ ] Run `dotnet test Tests/LightningSPP.Tests/LightningSPP.Tests.csproj -c Release`
- [ ] Run `wwwmmm engines` to confirm both engines execute on your host

---

## 5. Where the truth lives

| Question | Source |
| --- | --- |
| What does the CLI do? | `Docs/MANUAL/03-KOMPLOTI-CLI.md` |
| Which environment variables exist? | `Docs/MANUAL/02-KONFIGURIMI.md` |
| What is the API surface? | `Docs/MANUAL/05-API-SDK.md` |
| What is proven and what is not? | `Docs/MANUAL/10-KUFIJTE-DHE-PRETENDIMET.md` |
| Compliance position? | `Docs/COMPLIANCE/README.md` |

---

*© 2025-2026 ABA GmbH. All rights reserved.*
