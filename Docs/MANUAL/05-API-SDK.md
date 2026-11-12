# 05 — API bibliotekare dhe SDK

Sipërfaqja programatike që përdoret nga C#. Emrat dhe nënshkrimet janë marrë nga
`PublicAPI/LightningAPI.cs`, `SDK/Lightning314.cs`, `SDK/UniversalSDK.cs`,
`Services/*.cs`.

## 5.1 `wwwmmm.PublicAPI.LightningAPI`

Klasë statike; të gjitha metodat e rënda janë asinkrone dhe pranojnë
`CancellationToken`.

### Konfigurimi

```csharp
using wwwmmm.PublicAPI;
using wwwmmm.Configuration;

LightningAPI.Configure(LightningConfiguration.RealProduction);
// ose ndryshe:
LightningAPI.UseRealServices();
```

`LightningConfiguration` kontrollon: `MaxConcurrency`, `PrintOutputDirectory`,
`StigmaProfile`, `ImageEngine`, `EnablePerformanceLogging`,
`EnableMemoryOptimization`. `MaxConcurrency < 1` hidhet si
`ArgumentOutOfRangeException` — konfigurim i pavlefshëm nuk pranohet.

> `UseSimulatedServices()` dhe `LightningConfiguration.FastSimulated` janë
> **hequr** nga kodi. Nuk ka mod të simuluar, as komandë `simulated`. Çdo thirrje
> që i përmend nuk kompilohet; kjo është me qellim.

### Pipeline

```csharp
var result = await LightningAPI.ExecuteLightningPipelineAsync("dokument.png");
if (!result.Success) Console.WriteLine(result.Error);
foreach (var step in result.Steps) Console.WriteLine(step);
```

```csharp
var request = new PipelineRequest(
    Source: "dokument.png",
    ScanMode: ScanMode.Resonance,
    Quality: 600,
    ProcessingEngine: ProcessingEngine.Hybrid,
    PrintQuality: PrintQuality.Stigma,
    Copies: 1);

var custom = await LightningAPI.ExecuteCustomPipelineAsync(request);
```

`PipelineRequest` është `record` me këto fusha dhe default-e:
`Source`, `ScanMode = Lightning`, `Quality = 300`, `UseResonance = true`,
`UseNanoDecibel = true`, `ProcessingEngine = Auto`, `UseAI = true`,
`UseClisonix = true`, `OptimizationLevel = 3`, `UseStigmaMemory = true`,
`ShouldPrint = true`, `PrintDestination = "default"`,
`PrintQuality = Stigma`, `UseStigma = true`, `Copies = 1`.

**Rëndësishme:** `WWWMMM_STIGMA_LEVEL` mbishkruan këto vlera përmes
`StigmaProfiles.Apply`. Nëse ke vendosur profil `3` (lightning), kërkesa për
`Resonance`/600 DPI nuk do të respektohet. Kjo është sjellje e dokumentuar,
jo defekt — por duhet ditur, se ndryshe mendon që kontrollon diçka që kontrollon
profili.

### Operacione të vetme

| Metoda | Input | Kthen |
| --- | --- | --- |
| `QuickScanAsync(string source, ct)` | Rruga e skedarit | `ScanResult` |
| `HighQualityScanAsync(string source, ct)` | Rruga e skedarit | `ScanResult` |
| `AIProcessAsync(byte[] data, ct)` | Bajt-at | `ProcessingResult` |
| `HybridProcessAsync(byte[] data, ct)` | Bajt-at | `ProcessingResult` |
| `StigmaPrintAsync(byte[] data, ct)` | Bajt-at | `PrintResult` |
| `BatchProcessAsync(List<string> sources, ct)` | Listë rrugësh | `List<PipelineResult>` |

Të gjitha kthejnë objekte me `Success` dhe `Error`. Një `Success == false` **nuk**
hedh exception; është përgjegjësia e thirrësit ta kontrollojë.

### Kodimi i output-it

```csharp
byte[] jpeg = LightningAPI.EncodeOutput(scanResult.Data, OutputFormat.Jpeg, quality: 85);
byte[] tiff = LightningAPI.EncodeMultiPageOutput(pages, OutputFormat.Tiff);
byte[] pdf  = LightningAPI.EncodeMultiPageOutput(pages, OutputFormat.Pdf);
```

- `EncodeOutput(byte[] rasterImage, OutputFormat format, int quality = 90)`
- `EncodeMultiPageOutput(IReadOnlyList<byte[]> rasterPages, OutputFormat format, int quality = 90)`

Formatet e mbështetura: `Png`, `Jpeg`, `Bmp`, `Tiff`, `Pdf`. TIFF dhe PDF
pranojnë disa faqe.

## 5.2 Tipet

```csharp
enum ScanMode { Lightning, TideWave, Resonance, NanoDecibel }
enum ProcessingEngine { Auto, CLX, CLX_I, XCL, Clisonix, Hybrid }
enum PrintQuality { Draft, Normal, High, Stigma }
enum OutputFormat { Png, Jpeg, Bmp, Tiff, Pdf }
enum ImageEngine { Skia, ImageSharp }
```

Rezultatet:

```csharp
record ScanResult(byte[] Data, ScanMetadata Metadata, bool Success, string? Error = null);
record ProcessingResult(byte[] ProcessedData, ProcessingMetadata Metadata, bool Success, string? Error = null);
record PrintResult(bool Success, PrintMetadata Metadata, string? Error = null);
record PipelineResult(bool Success, long TotalTimeMs, List<string> Steps, string? Error = null);
```

Mos u mbështet në `TotalTimeMs` si SLO; është kohë e matur në atë ekzekutim.

## 5.3 `wwwmmm.SDK.Lightning314`

Rrugë e shkurtër për përpunim të shpejtë:

```csharp
using wwwmmm.SDK;

var a = await Lightning314.Instant(data);
var b = await Lightning314.UltraFast(data);
var c = await Lightning314.Lightning(data);
var many = await Lightning314.Parallel(files, maxParallelism: 4);
```

Rregull i fortë, i vendosur pas një defekti real: `Parallel(..., 0)` ose vlerë
negative **refuzohet**. Më parë krijohej një semafor me kapacitet zero dhe
thirrja priste pafund. Nëse e modifikon këtë kod, mbaj testin që e mbulon.

Emrat `Instant`/`UltraFast`/`Lightning` janë emra rrugësh, jo garanci latence.
Matje reale: `wwwmmm speed` ose `docs/BENCHMARK_PROTOCOL.md`.

## 5.4 `wwwmmm.SDK.UniversalSDK`

```csharp
var sdk = UniversalSDK.Initialize(DeviceProfile.Medical);
var result = await sdk.ProcessAsync(scanData);
```

Profilet: `Medical`, `Mobile`, `IoT`, `Embedded`, `Desktop`, `Web`, `Cloud`.
Kufijtë e memories dhe latencës që shfaqen në dokumentacion janë objektiva
projektimi; matja reale bëhet në pajisjen e synuar. Një profil nuk e bën
automatikisht produktin të certifikuar për përdorim klinik.

## 5.5 Sjellja ndaj gabimeve

- Input jo-imazh (përfshirë PDF të papërpunuar) → `InvalidDataException` /
  `ArgumentException` e kapur brenda servisit dhe kthyer si `Success = false`
  me mesazh. Nuk ka përpunim "best effort" të një payload-i arbitrar.
- Skedari që mungon → `FileNotFoundException` në rrugën e skanimit.
- Konfigurim i pavlefshëm (`WWWMMM_STIGMA_LEVEL`, `MaxConcurrency`,
  `WWWMMM_IMAGE_ENGINE`) → exception në nisje, jo rënie e heshtur.

Parimi: **konfigurim i gabuar dështon me zë**, sepse një fallback i heshtur e
bën produktin të paparashikueshëm në prodhim.

## 5.6 Integrim minimal

```csharp
using wwwmmm.Configuration;
using wwwmmm.PublicAPI;

LightningAPI.Configure(new LightningConfiguration
{
    MaxConcurrency = 2,
    PrintOutputDirectory = Path.Combine(Path.GetTempPath(), "lightning-out")
});

var scan = await LightningAPI.QuickScanAsync("faqja.png");
if (!scan.Success) { Console.Error.WriteLine(scan.Error); return 1; }

var processed = await LightningAPI.HybridProcessAsync(scan.Data);
if (!processed.Success) { Console.Error.WriteLine(processed.Error); return 1; }

var png = LightningAPI.EncodeOutput(processed.ProcessedData, OutputFormat.Png);
await File.WriteAllBytesAsync("result.png", png);
return 0;
```

## 5.7 Çka API-ja nuk premton

- Nuk premton rezultat identik bit-për-bit ndërmjet motorëve (`Skia` vs
  `ImageSharp` janë rrugë të ndryshme, pa fallback).
- Nuk premton latencë nga emrat e metodave.
- Nuk premton OCR: tekst nga skanime nuk nxirret nëse nuk ka mjet real
  (`pdftotext` për PDF me tekst).
- Nuk premton inferencë AI pa `LSPP_ONNX_MODEL_PATH`.
</content>
