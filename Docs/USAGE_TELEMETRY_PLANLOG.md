# Planlog: Persistent Production Usage and Daily Git Evidence

Status: approved scope; this commit records the implementation plan. No production usage figures are asserted here.

## Verified baseline

- Customer identity, account state, entitlements, billing receipts and related records are persisted through `CustomerStore` in Longo.
- `Services/LightningMetrics.cs` currently keeps global pipeline request/success/failure counters and scan/process/print call and duration totals in static process memory.
- Those counters restart at zero when the process restarts. They are global, not customer-scoped, and do not provide a durable daily history.
- The current metrics surface is therefore not a source for reconstructing the full two-month live history.
- Lightning SPP has been live for about two months according to the owner. Historical totals must be recovered from real retained production sources before publication.

## Required outcome

1. **Durable detailed usage ledger**
   - Persist each usage event to the private Longo data store.
   - Associate authenticated calls with the verified internal customer/account identity; preserve anonymous traffic as anonymous.
   - Capture UTC timestamp, unique event ID, operation/route, HTTP status, outcome, elapsed time, input/output byte counts, billable-unit basis, plan or license tier where resolved, release/commit identity, and a sanitized failure category.
   - Enforce idempotency so a retried event cannot be counted twice.
   - Do not store request or response bodies, document contents/names, passwords, cookies, authorization headers, Stripe secrets, or license keys.
   - Do not silently discard events. Track persistence failures/backlog and surface them operationally without turning an unrecorded event into a successful usage record.

2. **No-reset totals and daily rollups**
   - Lifetime totals are derived from durable events or durable aggregates; process memory is only a cache.
   - Store daily rollups by UTC date, with endpoint-level totals and success/failure counts.
   - Record bytes in/out and latency distributions (at least count, p50, p95, max) only where actual observations exist.
   - Preserve historical days. Corrections must be traceable and must not overwrite evidence without a revision/audit trail.

3. **Customer and operator views**
   - Provide an authenticated customer usage view limited to that customer's own records.
   - Provide an admin/operator aggregate view with date range, route, outcome, and plan filters.
   - Keep detailed customer usage private in Longo. Never put customer identifiers or per-customer rows in this public repository.

4. **Daily public JSON in Git**
   - Publish one machine-readable JSON aggregate per UTC day under a documented path such as `artifacts/usage/daily/YYYY-MM-DD.json`.
   - The public file may contain overall counts, endpoint-level counts, outcomes, byte totals, latency summaries, service version/commit and source-window metadata. It must not contain names, emails, stable customer IDs, IP addresses, payload details, or secrets.
   - Make publication idempotent for each date and commit only validated production measurements. Use a scheduled GitHub Actions workflow with least-privilege `contents: write`; production must not hold a Git write credential.
   - Mark incomplete or unavailable periods explicitly. Never generate placeholder zeros or backfill estimates as observed facts.

5. **Historical backfill**
   - Inventory retained origin/proxy/edge logs and any existing production telemetry for the live period.
   - Reconcile sources, document coverage and timezone, and publish only verified days.
   - Missing days remain marked as gaps; do not infer them from uptime, benchmark runs, billing receipts, or current counters.
   - Keep workstation benchmark runs separate from production usage evidence.

## Implementation sequence

- [ ] Confirm all production request paths and where authenticated customer/license identity is available.
- [ ] Define the versioned private usage-event and daily-rollup schemas.
- [ ] Implement durable, idempotent persistence in Longo and restart-safe cumulative aggregation.
- [ ] Instrument success and failure outcomes across customer-facing API operations, including bytes and elapsed time.
- [ ] Add customer-scoped usage API and operator-only aggregate access.
- [ ] Add a daily exporter and public, redacted JSON artifact format.
- [ ] Add a scheduled GitHub Actions publisher with minimal permissions and duplicate-date protection.
- [ ] Recover and reconcile the available live-period history; publish gaps honestly.
- [ ] Validate concurrency, restart recovery, duplicate delivery, authorization boundaries, redaction, export integrity, and failure visibility.
- [ ] Update the benchmark and product pages to distinguish workstation benchmarks, live production usage, customer workload results, and cross-vendor comparisons.
- [ ] Deploy only through the existing reviewed CI/CD and production gates.

## Acceptance conditions

- Restarting or replacing the application process does not reset lifetime or daily totals.
- Replaying the same event does not increment totals twice.
- Customer A cannot read Customer B's usage.
- Public Git JSON contains only anonymized aggregates and can be reproduced from the private durable source.
- Every published observation has a time window, source, release identity and completeness status.
- No production metric, historical total, benchmark result, SLA, or comparative claim is published without its corresponding evidence.

## Evidence locations reviewed

- `Services/LightningMetrics.cs`
- `Docs/SLA_SLO.md`
- `Docs/MANUAL/04-SHERBIMI-HTTP.md`
- `Web/CustomerStore.cs`
- `Web/CustomerAccounts.cs`
- `Web/LicenseApiServer.cs`
- `deploy/production/README.md`
