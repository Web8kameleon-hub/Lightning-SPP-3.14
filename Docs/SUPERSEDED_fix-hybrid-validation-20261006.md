# Status: superseded by `main`

Branch: `fix/hybrid-validation-20261006`
Checked against `main` on: 2026-11-08
Verified by: repository saved-work consolidation audit

## Finding

Every production-relevant change on this branch is already present on `main`,
at or beyond the level implemented here. A real three-way merge
(`main` + this branch, base `696ae81`) produces **zero file differences**
from `main`: every conflicting hunk resolved to content `main` already has.

Evidence, checked commit-by-commit against current `main`:

- `Web/LicenseApiServer.cs` — this branch forces
  `UseAI:false, UseClisonix:false, UseStigmaMemory:false` on `/api/try/process`
  and adds a failure check. `main` carries the identical options plus stronger
  error handling (`throw new DocumentException(...)`) in
  `Web/TryDocumentProcessor.cs`, after the endpoint was refactored out of
  `LicenseApiServer.cs` into a dedicated processor class.
- `.github/workflows/ci-cd.yml` — this branch adds
  `SPP_TEST_NATIVE_DOCUMENTS=1` and installs `poppler-utils` for the unit-test
  job. `main` already has both, plus `fonts-dejavu-core`,
  `SPP_BRIDGE_TEST_ALLOW_WRITES`, a test filter, and an additional
  "require 100 percent test execution and pass rate" gate.
- `Tests/LightningSPP.Tests/ApiTryProcessIntegrationTests.cs` — this branch's
  added assertions (`compressedBytes > 0`, `outputArtifactBase64`,
  `afterImageBase64`, etc.) are a strict subset of what `main`'s current test
  already asserts, which also covers the ZIP/PDF import paths this branch
  does not touch.
- `Examples/RealVsSimulatedExamples.cs` — this branch modifies the file;
  `main` deliberately removed it in commit `f909f82` ("feat: add verified
  document flows and harden release safety") as part of a documented cleanup.
  No equivalent content was left orphaned elsewhere.
- `Tests/LightningSPP.Tests/GoldenPipelineTests.cs`,
  `Tests/LightningSPP.Tests/PdfDocumentTests.cs`, `Tests/SimpleTests.cs`,
  `Tests/LightningSPP.Tests/Golden/process_hybrid.sha256`,
  `Docs/COMPLETION_VERIFICATION_2026-10-06.md` — auto-merge cleanly with no
  net change from `main`'s current content.

## Decision

No pull request was opened for this branch: a PR's "Files changed" view
compares against the fork point, not against a real merge result, and would
have misrepresented this as a large, partially conflicting change (including
appearing to remove `main`'s Fedora/RHEL/Rocky DejaVu font fix from
`Workflow/FileTools.cs`, which it does not — that file is untouched by this
branch and the merge keeps `main`'s version intact). Opening a PR whose
displayed diff does not match its real effect would violate this
repository's NO FAKE, PRODUCTION GRADE acceptance gate.

This branch is kept as-is on `origin` for historical record. It is safe to
delete at any time without losing work; everything in it is already shipped
on `main`.
