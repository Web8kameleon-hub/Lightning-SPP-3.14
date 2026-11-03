# Lightning SPP 3.14 Release Evidence

## Scope

This document is the release companion for code, UI, billing, licensing, benchmark, edge-case and negative-test evidence. It does not replace external certification or a customer contract.

## P0 engineering verification — 2026-11-03

Subsequent semantic evaluation: [local AI reality check](AI_MODEL_EVALUATION_2026-11-03.md).
The tested `llama3.2:3b` configuration is **not accepted for autonomous code or
workflow advice**. Nine curl questions produced eight complete answers that
failed their full rubric and one timeout. The earlier passing real-inference
test below establishes connectivity, not reasoning quality.

Scope: local candidate on base commit `ca577b3`, with the uncommitted SDK,
test-fixture and roadmap changes in this checkout. No commit, push or deployment
was performed. Overall production acceptance remains **pending**.

Confirmed defect and correction: `Lightning314.Parallel` accepted concurrency
zero, creating a semaphore that never released work. The complete baseline run
stalled at `SdkCompressionTests.InvalidBatteryAndConcurrencyAreRejected`; a
bounded diagnostic run identified the active test. The SDK now rejects null
batches and concurrency below one before creating the semaphore. Regression
tests exercise zero/negative concurrency through SDK/Cloud, empty batches,
ordered lossless output and null input. Tests use bounded waits so recurrence
fails rather than stalling the suite.

The real Java Longo fixture also aborted startup after a single one-second
HTTP probe timeout. It now retries connection/probe timeouts within a bounded
15-second startup window, disposes health responses, and terminates its child
process on failed startup. It still requires a real healthy service; no mock,
skipped assertion or production timeout change was introduced.

The FluidEngine real-AI test previously assumed `clisonix-ocean:v2` existed.
It now requires explicit `SPP_FLUID_TEST_MODEL`. Missing configuration or an
undiscovered identity fails; no automatic fallback or model download occurs.
The test uses only local Ollama at `http://localhost:11434` and test prompts.

Executed checks:

- `dotnet build wwwmmm.csproj -c Release --no-restore`: passed, zero warnings/errors.
  The private Six Labors license was loaded without exposing its value.
- Full managed test run, `p0-final.trx`: 323 passed, zero failed, 15 skipped,
  338 total. This is not a 100% execution result. Eight native-document tests
  and seven isolated bridge-upstream tests were not enabled in that run.
- Full suite with `SPP_TEST_NATIVE_DOCUMENTS=1` and existing local Poppler
  26.09.0 binaries on the process PATH, `p0-native-final.trx`: 331 passed,
  zero failed, seven skipped, 338 total. All eight previously skipped native
  document tests executed successfully, including malformed input, cleanup,
  multi-page processing and refusal to return partial PDF output on failure.
  The remaining seven skips are real Excel/NodeDB upstream integration tests.
- `dotnet bin/Release/net10.0/wwwmmm.dll test`: five smoke tests passed after
  the SDK correction. These exercise software output, not physical printing.
- FluidEngine `p0-fluid-configured.trx`: 14 passed, zero failed/skipped with
  actual local inference on discovered `llama3.2:3b`, digest
  `a80c4f17acd55265feec403c7aef86be0c25983ab279d83f3bcd3abbcb5b8b72`.
  This run preceded the SDK correction; it does not establish document quality
  or production model suitability. Restore reported NU1900 because NuGet
  vulnerability data was unavailable; no clean dependency audit is asserted.
- `python Scripts/validate-release-companions.py`: passed.
- Roadmap UTF-8/local-link validation and `git diff --check`: passed.

Reports are local generated TRX files in the corresponding test project's
`TestResults` directory, not committed production evidence. Initial sandbox
listener failures, the aborted baseline and an assembly-lock retry are diagnostic
failures, not successful test runs. A stale testhost from the canceled run was
stopped before rebuilding; no production process was stopped.

Remaining boundaries: live Excel/NodeDB acceptance, Docker validation (daemon
unavailable at inspection), customer email delivery, secure offline activation,
Linux/device acceptance and deployment health/recovery remain unverified by
this work. Existing findings in the account/bridge acceptance documents remain
open. The passing native tests are Windows evidence, not Linux or hardware
acceptance.

To reproduce the real FluidEngine AI check, first inspect the local Ollama
catalog, then explicitly set `SPP_FLUID_TEST_MODEL` to the installed exact name
selected for the test. With the private build environment loaded, run
`dotnet test wwwmmm.FluidEngine/tests/wwwmmm.FluidEngine.Tests/wwwmmm.FluidEngine.Tests.csproj -c Release`.
Do not infer model availability from this historical run or silently substitute
a different model. The existing CI workflows currently exercise the main test
project; they do not establish acceptance of this separate real-AI suite.

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
## Licensing hardening and compliance dossier — 3 November 2026

Scope: this checkout, uncommitted. No commit, push or deployment was performed.

### License verification is now asymmetric

Confirmed defect: `LicenseKey.Validate` verified HMAC signatures with the issuer
secret, so any offline activation required shipping that secret to the device,
and `LicenseManager.GetActiveLicense` returned the stored license without
re-verifying the signature at all. The stored file is AES-GCM encrypted with a
key derived from machine and user names, which are not secrets — a replaced file
was accepted.

Correction:

- New `Licensing/LicenseSignature.cs` — ECDSA P-256 / SHA-256, signature encoded
  as 128 uppercase hex characters so the key stays dash-separable.
- `LicenseKey.Generate` emits `LSP3-{KID}-{TIER}-{EXP}-{SEG}-{SIG}` when
  `LSPP_LICENSE_SIGNING_PRIVATE_KEYS` holds the active KID; otherwise it falls
  back to the existing HMAC `LSP2` format.
- `LicenseKey.Validate` verifies `LSP3` with `LSPP_LICENSE_PUBLIC_KEYS` only. No
  issuer secret is present on the verifying side.
- `LicenseManager.GetActiveLicense` re-verifies the signature on every load when
  a public key is configured, and replaces the stored `Tier`, `ExpirationDate`,
  with verified values; device/operation limits are derived from the verified
  tier, not from unsigned cached metadata. The current signature payload does
  not independently sign custom device/operation quotas.

Executed checks:

- `dotnet build -c Release`: zero warnings, zero errors.
- Full managed suite: **328 passed, 0 failed, 15 skipped, 343 total**, exit code
  0. Includes the five new tests in
  `Tests/LightningSPP.Tests/LicenseAsymmetricTests.cs`: public-key-only
  validation, tampered-tier rejection, missing-key rejection, wrong-key
  rejection, and expiry/tier preservation.

Boundaries: the legacy path (no public key configured) still returns the stored
license without local verification and must not be described as verified offline
activation. Device fingerprinting and server-side activation remain
unimplemented. `LSP2` HMAC keys are unchanged for issuers that have not migrated.

### Compliance dossier

New `Docs/COMPLIANCE/` with six documents: status and roadmap (`README.md`), gap
analysis against IEC 62304, ISO 14971, IEC 62366-1, ISO 13485, MDR, FDA 510(k),
HIPAA, GDPR and ISO 27001 (`GAP_ANALYSIS.md`), risk register (`RISK_REGISTER.md`),
third-party inventory (`SOUP_INVENTORY.md`), controls with their evidence
(`SECURITY_CONTROLS.md`), and a requirement-to-test matrix (`TRACEABILITY.md`).

Corrected claims: `SECURITY.md` and `COMPLIANCE.md` previously listed ISO 9001,
ISO 27001, ISO 13485, SOC 2, MDR/CE, FDA 510(k) and HIPAA as certified or in
progress, alongside TLS 1.3 termination, MFA, RBAC, FDA 21 CFR Part 11, annual
penetration testing and virus scanning. None of those are implemented or
evidenced. Both documents now state the actual position and link the dossier.

`Docs/MANUAL/10-KUFIJTE-DHE-PRETENDIMET.md` was updated to match: offline
activation moved from blocked to possible with a public key, and the previously
open certification contradiction is recorded as closed.

No certification, notified-body decision or regulator clearance exists. The
dossier is readiness material, not compliance evidence.

## Final main candidate acceptance ? 2026-11-03

This record supersedes earlier local candidate counts for the changed runtime.
GitHub Actions and the production environment deployment record for the resulting
commit are the authority for CI/promotion status; local validation does not prove
live deployment. Existing business-domain, model-quality, device and upstream
acceptance boundaries remain unchanged.

Corrections and consolidation:

- Repository policy: corrected unsupported privacy/licensing claims, reworded
  two question headings that triggered the strict claim gate, and removed
  trailing whitespace. The production policy is now enforced by CI/CD.
- Corrected the provenance tamper test to actually change its target value and
  assert that a mutation occurred before verifying rejection.
- Added SDK input/concurrency validation; asymmetric signing now respects KID
  expiry/rollover, and verified cached license limits derive from the tier.
- Reference CLI flow: native PDF text, declared extraction rules, page/offset
  references, review records, controlled batch pause/resume and reports.
  Missing workspaces cannot report successful verification; reports first check
  integrity. Batch ownership prevents concurrent writers and recovered counters
  account for manifest entries persisted before the checkpoint.
- Provenance append ownership, validation before append and explicit review-to-
  extraction linkage. Hashes use unambiguous JSON encoding. This hash chain is
  not an externally anchored signature: tail truncation/recomputed histories
  are not independently authenticated, and review identities in local CLI are
  operator assertions, not authenticated web identities.
- Document packages reject colliding names, preserve existing destinations,
  publish through a temporary file, reject duplicate archive entries and stream
  hash verification. Manifest reads are bounded to 16 MiB.
- Native text extraction terminates its child process on caller cancellation.
- Desktop SDK warning corrected and Windows targeting enabled for Linux builds.
- Deployment wrapper requires clean, exact shared main, fresh complete CI-scope
  TRX evidence and the existing health/recovery-aware deploy script; there is
  no test-skip flag. Deployment selection policy was not relaxed.

Executed on the final runtime source snapshot:

| Check | Actual outcome |
| --- | --- |
| `dotnet build -c Release --no-restore` | Passed, zero warnings/errors |
| Windows native CI scope | 358 passed, zero failed/skipped; strict TRX gate passed |
| Docker Linux test stage | 358 passed, zero failed, seven isolated-upstream skips, 365 total |
| Linux source parity | All 30 changed source/project files match the final working tree |
| FluidEngine real local inference suite | 14 passed, zero failed/skipped; connectivity only, not model quality acceptance |
| NuGet metadata refresh | Forced online restore completed without the earlier NU1900 warning; audit was not disabled |
| Deployment selection policy tests | Nine passed |
| Repository policy / release companions | Passed |
| Changed workflow actionlint / shell syntax | Passed |
| Local documentation links | 145 checked before this appendix; no missing target |
| Candidate credential-pattern scan | No matches for the checked private credential patterns; not an exhaustive security audit |

Native documents were enabled with `SPP_TEST_NATIVE_DOCUMENTS=1`. The Windows
scope excludes `BridgeUpstreamTests`, exactly as existing production CI does;
no upstream acceptance is inferred. The Linux full report's seven skips are
all those same isolated Excel/NodeDB cases. Generated TRX and source-hash records
are local under `artifacts/final-acceptance`, `artifacts/linux-final`,
`artifacts/final-fluid` and `artifacts/final-source-hashes.json`.

Final CLI verification exercised two actual text-layer PDF test fixtures through
run with a document limit, resume, review, integrity verification, report,
package and package verification. Repackaging onto an existing destination
failed with exit 1 and preserved its hash. `engines`, `real` and the five-test
smoke command also succeeded. These are fixture-based software checks, not
customer corpus, OCR, physical-device or 30,000-document acceptance.

The candidate runtime image identity is
`sha256:410c532d5ba470122e9f8c9943b83ed90033177afa6bb7d6082ea458a6bbddc7`.
It is local evidence, not the digest of a server-side rebuilt deployment.
The pre-deploy public health endpoint returned `ok=true`.

Initial failures remain diagnostic history: the original tamper test failed;
a Linux attempt failed on NuGet EOF/DNS; a later Windows run failed on local
Longo startup after an environment interruption. These are not reported as
passes. The successful final runs above supersede them for the exercised scope.
