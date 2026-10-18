# Lightning SPP — dynamic multitool scope

This is the current multitool implementation scope, based on the user-supplied
conversation and TODO dated 13 October 2026. `Docs/fabric-p0/INTEGRATION_PLAN.md`
is historical and is not the implementation plan for this work.

Lightning combines registered, permission-scoped tools rather than hardcoded
professional workflows. The lifecycle is: understand, collect evidence, propose
a plan, review, execute, verify, adapt, explain. Model output is a proposal, never
evidence that an operation ran. Each result retains source references and hashes.

Shared infrastructure: authenticated customer workspaces, immutable input
snapshots, tool contracts, configurable model adapters, dependency-aware plans,
durable jobs, cancellation, restart handling, contextual conversations and reports.
Capacity is constrained by configured resource budgets, with explicit failures
and pagination rather than unlimited allocation or fabricated completion.

Tool families: files, source audit, PDF text, vision, structured comparison,
reports, invoice/bank reconciliation, economic indicators and reviewed rules.
Clinical, construction and aviation checks require customer-owned rules, units,
versions and source references. Their output is a review finding, not clinical,
structural or airworthiness validation. No thresholds or confidence scores may
be invented. No silent cloud fallback, automatic shell execution or deletion.

Models and connectors are selected by operator configuration and live discovery.
No model is installed or trained merely because its name appears in configuration.
Mailbox and bank access require a selected read-only connector and credentials;
local imports are a separate capability and must be described as imports.

Existing customer auth, Longo billing/license data, PDF processing, device
adapters and UI are preserved. Production acceptance requires relevant build,
security, integration, browser and runtime evidence, plus rollback and health
verification. Prior downloaded evidence is historical, not proof of this code.
