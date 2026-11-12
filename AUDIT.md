# AUDIT — What is real vs. what is a wrapper

Last verified against the current worktree (version 3.14.1). This file is evidence-based:
every row cites the file that implements the behaviour. If a technology is renamed,
removed, or promoted between classifications, update this table in the same commit.

Verification command:

```bash
grep -rEn "TideWave|CLX|Clisonix|Resonance|NanoDecibel|NodeDb|NodeDB|StigmaMemory|FluidPipeline" \
  --include="*.cs" .
```

Classification legend:

- **REAL** — independent executable logic; the behaviour is observable in output bytes or control flow.
- **PARAMETER SET** — a named configuration of one shared engine; changes numeric parameters, not the algorithm.
- **WRAPPER** — routing/naming only; calls the same target as another entry point and adds no behaviour.
- **CONDITIONAL** — real only when an external resource is configured; otherwise fails closed.
- **DOC ONLY** — appears in documentation or console text but has no implementation.

---

## Image and processing pipeline

| Technology | Classification | Evidence | Notes |
|---|---|---|---|
| **Stigma execution profiles** | **REAL** | `Services/StigmaExecutionProfile.cs` — `StigmaProfiles.Apply()` | Rewrites scan mode, quality, processing engine, optimisation level and output quality for the whole pipeline. All three profiles also force `UseAI=false`, `UseClisonix=false`, `UseStigmaMemory=false`, so a profile can never silently request an unconfigured capability. Asserted by `StigmaExecutionProfileTests`. |
| **Lightning / TideWave / Resonance / NanoDecibel** | **PARAMETER SET** | `Services/Implementation/RealImageProcessor.cs`, `Services/Implementation/FastScanService.cs` | Each mode sets a resize multiplier and a filter chain (`Grayscale`, `Contrast`, `GaussianSharpen`, `GaussianBlur`) over one engine. Output differs per mode and is deterministic (`GoldenPipelineTests`), but these are four presets, not four algorithms. |
| **Two image engines (L/R)** | **REAL** | `Services/Imaging/ImageEngineAdapter.cs`, `Services/Imaging/ImageEngineSelection.cs` | L = SkiaSharp `RealImageProcessor`; R = ImageSharp `FastScanService`. Selected explicitly via `WWWMMM_IMAGE_ENGINE`; unsupported values throw. There is no silent fallback between engines and both are covered by `ImageEngineAdapterTests`. |
| **CLX** | **WRAPPER** | `RealAIProcessor.ClxProcessAsync` | Brotli `CompressionLevel.Fastest`. Reported as `CLX-Real-Compression`. |
| **CLX.i** | **WRAPPER** | `RealAIProcessor.ClxIProcessAsync` | Brotli `CompressionLevel.Optimal`. Reported as `CLX.i-Real-Optimal`. |
| **XCL** | **WRAPPER** | `RealAIProcessor.XclProcessAsync` | Brotli `CompressionLevel.SmallestSize` plus a rented memory pool. Reported as `XCL-Real-Maximum`. |
| **Hybrid** | **REAL (compression pipeline)** | `RealAIProcessor.HybridRealProcessAsync` | Three sequential Brotli passes (Fastest → Optimal → SmallestSize). Real executable staging, but it is compression, not image analysis. Throws `NotSupportedException` if `UseClisonix` or `UseStigmaMemory` is requested. |
| **Auto** | **REAL** | `RealAIProcessor.AutoSelectEngineAsync` | Chooses CLX / CLX.i / XCL / Hybrid by input-size thresholds at 1 KiB, 100 KiB and 1 MiB. Round-tripping across those thresholds is tested by `BalancedCompressionRoundTripsAcrossAutoThreshold`. |
| **Clisonix Global AI** | **CONDITIONAL** | `RealAIProcessor.RunInference` | Real ONNX Runtime inference, but only with `LSPP_ONNX_MODEL_PATH` and `LSPP_ONNX_INPUT_SHAPE` set. Requires exactly one float32 tensor input and one float32 output; validates shape against byte length. Fails closed otherwise. Not enabled by any Stigma profile. |
| **Stigma Memory** | **REAL in FluidEngine / NOT SUPPORTED in the image pipeline** | `wwwmmm.FluidEngine/.../Pipelines/StigmaMemoryFilmPipeline.cs`; `RealAIProcessor.HybridRealProcessAsync` | FluidEngine implements it as a typed step that stores bytes in `FluidLayer` and returns a session id. Requesting it through `ProcessingOptions` against `RealAIProcessor` throws rather than pretending to work. |

## Fluid engine

| Technology | Classification | Evidence | Notes |
|---|---|---|---|
| **wwwmmm Fluid Engine** | **REAL** | `wwwmmm.FluidEngine/src/wwwmmm.FluidEngine.Core/` | Typed pipelines (`FluidPipeline<TStart,TCurrent>`, `IFluidStep<TIn,TOut>`), `FluidLayer` state, `PipelineRegistry`, OpenAPI generation, binary/data/vision steps. Referenced by `wwwmmm.csproj` and exercised by its own test project. |
| **Fluid HTTP surface** | **REAL** | `Web/FluidApiHandler.cs`, wired from `Web/LicenseApiServer.cs` | Serves only the operations the handler implements; `OpenApiGenerator` states that other registry entries are library-only. |

## Documents and devices

| Technology | Classification | Evidence | Notes |
|---|---|---|---|
| **PDF creation** | **REAL** | `Documents/PdfDocumentService.cs` — `CreateFromImages` | Emits a real multi-page PDF via SkiaSharp. Covered by `CreatesRealMultipagePdfFromImages`. |
| **PDF page rendering** | **CONDITIONAL** | `Documents/PdfDocumentService.cs` — `RenderPageAsync`, `Documents/NativeCommand.cs` | Requires Poppler (`pdfinfo`/`pdftoppm`). With the tools absent it throws `DocumentException`. Tests are gated behind `SPP_TEST_NATIVE_DOCUMENTS=1`. |
| **Physical printing** | **PARTIAL, Linux only** | `Documents/LinuxDeviceService.cs` | Submits a PDF to the OS print queue on Linux; throws `PlatformNotSupportedException` elsewhere. Not verified against real hardware. |
| **Lossless `.br` artifact** | **REAL** | `Services/Implementation/RealPrintService.cs` | Writes real Brotli artifacts with copy and cleanup support. An artifact writer, not a printer driver. |
| **NodeDB bridge** | **REAL adapter, unverified upstream** | `Integrations/OsClxNodeDbBridge.cs`, `Integrations/BridgeCommands.cs` | HTTP client with API key + HMAC signing and strict response validation (`node_id`, `trust_domain`, cycle signals). Covered by `BridgeContractTests`; the live round-trip test (`BridgeUpstreamTests`) is gated and currently skipped because no upstream service is configured. |
| **Excel / Excel Core bridges** | **REAL adapter, unverified upstream** | `Integrations/ClisonixExcelBridge.cs`, `Integrations/ClisonixExcelCoreBridge.cs` | Same pattern: contract tests run locally, upstream integration tests skip unless isolated services are supplied. |

---

## What this means in plain terms

1. **The image pipeline is one engine family with presets.** Scan modes are parameter sets;
   CLX / CLX.i / XCL are Brotli compression levels. Only Hybrid, Auto and Stigma combine them
   into genuinely different execution paths.

2. **Stigma is the one real meta-feature in the image pipeline**, and it is now defensive:
   each profile disables AI, Clisonix and Stigma Memory so a profile cannot request a
   capability that is not configured.

3. **The second engine is real, not decorative.** Skia and ImageSharp are two independent
   implementations behind an explicit selector with no fallback.

4. **FluidEngine is the most substantial new subsystem** and it is real code with its own
   tests, not a naming layer.

5. **AI and native tools fail closed.** Clisonix needs a configured ONNX model; PDF rendering
   needs Poppler; Stigma Memory is rejected by the image pipeline. None of them silently
   degrade into a fabricated success.

## Open follow-ups

- `CLX / CLX.i / XCL` remain compression-level presets under engine names that read like
  separate algorithms. Either differentiate them in code or state the mapping wherever the
  names are exposed to customers.
- `Stigma Memory` exists in FluidEngine but is not wired into the image pipeline. Document the
  split, or provide a single entry point.
- NodeDB and the Excel bridges have local contract coverage but no verified upstream run.
- Physical printing is Linux-only and unverified on hardware.
