# AUDIT — What is real vs. what is a wrapper (Lightning SPP 3.14)

This audit exists because the README and the marketing pages list many technology names
(TideWave, CLX, CLI.I, XCL, Resonance, NanoDecibel, Clisonic Global AI, Stigma, Stigma Memory,
NodeDB Fluid) without stating which ones are independent executable logic and which ones are
naming or parameter presets.

The classification below is produced by reading the source and by this command:

```bash
grep -rEn "TideWave|CLX|CLI_I|XCL|Clisonic|Resonance|NanoDecibel|NodeDB|StigmaMemory|UseStigma" \
  --include="*.cs" .
```

Classification legend:

- **REAL** — independent executable logic that changes program behaviour in a way that is
  observable in the output bytes or in the control flow.
- **PARAMETER SET** — a named configuration of one shared engine. It changes numeric parameters
  only; there is no second algorithm.
- **WRAPPER** — routing / naming only. It calls the same target as another entry point and adds
  no behaviour.
- **CONDITIONAL** — real only when an external resource is configured; otherwise it fails closed.
- **DOC ONLY** — the name appears in documentation and console text but has no implementation.

---

## Classification table

| Technology | Classification | Evidence (file / line) | Notes |
|---|---|---|---|
| **Stigma** | **REAL** | `Services/StigmaExecutionProfile.cs` — `StigmaProfiles.Apply()` | Selects scan mode, processing engine, quality, optimization level and output quality. Changes the whole execution path. Covered by `Tests/LightningSPP.Tests/StigmaExecutionProfileTests.cs`. |
| **Lightning / TideWave / Resonance / NanoDecibel** (scan modes) | **PARAMETER SET** | `Services/Implementation/RealImageProcessor.cs` — `ResizeByQuality()`, `ApplyModeProcessing()` | One SkiaSharp engine. Each mode sets a resize multiplier (1.0 / 1.2 / 1.8 / 2.5) and a colour/edge pass. Output differs and is deterministic (`GoldenPipelineTests`), but it is not four separate algorithms. |
| **CLX / CLI.I / XCL** (processing engines) | **WRAPPER** | `Services/Implementation/RealAIProcessor.cs` — `ClxProcessAsync` / `CliProcessAsync` / `XclProcessAsync` | All three call the same `CompressAsync()`; the only difference is the Brotli level (Fastest / Optimal / SmallestSize). No independent processing algorithm exists. |
| **Hybrid** (processing engine) | **WRAPPER** | `RealAIProcessor.ProcessAsync` maps `Hybrid` → `XclProcessAsync`; `Services/Implementation/HybridProcessingService.cs` | The service comment itself reads “Compatibility wrapper that routes all processing to REAL AI processor.” |
| **Clisonic Global AI** | **CONDITIONAL** | `RealAIProcessor.RunInference()` | Real ONNX Runtime inference only when `LSPP_ONNX_MODEL_PATH` and `LSPP_ONNX_INPUT_SHAPE` are set. Otherwise it throws and the result is reported as unavailable. The default pipeline does **not** enable it. |
| **Stigma Memory** | **WRAPPER (inert flag)** | `ProcessingOptions.UseStigmaMemory` | The flag is passed through `LightningPipeline` into `ProcessingOptions` but is never read in `RealAIProcessor`. It has no runtime effect today. |
| **Stigma Print** | **WRAPPER** | `Services/Implementation/StigmaPrintService.cs` | “Compatibility wrapper that routes all print operations to REAL print service.” |
| **Lossless print artifact** | **REAL** | `Services/Implementation/RealPrintService.cs` | Writes a real Brotli-compressed `.br` artifact to disk with copy/cleanup support. It is an artifact writer, **not** a physical printer driver. |
| **NodeDB Fluid** | **DOC ONLY** | No `.cs` match in the grep above | Named on the technology page and README as “adaptive sink/bridge”. No implementation file references it. |
| **FastScanService** | **REAL, unwired** | `Services/Implementation/FastScanService.cs` | A second, real SkiaSharp scan implementation, but `Configuration/LightningConfiguration.cs` (`ServiceFactory.CreateScanService()`) returns `RealImageProcessor`, so `FastScanService` is not on the default path. |

---

## What this means in plain terms

1. **The real engine is SkiaSharp image transformation plus Brotli compression.** The scan modes
   are real parameter sets over that engine; the processing “engines” are compression levels.

2. **Stigma is the one genuine meta-feature.** It is the only component whose selection rewrites
   the control path end to end, and it is the only one with dedicated tests asserting that
   selection changes scan mode, engine and output quality together.

3. **Nothing in the repository claims a physical printer today.** `README.md`,
   `Web/wwwroot/features`-equivalent copy and `RealPrintService` all state the output is a
   lossless artifact and that a platform adapter would be required. This audit agrees: there is
   no printer driver code in the tree.

4. **AI is opt-in and honest.** The ONNX path validates tensor shape and element type before it
   runs, and fails closed when no model is configured. It must not be described as a default
   capability.

## Required follow-up (tracked, not yet done)

- Rename or re-document `CLX / CLI.I / XCL` so the UI does not imply three independent engines.
- Either implement `Stigma Memory` behaviour or remove the flag.
- Either implement `NodeDB Fluid` or remove it from the technology page.
- Decide whether `FastScanService` is the canonical scanner; if not, delete it to avoid two
  diverging engines.

This document is updated whenever a technology name is added, removed, or promoted between
classifications.
