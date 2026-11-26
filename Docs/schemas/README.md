# Usage contracts v1 — P1

These are validated contracts, not an implemented emitter, durable ledger or
production report. No production events or daily numbers are generated here.

- `usage-event.schema.json`: private event shape, resolved identity, explicit
  measurement availability, canonical routes and append-only correction reference.
- `daily-usage.schema.json`: public UTC-day aggregate, coverage gaps, observed
  measurement counts, cohort suppression and revision/source fingerprint.
- `Scripts/validate-usage-contracts.py`: Draft 2020-12 meta-schema and payload
  validation plus calendar-window/count/availability consistency checks.
- `Scripts/test-usage-contracts.py`: test-only fixtures and negative checks.

Install the pinned tool dependencies in
`Scripts/requirements-usage-contracts.txt`; then run from the repository root:

```powershell
python Scripts/validate-usage-contracts.py
python Scripts/test-usage-contracts.py -v
python Scripts/validate-usage-contracts.py --kind event private-event.json
python Scripts/validate-usage-contracts.py --kind daily public-day.json
```

The last two commands validate actual operator-supplied files; the names above
are usage examples, not files or reported production observations.

## Rules requiring the future ledger/publisher

1. The server resolves customer identity after authentication; schema conformance
   does not authenticate supplied JSON. Anonymous/operator/service records contain
   no customer identifier. No identity may be inferred from IP/body/email.
2. Event IDs are idempotency keys. Store each logical observation once. Conflicting
   replay is an error; an identical replay must not increment counts. A correction
   has a new ID, references an existing observation and retains the original audit
   record. The ledger must verify existence and enforce the correction chain;
   the schema alone cannot query storage or establish those facts.
3. Null means unavailable. A missing measurement cannot become zero. Daily sums
   carry the number of events actually measured; zero measured events requires a
   null sum. HTTP transport success is not document/AI/business acceptance.
4. UTC windows are half-open `[00:00Z, next day 00:00Z)`. Missing source windows
   are explicit gaps; complete days cannot be generated before the window ends.
   Counts are deduplicated and corrected before aggregation, never extrapolated
   from uptime or benchmarks.
5. Public data has no customer rows, arbitrary labels or stable customer IDs.
   Cohorts below the minimum of ten distinct contributors publish no totals.
   The future publisher must measure and verify that cohort privately. A schema
   assertion of `cohortSatisfied` is not proof that the privacy condition holds.
6. One UTC date has an idempotent current publication. Identical content does not
   create a revision; a correction increments revision and references the prior
   JSON content SHA-256. Preserve correction provenance and verify the prior hash
   against the real publication. The schema does not perform Git publication.
7. Unknown schema versions/fields are rejected. Changes require a versioned
   migration, relevant negative tests and privacy review. Raw IP, URLs/queries,
   names, tokens, cookies, license keys and bodies are excluded. The validator
   prints field/constraint failures, never supplied values.

P1 checks cover syntax, schema consistency and exercised examples. Runtime
ownership, idempotent persistence, actual privacy cohorts, daily publication and
historical completeness remain P2–P5 acceptance work. These contracts are not
billing authority.
