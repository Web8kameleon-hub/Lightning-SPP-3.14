# 07 — Operimi dhe rikuperimi

Runbook-u që përdoret në turnin e natës, nën presion, pa menduar. Çdo hap ka
komandë dhe provë.

## 7.1 Inventari i mjedisit

Mbaj të shkruar, jashtë repo-s, për çdo mjedis:

| Zë | Shembull |
| --- | --- |
| Host-i | `hel-core-01` |
| Commit / image digest | `c1183ee` / `sha256:...` |
| Porta e aplikacionit | `13080` (compose) / `11400` (systemd) |
| `LSPP_DATA_DIR` | `/var/lib/lightning` |
| Longo | `/opt/scanprocessprint/longo`, UID 10001, `0700` |
| Edge | nginx / Cloudflare |
| Pronar i rollback-ut | emri i personit |

Pa këtë inventar, çdo incident fillon me hamendje.

## 7.2 Deploy — Docker Compose

```bash
cp .env.example .env                 # vetëm herën e parë
openssl rand -hex 32                 # LSPP_ADMIN_TOKEN
openssl rand -hex 32                 # LSPP_LICENSE_SIGNING_KEY
chmod 600 .env
docker compose config --quiet
docker compose up -d --build --wait
curl -fsS http://localhost:13080/api/health
```

Rregulla:

- `LSPP_LICENSE_SIGNING_KEY` mbetet **i njëjti** ndërmjet përditësimeve;
  ndryshimi i tij shkëput licencat e ruajtura.
- Hostname-i dhe përdoruesi mbeten të njëjtë: enkriptimi i skedarit të licencës
  varet prej tyre.
- `docker compose down -v` **NDALOHET** gjatë përditësimit: fshin volumin
  `lightning-data` me licenca dhe audit log.
- Compose publikon vetëm në `127.0.0.1`; shërbimi nuk punon si root.

## 7.3 Deploy — blue-green (prodhimi aktual)

```bash
deploy/production/deploy.sh deploy <commit-sha>   # ndërton, verifikon, promovon
deploy/production/deploy.sh status                 # release aktiv / i mëparshëm
deploy/production/deploy.sh rollback               # kthehet te i mëparshmi
```

Rendi i sigurt që skripti zbaton dhe që duhet respektuar edhe manualisht:

1. Ndërto release-in e ri **krah** atij aktiv.
2. Verifiko direkt (pa edge): health, të gjithë asetet statike, vlefshmëria e
   licencës, refuzimi 401 i administrimit të paautorizuar.
3. Vetëm pastaj ndërro `spp_backend` dhe `nginx -s reload` (graceful).
4. Riprovo përmes domenit publik.
5. Lësho release-in e vjetër vetëm pas kullimit të kërkesave.

Nëse një hap dështon, release-i i ri çmontohet ose edge-i kthehet mbrapsht:
prodhimi mbetet ashtu si ishte.

**Parakusht i verifikuar një herë:** `BACKEND_LINE_REGEX` në `deploy.sh` duhet të
përputhet me rreshtin real `spp_backend` në konfigurimin e edge-it. Ky është
supozim "best effort" dhe duhet konfirmuar nga operatori me akses shell.

## 7.4 Deploy — Ubuntu systemd

```bash
sudo install -m 0600 /dev/stdin /etc/lightning.env   # LSPP_ADMIN_TOKEN, LSPP_LICENSE_SIGNING_KEY
deploy/ubuntu/deploy-app.sh
curl -fsS http://127.0.0.1:11400/api/health
```

- Publikimi: `/opt/lightning/publish`; gjendja e shkrueshme:
  `/var/lib/lightning`.
- Skripti bën fast-forward dhe ndalon në ndryshime divergjente: nuk i hedh
  punët lokale.
- Rollback: rikthe drejtorinë e mëparshme të publikimit dhe
  `systemctl restart lightning`.

## 7.5 Verifikimi pas çdo deploy-i (5 minuta)

```bash
# 1) health
curl -fsS https://$DOMAIN/api/health

# 2) statike
for p in / /features /technology /pricing /dashboard /account /try /swagger; do
  printf '%s ' "$p"; curl -s -o /dev/null -w '%{http_code}\n' "https://$DOMAIN$p"
done

# 3) admin pa token duhet 401
curl -s -o /dev/null -w '%{http_code}\n' -X POST \
  "https://$DOMAIN/api/license/issue" \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  --data 'email=x@example.com&company=X&tier=free'

# 4) metrika dhe alarme
curl -fsS https://$DOMAIN/api/metrics
curl -fsS https://$DOMAIN/api/alerts
```

Regjistro rezultatet (data, commit, statuset). Një deploy pa këto prova nuk
konsiderohet i kryer.

## 7.6 Backup

Çka duhet të hyjë në backup:

| Objekt | Pse |
| --- | --- |
| `LSPP_DATA_DIR` | Identitet, entitlement, workspace, audit |
| Longo (volum / `/opt/.../longo`) | Gjendja e llogarive dhe pagesave |
| Data-protection keys | Pa to, cookie-t dhe token-at e ruajtur bëhen të palexueshëm |
| `.env` (i enkriptuar) | Rikuperim konfigurimi |
| `active-release.env` | Cili release është aktiv |

Rregulla:

- Backup i enkriptuar, jashtë host-it, me leje të kufizuara.
- Ruaj hostname-in dhe përdoruesin bashkë me të dhënat: enkriptimi i licencave
  varet prej tyre; ndryshimi i tyre e bën gjendjen e restauruar të palexueshme.
- Testo restore **para** se të duhet. Një backup i paprovuar është hipotezë.

## 7.7 Restore

1. Ndalo shërbimin.
2. Rikthe `LSPP_DATA_DIR` dhe Longo në të njëjtat rrugë, me të njëjtin UID dhe
   leje.
3. Rikthe hostname-in/përdoruesin identikë.
4. Nis; verifiko `/api/health`, një hyrje llogarie reale në mjedis testi dhe
   `GET /api/license/status`.
5. Regjistro kohën e rikuperimit (RTO) dhe humbjen e pranuar (RPO).

**Nuk ka procedurë të miratuar për "rollback" të të dhënave të klientit.**
Rikthimi i të dhënave kërkon vendim të shqyrtuar; mos e bëj si hap rutine.

## 7.8 Matja e operimit (SLI/SLO)

`/api/metrics` jep:

- normën e suksesit të pipeline-it (bazuar në rezultatin e `ExecuteAsync`);
- latencën mesatare të scan/process/print.

Pragjet e brendshme (nga `Services/LightningMetrics.cs`, të dokumentuara në
`Docs/SLA_SLO.md`):

| Objektiv | Vlerë |
| --- | --- |
| Disponueshmëri | ≥ 99.5% |
| Scan mesatar | ≤ 25 ms |
| Process mesatar | ≤ 30 ms |
| Print mesatar | ≤ 25 ms |

Budxheti i gabimit për 99.5% në 30 ditë: **216 minuta**. Për SLA-në e
Enterprise (99.9%): **43.2 minuta**.

**Kufizim i njohur:** numëruesit janë në memorie dhe zerohen në rinisje; një
deploy i zëvendëson procesin dhe rifillon dritaren. Për histori afatgjata duhet
një backend metrikash që nuk ekziston në këtë kod. Mos raporto histori që nuk
është mbledhur.

## 7.9 Incidenti: rrjedha

| Fazë | Veprim | Provë |
| --- | --- | --- |
| Zbulim | `alertsActive=true` ose health i dështuar pas deploy-it | Dalja e komandës |
| Triazh | Cili SLI ra: disponueshmëri apo latencë; logjet rreth të njëjtit interval | Ekstrakt log |
| Zbutje | Nëse pas deploy-it → `deploy.sh rollback`. Përndryshe trajto si incident i release-it aktiv | `deploy.sh status` |
| Dokumentim | Fillimi/mbarimi, minutat e konsumuara nga budxheti | Shënim incidenti |
| Mësim | Test ose kontroll i ri që e kap herën tjetër | Commit |

## 7.10 Kufijtë e operimit që duhet thënë me zë

- Një instancë e vetme: gjendja e licencës dhe përdorimi janë lokale; nuk ka
  sinkronizim multi-node.
- Metrikat nuk mbijetojnë rinisjen.
- Email-i varet nga provider i verifikuar; "u dërgua" ≠ "u dorëzua".
- Printimi fizik nuk verifikohet nga sistemi.
- Longo është parakusht për llogari; pa të, regjistrimi dhe entitlement nuk
  punojnë.

## 7.11 Kalendari minimal

| Frekuenca | Detyra |
| --- | --- |
| Çdo deploy | Verifikimi i §7.5 |
| Ditor | Shiko `alerts` dhe regjistrin e auditimit të nënshkrimit |
| Javor | Testo një restore në mjedis të izoluar |
| Mujor | Rishiko çelësat, skadencat, aksesin e operatorëve |
| Çdo release | Lista e `06` §6.13 dhe `Docs/GO_LIVE_CHECKLIST.md` |
