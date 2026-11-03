# 09 — Bridges dhe ekosistemi

Lidhja me projektet e tjera nuk është marketing: është kontratë e versionuar,
autentikim, kufij, prova reale dhe rrugë çaktivizimi. Ky dokument përcakton
kontratën që duhet të plotësojë **çdo** bridge i ri, inventarin e asaj që
ekziston sot, dhe hapat për t'i lidhur projektet e mbetura pa i shpikur.

## 9.1 Gjendja reale (e regjistruar, e pa zbukuruar)

Burimi: `Docs/BRIDGES.md`.

| Bridge | Upstream | Operacione | Gjendja |
| --- | --- | --- | --- |
| `ClisonixExcelCoreBridge` | `clisonix.com/excel-core/excel_reporting_api.py` | `GET /api/lagter/v1/export` | Verifikuar lokalisht me shërbim real |
| `ClisonixExcelBridge` | `clisonix.com/services/excel/main.py` | `POST /api/excel/validate`, `/api/excel/parse` | Verifikuar lokalisht; fix upstream në draft PR #143, i pa bashkuar |
| `OsClxNodeDbBridge` | `OS-CLX/runtime/internal/api/ndb_store_handlers.go` | `GET /v1/ndb/nodes/{id}`, `POST /v1/ndb/nodes/{id}/telemetry` | Kontratë e testuar me mock; ingest/readback real **i paverifikuar** |

Fakte që duhen thënë me zë:

- Excel u provua me shërbime reale lokale; OS-CLX nuk përfundoi verifikimin e
  shkrimit/leximit me auth/HMAC.
- Ekzekutuesi i ndërtuar i OS-CLX u karantinua nga Windows Defender si
  `Trojan:Win32/Bearfoos.A!ml`. Kjo është regjistrim i një detektimi, jo
  gjykim mbi saktësinë e tij. **Nuk lejohet** përjashtim antivirus si rrugë për
  ta quajtur testin të kaluar; kërkohet shqyrtim sigurie.
- Asnjë bridge nuk është i pranuar për prodhim.
- Repository-t e tjerë të llogarisë nuk janë inventarizuar dhe nuk janë
  integruar. Ky dokument nuk pretendon se lista e mësipërme është e plotë.

## 9.2 Kontrata e detyrueshme për çdo bridge

Një hyrje e re pranohet vetëm me të gjashtë pikat:

1. **Identiteti.** Emri i projektit, revisioni upstream, pronari, mjedisi,
   operacionet reale. Një repository nuk është vetvetiu shërbim i lidhshëm.
2. **Skema.** Hyrje/dalje e versionuar, gabimet e mundshme, kufijtë e madhësisë,
   klasifikimi i të dhënave, kontroll pronësie në të dy anët.
3. **Siguria.** Destinacione të lejuara nga operatori, TLS, kredenciale shërbimi
   me privilegj minimal, rotacion, redaktim sekretesh në log.
4. **Rezistenca.** Deadline, anulim, backpressure. Retry vetëm me semantikë të
   provuar; timeout pas shkrimi = rezultat i panjohur. Deduplikim dhe readback
   përcaktohen nga kontrata upstream, jo nga supozimi i klientit.
5. **Provat.** Teste kontrate **dhe** teste me shërbimin real: auth që mungon/i
   gabuar, payload i pavlefshëm, përgjigje e dëmtuar, kufij, timeout,
   ndërprerje, izolim. Mock-et provojnë kontratën, jo lidhjen.
6. **Jeta.** Procedurë çaktivizimi/rollback, përputhshmëri versionesh, evidencë
   release-i. Mungesa e shërbimit shfaqet si mungesë aftësie, jo si sukses bosh.

## 9.3 Kufijtë e transportit (të implementuar)

| Kufi | Vlera | Ku |
| --- | --- | --- |
| Deadline | 30 s | `BridgeConnection` |
| Redirect automatik | i ndaluar | `AllowAutoRedirect = false` |
| Retry automatik | asnjë | — |
| Përgjigje JSON | 2 MiB | i detyruar gjatë leximit |
| Upload/export workbook | 10 MiB | `ReadBoundedFileAsync` |
| Skedar telemetrie | 64 KiB | `NodeDb` CLI |
| HTTPS | i detyrueshëm (loopback lejohet vetëm në teste) | `BridgeConnection` |
| URL me userinfo/query/fragment | refuzohet | `BridgeConnection` |

Këto mbrojtje ruhen. Mos i dobëso për të "bërë që të punojë".

## 9.4 Konfigurimi

```bash
SPP_EXCEL_CORE_URL=https://excel-core.internal
SPP_EXCEL_CORE_API_KEY=...
SPP_EXCEL_SERVICE_URL=https://excel.internal
SPP_EXCEL_SERVICE_API_KEY=...
SPP_OSCLX_URL=https://os-clx.internal
SPP_OSCLX_API_KEY=...
SPP_OSCLX_HMAC_SECRET=...
```

- GitHub PAT autentikon repository, **jo** shërbim. Nuk është kredencial bridge.
- Asnjëri prej aplikacioneve Excel të shqyrtuara nuk zbaton vetë auth Bearer:
  kredenciali ka vlerë vetëm nëse ingress-i e zbaton. Mos e ekspozo upload-in e
  dokumenteve publikisht pa auth.
- Prefiksi i rrugës vendoset në URL-në bazë, sepse nënshkrimi HMAC përdor
  shtegun e dërguar në HTTP; ai duhet të përputhet me atë që kontrollon
  HMACGuard i OS-CLX.

## 9.5 Telemetria e NodeDB — fusha të detyrueshme

Skedar UTF-8 JSON, jo më shumë se 64 KiB. Të detyrueshme:

```json
{
  "fabric_id": "core",
  "trust_domain": "internal",
  "pressure": 0.4,
  "resonance": 0.6,
  "health_score": 0.9,
  "memory": 0.5,
  "momentum": 0.3,
  "error_rate": 0.01,
  "queue_depth": 12,
  "latency_ms": 41
}
```

- Gjashtë sinjalet e normalizuara duhet të jenë të fundme në `[0, 1]`.
- `queue_depth` dhe `latency_ms` të fundme dhe jo-negative.
- Fusha opsionale: `role`, `capabilities`, `affinity_tags`,
  `preferred_task_types`, `burst_allowed`.
- Fusha që mungon numerike **refuzohet**; nuk trajtohet si zero.
- Fusha e panjohur refuzohet. Listat: maksimumi 64 elementë, 128 karaktere.
- `node_id`: 1–128 shkronja ASCII, shifra, `-`, `_`.

Ingest konsiderohet i kryer vetëm kur ack i upstream përputhet me nodin,
fabric-in, trust domain-in dhe vlerat e dërguara. Ky është ack i ingest-it,
**jo** provë replikimi ose ekzekutimi të detyrës.

## 9.6 Inventari i ekosistemit — si të vazhdohet

Tabela më poshtë është vendi ku regjistrohet çdo projekt i ekosistemit. Nuk
mbushet me hamendje: çdo rresht kërkon pronar dhe evidencë.

| Projekt | Lloji (repo/shërbim) | Revisioni | Pronari | Operacione | Kontrata | Gjendja |
| --- | --- | --- | --- | --- | --- | --- |
| Clisonix Excel Core | shërbim | `6a37245` | — | export LAGTER | `.xlsx` | verifikuar lokalisht |
| Clisonix Excel service | shërbim | `6a37245` + patch | — | validate/parse | JSON | fix në draft, i pa deployuar |
| OS-CLX | shërbim | `c54e104` | — | NodeDB | JSON/HMAC | i bllokuar nga shqyrtimi i sigurisë |
| Longo | shërbim | — | — | state HTTP | JSON | në përdorim për llogari |
| Ocean / Clisonix | shërbim | — | — | asistent, përkthim | HTTP | i palidhur me workflow |
| Lightning SPP | ky repo | HEAD | — | scan/process/output | CLI/API/HTTP | aktiv |

Rregull: një rresht pa pronar dhe pa revision është **i papranuar**, edhe nëse
kodi ekziston.

## 9.7 Radha e punës për t'i lidhur të gjitha

1. **Inventar.** Plotëso §9.6 me pronar dhe revision për çdo projekt.
2. **Një bridge në një kohë.** Mos hap pesë lidhje paralelisht; një kontratë e
   pranuar vlen më shumë se pesë gjysmë të provuara.
3. **Read-only së pari.** Vetëm pasi një lexim real është provuar, shto shkrim.
4. **Shkrimi kërkon rakordim.** Për çdo shkrim: idempotencë ose readback.
5. **Regjistro përjashtimet.** Nëse një upstream nuk zbaton auth, shkruaje dhe
   mos e quaj të sigurt.
6. **Çaktivizo në mënyrë të provueshme.** Një bridge duhet të mund të fiket pa
   restartuar produktin dhe pa lënë gjendje të varur.

## 9.8 Rreziku që duhet menaxhuar (SSRF dhe shkrimi)

- Asnjë URL nga përdoruesi nuk duhet të arrijë te `HttpClient`.
- Destinacionet vijnë vetëm nga mjedisi i operatorit.
- Redirect i ndaluar: parandalon kalimin nga host i lejuar në host të brendshëm.
- Rezolucioni DNS dhe rrjeti duhet të izolohen: një host i lejuar nuk duhet të
  arrijë në `169.254.169.254` ose shërbime metadata.
- Skemat e lejuara vetëm `http`/`https`.

## 9.9 Çka do të thotë "demonstrim force teknologjike"

Një demonstrim i vlefshëm lidh **vetëm aftësi të pranuara**:

```
dokument i autorizuar
  → hash i origjinalit
  → plan i shqyrtuar
  → përpunim real
  → bridge real i miratuar
  → eksport i validuar
  → raport provash (commit, mjedis, hash-e, kohë)
```

Përfshi një **dështim të kontrolluar** dhe rikuperim pa humbje të origjinalit.
Një demo që nuk tregon një dështim dhe rikuperimin e tij nuk provon forcë; provon
vetëm rrugën e lumtur.

## 9.10 Lidhja me dokumentet e tjera

- Kontrata aktuale: `Docs/BRIDGES.md`
- Programi i ngritjes: `Docs/TECHNOLOGY_EVOLUTION.md`
- Kufijtë: `10-KUFIJTE-DHE-PRETENDIMET.md`
- Pranimi: `11-MATRICA-E-PRANIMIT.md`
- Rregullat e sigurisë për bridges: `06` §6.9
