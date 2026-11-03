# Gjurmueshmëria — kërkesë, kontroll, provë

Matrica që lidh kërkesën me kontrollet dhe provat. Sipas IEC 62304, kjo është
lidhja që një auditor e ndjek. Pa të, "e testuam" është pohim.

## Si lexohet

- **ID**: identifikues i qëndrueshëm (`REQ-*`).
- **Prova**: skedar testi ose komandë që e vërteton në ekzekutim.
- **Statusi**: `PROVUAR`, `PJESSHËM`, `HAPUR`.

## Kërkesat

| ID | Kërkesa | Kontrolli | Prova | Statusi |
| --- | --- | --- | --- | --- |
| REQ-01 | Një input i pavlefshëm imazhi duhet të dështojë, jo të prodhojë rezultat | `RealImageProcessor.LoadBitmapOrCreateFromBinary` | `Tests/.../GoldenPipelineTests`, teste njësie | PJESSHËM |
| REQ-02 | Të dy motorët (Skia, ImageSharp) duhet të ekzekutohen eksplicit, pa fallback | `ImageEngineAdapter` | `wwwmmm engines` (i ekzekutuar) | PROVUAR |
| REQ-03 | Kodimi duhet të prodhojë PNG/JPEG/BMP/TIFF/PDF të vlefshëm | `OutputAdapter` | `OutputAdapterTests`, `PdfDocumentTests` | PROVUAR |
| REQ-04 | Konfigurim i pavlefshëm duhet të dështojë në nisje | `StigmaProfiles.FromEnvironment`, `ServiceFactory.Configure` | Teste konfigurimi | PROVUAR |
| REQ-05 | Production duhet të kërkojë sekrete dhe `LSPP_DATA_DIR` absolut | `RuntimeSecurity.ValidateProduction` | `ProductionSecurityTests` | PROVUAR |
| REQ-06 | Admin endpoint-et duhet të refuzojnë kërkesat pa token | `LicenseApiServer` | `ApiLicenseActivationIntegrationTests` | PROVUAR |
| REQ-07 | Licenca duhet të verifikohet pa sekret në pajisje | `LicenseSignature` (ECDSA) | `LicenseAsymmetricTests` | PROVUAR |
| REQ-08 | Licencë e modifikuar duhet të refuzohet | Verifikim ECDSA i payload-it | `AsymmetricKey_TamperedTier_IsRejected` | PROVUAR |
| REQ-09 | Gjendja e ruajtur duhet të ri-verifikohet në ngarkim | `LicenseManager.GetActiveLicense` | `LicenseAsymmetricTests` | PROVUAR |
| REQ-10 | Webhook i përsëritur nuk duhet të krijojë dy entitlement-e | Dedup eventesh | `StripeWebhookTests`, `PaidFulfillmentTests` | PROVUAR |
| REQ-11 | CORS duhet të refuzojë origjina të palejuara | `LSPP_ALLOWED_ORIGINS` | `ApiCorsIntegrationTests` | PROVUAR |
| REQ-12 | Proxy headers pranohen vetëm nga IP të besuara | `LSPP_TRUSTED_PROXY_IPS` | `TrustedProxyTests` | PROVUAR |
| REQ-13 | Bridge nuk duhet të ndjekë redirect-e | `BridgeConnection` | Testet e kontratës | PROVUAR |
| REQ-14 | Bridge duhet të respektojë deadline dhe kufij | `BridgeConnection`, lexues i kufizuar | Testet e kufijve | PROVUAR |
| REQ-15 | Workflow i fikur në production duhet të kthejë 503 | `WorkflowApi` | Testet e workflow | PROVUAR |
| REQ-16 | Anulimi i workflow nuk duhet raportuar si sukses | `WorkflowRunner` | Testet e rikuperimit | PROVUAR |
| REQ-17 | Input PDF duhet renderuar para skanimit | `RealImageProcessor` | `PdfDocumentTests` | PROVUAR |
| REQ-18 | Asnjë mod i simuluar nuk duhet të ekzistojë | Hequr nga kodi | Build + suite (328 teste) | PROVUAR |
| REQ-19 | Metrikat nuk duhet të pretendohen si histori | Dokumentim + kod | `SLA_SLO.md`, MANUAL/07 | PROVUAR |
| REQ-20 | Printim fizik duhet të raportohet si i paverifikuar | `print-status` | Kontroll kodi | PROVUAR |

## Kërkesat që mbeten HAPUR

| ID | Kërkesa | Pse mungon |
| --- | --- | --- |
| REQ-21 | Vlerësim klinik i saktësisë | Kërkon protokoll dhe specialistë |
| REQ-22 | Skanim dhe printim fizik i verifikuar | Kërkon harduer |
| REQ-23 | Dorëzim email në marrës real të jashtëm | Kërkon provider të verifikuar |
| REQ-24 | Inferencë ONNX me model real klienti | Kërkon model dhe corpus |
| REQ-25 | Historik metrikash përtej rinisjeve | Kërkon backend metrikash |
| REQ-26 | Usability sipas IEC 62366-1 | Kërkon përdorues realë |
| REQ-27 | Restore i provuar i backup-it | Kërkon ushtrim të regjistruar |
| REQ-28 | Monitorim i dobësive (CVE) | Kërkon proces periodik |

## Si mbahet kjo matricë

1. Çdo kërkesë e re merr ID dhe provë përpara se të pranohet.
2. Kur një test ndryshon emër, matrica përditësohet në të njëjtin commit.
3. Një kërkesë `HAPUR` nuk fshihet nga matrica kur mbyllët; kalon në `PROVUAR`
   me lidhjen e provës.
4. Një kërkesë pa provë nuk pretendon status tjetër.

## Përditësimi i fundit

- Data: 2026-11-03
- Baza: build Release 0/0; suite 328 passed / 0 failed / 15 skipped
- Ndryshimi kryesor: REQ-07, REQ-08, REQ-09 u mbyllën me `LicenseAsymmetricTests`;
  REQ-18 u mbyll me heqjen e rrugës së simuluar.
