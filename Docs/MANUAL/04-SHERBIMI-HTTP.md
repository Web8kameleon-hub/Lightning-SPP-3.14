# 04 — Shërbimi HTTP

Referencë e sipërfaqes HTTP të ekspozuar nga `serve` (`Web/LicenseApiServer.cs`,
`Web/WorkflowApi.cs`, `Web/GuestAssistantApi.cs`, `Web/FluidApiHandler.cs`,
`Web/CustomerAccounts.cs`).

## 4.1 Modeli i sigurisë së sipërfaqes

- Serveri është `HttpListener` me prefiks `http://{LSPP_BIND_HOST}:{port}/`.
  TLS **nuk** kryhet nga aplikacioni; ai ofrohet nga reverse proxy (nginx,
  Cloudflare, ingress i platformës).
- Trupi maksimal i kërkesës: **1 MiB** (`MaxBodyBytes`), përveç rrugëve të
  `/api/try/*` që pranojnë deri **8 MiB**.
- Kufizim norme: **30 kërkesa/minutë për IP** për endpoint-et jo-publike;
  `/api/health` është i përjashtuar.
- Kufizim i veçantë për `/api/try/*`: **10 kërkesa/5 minuta për IP**, plus
  **një përpunim i vetëm njëkohësisht** (`SemaphoreSlim(1,1)`); kërkesa e dytë
  merr refuzim, nuk rradhitet pafund.
- CORS: vetëm origjinat në `LSPP_ALLOWED_ORIGINS`; bosh = asnjë.
- Admin: `Authorization: Bearer <LSPP_ADMIN_TOKEN>`, krahasim në kohë konstante
  (SHA-256 + `FixedTimeEquals`).

**Paralajmërim operativ:** nëse `LSPP_TRUST_PROXY_HEADERS=true` pa
`LSPP_TRUSTED_PROXY_IPS`, të gjithë klientët marrin IP-në e proxy-t. Kufiri 30/min
bëhet atëherë kufi **global** i shërbimit, dhe një klient i vetëm mund ta bllokojë
shërbimin për të gjithë. Vendos gjithmonë IP-të e proxy-t.

## 4.2 Faqet statike

| Rruga | Skedari | Qëllim |
| --- | --- | --- |
| `/` | `landing.html` | Faqja kryesore |
| `/features` | `features.html` | Aftësitë |
| `/technology` | `technology.html` | Teknologjitë |
| `/benchmarks` | `benchmarks.html` | Matjet |
| `/pricing` | `pricing.html` | Planet dhe termat |
| `/dashboard` | `dashboard.html` | Statusi i licencës së serverit |
| `/account` | `account.html` | Llogaria e klientit |
| `/workspace` | `workspace.html` | Workspace-i i dokumenteve |
| `/try` | `try.html` | Provë nga shfletuesi |
| `/pdf` | `pdf.html` | Faqja e produktit PDF |
| `/swagger` | `swagger.html` | Dokumentim interaktiv |
| `/banner` | `banner.html` | Material marketingu |
| `/openapi.json` | `openapi.json` | Kontrata OpenAPI |
| `/robots.txt`, `/sitemap.xml`, `/site.webmanifest` | — | SEO dhe PWA |

Rrugët statike mbrohen nga path traversal: rruga zgjidhet dhe verifikohet që
mbetet brenda `Web/wwwroot`.

## 4.3 Endpoint-et e API-së

### Publike / lexim

| Metoda | Rruga | Kthen |
| --- | --- | --- |
| `GET` | `/api/health` | `ok`, status i shërbimit; i përjashtuar nga limiter-i |
| `GET` | `/api/metrics` | SLI-t live: sukses pipeline, latenca për fazë |
| `GET` | `/api/alerts` | `alertsActive` sipas pragjeve SLO |
| `GET` | `/api/stigma/schema` | Skema Stigma |
| `GET` | `/api/license/status` | Gjendja e licencës së serverit dhe përdorimi |
| `GET` | `/api/assistant` | Gjendja e asistentit (kur është aktiv) |

`/api/metrics` dhe `/api/alerts` lexohen nga `Services/LightningMetrics.cs`.
Numëruesit janë **në memorie** dhe zerohen në çdo rinisje; një deploy me
zëvendësim procesi e rifillon dritaren e matjes. Kjo është e dokumentuar në
`Docs/SLA_SLO.md` dhe nuk është e njëjta gjë me histori metrikash afatgjatë.

### Kërkojnë Bearer admin

| Metoda | Rruga | Shënim |
| --- | --- | --- |
| `POST` | `/api/license/issue` | Emetim çelësi; form-encoded |
| `POST` | `/api/license/activate` | Aktivizim |
| `GET` | `/api/license/signing-status` | Statusi i çelësave të nënshkrimit |
| `POST` | `/api/stripe/recover` | Rikthim nga event i faturës |

Pa token, përgjigjja është **401**. Ky është kontrolli që duhet verifikuar pas
çdo deploy-i: një kërkesë e paautorizuar **duhet** të refuzohet.

### Webhook (nënshkrim, jo Bearer)

| Metoda | Rruga | Verifikim |
| --- | --- | --- |
| `POST` | `/api/stripe/webhook` | Nënshkrim Stripe me secreta legacy/snapshot/thin + dedup eventesh |

Webhook-u nuk pranon Bearer; ai verifikon nënshkrimin dhe regjistron eventin për
të mos e përpunuar dy herë. Event i përsëritur nuk duhet të krijojë dy
entitlemente.

### Provë nga shfletuesi (`/api/try/*`)

| Metoda | Rruga | Kufi |
| --- | --- | --- |
| `POST` | `/api/try/process` | 8 MiB, 10/5 min, 1 njëkohësisht |
| `POST` | `/api/try/package` | Njësoj |
| `POST` | `/api/try/archive` | Njësoj |

Këto rrugë janë të hapura për vizitorë (me aktivitet të regjistruar), prandaj
kufijtë janë pjesë e sigurisë, jo e rehatisë. Mos i rrit pa analizuar
abuzimin.

### Llogaria

| Rruga | Shënim |
| --- | --- |
| `/api/account/*` | Regjistrim, verifikim, hyrje, rikthim fjalëkalimi, licenca |
| `/api/assistant/conversation` | `POST`, kërkon sesion të vlefshëm |

Rrugët e llogarisë përdorin cookie sesioni, CSRF dhe validim origjine. Kërkesat
e identitetit **dështojnë të mbyllura** (fail closed) — nuk ka rrugë alternative
që i anashkalon.

### Workspace / workflow

| Rruga | Kusht |
| --- | --- |
| `/api/workflow/*` | Në production: `LSPP_WORKFLOW_ENABLED=true` **dhe** email i lejuar |
| `/api/fluid/*` | Pipeline Fluid; kërkon antiforgery në scope |

Pa `LSPP_WORKFLOW_ENABLED=true`, në production përgjigjja është **503** — jo
404, jo sukses i heshtur.

## 4.4 Statuset që duhet të dish

| Status | Kuptimi real |
| --- | --- |
| `200` | Sukses |
| `400` | Input i pavlefshëm (json, fusha, argument) |
| `401` | Pa token/ sesion; ose kredenciale të gabuara |
| `403` | I autentikuar por i paautorizuar; `workspace_access_denied` |
| `404` | Rrugë e panjohur |
| `429` | Limiter-i; prit dritaren, mos e anashkalo |
| `503` | Aftësia e fikur ose e pakonfiguruar (workflow, varësi) |

`403` me `authentication_required` do të thotë "hyr"; `403` me
`workspace_access_denied` do të thotë "hyrja nuk të jep akses" — përsëritja e
hyrjes nuk ndihmon.

## 4.5 Health, metrics, alerts: si t'i lexosh

```bash
curl -fsS https://example.com/api/health
curl -fsS https://example.com/api/metrics
curl -fsS https://example.com/api/alerts
```

- `health` duhet `200` + `ok=true`.
- `metrics` jep normën e suksesit dhe latencat mesatare për scan/process/print.
- `alerts` jep `alertsActive`. Sipas `Docs/SLA_SLO.md`, SLO-t e brendshme janë
  99.5% disponueshmëri dhe 25/30/25 ms mesatare; kalimi i tyre ndez alarmin
  para se SLA-ja e Enterprise (99.9%) të rrezikohet.

Këto pragje janë objektiva inxhinierike, jo garanci. Mos i citosh si SLA
përveç planit Enterprise, i cili është i vetmi me SLA kontraktuale sipas
`PRICING.md`.

## 4.6 Test i shpejtë i kufijve (para go-live)

```bash
# 1) publik
curl -fsS http://127.0.0.1:13080/api/health

# 2) admin pa token -> duhet 401
curl -s -o /dev/null -w '%{http_code}\n' \
  -X POST http://127.0.0.1:13080/api/license/issue \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  -d 'email=x@example.com&company=X&tier=free'

# 3) admin me token -> duhet 200 (vetëm në mjedis testi)
curl -s -o /dev/null -w '%{http_code}\n' \
  -H "Authorization: Bearer $LSPP_ADMIN_TOKEN" \
  http://127.0.0.1:13080/api/license/signing-status

# 4) CORS: origin i palejuar nuk duhet të marrë header allow-origin
curl -s -D - -o /dev/null -H 'Origin: https://evil.example' \
  http://127.0.0.1:13080/api/health
```

Hapi 2 dhe 4 janë provat që duhen ruajtur në evidencë pas çdo deployment-i.

## 4.7 Konfigurimi i proxy-t (nginx shembull)

```nginx
server {
    listen 80;
    server_name example.com;
    client_max_body_size 1m;

    location / {
        proxy_pass http://127.0.0.1:13080;
        proxy_set_header Host localhost;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Real-IP $remote_addr;
        add_header X-Content-Type-Options nosniff always;
    }
}
```

Pastaj TLS me `certbot --nginx -d example.com`. Porta e aplikacionit (13080)
mbetet e mbyllur nga jashtë; vetëm 80/443 ekspozohen.

Nëse edge-i është Cloudflare ose një nginx tjetër, vendos IP-të e tij në
`LSPP_TRUSTED_PROXY_IPS` dhe aktivizo `LSPP_TRUST_PROXY_HEADERS=true`.

## 4.8 Çka shërbimi HTTP nuk bën

- Nuk terminon TLS vetë.
- Nuk autentikon vizitorët e `/api/try/*` (për këtë kanë kufij).
- Nuk persiston metrika ndërmjet rinisjeve.
- Nuk printon fizikisht; `/api/workflow` printing kthen në adapterë CUPS të
  Linux-it, dhe një dorëzim në radhë nuk është provë e printimit.
</content>

</content>
