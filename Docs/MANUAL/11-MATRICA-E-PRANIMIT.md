# 11 — Matrica e pranimit

Një matricë e vetme që thotë, për secilën aftësi: kush e pranon, me çfarë prove,
dhe çka e bllokon. Pa këtë, "gati" është mendim.

## 11.1 Si lexohet

| Kolonë | Kuptimi |
| --- | --- |
| Aftësia | Njësi e veçantë e produktit |
| Niveli | `L` lokal · `I` integrim i izoluar · `P` prodhim |
| Kriteri | Prova minimale që duhet të ekzistojë |
| Gjendja | `PRANUAR` / `PJESSHËM` / `HAPUR` / `BLLOKUAR` |
| Pronari | Kush e mbyll |

Një aftësi nuk kalon në `P` sepse ka kaluar `L`. Kalimi është hap më vete.

## 11.2 Matrica

| Aftësia | Niveli | Kriteri i pranimit | Gjendja | Pronari |
| --- | --- | --- | --- | --- |
| Ndërtim i release-it | L | `dotnet build` 0/0 + suite i plotë | PRANUAR | Release |
| Përpunim imazhi (Skia) | L | Teste + `convert` real | PRANUAR | Engine |
| Përpunim imazhi (ImageSharp) | L | `engines` ekzekuton motorin R + testet | PRANUAR | Engine |
| Kodim PNG/JPEG/BMP/TIFF/PDF | L | Teste `OutputAdapter` | PRANUAR | Engine |
| `pdf-create` | L | PDF i hapur me shikues real | PRANUAR | Documents |
| `pdf-render` | L | Poppler real, faqe e saktë | PJESSHËM | Documents |
| `benchmark-documents` | L | Raport JSON me hash-e | PRANUAR | Documents |
| Shërbimi HTTP bazë | L | `/api/health` 200 | PRANUAR | Platform |
| Admin auth (401 pa token) | L | Provë e ruajtur | PRANUAR | Security |
| Kufizim norme | L | 429 pas kufirit | PRANUAR | Platform |
| CORS | L | Origin i palejuar refuzohet | PRANUAR | Security |
| Llogari + sesion | I | Longo real + SMTP real | PJESSHËM | Accounts |
| Verifikim email | I | Marrës real i jashtëm | HAPUR | Accounts |
| Pagesa Stripe (webhook) | I | Event real, dedup i provuar | PJESSHËM | Billing |
| Rikthim nga faturë | I | Event real i përdorur | PJESSHËM | Billing |
| Workspace (workflow) | I | Job i plotë me izolim pronari | PJESSHËM | Workflow |
| Model lokal (Ollama) | I | Inferencë reale + vlerësim | PJESSHËM | AI |
| Model ONNX | I | Model real + formë input-i | HAPUR | AI |
| Bridge Excel Core | I | Export real i verifikuar | PJESSHËM | Integrations |
| Bridge Excel service | I | Patch i deployuar + test real | BLLOKUAR | Integrations |
| Bridge OS-CLX NodeDB | I | Ingest/readback me HMAC | BLLOKUAR | Integrations |
| Printim fizik | I | Fletë e printuar + dëshmi | HAPUR | Devices |
| Skanim fizik | I | Skanim real nga SANE | HAPUR | Devices |
| Licencim offline (LSP3) | L | Nënshkrim ECDSA + verifikim me çelës publik | PRANUAR | Security |
| Device fingerprinting | P | Identitet pajisjeje i vërtetuar | HAPUR | Security |
| Metrika të qëndrueshme | P | Historik përtej rinisjes | HAPUR | Platform |
| Backup/restore i provuar | P | Ushtrim i regjistruar | HAPUR | Operations |
| Deploy i kthyeshëm | P | Rollback i provuar në atë release | PJESSHËM | Operations |
| Inventar ekosistemi | P | Çdo projekt me pronar/revision | HAPUR | Integration |
| Pretendime publike | P | Përputhje me `10` | HAPUR | Product |

## 11.3 Portat (gates)

**Gate 1 — Lokal.** Kalon kur: build 0/0, suite i plotë i gjelbër, `demo` dhe
`convert` prodhojnë skedarë realë, `/api/health` OK.

**Gate 2 — Integrim i izoluar.** Kalon kur: shërbimi real upstream është i
arritshëm në loopback ose rrjet të izoluar, testet e kontratës **dhe** të
shërbimit real kalojnë, dhe dështimet (auth, payload, timeout, ndërprerje)
trajtohen sipas kontratës.

**Gate 3 — Prodhim.** Kalon kur: Gate 2 i kaluar për çdo varësi të prekur,
backup/restore i provuar, rollback i provuar, verifikimi i `07` §7.5 i
regjistruar pas deploy-it, dhe asnjë pretendim publik nuk e kalon `10`.

## 11.4 Rregullat e matricës

- Një rresht `HAPUR` nuk bllokon release-in nëse aftësia nuk ekspozohet. Bllokon
  vetëm kur ekspozimi është i dukshëm për klientin.
- Një rresht `BLLOKUAR` me arsye sigurie **nuk** anashkalohet me përjashtim,
  flag ose provë manuale të padeklikuar.
- `PJESSHËM` do të thotë: funksionon për rastin e provuar, jo për të gjithë.
  Duhet thënë kështu në materialin e klientit.
- Pronari i një rreshti e mbyll me provë, jo me deklaratë.

## 11.5 Regjistri i provave

Për çdo mbyllje rreshti, regjistro:

```
Data:
Aftësia:
Commit / image digest:
Mjedisi (OS, runtime, hardware):
Komanda:
Rezultati (exit code, output relevant):
Kufizim i mbetur:
```

Ruaj provën (log, raport JSON, screenshot) pranë regjistrit. Një rresht pa
regjistër prove hapet përsëri në rishikimin tjetër.

## 11.6 Lidhja me dokumentet e tjera

- Lista go/no-go: `Docs/GO_LIVE_CHECKLIST.md`
- Evidenca e release-it: `Docs/RELEASE_EVIDENCE.md`
- Kufijtë: `10-KUFIJTE-DHE-PRETENDIMET.md`
- Programi i ngritjes: `Docs/TECHNOLOGY_EVOLUTION.md`
