# Lightning SPP — channel plan

Canonical spelling retained from the requested document: `chanellplan.md`.

| Channel | Contract and trust boundary |
| --- | --- |
| Customer | Existing protected customer cookie and CSRF; workspace owner inferred from session |
| Conversation | Goal and authorized document context; model-generated plans remain proposals |
| Capability | Registry exposes arguments, permissions, availability and dependencies |
| Model | Operator-configured endpoint and exact discovered identity; remote data requires consent |
| Execution | Validated dependency graph; bounded workers; cancellation; durable progress |
| Evidence | Tool outputs, source paths/hashes, plan/runtime hashes; uncertainty kept explicit |
| Recovery | Resume verified read-only work; uncertain external side effects require review |
| Review | Clinical/engineering rules and uncertain findings retain human acknowledgement |
| Output | Owner-scoped reports; originals preserved; device submission is not print completion |

Adapters extend these contracts without adding profession-specific routing to
the orchestrator. Documents, model replies and screen text are untrusted data.
They cannot grant tool permissions or authorize another customer's workspace.

Implementation sequence: contracts and scope → real tools → model discovery and
planning → durable runner → authenticated API and UI → domain review tools →
integration/security/browser tests → source-bound verification evidence.
