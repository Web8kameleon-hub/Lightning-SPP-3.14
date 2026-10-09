# Longo source provenance

Extracted from `Web8kameleon-hub/clisonix.com`, commit
`6a3724508d4f7592c033ec8814d007fb3e1008fc`, directory `java-longo/`.
The source repository remains unchanged. This code retains the source owner's
proprietary status; Lightning's MIT license does not relicense this directory.

Lightning adds a storage writer lock and an HTTP compare-and-swap operation for
one customer aggregate. Identity, paid receipts, license grants and notification
state therefore commit through Longo's real synced disk persistence together.
The service must have a private network and a persistent volume; never expose
its HTTP port publicly. Only one service may own a storage file.

The aggregate is bounded by Longo's 8 MiB request limit. Capacity monitoring,
backup/restore verification and a partitioning strategy are required before
growth reaches this bound. There is no SQL or memory fallback on storage failure.
