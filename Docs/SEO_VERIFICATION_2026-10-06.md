# Product identity in search

The homepage title now starts with Lightning SPP, the visible H1 names the
product, and the footer links to Clisonix Cloud. The existing processing and
benchmark claims were preserved. No new speed, certification or feature
claims were introduced.

Verified locally on 2026-10-06:

- `node tools/check-seo.mjs`: all seven public pages passed title uniqueness,
  descriptions, self-canonical URLs, main headings, JSON-LD parsing and sitemap
  membership checks.
- `dotnet build wwwmmm.csproj -c Release`: passed, zero warnings/errors.
- Full xUnit suite: 44 passed, zero failed/skipped.
- Candidate HTTP: homepage, features, technology, try, pricing, benchmarks,
  Swagger, robots, sitemap and health all returned 200. The new branded title
  was verified over HTTP.

Production already returned HTTP 200 for the inspected marketing pages,
robots and sitemap and supplied canonical URLs and structured data. These
changes strengthen product identity; they do not prove indexing or improve
a measured ranking. The user confirmed Search Console verification for both
domains. Use URL Inspection to inspect the live page, selected canonical and
indexing exclusion reason after deployment.

Deployment must follow `deploy/production/deploy.sh` with its candidate health
checks and rollback path. These local checks do not mean this revision has
been deployed. Keep benchmark measurements tied to their hardware, dataset
and repetition count.
