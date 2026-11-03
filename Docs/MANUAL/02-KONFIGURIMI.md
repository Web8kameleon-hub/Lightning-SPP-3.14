# 02 — Konfigurimi

Referencë e plotë e variablave të mjedisit që kodi **lexon vërtet**, e ndarë sipas
fushës. Nëse një variabël nuk figuron këtu, ai nuk është konfigurim i produktit —
mos e shpik.

Tre grupe: **B** = build (nuk shkon në runtime), **R** = runtime, **T** = vetëm
teste.

## 2.1 Mënyra e ekzekutimit (production vs. lokal)

| Variabël | Vlera | Efekti real |
| --- | --- | --- |
| `LSPP_ENVIRONMENT` | `Production` | Ndizet `RuntimeSecurity.IsProduction` |
| `DOTNET_ENVIRONMENT` | `Production` | E njëjta gjë (alternativë) |
| `ASPNETCORE_ENVIRONMENT` | `Production` | E njëjta gjë (alternativë) |

Kur cilado prej tyre është `Production`, në nisje ekzekutohet
`RuntimeSecurity.ValidateProduction()`, që **dështon menjëherë** nëse mungon
një prej kërkesave të mëposhtme. Kjo është sjellje e mirë: konfigurimi i
gabuar nuk niset në heshtje.

Kërkesat e detyrueshme në production:

1. `LSPP_ADMIN_TOKEN` ekziston, ≥ 32 bajt, nuk përmban `CHANGE`.
2. `LSPP_LICENSE_SIGNING_KEY` **ose** `LSPP_LICENSE_SIGNING_KEYS` ekziston.
3. `LSPP_DATA_DIR` është **rrugë absolute** dhe e shkrueshme (kodi shkruan një
   skedar provë dhe e fshin).

## 2.2 Rrjeti dhe shërbimi

| Variabël | Default | Shënim |
| --- | --- | --- |
| `PORT` | `13080` | Përdoret vetëm nëse `serve` nuk merr port si argument |
| `LSPP_BIND_HOST` | `+` në container, `localhost` jashtë | `+` dëgjon në të gjitha ndërfaqet |
| `LSPP_DATA_DIR` | `%APPDATA%\LightningSPP` / `~/.config/LightningSPP` | Në production duhet absolut |
| `LSPP_PUBLIC_ORIGIN` | `https://scanprocessprint.com` | Origjina publike në email/lidhje |
| `LSPP_ALLOWED_ORIGINS` | bosh | Listë me presje; bosh = asnjë origin i lejuar për CORS |
| `LSPP_TRUST_PROXY_HEADERS` | `false` | `true` vetëm kur ka proxy përpara |
| `LSPP_TRUSTED_PROXY_IPS` | bosh | IP-të e proxy-t, me presje |

**Rrezik i njohur:** nëse `LSPP_TRUST_PROXY_HEADERS=true` pa
`LSPP_TRUSTED_PROXY_IPS`, të gjitha kërkesat mund të grupohen nën IP-në e
proxy-t dhe limiter-i bëhet një kufi global, jo për klient. Vendos gjithmonë
të dyja bashkë.

## 2.3 Licencimi

| Variabël | Roli |
| --- | --- |
| `LSPP_ADMIN_TOKEN` | Bearer për endpoint-et administrative; krahasim në kohë konstante |
| `LSPP_LICENSE_SIGNING_KEY` | Çelës i vetëm HMAC i emetuesit (legacy LSP2) |
| `LSPP_LICENSE_SIGNING_KEYS` | Set i paketuar çelësash HMAC për rotacion |
| `LSPP_LICENSE_SIGNING_PRIVATE_KEYS` | `kid=<base64 PKCS8>` — çelës privat ECDSA i emetuesit. **Vetëm në server.** |
| `LSPP_LICENSE_PUBLIC_KEYS` | `kid=<base64 SPKI>` — çelës publik për verifikim. **Ky vendoset në pajisje.** |
| `LSPP_LICENSE_SIGNING_ACTIVE_KID` | ID e çelësit aktiv |
| `LSPP_LICENSE_SIGNING_KEY_EXPIRATIONS` | Skadenca për çelësat |
| `LSPP_LICENSE_SIGNING_KEY_ROLLOUTS` | Fazat e roll-out-it |
| `LSPP_LICENSE_SIGNING_GRACE_DAYS` | Dritarja e tolerancës |
| `LSPP_LICENSE_SIGNING_DEPRECATION_DAYS` | Paralajmërim deprecimi |
| `LSPP_LICENSE_SIGNING_SECONDARY_KID` | Çelës dytësor gjatë tranzicionit |
| `LSPP_LICENSE_SIGNING_SECONDARY_KID_UNTIL_UTC` | Fundi i dritares dytësore |
| `LSPP_SIGNING_AUDIT_PATH` | Rruga e auditimit të nënshkrimit |

**Dy formate, dy role:**

| Format | Nënshkrimi | Verifikohet me | Ku përdoret |
| --- | --- | --- | --- |
| `LSP3-…` | ECDSA P-256 (SHA-256) | **Vetëm çelës publik** | Pajisje klienti — pa sekret |
| `LSP2-…` | HMAC-SHA256 | Sekreti i emetuesit | Serveri / issuer legacy |

Kur `LSPP_LICENSE_SIGNING_PRIVATE_KEYS` përmban KID-në aktive, `LicenseKey.Generate`
prodhon automatikisht `LSP3`. Nëse jo, kalon në HMAC `LSP2`.

**Rregullat:**

- Në një pajisje klienti vendoset **vetëm** `LSPP_LICENSE_PUBLIC_KEYS`. Sekreti i
  emetuesit nuk shkon kurrë atje.
- Kur `LSPP_LICENSE_PUBLIC_KEYS` është konfiguruar, `LicenseManager.GetActiveLicense`
  **ri-verifikon nënshkrimin** në çdo ngarkim dhe hedh poshtë `Tier`/`Expiry`/`MaxDevices`
  të ruajtur, duke marrë vlerat e nënshkruara.
- Pa çelës publik dhe pa format `LSP3`, një licencë e ruajtur nuk mund të
  verifikohet lokalisht. Kjo rrugë mbetet për përputhshmëri dhe **nuk** duhet
  përshkruar si aktivizim i verifikuar offline.

**Çka mbetet e paimplementuar:** "device fingerprinting" dhe verifikim
server-side i pretenduar në `LICENSE_SYSTEM.md`. Mos i cito si funksione.

## 2.4 Profilet e ekzekutimit (Stigma)

`WWWMMM_STIGMA_LEVEL` kontrollon **të gjithë** pipeline-in. Vlera të pavlefshme
refuzohen në nisje (nuk ka rënie të heshtur në default).

| Vlerë | Profil | Efekt i vërtetë në konfigurim |
| --- | --- | --- |
| `1` | quality | NanoDecibel, ≥600 DPI, Hybrid, Stigma print |
| `2` | balanced | TideWave, 200–400 DPI, Auto, High print |
| `3` | lightning | Lightning, ≤200 DPI, CLX, Normal print |

Profil `quality` **çaktivizon** `UseAI`/`UseClisonix`/`UseStigmaMemory` në
konfigurimin e pipeline-it. Mos e përshkruaj si "maksimum AI"; është maksimum
cilësie me motorë vendorë.

`WWWMMM_IMAGE_ENGINE`:

| Vlerë | Motor | Rezultat |
| --- | --- | --- |
| `Skia` (default) | `RealImageProcessor` | Resize + grayscale/contrast ose edge-enhance |
| `ImageSharp` | `FastScanService` | Rruga ImageSharp |

Nuk ka fallback automatik ndërmjet motorëve. Një vlerë e panjohur hedh
`ArgumentOutOfRangeException`.

## 2.5 Workflow dhe modelet

| Variabël | Default | Efekti |
| --- | --- | --- |
| `LSPP_WORKFLOW_ENABLED` | `false` | Në production, pa `true` endpoint-et kthejnë 503 |
| `LSPP_WORKFLOW_ALLOWED_EMAILS` | bosh | Email të verifikuara; `*` lejon të gjitha; bosh = askush |
| `LSPP_WORKFLOW_ALLOW_REMOTE_MODELS` | `false` | Lejon rrugë jo-lokale |
| `LSPP_WORKFLOW_ALLOW_PRINT` | `false` | Lejon printim nga workflow |
| `LSPP_WORKFLOW_WORKERS` | `2` (1–32) | Numri i punëtorëve |
| `LSPP_MODEL_ROUTES_FILE` | bosh | Rrugë absolute JSON e operatorit |
| `LSPP_TOOL_ADAPTERS_FILE` | bosh | Adapterë read-only JSON |
| `LSPP_MODEL_QUALITY_FILE` | bosh | Politikë cilësie e bazuar në evidencë |
| `LSPP_REPORT_FONT_FILE` | bosh | Font monospace për raportet PDF |

Rregull absolut: konfigurimi i rrugëve dhe kredencialet **nuk** vendosen brenda
një workspace-i klienti. Token-at lexohen nga mjedisi i serverit dhe nuk
kthehen kurrë në shfletues.

## 2.6 AI / ML

| Variabël | Efekti |
| --- | --- |
| `LSPP_ONNX_MODEL_PATH` | Rruga e modelit ONNX real; pa të, inferenca reale nuk ndodh |
| `LSPP_ONNX_INPUT_SHAPE` | Forma e input-it, p.sh. `1x1x224x224` |

Pa `LSPP_ONNX_MODEL_PATH`, `RealAIProcessor` nuk bën inferencë me model real.
Çdo përshkrim tjetër është pretendim i pabazuar.

## 2.7 Pagesë dhe email

| Variabël | Roli |
| --- | --- |
| `STRIPE_SECRET_KEY` | Në production duhet `sk_live_` ose `rk_live_` |
| `STRIPE_PUBLISHABLE_KEY` | Çelësi publik për Checkout |
| `STRIPE_WEBHOOK_SECRET` | Sekreti legacy i webhook-ut |
| `STRIPE_WEBHOOK_SECRET_SNAPSHOT` | Sekreti i snapshot-it |
| `STRIPE_WEBHOOK_SECRET_THIN` | Sekreti i event-it thin |
| `LSPP_STRIPE_PLANS` | Hartëzimi plan → çmim; pa të, fulfillment hedh gabim |
| `SMTP_HOST` / `SMTP_PORT` / `SMTP_USER` / `SMTP_PASSWORD` / `SMTP_FROM` / `SMTP_USE_SSL` | Transporti email (default port 587, SSL aktiv) |
| `RESEND_API_KEY` | Përdoret kur `SMTP_HOST=smtp.resend.com` |
| `LONGO_URL` | URL e shërbimit të persistencës; pa të, llogaritë nuk punojnë |

Pa një dërgues të verifikuar, regjistrimi raporton gabim dërgimi. **Pranimi i
dërgimit nuk vërteton dorëzimin në inbox** — kjo kërkon provë me marrës real.

## 2.8 Bridges

| Variabël | Roli |
| --- | --- |
| `SPP_EXCEL_CORE_URL` | URL e Excel Core |
| `SPP_EXCEL_CORE_API_KEY` | Bearer opsional |
| `SPP_EXCEL_SERVICE_URL` | URL e microservice Excel |
| `SPP_EXCEL_SERVICE_API_KEY` | Bearer opsional |
| `SPP_OSCLX_URL` | URL e OS-CLX |
| `SPP_OSCLX_API_KEY` | `X-API-Key` |
| `SPP_OSCLX_HMAC_SECRET` | Sekret nënshkrimi për shkrime |

HTTPS kërkohet, përveç loopback HTTP në teste të izoluara. URL-të nuk mund të
përmbajnë kredenciale, query ose fragment. Detaje: `09-BRIDGES-EKOSISTEMI.md`.

## 2.9 Mjete native

| Variabël | Default | Roli |
| --- | --- | --- |
| `SPP_PDFINFO_PATH` | `pdfinfo` në PATH | Rruga absolute e pdfinfo |
| `SPP_PDFTOPPM_PATH` | `pdftoppm` në PATH | Rruga absolute e pdftoppm |
| `SPP_PDFTOTEXT_PATH` | `pdftotext` në PATH | Rruga absolute e pdftotext |

Vendos rrugë absolute kur mjetet nuk janë në PATH të shërbimit (shpesh ndodh
nën systemd, ku PATH është minimal).

## 2.10 Variabla vetëm testimi

| Variabël | Vlera | Kuptimi |
| --- | --- | --- |
| `SPP_TEST_NATIVE_DOCUMENTS` | `1` | Ndiz testet reale Poppler |
| `SPP_BRIDGE_TEST_ALLOW_WRITES` | `isolated-loopback` | Lejon testet reale upstream vetëm në loopback |
| `SPP_FLUID_TEST_MODEL` | emri i modelit | Model real për testin AI (p.sh. `llama3.2:3b`) |
| `SPP_WORKFLOW_BROWSER` | `1` | Fixture headless e workflow |
| `SPP_ACCOUNT_BROWSER` | `1` | Fixture headless e llogarisë |
| `LIGHTNING_GOLDEN_UPDATE` | `1` | Rishkruan golden outputs |

Këto **nuk** duhet të aktivizohen në production. Ato nuk janë konfigurim
funksionaliteti, janë kontroll testesh.

## 2.11 Sekretet e build-it

| Variabël | Ku | Rregull |
| --- | --- | --- |
| `SIXLABORS_LICENSE` | build | Vetëm në mjedisin e build-it / BuildKit secret |
| `SixLaborsLicenseKey` | MSBuild | Lexohet vetëm gjatë kompilimit |

Ndalime:

- mos e vendos në `ENV`/`ARG` të Dockerfile;
- mos e vendos në `env_file` të runtime-it;
- mos e shkruaj në README, në docs, në log, në commit.

Nëse një vlerë e tillë ka hyrë ndonjëherë në git, rrotulloje; fshirja e tekstual
nuk e heq nga historia.

## 2.12 Skedari shembull

`.env.example` dhe `.env.production.example` janë shabllone. Vlerat reale
vendosen nga menaxheri i sekreteve të operatorit, me leje `0600` dhe pronar
të drejtë. `.env` **nuk** versionohet.

## 2.13 Kontrolli para nisjes (preflight)

Ekzekuto këtë listë para çdo nisjeje në production:

- [ ] `LSPP_ENVIRONMENT=Production`
- [ ] `LSPP_ADMIN_TOKEN` ≥ 32 bajt, i rastësishëm, i ndryshëm nga çdo sekret tjetër
- [ ] `LSPP_LICENSE_SIGNING_KEY` ose `_KEYS` i pranishëm dhe i ruajtur jashtë repo-s
- [ ] `LSPP_DATA_DIR` absolut, ekzistues, me leje vetëm për përdoruesin e shërbimit
- [ ] `LSPP_TRUST_PROXY_HEADERS` dhe `LSPP_TRUSTED_PROXY_IPS` vendosur **bashkë**
- [ ] `LSPP_ALLOWED_ORIGINS` përmban vetëm domenet reale
- [ ] `WWWMMM_STIGMA_LEVEL` një nga `1|2|3`
- [ ] Nëse workflow është aktiv: `LSPP_WORKFLOW_ALLOWED_EMAILS` jo bosh
- [ ] Nëse Stripe është aktiv: të tre sekretet e webhook-ut të vendosura
- [ ] `LONGO_URL` i arritshëm (nëse llogaritë janë aktive)
- [ ] Asnjë sekret në `docker history`, në repo, ose në log

Kush e kalon këtë listë pa provë, nuk ka konfigurim production — ka shpresë.
</content>
