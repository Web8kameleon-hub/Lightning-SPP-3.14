# CRM, classification and hybrid document storage

Acceptance: local implementation under verification; **not accepted for production**.
Do not equate source availability with an active deployment.

The selected local CRM metadata mode is `LSPP_CRM_STORAGE=durable`. No NanoGrid
database format is invented. CRM and classification refer to a document; they do
not each store its payload. No second compression engine or Tide scheduler was
added to SPP.

## One canonical payload

New authenticated browser imports use `WorkspaceScope.ImportCanonicalAsync`.
One payload per SHA-256 per workspace is stored at `.lightning/originals/<hash>`.
Visible filenames are small references at
`documents/<hash>/<filename>.reference.json`; workflow tools use the logical
`documents/<hash>/<filename>` path and `WorkspaceScope.ReadAsync` checks the
reference, length and actual content hash. Multiple names for the same bytes
share the object. Protected directories, links and traversal remain rejected.

`documents.file-classified` creates only a category reference under
`reports/classified/<category>/<hash>/<filename>.reference.json`. Its output
`documentPath` identifies the document to read/link; `path` identifies the reference.
Classification and CRM metadata point to the same canonical hash. Metadata,
audit, journal and reference files are separate from document payloads.

The existing legacy `ImportAsync` API and existing files are preserved for
compatibility. They can retain old physical copies; this change does not claim
that every historical workspace has been deduplicated. No original or backup is
deleted automatically. Migration requires inventory, a recoverable backup,
reference/hash verification and a separately verified switch before deleting
redundant legacy files. Different workspaces deliberately do not share private
objects across owners.

## CRM metadata and classification

CRM JSON records remain under `.lightning/crm`, preserving the existing category
layout. An exclusive OS file lease serializes cooperating local readers/writers.
A flushed transaction journal precedes record and audit publication. Reads and
startup replay a pending transaction before exposing records. Changed-customer
updates compare the previous serialized record hash; conflicting records,
corrupt JSON, corrupt journals and linked paths fail explicitly. Cancellation
before commit writes nothing; a committed transaction finishes publication.
Recovery fixtures exercise interruption states; no physical power-loss or
distributed-storage guarantee is claimed. Back up the entire private workspace,
including a pending journal. Never delete a journal to make startup pass.

The record reader has a 1 MiB per-record limit and category snapshot limits of
50,000 records / 64 MiB. These are fail-closed budgets, not measured capacity.
`batched` mode and the three CRM batch controls are rejected because they have no
implemented runtime. Tide's own bounded queue remains a separate actual contract.

Workflow tools expose customer creation/list/detail/status, contact creation,
actual document linking and summary counts. Contact creation is not replay safe;
reconcile an interrupted attempt before resubmission. Browser requests require
an authenticated owner, CSRF and explicit `allowCrm`; each run checks consent
again. The CLI requires `--allow-crm`. Account identity still uses the existing
Longo-backed customer store; business CRM records do not replace login accounts.

`documents.classify` takes `path` and `taxonomy` (a reviewed imported JSON).
It uses real UTF-8 text, existing PDF extraction/OCR, or enabled raster OCR.
The report records taxonomy name/version/hash, source hash, matched terms,
rule score, fallback and truncation. It never claims a rule score is AI accuracy.
Every result requires review. The operator's generic template is not customer
validation. Large work remains split into the existing 256-step checkpointed
plans; no bulk-throughput or real-model classification acceptance is claimed.

## Existing Stigma Film Memory integration

Workflow persistence calls `StigmaMemoryFilmPipeline.RecordAsync`, using the
existing FluidEngine module and private workspace persistence callback.
One record per job/step lives under `.lightning/film`; an unchanged terminal
record is retained without another event copy. A failed step that later succeeds
updates that same record. Fields are job/step IDs, actual state, runtime hash,
result hash, source-hash-set fingerprint, verification label and recording time.
Raw documents, filenames, prompts and customer details are excluded. This is
local execution metadata; it is not the signed distributed Tide protocol.
It shares workspace retention/access policy with job state; no independent TTL
or forensic-erasure claim is introduced. Keep workspace storage private and
encrypted for sensitive data. Film persistence errors propagate; they do not
manufacture successful evidence.

The older `ExecuteAsync(byte[])` overload remains transient FluidLayer storage
for compatibility. New document workflows do not send their payloads into it.

## Verified upstream source, not an invented substitute

Inspected `clisonix/clisonix.com` revision
`a5587565675e7e934a29e356d6e2b3f8bd312de5`, reached through the old
`Web8kameleon-hub` repository URL:

- `packages/nanogrid/README.md` identifies the reusable framing/CBOR/HMAC/CRC
  package. It does not establish a document-database API.
- `nanogrid/protocol/src/storage_engine.rs` contains an in-memory index and a
  TODO for loading it on restart. It cannot currently replace durable CRM storage.
- `services/tide_runtime/README.md` describes the real single-node SQLite queue,
  verified zlib compression, hash acknowledgement, cleanup and drain lifecycle.
  Its token is operator-wide, not customer isolation. Reuse this service through
  its authenticated contract; do not expose it as a public multi-tenant backend.
- `docs/architecture/TIDE_BATICA_ZBATICA_TARGET_ARCHITECTURE.md` distinguishes
  the implemented single-node slice from planned distributed scheduling.

A concrete defect was reproduced in the actual upstream
`nanogridata_protocol_v1.py`: the header declares 14 bytes and passes nine fields
to `struct.pack`, but its format `>BBBBBHIH` accepts eight fields and is 13 bytes.
The actual source raises `struct.error`; it was not used as a working SPP codec.
The two-site correction is retained in
[nanogridata-header.patch](bridges/nanogridata-header.patch). The patch passed
`git apply --check` against the inspected source and its corrected actual
`NanogridataHeader` passed a 14-byte encode/decode round-trip. It has not been
integrated or deployed upstream, and header verification is not full codec,
cryptography or service acceptance.

No NanoGrid/Tide configuration keys were found in the local `.env`,
`.env.production` or `.env.build` files inspected (existing files only).
The source service's loopback address on its host does not prove local
availability or authorize inventing runtime credentials. Live authenticated
integration and upstream codec acceptance remain required before enabling a
hybrid remote backend. There is no silent fallback pretending to be NanoGrid.
