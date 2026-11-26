# P0/P1 completion evidence — 2026-11-17

Scope: source route/identity inventory and validated JSON contracts. This is
**not** durable usage instrumentation, historical production data, billing
acceptance, public daily publication or a deployment record.

The merged working candidate incorporates `origin/main@186a25d` and the local
workspace candidate `f475f34`. P0/P1 deadlines were already overdue when the
Planlog gate was executed. Neither deadline was changed and no earlier completion
is claimed. The maintainer requested creation of the missing deliverables.

P0: inspected the HTTP dispatcher and delegated account, workflow, assistant,
Fluid and Stripe handlers. `USAGE_TELEMETRY_ROUTE_MATRIX.md` maps all dispatched
families, authentication sources, license-vs-customer distinctions, rejected and
unmapped requests. Potential production-history sources and uninspected retention
and coverage gaps are explicit. No production host logs were reconciled.

P1: both version-1 schemas passed Draft 2020-12 meta-schema validation. The
validator adds cross-field UTC-day, correction-self-reference, outcome-count,
measurement-availability and finite-number checks. The accompanying README
records which conditions still require real ledger/publisher verification.

Checks actually executed:

```text
python Scripts/validate-usage-contracts.py
PASS schema structure: event
PASS schema structure: daily

python Scripts/test-usage-contracts.py -v
14 tests passed
```

Fixtures exist only in the contract tests and are expressly identified as
invented test inputs. No production event/day JSON was generated or published.
Cases include private-field rejection, anonymous/customer separation, unknown
measurements, negative bytes, NaN/infinity, raw queries, invalid dates, correction
self-reference, unavailable/suppressed days, cohort bypass, day/coverage windows,
revision fingerprints, count totals and measurement counts.

The `verified` phase states apply only to these P0/P1 deliverables and exercised
checks. P2–P7 remain planned with unchanged acceptance requirements. Runtime
customer attribution, durable idempotency, actual cohort privacy, source coverage,
production integration and rollout remain unverified by this evidence.
