# Lightning SPP — SLA / SLO / Error Budget

This document ties together three things that already exist separately in
this repository, instead of inventing new numbers:

- **SLO (internal engineering target)** — enforced in code, in
  [`Services/LightningMetrics.cs`](/Services/LightningMetrics.cs), and exposed
  live at `/api/metrics` and `/api/alerts`.
- **SLA (customer-facing commitment)** — defined per plan in
  [`PRICING.md`](/PRICING.md); only the Enterprise plan carries a contractual
  uptime SLA today.
- **Zero-downtime deploy practice** — the operational mechanism
  ([`deploy/production/deploy.sh`](/deploy/production/deploy.sh)) that exists
  specifically so shipping a release does not itself consume error budget.

## 1) Service Level Indicators (SLIs)

Measured continuously by the running process and readable at any time via
`GET /api/metrics`:

| SLI | Definition | Source |
|---|---|---|
| Availability | `pipelineSuccess / pipelineRequests * 100` | `LightningPipeline.ExecuteAsync` outcome, counted via `LightningMetrics.RecordPipeline` |
| Scan latency | average `ms` per scan call | `LightningMetrics.RecordScan` |
| Process latency | average `ms` per process call | `LightningMetrics.RecordProcess` |
| Print latency | average `ms` per print call | `LightningMetrics.RecordPrint` |

**Known limitation (stated here, not hidden):** these counters live in
process memory (`LightningMetrics`, static fields under a single lock) and
reset to zero on every process restart. A zero-downtime deploy replaces the
process, so the SLI window effectively restarts with each release. This is
sufficient for live alerting on the current release, but it is **not** a
substitute for a persisted, cross-release metrics store if long-term
historical SLO reporting is ever required — that would need an external
metrics backend (e.g. scraping `/api/metrics` on an interval into a
time-series store), which does not exist in this codebase today.

## 2) Service Level Objectives (SLOs) — internal engineering target

These are the actual constants compiled into `LightningMetrics.cs` today;
breaching either one flips `alertsActive=true` on `/api/metrics` and adds a
line to `/api/alerts`:

| Objective | Target | Constant |
|---|---|---|
| Availability | ≥ 99.5% | `SloAvailabilityTargetPct` |
| Scan avg latency | ≤ 25 ms | `SloScanAvgTargetMs` |
| Process avg latency | ≤ 30 ms | `SloProcessAvgTargetMs` |
| Print avg latency | ≤ 25 ms | `SloPrintAvgTargetMs` |

These are deliberately stricter than the customer-facing SLA below — the
internal target is meant to trip an alert *before* a customer-facing
commitment is at risk, not at the same threshold as the commitment.

### Error budget (99.5% monthly SLO)

A 30-day month has 43,200 minutes. At 99.5% availability the allowed
unavailable time is:

```
43,200 × (1 − 0.995) = 216 minutes/month  (≈ 3h 36m)
```

If `alertsActive=true` is observed, treat it as error-budget burn: investigate
before it compounds toward the 216-minute monthly ceiling, per the escalation
path in section 4.

## 3) Service Level Agreement (SLA) — customer-facing commitment

Per [`PRICING.md`](/PRICING.md), only one plan carries a contractual
uptime number; the others are explicitly sold **without** an SLA:

| Plan | Support response | Uptime SLA |
|---|---|---|
| Free (Developer) | Community | ❌ None |
| Pay-as-you-go | Email, 48h | ❌ None |
| Unlimited | Priority email, 24h | ❌ None |
| Enterprise | 24/7 phone | ✅ 99.9% |

### Enterprise error budget (99.9% monthly SLA)

```
43,200 × (1 − 0.999) = 43.2 minutes/month
```

This is tighter than the 216-minute internal SLO budget above by design: the
internal SLO must be met comfortably before the external SLA is at risk. If
the internal 99.5% SLO is ever breached, assume the Enterprise 99.9% SLA is
already at risk and escalate immediately, not after the fact.

Do not advertise 99.9% (or any uptime number) to Free/Pay-as-you-go/Unlimited
customers — `PRICING.md` explicitly sells those tiers without an SLA, and
`Docs/GO_LIVE_CHECKLIST.md` section 8 requires UI/marketing claims to match
the real business model.

## 4) Incident response & escalation

1. **Detect:** `/api/alerts` returns `alertsActive=true`, or the post-deploy
   public health check in `.github/workflows/ci-cd.yml`
   (`deploy-production` job) fails.
2. **Triage:** check `/api/metrics` for which SLI breached (availability vs.
   a specific stage's latency), and `InternalAuditLogger` output for errors
   around the same timestamps.
3. **Mitigate:**
   - If the breach follows a deploy: run
     `deploy/production/deploy.sh rollback` — this restarts the previous
     (still-cached, not rebuilt) release and swaps the edge back with the
     same graceful, zero-downtime `nginx -s reload` used for forward
     deploys. See [`deploy/production/README.md`](/deploy/production/README.md).
   - If the breach is unrelated to a recent deploy: treat as a standard
     incident against the running release; `deploy.sh status` shows which
     commit is active.
4. **Document:** record start/end time of the breach and estimated minutes
   consumed against the error budget in section 2/3 above, in the incident's
   own notes (this repo does not currently automate error-budget bookkeeping
   across incidents).

## 5) Why the zero-downtime deploy mechanism exists

Every `systemctl restart`-style or `docker compose down && up` deploy is
itself a source of unavailability — on a single-instance service, a deploy
that takes even a few seconds to restart is a guaranteed SLO/SLA breach if it
happens often enough. `deploy/production/deploy.sh` exists specifically to
remove deploys as a source of error-budget consumption: the new release is
verified (health, static assets, license status, unauthenticated-admin
rejection) **before** the edge is switched, the switch itself is a graceful
`nginx -s reload` (no dropped connections), and the previous release keeps
running until the switch is confirmed — so a routine release should consume
~0 minutes of the budgets in sections 2 and 3, leaving that budget for actual
incidents.

## Related documents

- [`Docs/GO_LIVE_CHECKLIST.md`](/Docs/GO_LIVE_CHECKLIST.md) — go/no-go gate,
  section 6 ("Observability & SLO Readiness") and section 7 ("rollback
  procedure") are satisfied by this document plus `deploy.sh`.
- [`PRICING.md`](/PRICING.md) — the only source of truth for what is actually
  sold to customers; this document must stay consistent with it, not the
  other way around.
- [`deploy/production/README.md`](/deploy/production/README.md) — operational
  runbook for the deploy/rollback commands referenced above.
