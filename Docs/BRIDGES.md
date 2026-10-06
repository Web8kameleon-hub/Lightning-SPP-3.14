# Lightning SPP bridges

Status: implemented locally; **not accepted for production deployment**. Excel was
verified against real local services with the upstream cleanup fix below. OS-CLX
telemetry integration still requires real runtime verification. The other
repositories in the account have not been integrated.

These adapters are available in the `wwwmmm.Integrations` namespace and through
the `bridge` CLI command. They do not add public HTTP endpoints or change the
existing scan/process/output pipeline. Configuration is explicit; no service
address, credential, telemetry signal or successful result is invented.

## Implemented contracts

| Adapter | Upstream source | Operations |
| --- | --- | --- |
| `ClisonixExcelCoreBridge` | `clisonix.com/excel-core/excel_reporting_api.py` | `GET /api/lagter/v1/export`, returning the upstream LAGTER `.xlsx` workbook |
| `ClisonixExcelBridge` | `clisonix.com/services/excel/main.py` | Multipart `POST /api/excel/validate` and `/api/excel/parse` for `.xlsx` files |
| `OsClxNodeDbBridge` | `OS-CLX/runtime/internal/api/ndb_store_handlers.go` | `GET /v1/ndb/nodes/{nodeId}` and `POST /v1/ndb/nodes/{nodeId}/telemetry` |

Excel Core and the Excel microservice are different applications. Point each
adapter at the corresponding deployed service. The reviewed workbook client at
`ocean-core/excel_core_integration.py` calls `/api/v1/workbooks/...`; these adapters
do not assume those routes exist in either of the reviewed Excel services.

Excel parsing returns the first worksheet, at most **100 data rows**, and an
upstream `rows` count that can be larger. With the supplied upstream fix, zero and
false cells are preserved and duplicate headers receive unique names without
overwriting existing columns (for example, `Name, Name, Name_2` becomes
`Name, Name_3, Name_2`). The adapter rejects duplicate headers and rows with missing
or unexpected columns. It does not evaluate formulas, perform
OCR, scan hardware, print to hardware or render PDF. LAGTER workbook values come
from the upstream reporting application; downloading them does not independently
validate their interpretation as quality, compliance or performance measurements.

## Configuration and CLI

Set actual service base URLs through your deployment's environment/secret manager:

| Environment variable | Purpose |
| --- | --- |
| `SPP_EXCEL_CORE_URL` | Excel Core reporting application base URL |
| `SPP_EXCEL_CORE_API_KEY` | Optional bearer credential for the authenticated Excel Core ingress |
| `SPP_EXCEL_SERVICE_URL` | Excel parse/validate microservice base URL |
| `SPP_EXCEL_SERVICE_API_KEY` | Optional bearer credential for its authenticated ingress |
| `SPP_OSCLX_URL` | OS-CLX runtime API base URL |
| `SPP_OSCLX_API_KEY` | OS-CLX `X-API-Key` credential, when required |
| `SPP_OSCLX_HMAC_SECRET` | OS-CLX write-signing secret, when required |

GitHub PATs authenticate repository access; they are **not** service credentials.
Neither reviewed Excel application implements bearer authentication itself. The
configured Excel credential requires an ingress that actually enforces it. Do
not expose unauthenticated document uploads publicly as part of this integration.

HTTPS is required except for loopback HTTP used in isolated local tests. Base URLs
cannot contain user information, query strings or fragments. Configure URL prefixes
as part of the base URL when your reverse proxy uses them; signing uses the path
sent over HTTP, which must match the path checked by OS-CLX HMACGuard.

From the repository root, after configuring the actual URLs:

```powershell
dotnet run --project wwwmmm.csproj -- bridge excel-export report.xlsx
dotnet run --project wwwmmm.csproj -- bridge excel-validate report.xlsx
dotnet run --project wwwmmm.csproj -- bridge excel-parse report.xlsx
dotnet run --project wwwmmm.csproj -- bridge node-read lightning-spp
dotnet run --project wwwmmm.csproj -- bridge node-ingest lightning-spp measured-telemetry.json
```

`excel-export` creates a new file and refuses to overwrite an existing destination.
It checks the returned XLSX container and workbook metadata before saving. Parse,
validate and NodeDB commands write the upstream JSON response to stdout. Failures
write a diagnostic to stderr and return exit code 1; incorrect usage returns 2.
Ctrl+C cancels the request. SDK callers can supply a cancellation token.

The telemetry file must be UTF-8 JSON. Required fields are `fabric_id`,
`trust_domain`, `pressure`, `resonance`, `health_score`, `memory`, `momentum`,
`error_rate`, `queue_depth` and `latency_ms`. Supply measured signals with their
definitions; omitted numeric fields are rejected rather than treated as zero.
The six normalized signals must be finite values in `[0, 1]`; queue depth and
latency must be finite and nonnegative. Optional fields are `role`, `capabilities`,
`affinity_tags`, `preferred_task_types` and `burst_allowed`. Unknown properties
are rejected. Tag lists are bounded to 64 entries of at most 128 characters.

Node IDs contain 1–128 ASCII letters, digits, hyphens or underscores. The bridge
sends the upstream viewer role for reads and operator role for ingest; these
headers do not replace API-key authentication. It does not promote node trust.
Ingest succeeds only after the upstream acknowledgement matches the node, fabric,
trust domain and supplied pressure/resonance/memory/momentum. This is an ingest
acknowledgement, not proof of distributed replication or task execution.

Requests have a 30-second deadline, no automatic redirects and no automatic
retries. JSON responses are bounded to 2 MiB; workbook uploads and exports to
10 MiB. A timeout or interrupted write can have an unknown upstream outcome:
read the node back before retrying. Invalid Excel validation results, malformed
responses and HTTP errors are failures even when an upstream returns HTTP 200.

## Upstream Excel correction

The reviewed Excel microservice leaves a read-only openpyxl workbook open and
then unlinks its upload. On Windows this raises `PermissionError: WinError 32`
and turns otherwise successful validation/parsing into HTTP 500. The separate
repository correction is supplied as
[clisonix-excel-cleanup.patch](bridges/clisonix-excel-cleanup.patch).

Apply it in a checkout of `Web8kameleon-hub/clisonix.com`, review and test that
checkout, and deploy the corrected Excel service before accepting the Windows
integration. It closes the workbook in both `finally` blocks before cleanup and
also fixes zero/false data loss, duplicate-column overwrites, row counting and
the silently ignored request for a missing worksheet. Parse previews retain only
the first 100 rows in memory while counting all nonempty rows. Upload reads are
bounded to the size limit plus one byte. The patch includes eight real-workbook
tests. The two Excel service files were committed and pushed to
`clisonix.com` as `644e0bb250c71d43f39df02f1267c52e6a97f566`, on branch
`fix/excel-data-preservation`, with [draft PR #143](https://github.com/Web8kameleon-hub/clisonix.com/pull/143).
They have not been merged or deployed. The GitHub governance check rejected the
branch: its configured primary work branch is `fix/hetzner-swarm-rollout`, and the
repository exceeds its maximum of one open PR. These checks were not bypassed.
The SPP bridge changes in this checkout remain uncommitted.

```powershell
git apply --check C:/path/to/wwwmmm/Docs/bridges/clisonix-excel-cleanup.patch
git apply C:/path/to/wwwmmm/Docs/bridges/clisonix-excel-cleanup.patch
# In services/excel, using the service environment:
python -m unittest test_excel_cleanup -v
```

## Verification recorded on 2026-10-03

Excel-only upstream changes are pushed to draft
[PR #143](https://github.com/Web8kameleon-hub/clisonix.com/pull/143): code/tests
commit `644e0bb250c71d43f39df02f1267c52e6a97f566`, followed by Excel release
evidence commit `a05e243`. No merge or production deployment has occurred.

On the actual Hetzner host `178.63.89.121`, the candidate image was built from
the code commit and all eight Python tests passed inside it. Real HTTP tests
in an isolated candidate container also passed health, validation, parsing,
zero/false cells, duplicate headers, 120-row counting, 100-row preview, missing
worksheet 404, invalid-workbook failure cases and upload cleanup. Candidate
containers used no production mounts and were removed afterward.

The active `clisonix-excel` service in `/opt/clisonix.com` uses
`docker-compose.unified.yml`, project `clisonixcom`; its image remained unchanged
and its health endpoint remained healthy. The existing image was preserved as
`clisonix-excel:rollback-20261003-3ef5d3b`. The working checkout has unrelated
local changes that must be preserved.

Candidate and rollback Compose override files were prepared in the isolated
server staging directory and validated using real `docker compose config`
output. A full configuration comparison confirmed that only the Excel image
differs; all other service configuration, including volumes, is preserved.
Neither replacement nor rollback was executed.

Deployment remains blocked by contradictory CI policies: governance requires
PR head `fix/hetzner-swarm-rollout`, while release evidence requires `release/*`.
Governance also permits only one open PR, but five were open at verification.
The release evidence document's content passed local validation; its branch
policy still fails. Existing auto-deploy defaults affect other services and
lack a verified Excel health gate. No CI policy was weakened.

Reviewed upstream revisions:

- `clisonix.com`: `6a3724508d4f7592c033ec8814d007fb3e1008fc`.
- `OS-CLX`: `c54e104f94adb972b0c69e2a6ac750242c0c1048`.

Checks actually executed:

- `dotnet build wwwmmm.csproj --no-restore`: passed, zero warnings/errors.
- 51 bridge contract tests: passed. Their mock replies are protocol/failure tests,
  not evidence of a live service.
- The full 77-test run with local upstream integration enabled initially had
  75 passes and two failures: unclosed Excel upload on Windows, and the removed
  OS-CLX runtime executable.
- After the Excel fixes and expanded tests, the run excluding only
  `NodeDbTestFixture_IsIngestedWithAuthAndHmacThenReadBack` passed **101 tests**,
  with zero skipped tests. The excluded NodeDB test is still unverified.
- The 101-test result comprises 44 existing tests, 51 bridge protocol/failure
  tests, and six real Excel HTTP integration tests. Cases exercised include
  malformed schemas, mismatched NodeDB acknowledgements, chunked-response size
  bounds, interrupted reads, cancellation and unknown write outcomes. These
  NodeDB protocol tests use test-only mock responses.
- Real local Excel Core export produced an XLSX file; the corrected real Excel
  microservice validated its five sheets and parsed its `Overview` worksheet.
- Eight upstream Python tests passed, including invalid-workbook failure paths,
  upload removal, zero/false preservation, duplicate headers, 120-row preview
  counting and HTTP 404 for a requested worksheet that is missing.
- CLI Excel export, validation and parsing passed against the real local Excel
  services. A second export to the same file was refused with exit code 1 and its
  SHA-256 stayed unchanged. The upstream patch passed reverse-application checks.
- OS-CLX built with `go build ./cmd/api` and initially returned HTTP 200 from
  `/healthz`. Windows Defender subsequently quarantined the built executable as
  `Trojan:Win32/Bearfoos.A!ml`. This records the Defender detection; its correctness
  has not been adjudicated. NodeDB write/read verification did not complete.

The local OS-CLX kernel/Kloud backends were not running. Its initial HTTP health
response therefore does not establish kernel, fluid-cycle, federation or
scheduler health. No production endpoints were changed or written to.

Real upstream tests are opt-in and restricted to loopback services. With isolated
Excel Core, corrected Excel service and a security-reviewed OS-CLX runtime,
configure their URLs, the test runtime's API key/HMAC secret, and:

```powershell
$env:SPP_BRIDGE_TEST_ALLOW_WRITES = 'isolated-loopback'
dotnet test Tests/LightningSPP.Tests/LightningSPP.Tests.csproj --filter FullyQualifiedName~BridgeUpstreamTests
```

The NodeDB test writes explicitly identified fixture signals to a new test node.
They verify the real ingest contract, not measured production performance.

Production acceptance remains pending: resolve the runtime security detection,
complete real NodeDB ingest/readback/auth/HMAC tests, deploy the upstream Excel
fix, provide the real protected service URLs/credentials, and verify the actual
deployed workflows. Deployment and integration of the remaining account
repositories are outside the completed work recorded here.
