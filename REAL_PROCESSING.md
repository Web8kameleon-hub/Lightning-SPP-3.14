# Lightning SPP 3.14 — përpunimi real

Ky dokument përshkruan çka pipeline-i ekzekuton **vërtet**, cilat motorë marrin
pjesë dhe si matet. Nuk përmban krahasime me një "mod të simuluar", sepse ai mod
nuk ekziston më në kod.

## Çka ekzekutohet realisht

| Fazë | Implementimi | Çka prodhon |
| --- | --- | --- |
| Skanim | `RealImageProcessor` (SkiaSharp) ose `FastScanService` (ImageSharp) | Bajt imazhi PNG të përpunuar |
| Përpunim | `RealAIProcessor` | Bajt të kompresuar (Brotli) |
| Artefakt | `RealPrintService` | Skedar `.br` lossless në disk |
| Kodim | `OutputAdapter` | PNG / JPEG / BMP / TIFF / PDF |

Asnjë nga këto nuk përdor `Task.Delay`, të dhëna të rastësishme si rezultat ose
përgjigje suksesi të rreme. Input i pavlefshëm dështon me gabim.

## Dy motorët e imazhit (L/R)

`ImageEngineAdapter` zgjedh **eksplicit** njërin prej dy implementimeve:

| Shkronja | `ImageEngine` | Klasa | Teknologjia |
| --- | --- | --- | --- |
| L | `Skia` (default) | `RealImageProcessor` | SkiaSharp |
| R | `ImageSharp` | `FastScanService` | SixLabors.ImageSharp |

Zgjedhja bëhet me `WWWMMM_IMAGE_ENGINE`. **Nuk ka fallback automatik**: nëse një
motor dështon, kthen gabim; nuk e zëvendëson tjetri. Kjo është me qellim — një
fallback i heshtur e bën sjelljen e prodhimit të paparashikueshme.

Të dy motorët prodhojnë bajt të ndryshëm për të njëjtin input; kjo është e
pritshme dhe e matshme.

### Verifikim i të dy motorëve

```bash
dotnet run --project wwwmmm.csproj -- engines
```

Komanda ekzekuton të katër modelet e skanimit në të dy motorët dhe shtyp
madhësinë dhe SHA-256 të shkurtër për çdo rezultat. Nëse një motor nuk jep
bajt, komanda dështon.

Shembull i një ekzekutimi real (128×128 PNG fixture, Windows 11, .NET 10,
Release build):

```text
Engine selection: Skia
  Lightning        3,170 bytes     90 ms  sha256:ed717acb1735bdaa
  TideWave         7,145 bytes      5 ms  sha256:27c491769c14c49a
  Resonance          771 bytes    199 ms  sha256:7df9bd1c699cca0c
  NanoDecibel      1,147 bytes    415 ms  sha256:4d7d3fe2d19f430a

Engine selection: ImageSharp
  Lightning        6,340 bytes    114 ms  sha256:ad3d72d2a95f188b
  TideWave         7,376 bytes     61 ms  sha256:88768663b6f0cae5
  Resonance       15,952 bytes     16 ms  sha256:9b6e8b513c687616
  NanoDecibel     15,387 bytes     27 ms  sha256:0927e9ed4ca8bc6d
```

Këto kohë janë matje të një ekzekutimi në një makinë; nuk janë garanci dhe nuk
duhen citosh si përformancë e produktit. E vetmja gjë që provojnë është se të dy
motorët punuan dhe dhanë rezultate të ndryshme.

## Modelet e skanimit

| Model | Skia | ImageSharp |
| --- | --- | --- |
| `Lightning` | grayscale + contrast 1.05 | profil i lehtë |
| `TideWave` | grayscale + contrast 1.12 + saturation | profil i balancuar |
| `Resonance` | edge-enhance (Sobel) + contrast 1.25 | profil i precizionit |
| `NanoDecibel` | edge-enhance + contrast 1.35 + saturation | profil maksimal |

Rezolucioni kufizohet nga `8 000 000` pikselë dhe 8192 px për anë, pavarësisht
DPI-së së kërkuar.

## Përpunimi

Motorët (`ProcessingEngine`) ndryshojnë nivelin e kompresimit lossless, jo
saktësinë e ndonjë modeli:

| Motor | Sjellja |
| --- | --- |
| `CLX` | Brotli, nivel i shpejtë |
| `CLX_I` | Brotli, nivel i balancuar |
| `XCL` | Brotli, nivel maksimal |
| `Clisonix` | i njëjti trajtim bajtash me opsione shtesë |
| `Hybrid` | sekuencë e motorëve me nivelin më të lartë |

**Inferenca e modelit është operacion i veçantë.** Ajo ekzekutohet vetëm kur
`LSPP_ONNX_MODEL_PATH` tregon një model real dhe input-i është tensor `float32`
me formën e deklaruar në `LSPP_ONNX_INPUT_SHAPE`. Pa to, nuk ndodh inferencë; nuk
ka zëvendësim të heshtur.

## Artefakti i output-it

`print` nuk printon. Ai shkruan një skedar Brotli lossless (`.br`) në disk dhe
kthen rrugën. Printimi fizik është i mundur vetëm në Linux, përmes radhës së
sistemit (`Documents/LinuxDeviceService.cs`), dhe nuk është verifikuar në
harduer në këtë repository.

## Konfigurimi

```csharp
using wwwmmm.Configuration;
using wwwmmm.PublicAPI;

LightningAPI.Configure(new LightningConfiguration
{
    PrintOutputDirectory = @"C:\Output",
    MaxConcurrency = 4,          // < 1 refuzohet
    ImageEngine = ImageEngine.Skia
});
```

```bash
WWWMMM_IMAGE_ENGINE=ImageSharp   # zgjedh motorin R
WWWMMM_STIGMA_LEVEL=1            # 1=quality, 2=balanced, 3=lightning
```

## Komandat e verifikimit

```bash
dotnet run --project wwwmmm.csproj -- engines      # L/R motorët
dotnet run --project wwwmmm.csproj -- real         # rrjedha e plotë
dotnet run --project wwwmmm.csproj -- speed        # kohë informuese
dotnet run --project wwwmmm.csproj -- demo         # validim me fixture
```

`speed` mat median dhe p95 mbi 20 ekzekutime pas 5 ngrohjeve me input 64 KiB të
përzier. Rezultatet varen nga mjedisi; pragjet e regresionit mbahen nga suite-i
i testeve, jo nga ky dokument.

## Kufijtë

- Skanimi fizik (`device scan`) nuk është verifikuar në harduer.
- Printimi fizik nuk është verifikuar në harduer.
- OCR nuk ekziston në këtë kod.
- Inferenca ONNX kërkon model real të konfiguruar.
- Numrat e performancës janë matje mjedisi, jo garanci.

Për gjendjen e plotë të aftësive shih
[Docs/MANUAL/10-KUFIJTE-DHE-PRETENDIMET.md](Docs/MANUAL/10-KUFIJTE-DHE-PRETENDIMET.md)
dhe [Docs/MANUAL/11-MATRICA-E-PRANIMIT.md](Docs/MANUAL/11-MATRICA-E-PRANIMIT.md).
