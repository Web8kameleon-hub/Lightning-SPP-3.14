# Lightning SPP 3.14 — Manuali i përdorimit dhe rregullat e sigurisë

Ky është manuali operativ i Lightning SPP. Është shkruar për dy lexues:
**operatorin** që e instalon dhe e mban në prodhim, dhe **inxhinierin** që e
integron ose e ndryshon.

Parimi i vetëm i manualit: **çdo fjali duhet të jetë e vërtetuar nga ky
repository**. Aty ku kodi nuk e provon një aftësi, manuali e shënon si e hapur.
Nuk ka shembuj imagjinarë, nuk ka metrikë të shpikur dhe nuk ka certifikim të
padeklaruar.

## Nga ku të fillosh

| Nëse je... | Lexo në këtë radhë |
| --- | --- |
| Duke e instaluar për herë të parë | `01` → `02` → `03` |
| Duke e vënë në prodhim | `02` → `06` → `07` → `11` |
| Duke integruar nga kodi | `05` → `04` → `03` |
| Duke lidhur një projekt tjetër | `09` → `06` §6.9 → `11` |
| Duke zgjidhur një problem | `08` (dhe `07` për incidente) |
| Duke shkruar material klienti | `10` (i detyrueshëm) |

## Përmbajtja

| # | Dokument | Përmbajtja |
| --- | --- | --- |
| 01 | [Instalimi](01-INSTALLIMI.md) | Kërkesat, build-i, arkivat, verifikimi i parë |
| 02 | [Konfigurimi](02-KONFIGURIMI.md) | Të gjitha variablat e mjedisit që kodi lexon |
| 03 | [Komploti CLI](03-KOMPLOTI-CLI.md) | Çdo komandë, argumente, kode daljeje |
| 04 | [Shërbimi HTTP](04-SHERBIMI-HTTP.md) | Endpoint-e, faqe, auth, CSRF, CORS, kufij |
| 05 | [API dhe SDK](05-API-SDK.md) | `LightningAPI`, `Lightning314`, `UniversalSDK`, tipet |
| 06 | [Rregullat e sigurisë](06-RREGULLAT-E-SIGURISE.md) | Parimet, ndalimet, sekretet, incidentet |
| 07 | [Operimi dhe rikuperimi](07-OPERIMI-DHE-RIKUPERIMI.md) | Deploy, rollback, backup, restore, SLI/SLO |
| 08 | [Zgjidhja e problemeve](08-ZGJIDHJA-E-PROBLEMEVE.md) | Diagnostikim sipas simptomës |
| 09 | [Bridges dhe ekosistemi](09-BRIDGES-EKOSISTEMI.md) | Kontrata, inventari, hapat për t'i lidhur |
| 10 | [Kufijtë dhe pretendimet](10-KUFIJTE-DHE-PRETENDIMET.md) | Çka është provuar dhe çka jo |
| 11 | [Matrica e pranimit](11-MATRICA-E-PRANIMIT.md) | Portat dhe gjendja për çdo aftësi |

## Tre gjërat që duhet t'i dish para se të vazhdosh

1. **`print` nuk printon.** Prodhon një artefakt lossless Brotli (`.br`).
   Printimi fizik është vetëm Linux, përmes radhës së sistemit, dhe **nuk është
   verifikuar në harduer**.
2. **`scan` nuk skanon nga hardueri.** Lexon një imazh ekzistues dhe aplikohet
   pipeline-i real i përpunimit. Skanimi fizik (`device scan`) është i
   paverifikuar.
3. **Aktivizimi offline kërkon çelësin publik, jo sekretin.** Formati `LSP3`
   nënshkruhet me ECDSA dhe verifikohet në pajisje me `LSPP_LICENSE_PUBLIC_KEYS`.
   Nëse një pajisje konfigurohet me sekretin e emetuesit (HMAC `LSP2`), modeli i
   sigurisë është i shkatërruar — shih `02` §2.3.

Këto nuk janë detaje teknike: janë kufijtë që ndajnë një produkt të vërtetë nga
një demonstrim i bukur.

## Lidhja me dokumentet ekzistuese

Ky manual nuk i zëvendëson dokumentet historike; i organizon dhe i bën të
përdorshme.

| Dokument | Roli |
| --- | --- |
| [README.md](../../README.md) | Prezantimi i produktit |
| [QUICKSTART.md](../../QUICKSTART.md) | Rruga minimale |
| [Docs/INSTALLATION.md](../INSTALLATION.md) | Instalimi i arkivave runtime |
| [Docs/ARCHITECTURE.md](../ARCHITECTURE.md) | Arkitektura e komponentëve |
| [Docs/API_Documentation.md](../API_Documentation.md) | Referencë API (me kujdes për latencat) |
| [Docs/PRODUCTION_DEPLOYMENT.md](../PRODUCTION_DEPLOYMENT.md) | Hosting dhe proxy |
| [deploy/production/README.md](../../deploy/production/README.md) | Blue-green deploy |
| [Docs/BRIDGES.md](../BRIDGES.md) | Kontratat e bridges |
| [Docs/WORKFLOW_RUNTIME.md](../WORKFLOW_RUNTIME.md) | Modeli i workflow |
| [Docs/SLA_SLO.md](../SLA_SLO.md) | SLI, SLO, SLA, error budget |
| [Docs/GO_LIVE_CHECKLIST.md](../GO_LIVE_CHECKLIST.md) | Go/no-go |
| [Docs/COMPLIANCE/README.md](../COMPLIANCE/README.md) | Dosja e përputhshmërisë: boshllëqe, rreziqe, SOUP, gjurmueshmëri |
| [Docs/TECHNOLOGY_EVOLUTION.md](../TECHNOLOGY_EVOLUTION.md) | Programi i ngritjes |
| [AGENTS.md](../../AGENTS.md) | Rregulli "NO FAKE, production grade" |

## Rregulli që mban gjithçka bashkë

> Nëse nuk është reale, e vërtetuar dhe e nivelit të prodhimit, nuk kalon.

Ky rregull vlen për kodin, për dokumentin, për faqen e marketingut dhe për
përgjigjen që i jepet një klienti. Manuali është shkruar që ta bëjë atë rregull
të përdorshëm në punën e përditshme, jo vetëm të shtypur në një skedar.
