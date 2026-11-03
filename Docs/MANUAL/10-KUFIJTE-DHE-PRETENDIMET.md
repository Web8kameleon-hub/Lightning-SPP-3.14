# 10 — Kufijtë dhe pretendimet

Ky dokument ekziston për një arsye: të pengojë që produkti të premtojë më shumë
sesa provon. Çdo fjali këtu mund të përdoret për të refuzuar një pretendim
marketingu ose një deklaratë në një ofertë.

## 10.1 Tabela e së vërtetës

| Aftësi | Gjendja reale | Prova |
| --- | --- | --- |
| Përpunim imazhi real | ✅ Punon | SkiaSharp, `RealImageProcessor` |
| Kodim PNG/JPEG/BMP/TIFF/PDF | ✅ Punon | `OutputAdapter`, teste |
| Artefakt lossless `.br` | ✅ Punon | Brotli, `print` |
| PDF nga imazhe | ✅ Punon | `document pdf-create` |
| Rendering PDF | ⚠️ Kërkon Poppler | `document pdf-render` |
| Skanim fizik | ❌ I paverifikuar | `device scan` vetëm Linux |
| Printim fizik | ❌ I paverifikuar | `device print`, radhë CUPS |
| OCR | ❌ Nuk ekziston | Asnjë motor OCR në kod |
| Inferencë AI me model real | ⚠️ Kërkon `LSPP_ONNX_MODEL_PATH` | `RealAIProcessor` |
| Workflow me plane | ⚠️ Funksionon; i fikur në prodhim | `/api/workflow`, 503 pa flag |
| Bridges Excel | ⚠️ Verifikuar lokalisht | `Docs/BRIDGES.md` |
| Bridges OS-CLX | ❌ Ingest real i paverifikuar | `Docs/BRIDGES.md` |
| Llogari klienti | ⚠️ Funksionon me Longo + SMTP të verifikuar | `CustomerAccounts` |
| Pagesa Stripe | ⚠️ Kod i plotë; pranimi i jashtëm i hapur | `StripeBilling`, webhook |
| Aktivizim offline i licencës | ✅ I mundur: nënshkrim ECDSA, verifikohet me çelës publik | `LicenseAsymmetricTests` |
| Certifikime ISO/CE/FDA/HIPAA | ❌ Pa evidencë në repo | `Docs/COMPLIANCE/README.md` |
| Përdorim mjekësor | ❌ Kërkon validim klinik | — |

Legjenda: ✅ provuar dhe i përdorshëm; ⚠️ i kushtëzuar; ❌ nuk provohet.

## 10.2 Fraza që NDALOHET të përdoren pa provë

Mos shkruaj dhe mos thuaj:

- "AI-powered" kur `LSPP_ONNX_MODEL_PATH` nuk është konfiguruar.
- "OCR" kur nuk ekziston asnjë motor OCR.
- "printon" për `print`, që prodhon një artefakt `.br`.
- "skanon dokumente" për `scan`, që lexon imazhe ekzistuese.
- "certifikuar ISO/CE/FDA/HIPAA" — nuk ka evidencë auditimi në këtë repository.
- "i gatshëm për mjekësi" — kërkon validim klinik dhe certifikim tregu.
- "zero-downtime" për çdo deploy; është e vërtetë vetëm për
  `deploy/production/deploy.sh`, jo për `docker compose up`.
- "SLA 99.9%" për plane që nuk e kanë; vetëm Enterprise sipas `PRICING.md`.
- "licenca verifikohet server-side" — verifikimi bëhet lokalisht me çelës
  publik; ai nuk është shërbim server-side.
- "të gjitha projektet e ekosistemit janë të lidhura" — inventari nuk është i plotë.

## 10.3 Kufijtë teknikë që klienti duhet t'i dijë

1. **Imazhi, jo dokumenti.** `scan` pranon imazhe. PDF duhet renderuar më parë.
2. **Pa OCR.** Skedari PDF me tekst lexohet vetëm përmes `pdftotext` në workflow;
   skanimet nuk konvertohen në tekst pa një motor OCR që nuk ekziston këtu.
3. **Profil i vetëm aktiv.** `WWWMMM_STIGMA_LEVEL` mbishkruan parametrat e
   kërkesës; nuk mund të kesh njëkohësisht "maksimum cilësie" dhe "maksimum
   shpejtësi" në të njëjtin proces.
4. **Pa fallback motorësh.** `Skia` dhe `ImageSharp` nuk zëvendësojnë njëri-tjetrin.
5. **Metrika jo persistente.** Numëruesit zerohen në rinisje.
6. **Një instancë.** Licenca dhe përdorimi janë lokalë; nuk ka sinkronizim.
7. **Email i kushtëzuar.** Pa dërgues të verifikuar, regjistrimi raporton gabim.
8. **Printim vetëm Linux** dhe i paverifikuar në harduer.
9. **Kufij input-i:** 1 MiB trupi HTTP (8 MiB për `/api/try/*`), 32 imazhe për
   `pdf-create`, 1 MiB për plan workflow.
10. **Bridges me kufij:** 30 s deadline, 2 MiB JSON, 10 MiB workbook, 64 KiB
    telemetri.

## 10.4 Kontradikta dokumentacioni që duhet zgjidhur

| Dokument | Pretendimi | Realiteti | Veprimi |
| --- | --- | --- | --- |
| `SECURITY.md`, `COMPLIANCE.md` | ISO 9001/27001/13485, MDR, FDA, HIPAA të certifikuara | Pa evidencë | **U korrigjua 2026-11-03**: të dy deklarojnë tani statusin real |
| `LICENSE_SYSTEM.md` | Verifikim server-side, device fingerprinting | Rrjedha lokale përdor HMAC të emetuesit | Pjesërisht: LSP3 heq nevojën për sekret në klient; fingerprinting mbetet i paimplementuar |
| `README.md` (SLA/perf) | "objektiva benchmark-u" | Në rregull; nuk janë garanci | Mbaj këtë formulim |
| `Docs/API_Documentation.md` | Latenca 10/30/50/80 ms | Varen nga hardware dhe input | Mat, mos cito |

Rregull: kur dokumentet kundërshtojnë njëri-tjetrin, **burimi i së vërtetës është
kodi dhe evidenca e ekzekutuar**, jo dokumenti më optimist.

## 10.5 Si të shkruhet një pretendim i pranueshëm

Formula:

```
[Çka] u mat në [mjedis: OS, CPU, RAM, version]
me [corpus/input i përshkruar] dhe [konfigurim pa sekrete],
duke përdorur [komandë/metodë], [numër] përsëritje;
rezultat: [vlerë] (p50/p95/p99 kur ka kuptim).
Kufizim: [çka nuk provohet].
```

Shembull i pranueshëm:

> Në Windows 11, Ryzen 7, 32 GB, build `c1183ee`, `wwwmmm speed` me input 64 KiB
> dhe 20 ekzekutime pas 5 ngrohjeve: median X ms, p95 Y ms. Matja është
> informative për këtë mjedis dhe nuk është garanci.

Shembull i papranueshëm:

> "Deri 1000x më shpejt se konkurrenca."

## 10.6 Verifikime të hapura (lista e së vërtetës)

Këto mbeten të hapura derisa të ekzistojë provë e regjistruar:

- [ ] Ingest/readback real i OS-CLX me auth dhe HMAC
- [ ] Printim fizik në harduer të verifikuar
- [ ] Skanim fizik nga pajisje SANE e verifikuar
- [ ] Inferencë ONNX me model real të klientit
- [ ] Dorëzim email në marrës real të jashtëm
- [x] Aktivizim offline i sigurt (ECDSA me çelës publik — `LicenseAsymmetricTests`)
- [ ] Histori metrikash përtej rinisjeve
- [ ] Pranim nga klient i jashtëm për rrjedhën e plotë
- [ ] Inventar i plotë i ekosistemit me pronar për çdo projekt
- [x] Zgjidhje e kontradiktës së certifikimeve (`SECURITY.md`, `COMPLIANCE.md`)

## 10.7 Përgjegjësia

Kush publikon një pretendim pa provë e mban përgjegjësinë, edhe nëse kodi
ekziston. Kjo nuk është formalitet: klientët vendosin mbi bazë të këtyre
fjalive, dhe një pretendim i rremë kthen një produkt teknik në një rrezik
kontraktual.
