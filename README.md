# Lightning SPP 3.14

Pipeline Scan-Process-Print për dokumente dhe imazhe

Programi për organizimin, sigurinë, operimin dhe bridges të ekosistemit:
[udhërrëfyesi i ngritjes teknologjike](Docs/TECHNOLOGY_EVOLUTION.md).
Ky plan përcakton punën dhe provat e kërkuara; nuk deklaron aftësi të papranuara si të gatshme për prodhim.

Për paketat runtime Windows/Linux, varësitë native dhe përditësimin pa humbur
të dhënat, shih [udhëzimin e instalimit](Docs/INSTALLATION.md). Arkivat ndërtohen
dhe kontrollohen me `Scripts/package-release.py` dhe `Scripts/verify-package.py`;
publikimi dhe përdorimi nga klientët janë verifikime të veçanta.

Lightning SPP 3.14 ofron pipeline .NET për përpunimin e dokumenteve dhe imazheve, me CLI, API dhe ndërfaqe desktop. Emrat e profileve mobile/IoT/cloud në SDK nuk provojnë mbështetje native për pajisje ose validim në ato mjedise. Pipeline-i krijon artefakte; dërgimi fizik në printer bëhet vetëm nga rruga e veçantë Linux/CUPS kur ajo konfigurohet.

## Çfarë sjell versioni 3.14

- Procesim real i të dhënave (jo i simuluar)
- Integrim i komponentëve të përpunimit të imazhit, AI/ML dhe kompresimit
- API publike për integrim të shpejtë në aplikacione ekzistuese
- Mbështetje për ekzekutim lokal dhe containerized

## REAL Processing

Versioni 3.14 përdor komponentë realë të ekosistemit .NET:

- SkiaSharp për përpunim imazhi
- Microsoft.ML për AI/ML
- Brotli për kompresim efikas
- File I/O për gjenerim output-i real në disk

```csharp
using SixLabors.ImageSharp;

// Përpunim real i imazhit
image.Mutate(x => x.DetectEdges().GaussianSharpen(3.0f));

// Kompresim real
using var brotli = new BrotliStream(output, CompressionLevel.SmallestSize);

// Output real në disk
await File.WriteAllBytesAsync(filePath, processedData);
```

Detaje shtesë: [REAL_PROCESSING.md](REAL_PROCESSING.md)

## Skenarë përdorimi

Kodi aktual mbulon workflows dokumentesh/imazhesh përmes ndërfaqeve të dokumentuara dhe ekzekutimit lokal ose të container-izuar. Mbështetja për një pajisje, sistem operativ ose ngarkesë të caktuar duhet provuar veçmas. Profilet me emër mjekësor nuk janë validim klinik; emrat mobile/IoT/cloud nuk janë garanci për përputhshmëri, bateri, memorie apo performancë.

### Agjenti, faturat dhe printimi

Model Gateway mund të propozojë hapa për një kërkesë në gjuhë natyrore, por plani
kthehet si `PROPOSED_REQUIRES_REVIEW` dhe ekzekutimi kërkon shqyrtim të qartë.
`finance.reconcile` pranon regjistra të strukturuar, të normalizuar dhe alokime
të shqyrtuara; nuk nxjerr vetë fusha nga faturat dhe nuk klasifikon PDF të skanuar.
`documents.pdf-text` nxjerr vetëm tekstin ekzistues me Poppler, pa OCR.

Prandaj kërkesa “nga këto fatura, printo vetëm …” nuk është ende rrjedhë
autonome e plotë: faturat duhet të jenë të lexuara dhe të normalizuara, ndërsa
veprimi duhet miratuar. Mjeti aktual i printimit dërgon PDF te një queue i emërtuar
Linux/CUPS; statusi i queue nuk provon se letra doli fizikisht. Email-i i
Lightning SPP dërgon licenca, jo lexime inbox-i. Excel Core gjeneron raporte
LAGTER; shërbimi i veçantë Excel parse/validate lexon workbook-e, jo fatura PDF.
Detajet dhe kufijtë janë te [portofoli i proceseve](Docs/PARTNER_PROCESS_PORTFOLIO.md)
dhe [kontratat e bridges](Docs/BRIDGES.md).

## API e SDK-së

```csharp
using wwwmmm.SDK;

var result = await Lightning314.Instant(data);
```

### Profile SDK-je (emra skenarësh, jo validim pajisjesh)

Profilet zgjedhin variante të përpunimit/kompresimit. Nuk provojnë përputhshmëri
native me pajisjen, kufij realë të memories, kursim baterie apo përshtatshmëri
klinike.

```csharp
// Medical profile
var sdk = UniversalSDK.Initialize(DeviceProfile.Medical);
var result = await sdk.ProcessAsync(scanData);

// Mobile profile: byte[] in, byte[] out
byte[] mobileResult = await MobileAPI.ProcessMobile(data, batteryLevel: 75);

// IoT profile: byte[] in, byte[] out, bounded memory
byte[] iotResult = await IoTAPI.ProcessConstrained(data, maxMemoryKB: 64);

// Cloud profile: batch of byte[] in, CloudBatchResult out
CloudBatchResult cloudResult = await CloudAPI.ProcessMassive(pages, maxParallelism: 4);
```

## Teknologjitë kryesore

- TideWave: skanim i balancuar
- CLX: motor bazë përpunimi
- CLX.i: përpunim interaktiv
- XCL: përpunim i zgjeruar për raste komplekse
- Resonance: fokus në precizion
- NanoDecibel: fokus në cilësi maksimale
- Clisonix Global AI: përmirësim i asistuar nga AI
- Stigma: kontroll adaptiv i të gjithë pipeline-it dhe artifact output lossless\n- Stigma Memory: profilizim i përdorimit të memories

## Profilet Stigma

`WWWMMM_STIGMA_LEVEL` kontrollon realisht të gjithë pipeline-in:

| Vlera | Profili | Sjellja |
| --- | --- | --- |
| `1` | quality | NanoDecibel, Hybrid + AI, Stigma lossless |
| `2` | balanced | TideWave, Auto + AI, output high |
| `3` | lightning | Lightning, CLX fast path, pa faza të kushtueshme |

Shembull:

```bash
WWWMMM_STIGMA_LEVEL=3 dotnet run -- pipeline document.pdf
```

Vlerat e pavlefshme refuzohen gjatë nisjes, që konfigurimi të mos dështojë në heshtje.

## Instalimi i shpejtë

Build-i kërkon licencën private SixLabors në mjedis. Në PowerShell, vendos
`SIXLABORS_LICENSE` në `.env.build` lokal (nuk versionohet), pastaj ekzekuto
`. ./Scripts/Load-BuildEnvironment.ps1` në të njëjtën seancë para komandave më poshtë.
Për Docker përdor `--secret id=sixlabors_license,env=SIXLABORS_LICENSE`;
vlera nuk duhet vendosur në Dockerfile `ENV`/`ARG` ose në konfigurimin runtime.

```bash
dotnet build
dotnet run
```

## Përdorimi

### API publike

```csharp
using wwwmmm.PublicAPI;

// Pipeline i plotë: Scan -> Process -> Print
var result = await LightningAPI.ExecuteLightningPipelineAsync("page.png");

// Skanim i shpejtë
var scanResult = await LightningAPI.QuickScanAsync("photo.jpg");

// Skanim me cilësi të lartë
var hqScan = await LightningAPI.HighQualityScanAsync("image.png");

// Përpunim me AI
var processResult = await LightningAPI.AIProcessAsync(data);

// Përpunim hibrid
var hybridResult = await LightningAPI.HybridProcessAsync(data);

// Printim me Stigma
var printResult = await LightningAPI.StigmaPrintAsync(data);

// Përpunim në grup
var sources = new List<string> { "doc1.pdf", "doc2.pdf", "doc3.pdf" };
var batchResults = await LightningAPI.BatchProcessAsync(sources);

// Output real në formate të shkarkueshme
byte[] jpeg = LightningAPI.EncodeOutput(scanResult.Data, OutputFormat.Jpeg, quality: 85);
byte[] tiff = LightningAPI.EncodeMultiPageOutput(pages, OutputFormat.Tiff);
byte[] pdf  = LightningAPI.EncodeMultiPageOutput(pages, OutputFormat.Pdf);
```

### Command Line Interface

```bash
wwwmmm scan document.pdf
wwwmmm process
wwwmmm print
wwwmmm pipeline document.pdf
wwwmmm batch doc1.pdf doc2.pdf doc3.pdf
wwwmmm convert document.png tiff 90
wwwmmm demo
wwwmmm serve 8080
wwwmmm issue-key unlimited admin@company.com "Acme" 10 365 stripe
```

`wwwmmm convert <input> <png|jpeg|bmp|tiff|pdf> [quality]` ekzekuton skanimin real
dhe shkruan skedarin e konvertuar pranë input-it. TIFF dhe PDF pranojnë disa faqe
përmes `LightningAPI.EncodeMultiPageOutput`.

Kujdes: `scan`, `process`, `print`, `pipeline` dhe `batch` pranojnë **skedarë
imazhi**. Një PDF refuzohet me mesazh të qartë; render-o më parë me
`wwwmmm document pdf-render <input.pdf> 1 faqja1.png`. Shembujt më sipër me
zgjatje `.pdf` janë ilustrim i formës së komandës, jo input i pranuar.

## REST API dhe Dashboard

- GET /: landing page
- GET /dashboard: dashboard i menaxhimit të licencave
- GET /api/health: status i shërbimit
- GET /api/license/status: status dhe përdorim i licencës
- POST /api/license/issue: gjenerim licence
- POST /api/license/activate: aktivizim licence\n- POST /api/stripe/webhook: verifikim firme Stripe me secrets legacy/snapshot/thin dhe deduplikim eventesh

Dokumentimi: [Docs/CLOUD_API.md](Docs/CLOUD_API.md)

## Desktop GUI

Aplikacioni desktop (Windows Forms) gjendet këtu:

- [Desktop/LightningSPP.Desktop/LightningSPP.Desktop.csproj](Desktop/LightningSPP.Desktop/LightningSPP.Desktop.csproj)

Ekzekutimi:

```bash
dotnet run --project Desktop/LightningSPP.Desktop/LightningSPP.Desktop.csproj
```

Detaje: [Docs/DESKTOP_GUI.md](Docs/DESKTOP_GUI.md)

## Arkitektura e projektit

```text
wwwmmm/
├── Services/
│   ├── IScanService.cs
│   ├── IProcessingService.cs
│   ├── IPrintService.cs
│   ├── LightningPipeline.cs
│   └── Implementation/
│       ├── FastScanService.cs
│       ├── HybridProcessingService.cs
│       └── StigmaPrintService.cs
├── PublicAPI/
│   └── LightningAPI.cs
└── Program.cs
```

## Mënyrat e skanimit

- Lightning Mode: prioritet shpejtësie
- TideWave Mode: balancë shpejtësi/cilësi
- Resonance Mode: prioritet precizioni
- NanoDecibel Mode: prioritet cilësie

## Motorët e përpunimit

- CLX: motor bazë dhe efikas
- CLX.i: përpunim interaktiv me kontroll të zgjeruar
- XCL: për workload-e komplekse
- Clisonix Global AI: përmirësim automatik
- Hybrid: kombinim i motorëve për rezultate maksimale

## Cilësia e printimit

- Draft
- Normal
- High
- Stigma (artifact lossless me Brotli; jo driver printeri fizik)

## Performanca

Metrikat varen nga hardware, konfigurimi dhe input-i. Objektivat e latencës janë objektiva benchmark-u, jo garanci universale. Rezultatet duhen publikuar me hardware, dataset, konfigurim dhe numër përsëritjesh. Për referencë lokale, shih:

- [SUMMARY.md](SUMMARY.md)
- [REAL_PROCESSING.md](REAL_PROCESSING.md)

## NodeDB Fluid

Lightning SPP ka CLI bridge për leximin dhe ingest-in e telemetry te endpoint-et
NodeDB të OS-CLX. Store-i i shqyrtuar në OS-CLX është në memorie dhe ka kufi
512 node; bridge-i nuk provon persistence, replikim ose ekzekutim detyrash.
Kloud ka një implementim tjetër të control-plane me lease dhe WAL, por gjendja
e node/lease mbahet në memorie, WAL nuk rikuperohet gjatë nisjes dhe gabimet e
shkrimit në WAL nuk propagohen. Krahasimi dhe revision-et janë te
[kontratat e bridges](Docs/BRIDGES.md).

## Deployment

Gjendja e target-eve dhe kufijtë:

- Target framework është .NET 10; `PublishAot` është çaktivizuar dhe Native AOT
  nuk është validuar për varësitë aktuale.
- Dockerfile dhe workflow CI mbulojnë ndërtim/testim container-ash Linux.
- Ekzekutimi është provuar në Windows dhe Linux për rrugët përkatëse; macOS nuk
  deklarohet si platformë e verifikuar.
- Printimi fizik i workflow-it përdor Linux/CUPS; aplikacioni WinForms në
  `Desktop/` është sipërfaqe e veçantë Windows.
- Build-i përdor invariant globalization.

### Docker

```bash
# Build
docker build -t lightning-spp .

# Run
docker run lightning-spp demo
```

For production hosting, see [Docs/PRODUCTION_DEPLOYMENT.md](Docs/PRODUCTION_DEPLOYMENT.md) for Docker Compose, Ubuntu systemd, and nginx reverse proxy options.

## Naming convention

Për konsistencë ndërmjet source, package dhe CLI:

- Repository/Source root: wwwmmm
- C# namespace kryesor: wwwmmm
- NuGet package ID: LightningSPP
- Produkt publik: Lightning SPP 3.14

## Dokumentimi

- [MANUAL.md](MANUAL.md) — manuali i përdorimit, rregullat e sigurisë, operimi dhe matrica e pranimit
- [Docs/COMPLIANCE/README.md](Docs/COMPLIANCE/README.md) — dosja e përputhshmërisë: boshllëqe, rreziqe, SOUP, kontrolle, gjurmueshmëri
- [QUICKSTART.md](QUICKSTART.md)
- [Docs/API_Documentation.md](Docs/API_Documentation.md)
- [Docs/ARCHITECTURE.md](Docs/ARCHITECTURE.md)
- [Docs/DEPLOYMENT.md](Docs/DEPLOYMENT.md)
- [Docs/SLA_SLO.md](Docs/SLA_SLO.md)
- [Docs/GO_LIVE_CHECKLIST.md](Docs/GO_LIVE_CHECKLIST.md)
- [deploy/production/README.md](deploy/production/README.md)
- [Wiki/Home.md](Wiki/Home.md)

## Licenca

Lightning SPP ndan licencat sipas komponentëve: **SDK/client MIT; motori dhe runtime-i komercial**, sipas [LICENSE](LICENSE).

- `SDK/`, `Examples/`, `Desktop/`, `VSExtension/` dhe përmbajtja origjinale e `Web/wwwroot/` përdorin [MIT](LICENSE-MIT.txt).
- Motori, shërbimet e përpunimit, serveri dhe Longo përdorin [licencimin komercial](LICENSE-COMMERCIAL.txt), sipas marrëveshjes përkatëse me shkrim. Kontakt: <clisonix@pm.me>.
- Paketa aktuale `LightningSPP` përmban të dy grupet; nuk është paketë e motorit vetëm nën MIT.
- Të drejtat MIT të dhëna më parë për kopjet/versionet e mëparshme nuk revokohen nga kjo politikë. Varësitë ruajnë licencat e tyre.
- Planet dhe çelësat e produktit përshkruhen te [PRICING.md](PRICING.md); një çelës aktivizimi nuk jep vetvetiu të drejtën e rishpërndarjes së kodit komercial.

## Kontributet

Kontributet janë të mirëpritura. Udhëzimet: [CONTRIBUTING.md](CONTRIBUTING.md)

## Support

Për pyetje ose raportime, hap një issue në repository.

## Community & Contributors

Duam sa më shumë kontribues aktivë 🚀

- Discussions: <https://github.com/Web8kameleon-hub/Lightning-SPP-3.14/discussions>
- Issues: <https://github.com/Web8kameleon-hub/Lightning-SPP-3.14/issues>
- Contributing guide: [CONTRIBUTING.md](CONTRIBUTING.md)
- Stepstones roadmap: [STEPSTONES.md](STEPSTONES.md)
- Community wiki page: [Wiki/Community-Guide.md](Wiki/Community-Guide.md)

### Quick start për contributor të rinj

1. Fork repo
2. Zgjidh një issue me label `good-first-issue`
3. Hap PR të vogël + test/dokumentim
4. Bashkohu në Discussions për ide të reja



## Kufijtë e versionit 3.14

- Output-i i printimit është artifact Brotli `.br` lossless. Përveç tij, `OutputAdapter`
  prodhon PNG, JPEG, BMP, TIFF dhe PDF realë si skedarë të shkarkueshëm
  (`LightningAPI.EncodeOutput`, `LightningAPI.EncodeMultiPageOutput`).
- Printimi fizik është vetëm në Linux (`Documents/LinuxDeviceService.cs`, përmes radhës së
  printimit të sistemit) dhe nuk është verifikuar në hardware; platformat e tjera hedhin
  `PlatformNotSupportedException`. Rendering i faqeve PDF kërkon Poppler.
- Përdorimi mjekësor kërkon validim klinik dhe certifikimet ligjore për tregun ku shpërndahet.
- ISO, CE, FDA, GDPR dhe HIPAA nuk deklarohen si certifikime të produktit pa evidencë të auditueshme.

<!-- LIGHTNING-EVIDENCE:START -->
## Current verified status

This status is generated from recorded verification evidence. It does not replace product documentation or architecture history.

| Check | Result |
| --- | --- |
| Evidence | PARTIAL |
| Commit | uncommitted working tree |
| Build | GREEN (0 warnings, 0 errors) |
| Tests | 328 passed, 0 failed, 15 skipped, 343 total |
| Recorded | 2026-11-03 |

The test row describes the recorded Windows run in this working tree. It is not a combined Windows/Linux result and not a committed revision. The 15 skips are the native-document and isolated upstream integration tests that were not enabled in this run.

PDF Processor worktree verification (base `d390369`; source and artifact hashes recorded in `evidence/pdf-processor-verification.json`): build PASS; Windows 126/0/13, Linux 132/0/7, PDF boundary suite with Poppler enabled 24/0/0 (passed/failed/skipped). Browser workflow and downloaded PDF re-rendering: PASS. These are local results, not a production deployment.

Linux/native PDF: 6 passed, 0 failed, 0 skipped at `c1183ee` (Historical native PDF run reported by the user; separate from the Windows baseline.).

Production evidence is recorded separately at `c1183ee`: Recorded production evidence from the prior session and user-reported real two-page /try verification; not rechecked in this task.. No production check or deployment is performed by this generator.

### Verification gaps

- Real Excel and NodeDB upstream integration tests were skipped because their isolated services were not enabled.
- Successful real-model ONNX inference has not been verified.
- Physical scanner and printer operation has not been verified.

**Evidence status is not the same as production status.**
<!-- LIGHTNING-EVIDENCE:END -->
