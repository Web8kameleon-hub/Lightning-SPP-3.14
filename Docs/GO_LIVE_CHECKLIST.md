# Go-Live Checklist (Market Readiness)

Status fields: `Owner` • `Due` • `Done (yes/no)` • `Evidence link`

## 1) Domain & Hosting Security
- [ ] Enable SSL/TLS certificate for `scanprocessprint.com`
- [ ] Enforce HTTPS redirect (HTTP -> HTTPS)
- [ ] Enable Domain Guard / transfer lock
- [ ] Validate DNS records (A/AAAA/CNAME/MX/TXT) against production plan

## 2) CI/CD Release Gates
- [ ] Require passing CI checks on `main` PRs (`build`, `test`, security scan)
- [ ] Block direct pushes to `main` (branch protection)
- [ ] Tag-based release process documented (`vX.Y.Z`)

## 3) Production Operations
- [ ] Backup + restore runbook tested (RTO/RPO documented)
- [ ] Monitoring + alerting enabled (API health, error rate, latency)
- [ ] Incident response contact/escalation path documented

## 4) Compliance & Claims
- [ ] Keep only verifiable compliance claims in public docs
- [ ] Mark pending certifications/approvals as in-progress with date
- [ ] Keep audit trail for license/security-sensitive operations

## 5) Release Readiness
- [ ] Final release checklist completed (test pass, rollback plan, owner approval)
- [ ] Versioned release notes published
- [ ] Post-release verification completed (health checks + smoke tests)

