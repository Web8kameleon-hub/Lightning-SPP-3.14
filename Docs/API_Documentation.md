# API Documentation - Lightning Scan-Process-Print System

## Përmbledhje

Lightning SPP është një sistem i plotë për skanim, përpunim dhe printim që integron teknologjitë më të avancuara:

- **TideWave**, **Resonance**, **NanoDecibel** për skanim
- **CLX**, **CLI.I**, **XCL**, **Clisonic AI** për përpunim
- **Stigma**, **Stigma Memory** për printim

## API Publik

### Namespace: `wwwmmm.PublicAPI`

#### LightningAPI

Klasa statike që ekspozon të gjitha funksionalitetet kryesore.

---

### Metodat Kryesore

#### 1. ExecuteLightningPipelineAsync

Pipeline i plotë: Scan → Process → Print

```csharp
Task<PipelineResult> ExecuteLightningPipelineAsync(
    string source,
    CancellationToken cancellationToken = default
)
```

**Parametrat:**

- `source`: Burimi për skanim (emri i file-it)
- `cancellationToken`: Token për anulim (opsional)

**Kthyen:**

- `PipelineResult` me detajet e ekzekutimit

**Shembull:**

```csharp
var result = await LightningAPI.ExecuteLightningPipelineAsync("document.pdf");
if (result.Success)
{
    Console.WriteLine($"Completed in {result.TotalTimeMs}ms");
}
```

---

#### 2. ExecuteCustomPipelineAsync

Pipeline me konfigurim custom

```csharp
Task<PipelineResult> ExecuteCustomPipelineAsync(
    PipelineRequest request,
    CancellationToken cancellationToken = default
)
```

**Parametrat:**

- `request`: Konfigurim i detajuar për pipeline

**Shembull:**

```csharp
var request = new PipelineRequest(
    Source: "photo.jpg",
    ScanMode: ScanMode.NanoDecibel,
    Quality: 600,
    ProcessingEngine: ProcessingEngine.Hybrid,
    UseAI: true,
    PrintQuality: PrintQuality.Stigma
);

var result = await LightningAPI.ExecuteCustomPipelineAsync(request);
```

---

#### 3. QuickScanAsync

Skanim i shpejtë me TideWave + Resonance

```csharp
Task<ScanResult> QuickScanAsync(
    string source,
    CancellationToken cancellationToken = default
)
```

**Performance:** ~30ms

**Shembull:**

```csharp
var result = await LightningAPI.QuickScanAsync("document.pdf");
Console.WriteLine($"Scanned {result.Data.Length} bytes");
```

---

#### 4. HighQualityScanAsync

Skanim me cilësi të lartë duke përdorur NanoDecibel

```csharp
Task<ScanResult> HighQualityScanAsync(
    string source,
    CancellationToken cancellationToken = default
)
```

**Performance:** ~80ms
**Resolution:** 600 DPI

**Shembull:**

```csharp
var result = await LightningAPI.HighQualityScanAsync("photo.jpg");
Console.WriteLine($"Resolution: {result.Metadata.Resolution} DPI");
```

---

#### 5. AIProcessAsync

Përpunim me Clisonic Global AI

```csharp
Task<ProcessingResult> AIProcessAsync(
    byte[] data,
    CancellationToken cancellationToken = default
)
```

**Teknologjia:** Clisonic Global AI
**Performance:** ~40ms

**Shembull:**

```csharp
var scanResult = await LightningAPI.QuickScanAsync("doc.pdf");
var processResult = await LightningAPI.AIProcessAsync(scanResult.Data);
```

---

#### 6. HybridProcessAsync

Përpunim me të gjithë motorët (CLX + CLI.I + XCL + AI)

```csharp
Task<ProcessingResult> HybridProcessAsync(
    byte[] data,
    CancellationToken cancellationToken = default
)
```

**Teknologjia:** Hybrid Multi-Engine
**Performance:** ~50ms

**Shembull:**

```csharp
var result = await LightningAPI.HybridProcessAsync(data);
Console.WriteLine($"Engine: {result.Metadata.Engine}");
```

---

#### 7. StigmaPrintAsync

Printim me cilësi premium duke përdorur Stigma + Resonance

```csharp
Task<PrintResult> StigmaPrintAsync(
    byte[] data,
    CancellationToken cancellationToken = default
)
```

**Teknologjia:** Stigma Premium + Resonance
**Performance:** ~60ms

**Shembull:**

```csharp
await LightningAPI.StigmaPrintAsync(data);
```

---

#### 8. BatchProcessAsync

Përpunim në grup për dokumente të shumta

```csharp
Task<List<PipelineResult>> BatchProcessAsync(
    List<string> sources,
    CancellationToken cancellationToken = default
)
```

**Shembull:**

```csharp
var docs = new List<string> { "doc1.pdf", "doc2.pdf", "doc3.pdf" };
var results = await LightningAPI.BatchProcessAsync(docs);

var successful = results.Count(r => r.Success);
Console.WriteLine($"Processed {successful}/{docs.Count}");
```

---

## Tipet e të Dhënave

### ScanMode

```csharp
public enum ScanMode
{
    Lightning,      // ~10ms - Më i shpejti
    TideWave,       // ~30ms - Balancuar
    Resonance,      // ~50ms - Precizion i lartë
    NanoDecibel     // ~80ms - Cilësi maksimale
}
```

### ProcessingEngine

```csharp
public enum ProcessingEngine
{
    Auto,           // Zgjedh automatikisht
    CLX,            // ~20ms - Motor bazë
    CLI_I,          // ~25ms - Interaktiv
    XCL,            // ~30ms - I zgjeruar
    Clisonic,       // ~40ms - AI-powered
    Hybrid          // ~50ms - Të gjithë motorët
}
```

### PrintQuality

```csharp
public enum PrintQuality
{
    Draft,          // Më i shpejti
    Normal,         // Standard
    High,           // Cilësi e lartë
    Stigma          // Premium - Cilësi maksimale
}
```

### PipelineRequest

```csharp
public record PipelineRequest(
    string Source,                          // Burimi për skanim
    ScanMode ScanMode = ScanMode.Lightning, // Mënyra e skanimit
    int Quality = 300,                      // DPI
    bool UseResonance = true,               // Aktivizo Resonance
    bool UseNanoDecibel = true,             // Aktivizo NanoDecibel
    ProcessingEngine ProcessingEngine = ProcessingEngine.Auto,
    bool UseAI = true,                      // Aktivizo AI
    bool UseClisonic = true,                // Aktivizo Clisonic
    int OptimizationLevel = 3,              // Niveli 1-5
    bool UseStigmaMemory = true,            // Aktivizo Stigma Memory
    bool ShouldPrint = true,                // A duhet të printohet
    string PrintDestination = "default",    // Destinacioni
    PrintQuality PrintQuality = PrintQuality.Stigma,
    bool UseStigma = true,                  // Aktivizo Stigma
    int Copies = 1                          // Numri i kopjeve
);
```

### PipelineResult

```csharp
public record PipelineResult(
    bool Success,           // A përfundoi me sukses
    long TotalTimeMs,       // Koha totale në ms
    List<string> Steps,     // Hapat e ekzekutuar
    string? Error = null    // Gabimi (nëse ka)
);
```

### ScanResult

```csharp
public record ScanResult(
    byte[] Data,            // Të dhënat e skanuara
    ScanMetadata Metadata,  // Metadata
    bool Success,           // Statusi
    string? Error = null    // Gabimi (nëse ka)
);
```

### ProcessingResult

```csharp
public record ProcessingResult(
    byte[] ProcessedData,        // Të dhënat e përpunuara
    ProcessingMetadata Metadata, // Metadata
    bool Success,                // Statusi
    string? Error = null         // Gabimi (nëse ka)
);
```

### PrintResult

```csharp
public record PrintResult(
    bool Success,           // Statusi
    PrintMetadata Metadata, // Metadata
    string? Error = null    // Gabimi (nëse ka)
);
```

---

## Shembuj të Përparuar

### Shembull 1: Workflow i plotë me error handling

```csharp
try
{
    // Skanim
    var scanResult = await LightningAPI.HighQualityScanAsync("document.pdf");
    if (!scanResult.Success)
    {
        Console.WriteLine($"Scan failed: {scanResult.Error}");
        return;
    }

    // Përpunim me AI
    var processResult = await LightningAPI.AIProcessAsync(scanResult.Data);
    if (!processResult.Success)
    {
        Console.WriteLine($"Processing failed: {processResult.Error}");
        return;
    }

    // Printim
    var printResult = await LightningAPI.StigmaPrintAsync(processResult.ProcessedData);
    if (printResult.Success)
    {
        Console.WriteLine("Pipeline completed successfully!");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
```

### Shembull 2: Batch processing me progress tracking

```csharp
var documents = Directory.GetFiles("./scans", "*.pdf");
var completed = 0;

foreach (var doc in documents)
{
    var result = await LightningAPI.ExecuteLightningPipelineAsync(doc);
    if (result.Success)
    {
        completed++;
        Console.WriteLine($"Progress: {completed}/{documents.Length}");
    }
}
```

### Shembull 3: Pipeline me konfigurim optimal

```csharp
var request = new PipelineRequest(
    Source: "important-doc.pdf",
    ScanMode: ScanMode.Resonance,        // Precizion i lartë
    Quality: 600,                         // DPI maksimal
    ProcessingEngine: ProcessingEngine.Hybrid, // Të gjithë motorët
    UseAI: true,                          // AI aktiv
    OptimizationLevel: 5,                 // Maksimum
    PrintQuality: PrintQuality.Stigma,    // Cilësi premium
    Copies: 3                             // 3 kopje
);

var result = await LightningAPI.ExecuteCustomPipelineAsync(request);
```

---

## Performance Benchmarks

| Operacioni        | Koha mesatare | Teknologjia          |
| ----------------- | ------------- | -------------------- |
| Lightning Scan    | 10ms          | Lightning            |
| TideWave Scan     | 30ms          | TideWave             |
| Resonance Scan    | 50ms          | Resonance            |
| NanoDecibel Scan  | 80ms          | NanoDecibel          |
| CLX Processing    | 20ms          | CLX                  |
| CLI.I Processing  | 25ms          | CLI.I                |
| XCL Processing    | 30ms          | XCL                  |
| Clisonic AI       | 40ms          | AI                   |
| Hybrid Processing | 50ms          | Multi-Engine         |
| Stigma Print      | 60ms          | Stigma + Resonance   |
| **Full Pipeline** | **120ms**     | **All Technologies** |

---

## Best Practices

### 1. Zgjedhja e Scan Mode

- **Lightning**: Për dokumente normale, ku shpejtësia është prioritet
- **TideWave**: Për balancë të mirë midis shpejtësisë dhe cilësisë
- **Resonance**: Për dokumente të rëndësishme që kërkojnë precizion
- **NanoDecibel**: Për foto dhe grafika me cilësi maksimale

### 2. Zgjedhja e Processing Engine

- **CLX**: Për përpunim të shpejtë bazik
- **CLI.I**: Për kontroll interaktiv
- **XCL**: Për të dhëna komplekse
- **Clisonic**: Kur AI është i nevojshëm
- **Hybrid**: Për cilësi maksimale (përdor të gjithë motorët)
- **Auto**: Le sistemin të zgjedhë automatikisht

### 3. Optimization Level

- **1-2**: Për dokumente të thjeshta
- **3**: Balancuar (default)
- **4-5**: Për dokumente komplekse që kërkojnë cilësi maksimale

### 4. Batch Processing

Për volume të mëdha, përdor `BatchProcessAsync` që është optimizuar për paralelizim.

---

## Troubleshooting

### Pipeline dështon

Kontrollo `result.Error` dhe `result.Steps` për të parë ku ka ndodhur problemi.

### Performance i ulët

- Zvogëlo `OptimizationLevel`
- Përdor `ScanMode.Lightning` në vend të `NanoDecibel`
- Çaktivizo teknologjitë që nuk janë thelbësore

### Memory issues në batch

Përpuno në grupe më të vogla në vend që të ngarkosh të gjitha njëherësh.

---

## Licensa dhe Support

Projekti është i hapur dhe i gatshëm për përdorim publik. Për pyetje dhe support, referoju dokumentacionit kryesor në README.md.

---

Ky dokument mbuloi endpoint-et kryesore, parametrat, shembujt e përdorimit dhe praktikat e rekomanduara të integrimit për Lightning SPP 3.14.
