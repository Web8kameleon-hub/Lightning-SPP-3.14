# Evidence — 4 November 2026

Companion record for `Docs/FLOW.md`. Scope: this checkout; local build and local
suite only. **Nothing was deployed** and no server was touched.

## What this change adds

- `Intake/IntakeSource.cs`, `Intake/ScanProviderRegistry.cs` — declared intake
  kinds and an honest scan-provider probe (SANE only when `scanimage` exists;
  WIA detected but not implemented; TWAIN reported unavailable).
- `Documents/TextLayerExtractor.cs` — Poppler `pdftotext` text layer with the
  SHA-256 of the exact source bytes.
- `Extraction/FieldRule.cs`, `Extraction/FieldExtractor.cs` — operator-owned
  regex rules with validation, match timeout, and page/offset references.
- `Provenance/ProvenanceTrail.cs` — append-only hash-chained record per derived
  field, with named reviewer decisions.
- `Batch/BatchModels.cs`, `Batch/BatchRunner.cs` — resumable batch with an atomic
  checkpoint and a manifest that records each source hash.
- `Packaging/DocumentPackager.cs` — reproducible archive with a hash manifest and
  verification.
- `Flow/FlowCommands.cs`, `Flow/FlowBatch.cs`, `Program.cs` — the `flow` commands.
- `Scripts/preflight-policy.py`, `Scripts/gate-and-deploy.sh` — the deploy gate.

## Executed checks

| Check | Actual outcome |
| --- | --- |
| `dotnet build -c Release` | Passed, zero warnings, zero errors |
| Full xUnit suite | 350 passed, 0 failed, 15 skipped, 365 total |
| Reference-flow tests | 15 passed: extraction with page/offset, missing-required, trail chain and tamper detection, intake ordering, provider honesty, batch interruption/resume, failing-document isolation, integrity mismatch, package reproducibility, package tamper, real Poppler text-layer read of a generated two-page PDF |
| `python Scripts/preflight-policy.py` | PASS on all five checks |

One earlier run reported a single failure in 358 tests. It did not reproduce in
the two following runs and is not claimed as fixed: the failing test was never
identified, so it stays an unexplained transient.

## Boundaries

No OCR, no physical scanning, no customer corpus, no 30,000-document capacity
measurement, no live deployment, no certification.

## Deploy gate

`Scripts/gate-and-deploy.sh` refuses an unclean tree, then runs the policy gate,
the Release build and the full suite, and only on success calls
`deploy/production/deploy.sh deploy <sha>` — the existing blue-green runbook for
the production host. It has no flag that skips the suite silently.

Note: `Docs/RELEASE_EVIDENCE.md` is held open by the editor, which rewrites the
file from its own buffer and discards external appends. The record lives here
instead of being appended there.
