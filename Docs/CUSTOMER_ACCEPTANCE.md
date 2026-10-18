# Customer delivery acceptance — 2026-10-18

This record separates exercised behavior from prerequisites that still block
acceptance. The owner's Stripe payment is an internal production test, not
evidence of an external customer or customer satisfaction.

## Account and license delivery

- Production identity was `ACTIVE` with `emailVerified=false`. An active record
  is not a verified login and is separate from the shared workspace license.
- A real sender test to the owner's lowercase address was provider-accepted
  (HTTP 200); its uppercase equivalent was rejected (HTTP 403). Identity remains
  case-insensitive; the delivery adapter now uses its canonical lowercase form.
- Commit `159a726` passed CI/CD and deployed. Mixed-case registration for the
  existing owner returned 200 after deployment; the verification email was
  accepted for sending. Password recovery was requested through the public API.
- Inbox arrival, the owner's use of the verification/reset links and subsequent
  authenticated use remain separate acceptance steps. No verification flag or
  password was changed directly in production state.
- A real send from `noreply@scanprocessprint.com` was rejected with HTTP 403:
  Resend reported that this domain is not verified. Runtime still uses
  `onboarding@resend.dev`, which permits only its owner's testing address.
  Public customer registration is **not accepted** until the sender domain is
  verified in the actual Resend account, the private runtime sender is updated,
  and another controlled recipient completes the flow.
- The configured provider key is send-only. Domain administration and inbox
  verification cannot be established using that key. No secrets belong here.

## Runtime distribution

The packaging scripts build self-contained `win-x64` ZIP and `linux-x64` tar.gz
archives with component license notices, per-file hashes and archive SHA-256
sidecars. The verifier extracts the archive, checks its manifest, executes the
runtime, creates a PDF and exercises the document measurement command. Linux
validation uses Poppler for actual PDF processing. See [INSTALLATION.md](INSTALLATION.md).

The local checks exercised Windows 11 and an Ubuntu 24.04 runtime-dependencies
container without an installed .NET SDK/runtime. They do not establish signed
Windows installation, every Linux distribution, physical scanner/printer
compatibility or installation by an external customer. GitHub workflow results
and published assets must be checked before claiming public availability.

## Document and performance evidence

`benchmark-documents` records SHA-256 identities, OS/runtime, input/output sizes,
all measured durations, median and maximum time, page count and first-page RGB
change. It validates every generated PDF page outside the timed interval.
One warm-up is excluded; 1–10 measured runs are supported. Existing reports and
input documents are not overwritten. Rejected documents produce failure entries
and a nonzero exit status.

Current exercised examples use repository images and generated PDF test fixtures.
They are not a representative customer corpus. RGB differences quantify change,
not OCR accuracy or perceptual quality. Rasterized PDF output does not preserve
selectable text, vectors, forms or PDF metadata; output can grow substantially.
AI inference is disabled in the measured demo pipeline.

For acceptance by a customer, record their authorized document corpus, required
features, expected output, tolerance for changed content, operational time/size
limits, observed failures and explicit acceptance of the delivered output.
Keep private files and customer identifiers out of this repository. No external
customer acceptance is asserted by this record.
