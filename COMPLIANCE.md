# Compliance & standards — current status

## Company information

**ABA GmbH**
Ledjan Ahmati
Wattenscheider Hellweg 199
44869 Bochum, Germany
HRB 21069 (Registergericht Bochum)
Web: https://aba-gmbh.de
E-mail: clisonix@pm.me

---

## Status statement

Earlier versions of this document listed ISO 9001, ISO 27001, ISO 13485, SOC 2,
MDR/CE marking, FDA 510(k) and HIPAA as certified or in progress. **That was
incorrect.** No certificate, notified-body decision, FDA clearance or audit
report exists in this repository, and none may be claimed until the evidence
exists.

The accurate position:

| Standard / regulation | Status |
| --- | --- |
| ISO 9001:2015 — Quality management | Not certified. No QMS, no internal audit. |
| ISO 27001:2022 — Information security | Not certified. Technical controls exist; no ISMS. |
| ISO 13485:2016 — Medical device QMS | Not certified. Prerequisite for MDR. |
| ISO 14971:2019 — Risk management | Partial. A working risk register exists; no approved risk file. |
| IEC 62304 — Software lifecycle | Partial. Tests and architecture exist; no safety classification or traceability matrix in auditable form. |
| IEC 62366-1 — Usability | Not started. |
| MDR 2017/745 (CE) | Not applied. No notified body, no DoC, no clinical evaluation. |
| FDA 510(k) | Not submitted. |
| HIPAA | Not attested. Technical controls only; agreements and audit are operator responsibilities. |
| GDPR | Not certified. Local processing; legal duties remain with the operator. |

## What does exist

A compliance readiness dossier with explicit gaps, owners and evidence:

- [Docs/COMPLIANCE/README.md](Docs/COMPLIANCE/README.md) — status and certification roadmap
- [Docs/COMPLIANCE/GAP_ANALYSIS.md](Docs/COMPLIANCE/GAP_ANALYSIS.md) — requirement-by-requirement gaps
- [Docs/COMPLIANCE/RISK_REGISTER.md](Docs/COMPLIANCE/RISK_REGISTER.md) — risk register with residual risk
- [Docs/COMPLIANCE/SOUP_INVENTORY.md](Docs/COMPLIANCE/SOUP_INVENTORY.md) — third-party components and their gaps
- [Docs/COMPLIANCE/SECURITY_CONTROLS.md](Docs/COMPLIANCE/SECURITY_CONTROLS.md) — controls mapped to code and tests
- [Docs/COMPLIANCE/TRACEABILITY.md](Docs/COMPLIANCE/TRACEABILITY.md) — requirement to test matrix

## Why this matters commercially

A customer in a regulated market asks three questions:

1. *Which certificate covers this product?* — Today: none.
2. *What does the risk file say?* — Today: a working register, not an approved file.
3. *Who validated clinical accuracy?* — Today: nobody.

Answering those honestly is the condition for eventually acquiring the
certificates. Claiming them early is a contractual and legal exposure.

## Medical use

**Medical use requires clinical validation and legal certification for the market
where the product is distributed.** Until then the product must be described as a
document/image processing toolkit, not as a medical device.

## Reporting and contact

- Security issues: `security@aba-gmbh.de` — see [SECURITY.md](SECURITY.md)
- Compliance questions: `compliance@aba-gmbh.de`
- Regulatory affairs: `regulatory@aba-gmbh.de`

---

**Document ID:** COMP-001
**Version:** 3.14.1
**Effective:** 2026-11-03
**Classification:** Public
