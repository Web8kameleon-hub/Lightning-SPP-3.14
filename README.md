# Lightning SPP 3.14
Universal Scan-Process-Print Platform

Lightning SPP 3.14 është një platformë e integruar për skanim, përpunim dhe printim dokumentesh/imazhesh, e ndërtuar për përdorim në desktop, web, IoT dhe cloud. Projekti kombinon performancë të lartë, arkitekturë modulare dhe ndërfaqe të shumta përdorimi (CLI, API, GUI).

## Çfarë sjell versioni 3.14

- Procesim real i të dhënave (jo i simuluar)
- Integrim i komponentëve të përpunimit të imazhit, AI/ML dhe kompresimit
- API publike për integrim të shpejtë në aplikacione ekzistuese
- Mbështetje për ekzekutim lokal dhe containerized

## REAL Processing

Versioni 3.14 përdor komponentë realë të ekosistemit .NET:

- SixLabors.ImageSharp për përpunim imazhi
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

Lightning SPP 3.14 është i përshtatshëm për:

- Pajisje mjekësore dhe diagnostikuese
- Aplikacione mobile (iOS/Android)
- Pajisje IoT dhe sisteme embedded
- Aplikacione desktop dhe web
- Workloads cloud me paralelizëm të lartë

## API e SDK-së

```csharp
using wwwmmm.SDK;

var result = await Lightning314.Instant(data);
```

### Profile të optimizuara sipas pajisjes

```csharp
// Medical profile
var sdk = UniversalSDK.Initialize(DeviceProfile.Medical);
var result = await sdk.ProcessAsync(scanData);

// Mobile profile
var mobileResult = await MobileAPI.ProcessMobile(data, batteryLevel: 75);

// IoT profile
var iotResult = await IoTAPI.ProcessConstrained(data, maxMemoryKB: 64);

// Cloud profile
var cloudResult = await CloudAPI.ProcessMassive(millionsOfItems, maxParallelism: 1000);
```

## Teknologjitë kryesore

- TideWave: skanim i balancuar
- CLX: motor bazë përpunimi
- CLI.I: përpunim interaktiv
- XCL: përpunim i zgjeruar për raste komplekse
- Resonance: fokus në precizion
- NanoDecibel: fokus në cilësi maksimale
- Clisonic Global AI: përmirësim i asistuar nga AI
- Stigma dhe Stigma Memory: printim premium dhe optimizim memorie

## Instalimi i shpejtë

```bash
dotnet build
dotnet run
```

## Përdorimi

### API publike

```csharp
using wwwmmm.PublicAPI;

// Pipeline i plotë: Scan -> Process -> Print
var result = await LightningAPI.ExecuteLightningPipelineAsync("document.pdf");

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
```

### Command Line Interface

```bash
wwwmmm scan document.pdf
wwwmmm process
wwwmmm print
wwwmmm pipeline document.pdf
wwwmmm batch doc1.pdf doc2.pdf doc3.pdf
wwwmmm demo
wwwmmm serve 8080
wwwmmm issue-key unlimited admin@company.com "Acme" 10 365 stripe
```

## REST API dhe Dashboard

- GET /: landing page
- GET /dashboard: dashboard i menaxhimit të licencave
- GET /api/health: status i shërbimit
- GET /api/license/status: status dhe përdorim i licencës
- POST /api/license/issue: gjenerim licence
- POST /api/license/activate: aktivizim licence

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
- CLI.I: përpunim interaktiv me kontroll të zgjeruar
- XCL: për workload-e komplekse
- Clisonic Global AI: përmirësim automatik
- Hybrid: kombinim i motorëve për rezultate maksimale

## Cilësia e printimit

- Draft
- Normal
- High
- Stigma (premium)

## Performanca

Metrikat varen nga hardware, konfigurimi i ekzekutimit dhe madhësia/lloji i input-it. Për referencë lokale, shih:

- [SUMMARY.md](SUMMARY.md)
- [REAL_PROCESSING.md](REAL_PROCESSING.md)

## NodeDB Fluid

NodeDB Fluid është një nyje hibride me overhead të ulët, e përdorur si sink/bridge adaptiv për kompatibilitet ndërmjet node-ve dhe profileve të ndryshme të ekzekutimit.

## Deployment

Platforma mbështet:

- .NET 10 dhe Native AOT
- Docker containers
- Cross-platform execution (Windows, Linux, macOS)
- Invariant globalization

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

- [QUICKSTART.md](QUICKSTART.md)
- [Docs/API_Documentation.md](Docs/API_Documentation.md)
- [Docs/ARCHITECTURE.md](Docs/ARCHITECTURE.md)
- [Docs/DEPLOYMENT.md](Docs/DEPLOYMENT.md)
- [Wiki/Home.md](Wiki/Home.md)

## Licenca

MIT License. Shih [LICENSE](LICENSE).

## Kontributet

Kontributet janë të mirëpritura. Udhëzimet: [CONTRIBUTING.md](CONTRIBUTING.md)

## Support

Për pyetje ose raportime, hap një issue në repository.

## Community & Contributors

Duam sa më shumë kontribues aktivë 🚀

- Discussions: https://github.com/Web8kameleon-hub/Lightning-SPP-3.14/discussions
- Issues: https://github.com/Web8kameleon-hub/Lightning-SPP-3.14/issues
- Contributing guide: [CONTRIBUTING.md](CONTRIBUTING.md)
- Stepstones roadmap: [STEPSTONES.md](STEPSTONES.md)
- Community wiki page: [Wiki/Community-Guide.md](Wiki/Community-Guide.md)

### Quick start për contributor të rinj
1. Fork repo
2. Zgjidh një issue me label `good-first-issue`
3. Hap PR të vogël + test/dokumentim
4. Bashkohu në Discussions për ide të reja

