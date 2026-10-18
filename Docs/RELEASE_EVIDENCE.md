# Lightning SPP 3.14 Release Evidence

## Scope

This document is the release companion for code, UI, billing, licensing, benchmark, edge-case and negative-test evidence. It does not replace external certification or a customer contract.

## Product facts

- Real processing uses SkiaSharp, ML.NET/ONNX integration, Brotli and physical file output.
- The base OpenAPI reference documents 25 HTTP operations: four runtime/schema operations, four workspace-license operations, fourteen customer-account/license/billing operations, two Stripe fulfillment operations and one image/PDF processing operation. Additional restricted workflow operations are described in [WORKFLOW_RUNTIME.md](WORKFLOW_RUNTIME.md).
- Stripe webhooks use signed payload verification with legacy, snapshot and thin destination secrets.
- Event IDs are deduplicated to prevent immediate replay processing.
- License tiers are Free, Pay as you go, Unlimited and Enterprise.
- The public `/pricing` page displays Free development/testing, €19.99/month Pay-as-you-go, €49.99/month Unlimited and custom Enterprise. Actual Stripe checkout terms must be verified before release acceptance.

## Evidence index

| Evidence | Location |
|---|---|
| Performance gates | `Tests/LightningSPP.Tests/PerformanceGateTests.cs` |
| Stripe signature and replay tests | `Tests/LightningSPP.Tests/StripeWebhookTests.cs` |
| Stripe boundary tests | `Tests/LightningSPP.Tests/StripeWebhookEdgeCaseTests.cs` |
| REST smoke tests | `Tests/LightningSPP.Tests/ApiSmokeTests.cs` |
| Signing authorization | `Tests/LightningSPP.Tests/ApiSigningStatusIntegrationTests.cs` |
| Golden deterministic outputs | `Tests/LightningSPP.Tests/GoldenPipelineTests.cs` |
| License rotation | `Tests/LightningSPP.Tests/LicenseKeyRotationTests.cs` |
| Stigma profiles | `Tests/LightningSPP.Tests/StigmaExecutionProfileTests.cs` |
| Benchmark method | `Docs/BENCHMARK_PROTOCOL.md` |
| Edge/negative matrix | `Docs/EDGE_CASES_AND_NEGATIVE_TESTS.md` |
| API schema | `Web/wwwroot/openapi.json` |

## Release decision

A release candidate is eligible only when:

- Release build and xUnit suite pass.
- Docker build and container smoke commands pass.
- Benchmark gates pass and publish their actual observed values.
- Pricing matches the configured Stripe products.
- Test-mode purchase, signed webhook, replay protection, key issuance and activation complete end to end.
- No unsupported competitor, medical, compliance or certification claim is introduced.
- Release notes link the commit and evidence documents.

## Known boundary

The legacy signature/replay cache is process-local. Paid fulfillment additionally stores customer entitlements and payment receipts in Longo using revision-based writes. Repeated recovery of the same paid invoice retained the existing key and one entitlement/receipt in the exercised production case; this is not a guarantee of exactly-once behavior for every event or failure scenario.

## API and distribution audit — 11 October 2026

Counts distinguish SDK interfaces, running processes and HTTP method/path operations:

| Interface or service | Actual scope | Evidence |
|---|---|---|
| Three core SDK service interfaces | Scan, Process and Output; the factory selects Skia/ImageSharp, RealAIProcessor and RealPrintService | `Configuration/LightningConfiguration.cs`, `Services/IScanService.cs`, `Services/IProcessingService.cs`, `Services/IPrintService.cs` |
| Two main production processes | Lightning SPP application and private Longo storage; shared Nginx provides the public edge | SSH container inspection: both application and Longo healthy |
| Public application API | 25 implemented operations; previously only eight documented | `Web/LicenseApiServer.cs`, `Web/CustomerAccounts.cs`, `Web/wwwroot/openapi.json` |
| Private Longo API | Nine operations: health; state GET/PUT; document create/list/get/replace/delete/query | `Persistence/Longo/src/main/java/com/clisonix/longo/LongoServer.java` |
| PDF processing | Implemented through native Poppler and the image-processing pipeline | `Documents/PdfDocumentService.cs`, `Web/TryDocumentProcessor.cs`; Linux native tests previously exercised |
| Linux physical-device adapters | CUPS queue submission/status and SANE discovery/scanning; hardware and configured drivers required | `Documents/LinuxDeviceService.cs`; no physical-device acceptance asserted by this audit |
| ONNX inference | Requires an existing model, configured input shape and float32 input; not automatically enabled by the presence of ML dependencies | `Services/Implementation/RealAIProcessor.cs` |

Distribution checks on this date:

- NuGet's flat-container index returned HTTP 404 for both `lightningspp` and `lightningspp.universal`. No published package was found under either name; project metadata identifies `LightningSPP` version `3.14.1`.
- GitHub lists release `v3.14.0` with an empty assets array. Source archives are not binary installers.
- Docker build/deployment definitions and an Ubuntu provisioning script exist. These are source-based deployment paths, not downloadable desktop installers.
- A Windows Forms project targets `net10.0-windows`; no MSI/MSIX packaging definition or published installer was found. No DEB/RPM package definition or published package was found.
- `NUGET_PUBLISH.md` still describes the older `LightningSPP.Universal` identity and includes claims inconsistent with current metadata. It is not evidence of publication.

The OpenAPI and Swagger page were corrected locally to cover all 25 implemented operations, production/local server URLs, account cookies/CSRF, administrator authorization and Stripe signatures. JSON/OpenAPI 3.0.3 validation and route coverage passed. `dotnet build` passed with zero warnings/errors; 16 relevant API/account tests passed with zero failures/skips. These documentation changes have not been deployed: the public specification still exposes eight operations at the audit time.

Wide distribution remains incomplete: package creation, clean-machine installation, runtime/native dependency verification and publication must be completed before advertising downloadable installers. Live verification-email and license-email delivery also remains blocked by the configured Resend sender restriction; provider rejection must not be described as delivery success.
