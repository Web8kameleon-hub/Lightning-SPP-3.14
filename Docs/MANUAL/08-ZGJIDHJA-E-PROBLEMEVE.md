# 08 — Zgjidhja e problemeve

Diagnostikim sipas simptomës. Për çdo rast: shkaku i mundshëm, verifikimi dhe
veprimi. Nuk ka "provo dhe shiko" pa hipotezë.

## 8.1 Build dhe nisje

| Simptomë | Shkak i mundshëm | Verifikim | Veprim |
| --- | --- | --- | --- |
| Build dështon te ImageSharp | `SIXLABORS_LICENSE` mungon | `echo $env:SIXLABORS_LICENSE` | Ngarko `.env.build` në seancë |
| `Load-BuildEnvironment.ps1` bllokohet | Politika e ekzekutimit | Mesazhi i politikës | Ngarko variablin manualisht; mos e çaktivizo politikën përgjithmonë |
| `InvalidOperationException: Production requires ...` | Mungon sekret ose `LSPP_DATA_DIR` | Teksti i plotë i gabimit | Vendos të tre kërkesat e §2.1 |
| `LSPP_DATA_DIR` refuzohet | Rrugë relative | Vlera e variablit | Përdor rrugë absolute |
| Nisja vdes në heshtje në systemd | PATH minimal, mungon Poppler | `journalctl -u lightning -n 100` | Vendos `SPP_PDFINFO_PATH`/`SPP_PDFTOPPM_PATH` absolute |
| `WWWMMM_STIGMA_LEVEL must be 1, 2, or 3` | Vlerë e pavlefshme | Vlera aktuale | Korrigjo; nuk ka fallback |

## 8.2 Përpunimi

| Simptomë | Shkak | Verifikim | Veprim |
| --- | --- | --- | --- |
| `PDF input must be rendered as a real page first` | U dha PDF si imazh | Zgjatja e skedarit | `document pdf-render` pastaj `scan` |
| `Input is not a supported image` | Payload binar ose i dëmtuar | `file <path>` | Përdor imazh real |
| `InvalidDataException: Image input is empty` | Skedar 0 bajt | `ls -l` | Rikrijo input-in |
| Output më i vogël se pritej | Profili Stigma e kufizon DPI-n | `echo $WWWMMM_STIGMA_LEVEL` | Profil `1` për cilësi, `3` për shpejtësi |
| Motor tjetër nga sa pritej | `WWWMMM_IMAGE_ENGINE` | Vlera e variablit | Zgjidh `Skia` ose `ImageSharp`; nuk ka fallback |
| `Parallel(..., 0)` var | Vlerë e pavlefshme paralelizmi | Kodi thirrës | Jep ≥ 1; refuzimi është i qëllimshëm |

## 8.3 Dokumente dhe PDF

| Simptomë | Shkak | Verifikim | Veprim |
| --- | --- | --- | --- |
| `pdf-render` dështon "tool not found" | Poppler mungon | `which pdfinfo pdftoppm` | Instalo `poppler-utils` |
| `pdf-render` dështon në systemd por punon në shell | PATH i shërbimit | `systemctl show -p Environment lightning` | Vendos rrugët absolute në env |
| `At most 32 images are supported` | Kufi i `pdf-create` | Numri i argumenteve | Ndaj në disa PDF |
| `Existing output files are never overwritten` | Skedari dalës ekziston | `ls` | Zgjidh emër të ri; mos fshi pa arsye |
| PDF hapet por faqja është bosh | Faqe jashtë intervalit ose PDF i dëmtuar | `pdfinfo` | Kontrollo numrin e faqeve |

## 8.4 Shërbimi HTTP

| Simptomë | Shkak | Verifikim | Veprim |
| --- | --- | --- | --- |
| Lidhja refuzohet në `localhost` | Bind vetëm në `+` ose port tjetër | `ss -ltnp` | Vendos `LSPP_BIND_HOST` dhe portin e saktë |
| Nuk arrihet nga jashtë | Bind në `localhost` | Njësoj | Proxy në loopback; mos e hap portën direkt |
| `429` për të gjithë klientët njëkohësisht | Proxy headers pa `LSPP_TRUSTED_PROXY_IPS` | Logjet e limiter-it | Vendos IP-të e proxy-t |
| `503` te `/api/workflow/*` | Workflow i fikur | `LSPP_WORKFLOW_ENABLED` | Aktivizo vetëm pas pranimit |
| `403 workspace_access_denied` | Email nuk është në listë | `LSPP_WORKFLOW_ALLOWED_EMAILS` | Shto email-in e verifikuar |
| `401` në admin edhe me token | Token i ndryshëm/mungon header | Krahaso vlerën | Përdor `Authorization: Bearer ...` |
| CORS bllokon shfletuesin | Origin mungon në listë | `LSPP_ALLOWED_ORIGINS` | Shto domenin real, pa wildcard |
| Trupi refuzohet | > 1 MiB (ose > 8 MiB te `/api/try`) | Madhësia e kërkesës | Zvogëlo ose ndaj |

## 8.5 Llogaria dhe email-i

| Simptomë | Shkak | Verifikim | Veprim |
| --- | --- | --- | --- |
| Regjistrimi "dërgoi" por s'erdhi email | Provider i paverifikuar | `SMTP_FROM`, `SMTP_HOST` | Konfiguro dërgues të verifikuar |
| `LONGO_URL is not configured` | Mungon variabli | Vlera | Nis Longo-n dhe vendos URL-në |
| Hyrja kthen 401 me fjalëkalim të saktë | Email i paverifikuar | `code` në përgjigje | Verifiko email-in; mos e "rregullo" duke e anashkaluar |
| Rikthimi i fjalëkalimit nuk punon | Token i skaduar/përdorur | Koha e kërkesës | Kërko token të ri |
| Sesioni mbetet i hapur pas daljes | Cache i shfletuesit | Provë në dritare private | Rifresko; kontrollo revokimin |

## 8.6 Pagesa

| Simptomë | Shkak | Verifikim | Veprim |
| --- | --- | --- | --- |
| Webhook 400 | Nënshkrim i pavlefshëm | Logjet e verifikimit | Kontrollo sekretin e duhur (legacy/snapshot/thin) |
| Event i përpunuar dy herë | Dedup nuk u regjistrua | Gjendja e eventit | Kontrollo ruajtjen; mos e riprovo verbërisht |
| `LSPP_STRIPE_PLANS is not configured` | Mungon hartëzimi | Vlera | Vendos hartëzimin plan→çmim |
| Çelësi test në production | `STRIPE_SECRET_KEY` pa `sk_live_` | Nisja e shërbimit | Në production pranohet vetëm `sk_live_`/`rk_live_` |

## 8.7 Bridges

| Simptomë | Shkak | Verifikim | Veprim |
| --- | --- | --- | --- |
| `Configure SPP_EXCEL_CORE_URL ...` | URL mungon | Vlera e variablit | Vendos URL reale me HTTPS |
| Dështon menjëherë | URL me query/kredenciale/fragment | Vlera | Përdor bazë të pastër |
| Timeout 30 s | Upstream i ngadaltë/i ndalur | Health i upstream | Mos e rrit verbërisht; rregullo upstream |
| `Invalid telemetry JSON` | Fusha që mungon ose e panjohur | Skedari | Plotëso të gjitha fushat e detyrueshme |
| Ingest "sukses" por nodi s'u lexua | Ack nuk përputhet | `node-read` | Trajto si dështim; mos e konsidero të kryer |
| `WinError 32` në Excel (upstream) | Workbook i hapur gjatë unlink | Logjet e shërbimit Excel | Apliko patch-in upstream `Docs/bridges/clisonix-excel-cleanup.patch` |

## 8.8 Workflow

| Simptomë | Shkak | Verifikim | Veprim |
| --- | --- | --- | --- |
| `Plan exceeds resource budget` | Plan > 1 MiB | Madhësia | Ndaj planin |
| `Unknown tool` | Mjet i pashpallur | Lista e mjeteve | Regjistro mjetin ose korrigjo planin |
| Cikël varësish | Plan i pavlefshëm | Grafi i hapave | Rregullo varësitë |
| `130` pas Ctrl+C | Anulim i qëllimshëm | Statusi | Inspekto gjendjen para `resume` |
| Rezultat i ndryshëm pas rifreskimit | Runtime/contract hash ndryshoi | Hash-et e job-it | Kërko plan të ri të shqyrtuar |

## 8.9 Diagnostikim sistematik

1. **Lexo gabimin e plotë**, jo vetëm rreshtin e parë: shkaku i vërtetë zakonisht
   është në mes të stack-ut.
2. **Gjej skedarin dhe rreshtin**, pastaj kodin që e hedh.
3. **Formulo hipotezë** pse dështon.
4. **Ndrysho një gjë**, ribëj provën.
5. **Ndal:** mos e fshih gabimin me try/catch; rregullo shkakun.

## 8.10 Komanda që nuk është zgjidhje

- `--force`, çaktivizim kontrolli, rritje kufiri, ose grant shtesë leje për të
  "kaluar" një provë.
- Printim i sekretit për verifikim.
- Anashkalim i nënshkrimit të webhook-ut.
- Heqje e dedup-it sepse "ndryshimi nuk po aplikohej".
- Përjashtim antivirus për ta quajtur një test të kaluar.

Këto nuk zgjidhin; e zhvendosin rrezikun te klienti.
