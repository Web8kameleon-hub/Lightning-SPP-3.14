# Prioritetet strategjike 2026-2027 (mesazh CEO → gjendje e verifikuar → roadmap)

## Konteksti

Mesazhi i CEO-s (7 Nëntor 2026) identifikon tre shtylla për të rritur njohjen e
ekosistemit brenda 2027:

1. Reduktim i zhurmës operacionale ("zero-noise systems")
2. Lehtësi integrimi për zhvillues ("Developer Experience")
3. Mbrojtje e përmbajtjes publike SEO nga scraping i jashtëm

Ky dokument lidh ato pritshmëri me gjendjen aktuale, të verifikuar në kod, dhe
propozon hapa konkretë. Asnjë nga hapat e roadmap-it më poshtë nuk është
implementuar ende — ato janë propozime, jo pretendime të kryera.

## Gjendja e verifikuar sot

### 1. Zero-noise systems

| Komponent | Gjendje | Burim |
| --- | --- | --- |
| Audit logging | Logger i thjeshtë statik, pa struktura/sampling (`Info`/`Warn`/`Error`) | [InternalAuditLogger.cs](/c:/Users/Admin/source/repos/wwwmmm/Web/InternalAuditLogger.cs) |
| Rate limiting | Limiter në memorje, per-proces, 120 kërkesa/minutë default, s'shkallëzohet nëpër instanca | [RuntimeSecurity.cs](/c:/Users/Admin/source/repos/wwwmmm/Web/RuntimeSecurity.cs) |
| Metrika | `LightningMetrics` ekziston si pikë observabiliteti operacional | referuar në udhëzimet e repos |

**Boshllëk:** nuk ka ende dedup/suppression të alarmeve, nuk ka logging të
strukturuar (JSON) me nivele konfigurueshme, dhe rate limiter-i nuk mban gjendje
të përbashkët mes instancave (zhurmë e rreme në deploy multi-instancë).

### 2. Developer Experience

| Komponent | Gjendje | Burim |
| --- | --- | --- |
| Onboarding | Udhëzues hap-pas-hapi me komanda copy-paste, Docker Compose, diagnostikim | [QUICKSTART.md](/c:/Users/Admin/source/repos/wwwmmm/QUICKSTART.md) |
| SDK | Vetëm 2 skedarë: `Lightning314.cs` (Instant API) dhe `UniversalSDK.cs` (profile: Medical/Mobile/IoT/Cloud) | [SDK/](/c:/Users/Admin/source/repos/wwwmmm/SDK) |
| Shpërndarje | Paketë NuGet `LightningSPP` e publikuar | [NUGET_PUBLISH.md](/c:/Users/Admin/source/repos/wwwmmm/NUGET_PUBLISH.md) |
| API docs | `openapi.json` + Swagger + `Docs/API_Documentation.md` | [openapi.json](/c:/Users/Admin/source/repos/wwwmmm/Web/wwwroot/openapi.json) |
| Kontribues | `CONTRIBUTING.md` + `STEPSTONES.md` me shkallë hyrjeje | [STEPSTONES.md](/c:/Users/Admin/source/repos/wwwmmm/STEPSTONES.md) |

**Boshllëk:** SDK ekziston vetëm për .NET; s'ka shembuj zyrtarë curl/Python/JS
krahas C#, dhe sipërfaqja publike e SDK-së s'ka një zotim të dokumentuar semver.

### 3. Mbrojtje SEO nga scraping

| Komponent | Gjendje | Burim |
| --- | --- | --- |
| Robots/Sitemap | `robots.txt` lejon faqet publike, bllokon `/api/`; `sitemap.xml` ekziston | [robots.txt](/c:/Users/Admin/source/repos/wwwmmm/Web/wwwroot/robots.txt) |
| Verifikim automatik | `tools/check-seo.mjs` verifikon titull, përshkrim, canonical, JSON-LD, sitemap | [check-seo.mjs](/c:/Users/Admin/source/repos/wwwmmm/tools/check-seo.mjs) |
| Evidencë | Rezultate të verifikuara lokalisht dhe në prodhim më 2026-10-06 | [SEO_VERIFICATION_2026-10-06.md](/c:/Users/Admin/source/repos/wwwmmm/Docs/SEO_VERIFICATION_2026-10-06.md) |

**Boshllëk:** s'ka kontroll teknik specifik anti-scraping përtej rate
limiter-it gjenerik — asnjë dallim User-Agent mes crawler-ave të njohur
(Googlebot) dhe scraper-ave të panjohur, asnjë WAF/CDN-layer, asnjë alarm
dedikuar për scraping-un e përmbajtjes.

## Roadmap i propozuar (12-18 muaj → 2027)

### Shtylla 1 — Zero-noise systems
- **Q1 2027:** Logging i strukturuar (JSON) me nivele konfigurueshme, zëvendësim i `InternalAuditLogger` aktual pa thyer sinjaturat ekzistuese.
- **Q1-Q2 2027:** Suppression/dedup i alarmeve bazuar në pragje të qëndrueshme, jo evente të izoluara.
- **Q2 2027:** Rate limiter i ndërgjegjshëm për deploy multi-instancë (shared store në vend të memorjes lokale), për të shmangur kufizime të rreme.
- **Q2-Q3 2027:** Teste regresioni për vëllimin e log-eve/alarmeve në `Tests/LightningSPP.Tests`.

### Shtylla 2 — Developer Experience
- **Q1 2027:** Shembuj zyrtarë curl/Python/JS krahas C# në `Docs/API_Documentation.md`, pa prekur kontratën ekzistuese të SDK-së.
- **Q1-Q2 2027:** Paketim i mundshëm i një klienti REST të hollë, i versionuar veçmas nga bërthama `LightningSPP`.
- **Q2 2027:** Konsol interaktiv "try it" i bazuar në `openapi.json`, përtej flow-it aktual të `/try`.
- **Q3 2027:** Zotim semver i dokumentuar për sipërfaqen publike të `SDK/` në `CONTRIBUTING.md`.

### Shtylla 3 — Mbrojtje SEO nga scraping
- **Q1 2027:** Logging (jo-bllokues fillimisht) i User-Agent për të dalluar crawler-a të njohur nga scraper-a të panjohur, si evidencë përpara çdo kufizimi.
- **Q1-Q2 2027:** Rate limits të shkallëzuar — më strikte për agjentë anonimë/të panjohur në faqet publike, duke ruajtur akses për bot-e kërkimi të verifikuar.
- **Q2 2027:** Meta/structured data pronësie të përmbajtjes, lidhur me `LEGAL_NOTICE.md`, të automatizuar si kontroll shtesë në `tools/check-seo.mjs`.
- **Q3 2027:** Vlerësim i mbrojtjes CDN/WAF kundër scraping-ut, dokumentuar në `Docs/DEPLOYMENT.md`, me rollout dhe rollback të verifikuar sipas rregullave të prodhimit.

## Status

Ky dokument regjistron gjendjen e verifikuar dhe propozimin e roadmap-it në
2026-11-07. Asnjë nga hapat e roadmap-it nuk është ende kodifikuar; çdo zbatim
i ardhshëm duhet të ndjekë portën e pranimit të detyrueshme (build, teste,
evidencë) përpara se të konsiderohet i plotë.
