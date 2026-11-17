# Workspace continuation — 2026-11-17

Acceptance status: **not accepted for production**. No deployment performed.
The three failures recorded below were subsequently investigated and resolved
for the exercised cases; see the continuation section. Other acceptance gates
remain open.
Existing staged and unstaged work was retained. `git fetch origin` completed;
`fix/security-vulns-20261112` matched its configured remote branch at inspection.
Only the primary worktree was registered.

## Scoped addition

`documents.file-classified` makes hash-verified copies under the authenticated
workspace's `reports/classified` area. It retains originals, requires file-read
and report-write permissions, checks the reviewed source hash, and refuses to
replace conflicting output. Each document uses a workflow step, allowing the
existing checkpoint mechanism to retain completed steps. This is a filing tool,
not evidence that model classification accuracy or bulk throughput is accepted.

## Actual verification

- `dotnet build wwwmmm.slnx --no-restore`: passed after code/test changes.
  ImageSharp reported a missing Six Labors license; Debug continues but Release
  licensing remains unresolved.
- Targeted `WorkflowTests` and `ExcelWorkflowToolTests`: 25 passed, zero failed
  and zero skipped. Filing tests exercise real local files, original retention,
  identical retry, conflicting destination, changed source, invalid category and
  permission rejection. Excel protocol tests are not live upstream evidence.
- Earlier OCR/workflow selection: 43 passed and three failed. Two OCR cases
  could not launch a required native Poppler tool during page rendering. The
  workspace HTTP integration test failed in `HttpListener.Start` with an invalid
  handle. These remain unresolved; no affected endpoint acceptance is claimed.
- Scoped code/test `git diff --check`: passed.

## Clisonix source inspection

The old owner URL resolves through authenticated GitHub CLI to
`clisonix/clisonix.com`. The connected GitHub app returned 404 at the old paths;
authenticated CLI reads succeeded. Main revision recorded after inspection:
`a5587565675e7e934a29e356d6e2b3f8bd312de5` (reads used main, not a pinned
revision, so this does not prove an atomic source snapshot).

Inspected `matia_service.py`, `matia_core.py`,
`apps/web/app/api/matia/route.ts` and `excel-core/lagter_v1_api.py`.
Matia exposes analyse/stream/screen operations over supplied screen text;
the inspected engine orchestrates metric collection and rule-based theories.
These contracts do not implement document filing or a mass classification API.
Excel Core exposes the existing LAGTER workbook export contract used by the SPP
bridge. Source inspection does not verify the running deployment or model quality.

Remaining at the initial snapshot: native OCR runtime verification, HTTP integration failure resolution,
live Excel service verification, real model classification and reviewed taxonomy,
CRM contract selection, physical scanner/printer verification, Release licensing,
and deployment/recovery acceptance. Keep workflow enablement gated.

## Continuation: three-test investigation and Clisonix agents

The three failures were reproduced before changes:

- `OcrTests.Reader_WithOcrEnabled_RecognizesARealRasterPage`
- `OcrTests.Workflow_PdfTextUsesOptInOcrForScannedPages`
- `WorkflowApiTests.CustomerWorkspaceRequiresCsrfAndKeepsDocumentsJobsAndReportsPrivate`

Both OCR failures arose because rendering called `pdfinfo`/`pdftoppm` by bare
name, while text extraction found the repository-bundled Poppler installation.
Rendering now discovers sibling binaries from that installation when no explicit
renderer path is configured. Explicit renderer paths remain authoritative and
must be absolute existing files. The HTTP test passed unchanged outside the
restricted execution sandbox. No authentication, CSRF or HTTP contract was weakened.

Actual subsequent checks:

- Solution Debug build after the renderer fix: passed with the same ImageSharp
  licensing warning.
- OCR/PDF tests with `SPP_TEST_NATIVE_DOCUMENTS=1`: 17 passed, zero skipped.
  These include actual raster PDF rendering and Tesseract recognition.
- Workflow HTTP integration outside the sandbox: one passed, zero skipped.
- Broader OCR/workflow/PDF/archive/processor selection outside the sandbox:
  71 passed, eight native opt-in cases skipped, zero failed. This run did not
  activate the native flag; it is not evidence for the eight skipped cases.
- Python agent tests: six passed, including real file hashing, worker growth and
  idle reduction, error/timeout propagation, queue bounds and cancellation cleanup.
- Final solution Debug build after adding four explicit renderer-path failure
  regressions: passed with the same licensing warning. Python `compileall` and
  AST parsing of all four Python source files passed; the copied registry's Git
  blob matches the pinned upstream blob.
- Final affected-test selection with `SPP_TEST_NATIVE_DOCUMENTS=1`, outside the
  sandbox: **83 passed, zero failed, zero skipped**. This includes the three
  original failures, four new missing/relative renderer-path regressions, native
  PDF page selection, archive/processor failure cases and owner-scoped HTTP flow.
  It supersedes the earlier subset results for these cases, not the full suite.

`Workflow/Python/clisonix_agents/registry.py` preserves the exact upstream
`agents.py` bytes at the revision recorded above (blob
`b95ad3ac29fa19bd9358e77de97ba7420e85deb3`). The accompanying pool is an SPP
adaptation of upstream bounded-queue and scaling concepts, not a byte copy of
upstream `agents/pool.py`. It schedules real caller-provided asynchronous handlers
with bounded concurrency. It is not a distributed service, model implementation,
or durable queue. It is not registered as a browser tool or enabled in production.
Upstream document agents' assumed accuracy and hardcoded Ocean endpoint were not
imported. See `Workflow/Python/README.md` for scope, provenance and usage.

At that checkpoint, Release licensing, live Excel/model/device verification, integration onto current
shared main, and deployment/recovery acceptance remain open. Fixing these three
tests does not accept all existing local changes for production.

## Correction: local Six Labors license verified

The private `.env.build` exists, contains a nonempty `SIXLABORS_LICENSE`, and is
ignored by Git and not tracked. Earlier builds had not loaded this configuration;
their warning was not evidence that the license was absent.

The existing `Scripts/Load-BuildEnvironment.ps1` maps that entry to
`SixLaborsLicenseKey`. Local PowerShell execution policy prevented dot-sourcing
the script. Reading the same private entry as data and setting those variables
only in the build process allowed the actual license validation to run. No system
execution policy was changed and no credential was added to Git.

`dotnet build wwwmmm.slnx --configuration Release --no-restore` with the private
license loaded **passed**, zero errors and no licensing warnings. Two CS8604
warnings came from the concurrently present, untracked
`Classify/ClassificationTaxonomy.cs` (lines 62 and 73). Those local sources were
not modified or accepted by this check. This verifies local Release license
acceptance; it does not verify the CI secret or server build environment.

Local license availability is no longer an acceptance blocker. The other live
integration, current-main and deployment/recovery gates remain open.

## CRM/classification continuation and single-payload requirement

Current acceptance remains **not accepted for production**. The following is
local verification of the current working tree, which also contains previously
staged/unstaged work; it is not proof of a clean committed revision or deployed
image. See [HYBRID_DOCUMENT_STORAGE.md](HYBRID_DOCUMENT_STORAGE.md) for the actual
layout, upstream findings and limits.

New browser imports store one canonical payload per SHA-256 per private
workspace. Logical filenames and classification folders are metadata references;
CRM links verify the actual bytes and hash. Legacy imports/copies are retained
without an implicit migration. CRM metadata uses a flushed recovery journal,
exclusive file lease, atomic record/audit publication and stale-update rejection.
Unimplemented batching settings fail instead of pretending to change behavior.
Separate CRM consent is checked at plan validation and again for execution.

Workflow execution uses the existing FluidEngine `StigmaMemoryFilmPipeline` to
persist one minimal record per job/step. Document bytes, filenames, prompt and
customer fields are absent from these Film records. Film writes are not a
second payload store or a distributed Tide implementation.

Checks actually executed after these changes:

- `dotnet build wwwmmm.slnx --configuration Release --no-restore`, with the
  private Six Labors license loaded: passed, zero warnings and zero errors.
- CRM/classification and core workflow selection: 35 passed, zero skipped.
  Recovery interruption states are test fixtures over real local files, not
  a physical crash or production-storage benchmark.
- Real HTTP owner/CSRF/CRM-consent integration with
  `SPP_WORKFLOW_BROWSER=1`: one passed. The browser imported a canonical
  document, saved/reviewed a plan and displayed actual read output and hash.
- Full CI-equivalent local selection with native tests enabled:
  `dotnet test Tests/LightningSPP.Tests/LightningSPP.Tests.csproj --configuration Release --no-build --filter FullyQualifiedName!~BridgeUpstreamTests`
  passed **391 tests, zero failed and zero skipped**. The excluded opt-in
  `BridgeUpstreamTests` are not covered by this result.
  TRX: `Tests/LightningSPP.Tests/TestResults/hybrid-workspace-20261117.trx`.
- JavaScript syntax checks passed for the workspace script and browser driver;
  changed-content whitespace checks and the new document's local link check passed.
- Actual pinned upstream NanoGrid Python source reproduced
  `struct.error: pack expected 8 items for packing (got 9)`. The retained
  two-site patch passed application checks against that source, and the corrected
  actual header passed a 14-byte encode/decode round-trip. It is not merged or
  deployed upstream and does not establish full codec/security acceptance.

Remaining gates: review and commit an independently reproducible source set;
integrate on current shared main without losing other work; fix/accept the
upstream NanoGrid codec and verify the real authenticated hybrid contract;
verify live Excel/model classification and physical devices where enabled;
complete CI plus deployment/rollback and exact-revision health verification.
No production readiness or rollout is claimed by local test results.
