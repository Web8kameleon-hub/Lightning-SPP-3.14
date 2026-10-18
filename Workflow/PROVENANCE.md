# Source and dependency provenance

This workflow implementation is new code in Lightning SPP. It preserves the
existing customer identity/Longo, native PDF and Linux device implementations.

The visible-page pattern was inspected in `Web8kameleon-hub/clisonix.com`, local
revision `163c881d87155a1c906ee1e265f0d3151e5f559d`,
`apps/web/app/modules/matia/page.tsx`, function `extractVisibleScreenText`.
The new browser implementation limits collection to visible non-form text from
the current workspace and requires opt-in; it is not a cross-window screen reader.

Ollama adapter contracts were checked against official documentation:
- https://docs.ollama.com/api/tags
- https://docs.ollama.com/api/chat

Accessible GitHub repository names were inventoried on 15 October 2026. This
inventory is not a complete code audit of all repositories. No historical claim
of 19 fully audited repositories or validated CLX model inference is adopted.
No fixed-confidence reasoning or synthetic production response was imported.

The configured read-only HTTP adapter is an extension contract, not a fabricated
provider integration. Unavailable native dependencies/models/connectors fail
explicitly. Mocks used by contract tests remain exclusively in test code.
