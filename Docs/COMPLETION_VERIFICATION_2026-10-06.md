# Completion verification — 6 October 2026

User commit `156cb147ae20070e65f69a1f1aef3077d8ad87f9` was already on
`origin/main`. Push and fetch succeeded; local/remote divergence was `0 0`.
Verification and repairs were performed in a separate worktree. The user's
excluded nested gitlink, backup and accidental filenames were not staged.

## Repairs

- Hybrid's golden baseline previously covered AI/memory options that now fail
  closed. Its new baseline covers the explicitly supported three-stage Brotli
  configuration. The test decompresses all three stages back to the exact input
  and checks identical output on repetition before comparing the golden hash.
- The CLI demo and full workflow explicitly exercise compression without
  claiming ONNX inference. CLI scan/pipeline tests use genuine generated PNG
  fixtures instead of random bytes presented as an image.
- The public Try endpoint requests the supported compression configuration and
  returns an error if processing fails. Its HTTP test now requires nonzero
  actual compressed output, preventing a scanned preview alone from passing.
- Linux CI installs Poppler and enables the native PDF tests; they are no
  longer silently omitted in that job.

## Checks and limits

- Windows Release build: zero warnings and errors.
- Windows xUnit: 118 passed, zero failed, 9 conditional integration tests skipped.
- CLI `demo`, `test` (5 checks) and `real`: passed.
- Six Excel bridge tests: passed, zero skipped, against isolated loopback Linux
  containers using the real Clisonix Excel and Excel Core source. This includes
  workbook export/validation/parse, duplicate headers, zero/false, 120 rows with
  a 100-row preview, and invalid workbook errors. Initial Excel Core verification
  failed because read-only test storage lacked `/tmp`; adding private tmpfs
  storage resolved that environment issue without changing application code.
- Linux .NET 10 with real Poppler: 120 passed, zero failed, 7 upstream bridge
  tests skipped. Combined with the separately executed Excel tests, 126 distinct
  tests have passed; this is not a single all-tests-passing execution.
- Seven public SEO pages passed metadata, JSON-LD and sitemap checks.

The NodeDB authenticated/HMAC write/read integration test remains unverified.
The earlier Defender quarantine recorded in [BRIDGES.md](BRIDGES.md) has not
been adjudicated; no quarantined executable was restored or protection bypassed.
No real ONNX model inference or physical scanner/printer verification is claimed.
This report does not accept the complete bridge release for production. Production
still serves the SEO release `c196cf6f7f79aa917916c91153a69f91d986dfa5`;
the new user integration commit and these repairs have not been deployed.
