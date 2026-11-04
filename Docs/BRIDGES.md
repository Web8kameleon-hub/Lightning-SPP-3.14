# Lightning SPP bridges

Status: implemented in the SPP CLI; **the complete bridge set is not accepted for
production**. Excel bridges were exercised against real local services in the
recorded tests. OS-CLX ingest/readback and the current deployed upstream revisions
still require isolated runtime verification. This document compares the related
repositories inspected on 2026-11-04; it is not a full inventory of the account.

These adapters are available in the `wwwmmm.Integrations` namespace and through
the `bridge` CLI command. They do not add public HTTP endpoints or change the
existing scan/process/output pipeline, and are not registered as workflow tools.
Configuration is explicit; no service address, credential, telemetry signal or
successful result is invented.

## Implemented contracts

| Adapter | Upstream source | Operations |
| --- | --- | --- |
| `ClisonixExcelCoreBridge` | `clisonix.com/excel-core/lagter_v1_api.py` | `GET /api/lagter/v1/export`, returning the upstream LAGTER `.xlsx` workbook |
| `ClisonixExcelBridge` | `clisonix.com/services/excel/main.py` | Multipart `POST /api/excel/validate` and `/api/excel/parse` for `.xlsx` files |
| `OsClxNodeDbBridge` | `OS-CLX/runtime/internal/api/ndb_store_handlers.go` | `GET /v1/ndb/nodes/{nodeId}` and `POST /v1/ndb/nodes/{nodeId}/telemetry` |

Excel Core and the Excel microservice are different applications. Point each
adapter at the corresponding deployed service. The reviewed workbook client at
`ocean-core/excel_core_integration.py` calls `/api/v1/workbooks/...`; these adapters
do not assume those routes exist in either of the reviewed Excel services.

Excel parsing returns the first worksheet and at most **100 data rows**; the
`rows` field reports the upstream count. The corrected deployment preserves
zero/false values and disambiguates duplicate headers (for example,
`Name, Name, Name_2` becomes `Name, Name_3, Name_2`). The SPP adapter also rejects
duplicate headers and rows with missing or unexpected columns. These are workbook
operations, not invoice OCR or classification. The bridge does not evaluate
formulas, scan hardware, print to hardware or render PDF. LAGTER workbook values
come from the upstream reporting application; downloading them does not
independently validate their interpretation as quality, compliance or performance
measurements.

## Upstream source comparison

The following describes repository source, not a claim that the current production
container is running the same revision:

| Capability | Repository evidence | Finding |
| --- | --- | --- |
| Excel Core reporting | `clisonix.com`, `clisonix-evolution` and `LONGO-` have the same `lagter_v1_api.py` source blob (`90fd8523`); Clisonix owns the Compose service definition | No more advanced code copy was found among these three. Excel Core generates LAGTER workbooks; it does not parse invoices or perform OCR. |
| Excel parse/validate | `clisonix.com@main` file blob `e04aa2ef`; `clisonix-evolution@main` and `LONGO-@main` share blob `7de54036` | The inspected `main` copies still lack the workbook-close and zero/false/duplicate-header/missing-sheet corrections. |
| Corrected Excel deployment | Upstream release evidence records a direct production deployment on 2026-10-03 from code revision `f7ab88b64aecf063b6d7987432ea04dc11fc00bd`, followed by live HTTP verification. PR #143 is now closed without merge; its head contains the fix but is not the default `main` source. | A prior release record is evidence of that deployment, not proof of the current running revision. Reconfirm the deployed revision before accepting the bridge. |
| OS-CLX NodeDB runtime | `OS-CLX@master` revision `c54e104f`; `runtime/internal/ndb/store.go` holds nodes in a map capped at 512 entries | The SPP adapter targets the runtime node read/telemetry endpoints. The reviewed store is in-memory, not durable persistence. |
| Kloud NodeDB control plane | `Kloud@main` revision `9e2d2356`; control-plane code appends a WAL and implements leases | It adds lease/topology operations, but node and lease state is in memory, WAL is not replayed at startup, and WAL write errors are swallowed. It is not a drop-in durable replacement. |

These are different scopes, not a single maturity ranking: OS-CLX provides the
runtime telemetry contract used by SPP; Kloud adds control-plane lease behavior
with the durability gaps above. The SPP bridge currently targets OS-CLX only.

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

GitHub access is separate from service authentication; no GitHub token is sent by
these adapters. The reviewed Excel applications do not implement bearer
authentication themselves. OS-CLX `APIKeyAuth` and `HMACGuard` are conditional:
the source bypasses each guard when its server secret is unset. Therefore the
service credentials are optional in SPP and are required only when the actual
runtime or ingress is configured to enforce them. The real NodeDB contract test
sets them specifically to exercise authenticated and rejected requests. Do not
expose unauthenticated document uploads publicly as part of this integration.

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
sends the upstream viewer role for reads and operator role for ingest; when the
upstream API-key guard is enabled, these headers do not replace that credential.
It does not promote node trust.
Ingest succeeds only after the upstream acknowledgement matches the node, fabric,
trust domain and supplied pressure/resonance/memory/momentum. This is an ingest
acknowledgement, not proof of distributed replication or task execution.

Requests have a 30-second deadline, no automatic redirects and no automatic
retries. JSON responses are bounded to 2 MiB; workbook uploads and exports to
10 MiB. A timeout or interrupted write can have an unknown upstream outcome:
read the node back before retrying. Invalid Excel validation results, malformed
responses and HTTP errors are failures even when an upstream returns HTTP 200.

## Upstream Excel correction and current source state

The `clisonix.com@main` parse/validate source still leaves read-only workbooks
open before upload cleanup, drops rows whose values are all zero/false, overwrites
duplicate headers, and silently selects the active sheet when a requested sheet
is missing. The separate source copies in `clisonix-evolution` and `LONGO-` do
not contain the inspected correction either.

The upstream release record reports that the corrected service was deployed
directly on 2026-10-03 from `f7ab88b64aecf063b6d7987432ea04dc11fc00bd`; it records
eight passing Python tests and live HTTP checks for health, workbook parsing,
zero/false preservation, duplicate headers, row count/preview, and missing-sheet
failure. That correction was not merged into the default `main` branch. PR #143
is closed without merge. The patch at
[clisonix-excel-cleanup.patch](bridges/clisonix-excel-cleanup.patch) is retained
as historical review material; do not apply it blindly to a newer checkout.
Verify the actual deployed source revision before relying on the production fix.

## Initial candidate verification recorded on 2026-10-03

This is the pre-replacement candidate snapshot. Its statements that the
production image was unchanged and that replacement had not yet run describe
that point in time; they are superseded by the subsequent upstream release record
of the scoped Excel-only deployment above.

The corrected Excel code and tests were developed in the upstream repository and
reviewed through PR #143. The PR was later closed without merge; the release
record separately reports a direct deployment.

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

At this candidate snapshot, deployment through the repository's CI policies was
blocked by contradictory branch rules. The later upstream release record reports
a scoped direct deployment and HTTP verification; this did not merge PR #143 or
change those CI policies.

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

The local OS-CLX kernel/Kloud backends were not running during this SPP
verification. Its initial HTTP health response therefore does not establish
kernel, fluid-cycle, federation or scheduler health. The SPP NodeDB
ingest/readback test did not complete against an isolated real runtime.

Real upstream tests are opt-in and restricted to loopback services. With isolated
Excel Core, corrected Excel service and a security-reviewed OS-CLX runtime,
configure their URLs, the test runtime's API key/HMAC secret, and:

```powershell
$env:SPP_BRIDGE_TEST_ALLOW_WRITES = 'isolated-loopback'
dotnet test Tests/LightningSPP.Tests/LightningSPP.Tests.csproj --filter FullyQualifiedName~BridgeUpstreamTests
```

The NodeDB test writes explicitly identified fixture signals to a new test node.
They verify the real ingest contract, not measured production performance.

Production acceptance remains pending: adjudicate the runtime security detection,
complete real NodeDB ingest/readback/auth/HMAC tests, and verify that the active
Excel service revision matches the upstream correction recorded above. Deploy the
correction only if the active service is not already running it. Configure the
actual service URLs and credentials only where the runtime or ingress enforces
them, then verify the deployed workflows. Deployment and integration of the
remaining account repositories are outside the completed work recorded here.
