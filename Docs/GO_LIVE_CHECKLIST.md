# Lightning SPP 3.14 — Go-Live Checklist (Tech + Business)

Përdor këtë listë si **go/no-go gate** para publikimit në production.

## 1) Release Integrity

- [ ] Branch/commit i release është i ngrirë dhe i identifikuar me SHA.
- [ ] `main` është sinkron me `origin/main` dhe pa ndryshime lokale.
- [ ] CI është **green** (build, tests, release-readiness workflow).
- [ ] `release-notes-v3.14.1.md` dhe `Docs/RELEASE_EVIDENCE.md` janë të përditësuara.

## 2) Functional Validation

- [ ] `dotnet test ./Tests/LightningSPP.Tests/LightningSPP.Tests.csproj` kalon.
- [ ] `dotnet run --project ./wwwmmm.csproj -- serve 8080` nis pa gabime kritike.
- [ ] `GET /api/health` kthen `ok=true`.
- [ ] `GET /api/metrics` dhe `GET /api/alerts` përgjigjen normalisht.
- [ ] Landing (`/`) dhe dashboard (`/dashboard`) hapen dhe CTA-t kryesore funksionojnë.

## 3) Licensing & Billing (Production)

- [ ] `LSPP_ADMIN_TOKEN` është vendosur në secret manager (jo në repo).
- [ ] `LSPP_LICENSE_SIGNING_KEYS` dhe `LSPP_LICENSE_SIGNING_ACTIVE_KID` janë valide.
- [ ] `/api/license/signing-status` kthen status të autorizuar me Bearer token admin.
- [ ] Stripe keys + webhook secrets (`legacy/snapshot/thin`) janë konfiguruar.
- [ ] Replay protection webhook është verifikuar (event dedup).

## 4) Cloudflare + Origin Security

- [ ] DNS për `scanprocessprint.com` dhe `www` është korrekt.
- [ ] Cloudflare `SSL/TLS = Full (strict)` dhe origin cert është valide.
- [ ] `Always Use HTTPS` aktiv.
- [ ] `LSPP_TRUST_PROXY_HEADERS=true` është aktiv në production.
- [ ] Nginx forwardon `CF-Connecting-IP` (ose ekuivalent) te aplikacioni.
- [ ] Endpoint-et admin (`/api/license/issue`, `/api/license/signing-status`) testohen me auth.

## 5) Runtime Hardening

- [ ] Service restart policy dhe boot persistence (`systemd`) aktive.
- [ ] Firewall lejon vetëm portat e nevojshme (80/443; SSH i kufizuar).
- [ ] Log rotation aktive (`InternalAuditLogger` + host logs).
- [ ] Nuk ka sekrete hardcoded në skedarë deploy/config.

## 6) Observability & SLO Readiness

- [ ] Baseline metrika: success/failure rate, p50/p95/p99 latency, throughput.
- [ ] Alerting pragje të përcaktuara për `pipelineFailure` dhe degradim performance.
- [ ] Dashboard operacional ka akses për ekipin on-call.
- [ ] Shih [Docs/SLA_SLO.md](/Docs/SLA_SLO.md) për SLI/SLO/SLA konkrete, error budget, dhe planin e eskalimit.

## 7) Data & Recovery

- [ ] Backup policy ekziston (database/artifacts + retention).
- [ ] Restore test është provuar të paktën 1 herë para go-live.
- [ ] Procedurë rollback e dokumentuar dhe testuar (rollback target + owner) — shih `deploy/production/deploy.sh rollback` dhe [deploy/production/README.md](/deploy/production/README.md).

## 8) Legal/Customer-Facing Claims

- [ ] Claims në UI janë vetëm të verifikuara nga benchmark/prova reale.
- [ ] Privacy/Terms/Security links nuk janë placeholder.
- [ ] Çmimet/licencat në UI përputhen me modelin real të biznesit.

## 9) Go/No-Go Sign-Off

- [ ] Engineering lead: ✅
- [ ] Security owner: ✅
- [ ] Product/Business owner: ✅
- [ ] Support/Operations owner: ✅

## Quick Verification Commands

```bash
dotnet test ./Tests/LightningSPP.Tests/LightningSPP.Tests.csproj
dotnet run --project ./wwwmmm.csproj -- serve 8080
curl http://localhost:8080/api/health
curl http://localhost:8080/api/metrics
```

Nëse një nga seksionet 1–5 dështon, release është **NO-GO** deri sa të korrigjohet.
