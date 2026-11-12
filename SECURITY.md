# Security Policy

## Supported Versions

We release patches for security vulnerabilities for the following versions:

| Version | Supported          |
| ------- | ------------------ |
| 3.14.x  | :white_check_mark: |
| 3.x.x   | :white_check_mark: |
| 2.x.x   | :x:                |
| < 2.0   | :x:                |

## Reporting a Vulnerability

**ABA GmbH** takes the security of Lightning SPP 3.14 seriously. If you believe you have found a security vulnerability, please report it to us as described below.

### How to Report

**Please do NOT report security vulnerabilities through public GitHub issues.**

Instead, please report them via email to:

**Security Contact:**  
Email: security@aba-gmbh.de  
PGP Key: Available on request

**Company Information:**  
ABA GmbH  
Ledjan Ahmati  
Wattenscheider Hellweg 199  
44869 Bochum, Germany  
HRB 21069 (Registergericht Bochum)

### What to Include

Please include the following information:

- Type of issue (e.g., buffer overflow, SQL injection, cross-site scripting, etc.)
- Full paths of source file(s) related to the manifestation of the issue
- The location of the affected source code (tag/branch/commit or direct URL)
- Any special configuration required to reproduce the issue
- Step-by-step instructions to reproduce the issue
- Proof-of-concept or exploit code (if possible)
- Impact of the issue, including how an attacker might exploit it

### Response Timeline

- **Initial Response:** Within 48 hours
- **Triage:** Within 5 business days
- **Fix Development:** Based on severity (Critical: 7 days, High: 14 days, Medium: 30 days)
- **Public Disclosure:** After fix is released

### Security Update Process

1. **Receive Report** - Vulnerability reported via secure channel
2. **Validate** - Confirm the vulnerability exists
3. **Develop Fix** - Create patch or workaround
4. **Test** - Thoroughly test the fix
5. **Release** - Publish security update
6. **Announce** - Public security advisory

## Security Measures

### Data Protection

What the code actually implements (each item is verified elsewhere in this
repository; nothing below is aspirational):

- **Encryption at rest** — license state is stored with AES-256-GCM
  (`Licensing/LicenseManager.cs`). The key is derived from machine and user
  names, so this is confidentiality, not integrity: the signature is what
  proves authenticity.
- **Encryption in transit** — **not terminated by the application.** The HTTP
  listener speaks plain HTTP and TLS must be provided by the reverse proxy.
- **Authentication** — password (PBKDF2, Identity V3, 600,000 iterations) plus
  cookie sessions. **No multi-factor authentication is implemented.**
- **Authorization** — owner isolation, admin bearer token, workspace email
  allowlist. **There is no general RBAC role system.**
- **Audit logging** — issuance and signing events (`SigningAuditTrail`,
  `InternalAuditLogger`); not a centralised SIEM feed.
- **License signatures** — ECDSA P-256, verifiable with a public key alone
  (`Licensing/LicenseSignature.cs`).

### Medical device and regulatory status

No medical-device compliance is claimed. FDA 21 CFR Part 11, ISO 13485 and
IEC 62304 conformance are **not established** by this repository; HIPAA requires
agreements and audit that only an operator can provide. See
[Docs/COMPLIANCE/README.md](Docs/COMPLIANCE/README.md).

### Data privacy

- GDPR is **not certified**. The processing pipeline runs locally and does not
  send documents to external services.
- Deletion of intermediate artifacts is an operator responsibility; the product
  does not automate retention policy.
- Account data lives in `LSPP_DATA_DIR` and Longo with restricted permissions.

### Secure development

- **Code review** — required before merge (`AGENTS.md`).
- **Static analysis / dependency scanning** — **no automated process exists**
  for CVE monitoring; this is an open item in
  [Docs/COMPLIANCE/SOUP_INVENTORY.md](Docs/COMPLIANCE/SOUP_INVENTORY.md).
- **Penetration testing** — **none has been performed.** No external audit is
  recorded in this repository.

## Security Best Practices

### For Developers

1. **Never commit secrets** - Use environment variables or secure vaults
2. **Validate all inputs** - Prevent injection attacks
3. **Use parameterized queries** - Prevent SQL injection
4. **Keep dependencies updated** - Regular security patches
5. **Follow least privilege** - Minimal permissions required

### For Deployment

1. **Use HTTPS** - Encrypt all network traffic
2. **Enable firewall** - Restrict network access
3. **Regular updates** - Apply security patches promptly
4. **Monitor logs** - Watch for suspicious activity
5. **Backup data** - Regular encrypted backups

### For Medical Devices

Only applicable after clinical validation and regulatory clearance, neither of
which exists today:

1. **Validate environments** — hardware and OS configuration must be frozen and
   recorded per deployment.
2. **Network isolation** — do not place a processing node on a shared network.
3. **Access control** — verified accounts only; workspace access is an explicit
   allowlist.
4. **Audit trails** — retain signing and admin logs; they are not shipped
   off-host by the product.
5. **Incident response** — follow
   [Docs/MANUAL/06-RREGULLAT-E-SIGURISE.md](Docs/MANUAL/06-RREGULLAT-E-SIGURISE.md) §6.12.

## Known Security Considerations

### Image Processing

- **Input validation** - All image files validated before processing
- **Memory limits** - Protection against memory exhaustion
- **Timeout protection** - Prevent denial of service

### AI/ML Processing

- **Model validation** - Only trusted ML models loaded
- **Input sanitization** - All ML inputs validated
- **Output verification** - ML outputs checked for anomalies

### File Operations

- **Path traversal prevention** — static file paths are resolved and verified to
  stay inside `Web/wwwroot`.
- **File size limits** — 1 MiB request bodies, 8 MiB for `/api/try/*`, bounded
  bridge payloads.
- **Virus scanning** — **not implemented.** No antivirus integration exists in
  this codebase; operators must provide it at the perimeter.

## Compliance status

No certification is claimed. This repository contains **no auditable evidence**
of ISO 9001, ISO 27001, ISO 13485, SOC 2, MDR/CE marking, FDA 510(k) clearance
or HIPAA attestation. `README.md` states the same; earlier drafts of this file
claimed otherwise and were incorrect.

What does exist is a readiness dossier with an explicit gap analysis:

| Document | Content |
| --- | --- |
| [Docs/COMPLIANCE/README.md](Docs/COMPLIANCE/README.md) | Status statement and certification roadmap |
| [Docs/COMPLIANCE/GAP_ANALYSIS.md](Docs/COMPLIANCE/GAP_ANALYSIS.md) | Gap analysis against IEC 62304, ISO 14971, IEC 62366-1, ISO 13485, MDR, FDA, HIPAA, GDPR, ISO 27001 |
| [Docs/COMPLIANCE/RISK_REGISTER.md](Docs/COMPLIANCE/RISK_REGISTER.md) | Risk register with owners and residual risk |
| [Docs/COMPLIANCE/SOUP_INVENTORY.md](Docs/COMPLIANCE/SOUP_INVENTORY.md) | Third-party component inventory |
| [Docs/COMPLIANCE/SECURITY_CONTROLS.md](Docs/COMPLIANCE/SECURITY_CONTROLS.md) | Security controls mapped to their evidence |
| [Docs/COMPLIANCE/TRACEABILITY.md](Docs/COMPLIANCE/TRACEABILITY.md) | Requirement to control to test matrix |

Regulatory approval requires an external notified body or regulator, a quality
management system, clinical evaluation and post-market surveillance. None of
those can be produced by this repository alone.

## Security Contact

For any security-related questions or concerns:

**ABA GmbH - Security Team**  
Email: security@aba-gmbh.de  
Phone: +49 (0) 234 XXX XXXX  
Address: Wattenscheider Hellweg 199, 44869 Bochum, Germany

**Business Hours:** Monday - Friday, 9:00 - 17:00 CET  
**Emergency:** 24/7 via email

---

**Last Updated:** 2024  
**Document Version:** 3.14.0  
**Responsible:** Ledjan Ahmati, CEO - ABA GmbH

---

**Lightning SPP 3.14 — security posture documented, certification not claimed**
**© 2024 ABA GmbH. All rights reserved.**
