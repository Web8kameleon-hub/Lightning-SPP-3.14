# Dedicated Hetzner bridge integration test environment

This is a **deployment plan**, not evidence that a test VM or services have been provisioned. Never run these integration writes against production, the current Clisonix host, or production data.

## Separation and prerequisites

Provision a separate Hetzner VM with its own test-only volumes, test-only service credentials and a dedicated unprivileged SSH account. Restrict inbound SSH to approved CI egress or a suitable controlled access path; do not publish Excel or OS-CLX application ports. Verify host fingerprint through an independent trusted channel. The source repositories for the actual upstream services must be reviewed and deployed on the test VM, not replaced with mock handlers.

Required **real** upstream contracts on the VM, bound to loopback only:
- Excel microservice: `127.0.0.1:8002` with `POST /api/excel/validate` and `POST /api/excel/parse`, including the Excel data-preservation correction.
- Excel Core: `127.0.0.1:8001` with `GET /api/lagter/v1/export` returning the expected five-sheet workbook.
- Security-reviewed OS-CLX NodeDB: `127.0.0.1:9094` with `GET /v1/ndb/nodes/{nodeId}` and `POST /v1/ndb/nodes/{nodeId}/telemetry`, test-only API key and HMAC signing. Investigate the previously recorded Defender detection before running the OS-CLX executable.

Use the repository's canonical deployment conventions for each upstream; do not assume the production Docker Compose stack can safely be copied to the test VM. Test NodeDB ingest uses explicit fixture telemetry and writes a new test node; isolate and expire those records.

## GitHub Actions Secrets

Configure these in the Lightning-SPP-3.14 repository or a protected CI environment, never in git or chat:
- `SPP_TEST_SSH_HOST` — dedicated VM DNS/IP, not production
- `SPP_TEST_SSH_USER` — restricted test SSH user
- `SPP_TEST_SSH_KEY` — private key for this test host only
- `SPP_TEST_SSH_KNOWN_HOSTS` — pinned, independently verified known_hosts line
- `SPP_TEST_OSCLX_API_KEY` — isolated NodeDB API key
- `SPP_TEST_OSCLX_HMAC_SECRET` — isolated NodeDB signing secret

CI opens SSH forwards to localhost ports 18002, 18001 and 19094. The bridge tests already require loopback URLs. All missing configuration or unreachable upstreams must cause FAIL, not SKIP.

## Acceptance gates

1. Confirm test VM is isolated from production (credentials, volumes, networks, no public upstream ports).
2. Verify the three actual upstream contracts, including negative authentication tests.
3. Require 276/276 reported tests PASS and no skipped/ignored tests. If the inventory changes, explain the difference rather than forcing a fixed count.
4. Preserve raw TRX, benchmark environment, benchmark output and truthful failure manifest; validate any release analysis documents before publish.
5. Only after all other Docker, license, security and release gates succeed, allow the existing restricted production deployment flow. Never deploy a test VM image to production without separate validation.

SSH port forwarding establishes a transport only; it does not establish application readiness. This document does not certify the runtime, infrastructure, or any CI result.
