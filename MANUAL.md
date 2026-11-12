# Lightning SPP — Manuali

Hyrja e vetme për manualin e përdorimit dhe rregullat e sigurisë.

**Manuali i plotë:** [Docs/MANUAL/README.md](Docs/MANUAL/README.md)

## Përmbajtja në një ekran

| # | Dokument | Për kë |
| --- | --- | --- |
| 01 | [Instalimi](Docs/MANUAL/01-INSTALLIMI.md) | Kush e instalon |
| 02 | [Konfigurimi](Docs/MANUAL/02-KONFIGURIMI.md) | Kush e konfiguron |
| 03 | [Komploti CLI](Docs/MANUAL/03-KOMPLOTI-CLI.md) | Kush e përdor nga terminali |
| 04 | [Shërbimi HTTP](Docs/MANUAL/04-SHERBIMI-HTTP.md) | Kush e ekspozon |
| 05 | [API dhe SDK](Docs/MANUAL/05-API-SDK.md) | Kush integron nga kodi |
| 06 | [Rregullat e sigurisë](Docs/MANUAL/06-RREGULLAT-E-SIGURISE.md) | **Të gjithë** |
| 07 | [Operimi dhe rikuperimi](Docs/MANUAL/07-OPERIMI-DHE-RIKUPERIMI.md) | Kush e mban në prodhim |
| 08 | [Zgjidhja e problemeve](Docs/MANUAL/08-ZGJIDHJA-E-PROBLEMEVE.md) | Kush diagnostikon |
| 09 | [Bridges dhe ekosistemi](Docs/MANUAL/09-BRIDGES-EKOSISTEMI.md) | Kush lidh projektet |
| 10 | [Kufijtë dhe pretendimet](Docs/MANUAL/10-KUFIJTE-DHE-PRETENDIMET.md) | Kush shkruan për klientin |
| 11 | [Matrica e pranimit](Docs/MANUAL/11-MATRICA-E-PRANIMIT.md) | Kush pranon release-in |

## Rruga më e shkurtër për të filluar

```bash
dotnet build wwwmmm.csproj -c Release
dotnet run --project wwwmmm.csproj -- --help
dotnet run --project wwwmmm.csproj -- demo
dotnet run --project wwwmmm.csproj -- serve 13080
curl -fsS http://localhost:13080/api/health
```

Build-i kërkon `SIXLABORS_LICENSE` në mjedisin e build-it (shih
`Docs/MANUAL/01-INSTALLIMI.md` §1.2).

## Tre kufij që nuk duhen harruar

1. `print` prodhon artefakt lossless `.br`, **jo** printim fizik.
2. `scan` përpunon imazh ekzistues, **jo** skanim nga harduer.
3. Aktivizimi offline përdor formatin `LSP3` (ECDSA) dhe verifikohet në pajisje
   me **çelës publik** (`LSPP_LICENSE_PUBLIC_KEYS`). Sekreti i emetuesit nuk
   shkon në klient; shih `Docs/MANUAL/02-KONFIGURIMI.md` §2.3.

Detaje: `Docs/MANUAL/10-KUFIJTE-DHE-PRETENDIMET.md`.
