# 03 — Komploti CLI: referencë e plotë

Burimi i së vërtetës: `Program.cs` (routing), `Documents/DocumentCommands.cs`,
`Workflow/WorkflowCommands.cs`, `Integrations/BridgeCommands.cs`.
Çdo komandë më poshtë ekziston në kod; asnjë nuk është shembull imagjinar.

## 3.0 Rregullat e përgjithshme

- Forma: `wwwmmm <komanda> [argumente...]` ose
  `dotnet run --project wwwmmm.csproj -- <komanda> [argumente...]`.
- Pa argumente: ekzekutohet `demo`.
- Komandë e panjohur: shtypet ndihma, exit code `0`.
- Komandat e dokumenteve, workflow dhe bridges kanë **exit code** kuptimplotë
  (shih 3.4).

### Kodet e daljes

| Kod | Kuptimi | Ku |
| --- | --- | --- |
| `0` | Sukses, ose ndihmë e shfaqur | Të gjitha |
| `1` | Dështim ekzekutimi (gabim aplikacioni, upstream, skedar) | dokumente, workflow, bridges, benchmark |
| `2` | Përdorim i gabuar (argumente të pavlefshme) | dokumente, workflow, bridges, `convert` |
| `130` | Anuluar me Ctrl+C | workflow |

Në PowerShell kontrolloje me `$LASTEXITCODE`; në bash me `echo $?`.
Një `1` nuk është "paralajmërim" — është dështim dhe duhet trajtuar.

---

## 3.1 Përpunimi bazë

### `scan <source>`

Skanim i shpejtë përmes `LightningAPI.QuickScanAsync`.

- Input: skedar imazhi real (PNG/JPEG/BMP…). PDF **refuzohet** me mesazh të
  qartë: duhet renderuar më parë me `document pdf-render`.
- Output: teknologji, DPI, madhësi bajt.
- Nuk shkruan skedar.

```bash
wwwmmm scan foto.jpg
```

### `process <source>`

Përpunim me Hybrid Multi-Engine mbi bajt-at e skedarit.

- Input: skedar ekzistues; nëse mungon, mesazh dhe dalje.
- Nuk shkruan skedar.

```bash
wwwmmm process dokument.png
```

### `print <source>`

Krijon **artefakt lossless Brotli** (`.br`), jo faqe fizike.

- Output: rruga e artefaktit dhe metadata.
- `PagesPrinted` në metadata është emër historik; nuk është numër faqesh të
  printuara fizikisht.

```bash
wwwmmm print dokument.png
```

### `pipeline <source>`

Rrjedha e plotë Scan → Process → Print, me listë hapash dhe status.

```bash
wwwmmm pipeline dokument.png
```

### `batch <source1> <source2> ...`

Përpunon shumë burime dhe raporton `sukses/total`, kohën totale dhe mesataren.

```bash
wwwmmm batch a.pdf b.pdf c.pdf
```

Kufizim praktik: memoria rritet me numrin e input-eve; për volume të mëdha
ndaj në grupe më të vogla.

### `convert <input> <png|jpeg|bmp|tiff|pdf> [quality 1-100]`

Konvertim real me koduesit e `OutputAdapter`.

- `quality` default `90`; jashtë `1..100` → exit `2`.
- Format i panjohur → exit `2`.
- Input që mungon → exit `1`.
- Skedari dalës shkruhet **pranë** input-it, me zgjatjen e formatit
  (`.png`, `.jpg`, `.bmp`, `.tiff`, `.pdf`).
- Ekzistuesi mbishkruhet në këtë komandë (ndryshe nga `document`/`bridge`, që
  refuzojnë mbishkrimin).

```bash
wwwmmm convert skanim.png tiff 95
```

---

## 3.2 Demonstrime dhe matje

| Komandë | Çka bën vërtet | Çka nuk provon |
| --- | --- | --- |
| `demo` | Validim real i përpunimit me fixture të identifikuara | Cilësi në corpus klienti |
| `real` | Rrjedha e plotë reale: skanim, kompresim, artefakte | Kapacitet prodhimi |
| `examples` | Shembuj përdorimi API | Pranim |
| `universal` | Shembuj SDK sipas profilit të pajisjes | Pranim |
| `speed` | Median/p95 mbi 20 ekzekutime, pas 5 ngrohjeve, input 64 KiB | Garancion performance |
| `engines` | Ekzekuton L (Skia) dhe R (ImageSharp) në 4 modelet, shtyp madhësinë dhe SHA-256 | Cilësinë e output-it |

`speed` është matje informuese e mjedisit tënd. Nuk është kriter pranimi;
pragjet e regresionit janë në suite-in e testimit.

**Nuk ekziston mod i simuluar.** Komanda `simulated`, `UseSimulatedServices()` dhe
`FastSimulated` janë hequr nga kodi; një komandë e panjohur shfaq ndihmën.

### `test` / `smoke-test`

Ekzekuton suite-in e **smoke test-eve**, jo suite-in e plotë. CLI e thotë këtë
me tekst paralajmërues. Për suite-in e plotë:

```bash
dotnet test Tests/LightningSPP.Tests/LightningSPP.Tests.csproj -c Release
```

---

## 3.3 Dokumente dhe pajisje

### `document pdf-create <new.pdf> <image>...`

Ndërton një PDF nga një ose më shumë imazhe.

- Maksimumi **32** imazhe (më shumë → gabim argumenti).
- Skedari dalës **nuk** mbishkruhet: nëse ekziston, dështon.
- Çdo imazh lexohet me kufi madhësie (`PdfDocumentService.MaxInputBytes`).
- Output: JSON `{ saved, pages }`.
- Nuk kërkon Poppler.

```bash
wwwmmm document pdf-create raport.pdf faqja1.png faqja2.png
```

### `document pdf-render <input.pdf> <page> <new.png>`

Renderon një faqe reale me Poppler.

- `page` duhet numër i plotë pozitiv.
- Kërkon `pdfinfo`/`pdftoppm` në PATH ose `SPP_PDFINFO_PATH`/`SPP_PDFTOPPM_PATH`.
- Dalja **nuk** mbishkruhet.
- Output: JSON `{ saved, page, totalPages }`.

```bash
wwwmmm document pdf-render raport.pdf 1 faqja1.png
```

### `device list-printers` / `device list-scanners`

Liston radhët CUPS dhe pajisjet SANE. Vetëm Linux; në platforma të tjera hedh
`PlatformNotSupportedException` (exit `1`).

### `device scan <sane-device> <dpi> <new.png>`

Skanim fizik përmes SANE, vetëm Linux, me pajisje reale të konfiguruar.
**Nuk është verifikuar në harduer në këtë repository.**

### `device print <cups-queue> <input.pdf>`

Dorëzon PDF në radhën CUPS. Suksesi i dorëzimit **nuk** vërteton printim fizik.

### `device print-status <job-id>`

Kthen gjendjen e radhës dhe `physicalCompletionVerified: false`. Ky fushë është
aty për arsye: mos e paraqit si provë të printimit.

**Rregull operativ:** pas një `print` të ndërprerë, kontrollo radhën para se ta
ripërsëritësh — mund të krijosh dublikatë fizikë.

---

## 3.4 Workflow (`workflow ...`)

```bash
wwwmmm workflow tools
wwwmmm workflow discover <route>
wwwmmm workflow ask <route> "<pyetje>"
wwwmmm workflow propose <root> <route> "<qëllim>"
wwwmmm workflow run <root> <plan.json> [--allow-remote] [--allow-print] [--allow-services]
wwwmmm workflow resume <root> <job-id> [--allow-remote] [--allow-print] [--allow-services]
```

Lejet bazë në CLI: `files.read`, `reports.write`, `model.invoke`.
Flag-et shtojnë: `--allow-remote` → `network.remote`; `--allow-print` →
`devices.print` + `devices.read`; `--allow-services` → `services.read`.

Rregulla:

- `run` refuzon planet mbi **1 MiB**.
- Plani duhet JSON i vlefshëm dhe i lexueshëm; mjetet e panjohura, argumentet e
  panjohura, varësitë që mungojnë dhe ciklet refuzohen.
- `ask` shënon `verification: MODEL_CLAIM_REQUIRES_REVIEW` — përgjigja e modelit
  është **pretendim**, jo fakt.
- `propose` shënon `state: PROPOSED_REQUIRES_REVIEW` dhe **nuk** ekzekuton.
- Anulim → exit `130`; inspekto gjendjen e ruajtur para `resume`.
- Çdo dalje është JSON në stdout; gabimet në stderr.

```bash
wwwmmm workflow tools
wwwmmm workflow run . plan.json --allow-services
```

---

## 3.5 Bridges (`bridge ...`)

```bash
wwwmmm bridge excel-export <new.xlsx>
wwwmmm bridge excel-validate <input.xlsx>
wwwmmm bridge excel-parse <input.xlsx>
wwwmmm bridge node-read <node-id>
wwwmmm bridge node-ingest <node-id> <measured-telemetry.json>
```

- `excel-export`: refuzon mbishkrimin; skedari duhet të mbarojë me `.xlsx`.
- Upload-et e Excel-it: kufi **10 MiB**; përgjigjet JSON: kufi **2 MiB**.
- Telemetria: kufi **64 KiB**, UTF-8 JSON, fusha të detyrueshme.
- Deadline 30 s, pa redirect automatik, pa retry automatik.
- Gabim përdorimi → `2`; dështim → `1`.

Detaje kontrate: `09-BRIDGES-EKOSISTEMI.md`.

---

## 3.6 Licencimi

```bash
wwwmmm activate <LICENSE-KEY>
wwwmmm license          # ose: status
wwwmmm deactivate
wwwmmm generate-license <free|unlimited|enterprise> [email] [company]
wwwmmm issue-key <tier> <email> <company> [devices] [days] [stripe|internal]
```

- `generate-license` është për zhvillim/testim; nuk është blerje.
- `issue-key` shkruan në audit log dhe kërkon materialin e emetuesit.
- Ruajtja: `%APPDATA%\LightningSPP\license.json` (Windows),
  `~/.config/LightningSPP/license.json` (Linux/macOS).

**Rrezik:** `issue-key` dhe `activate` përdorin të njëjtin sekret HMAC. Mos e
vendos në pajisje klienti. Shih `02-KONFIGURIMI.md` §2.3.

---

## 3.7 Shërbimi dhe mjetet

### `serve [port]` / `api [port]`

Nis serverin HTTP. Porti: argument → `PORT` → `13080`. Portë jashtë `1..65535`
→ `ArgumentException`.

### `benchmark-documents <input-dir> <new-report.json> [runs 1-10]`

Matje dokumentesh lokale.

- Lexon direktorinë **jo-rekursivisht**, nuk ngarkon dhe nuk modifikon skedarë.
- Direktoria e destinacionit duhet të ekzistojë; raporti duhet të jetë i ri.
- Kthen kod jo-zero kur ka input të refuzuar ose përpunim të dështuar.
- Mat pipeline-in demo grayscale — **jo** saktësi OCR, jo inferencë AI, jo
  përshtatshmëri klinike.
- Raporti mund të përmbajë hash-e dhe madhësi: trajtoje si privat.

```bash
wwwmmm benchmark-documents C:\docs C:\reports\raport.json 3
```

### `audit-imagesharp [root] [report.md]`

Skanon repo-n për përdorim real të SixLabors.ImageSharp dhe shkruan raport.

---

## 3.8 Ndihma

```bash
wwwmmm --help
```

Liston komandat, teknologjitë, çmimet dhe kontaktin. Nëse komanda që të duhet
nuk figuron aty, ajo nuk ekziston në këtë version.
