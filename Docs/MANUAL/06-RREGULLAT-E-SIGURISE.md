# 06 — Rregullat e sigurisë

Ky dokument është i detyrueshëm për këdo që instalon, konfiguron, operon ose
ndryshon Lightning SPP. Rregullat nuk janë rekomandime: aty ku shkruhet
**NDALOHET**, veprimi bllokohet derisa të ketë miratim dhe provë.

## 6.1 Nëntë parimet

1. **Dështo i mbyllur.** Në dyshim për identitet, autorizim ose pronësi,
   përgjigjja është refuzim. Asnjë rrugë alternative nuk e anashkalon
   kontrollin.
2. **Asnjë të dhënë e sajuar.** As metrikë, as rezultat, as certifikim, as
   benchmark nuk publikohet pa provë të ekzekutuar. Ky është rregulli qendror i
   `AGENTS.md`.
3. **Privilegj minimal.** Çdo shërbim, çelës dhe përdorues merr vetëm atë që i
   duhet për detyrën.
4. **Sekreti nuk shkon te klienti.** Materiali i emetuesit mbetet te emetuesi.
5. **Asnjë fallback i heshtur.** Motorët, providerët dhe rrugët e përpunimit nuk
   zëvendësohen automatikisht; mungesa është gabim eksplicit.
6. **Gjurmueshmëri.** Çdo veprim me efekt (emetim licence, aktivizim, pagesë,
   shkrim në upstream) lë gjurmë të lexueshme.
7. **Ndryshimi ka rrugë kthimi.** Pa rollback të provuar ose të dokumentuar me
   pronar, nuk ka deploy.
8. **Të dhënat e klientit janë të klientit.** Nuk përdoren për benchmark, nuk
   dërgohen te modele të jashtme pa pëlqim eksplicit.
9. **Prova është specifike.** "Funksionon" pa mjedis, version dhe komandë nuk
   është provë.

## 6.2 Sekretet — rregulla absolute

**NDALOHET:**

- të commit-osh `.env`, `.env.build`, `.env.production`, çelësa, certifikata
  private, fjalëkalime ose token-a;
- të vendosësh `SIXLABORS_LICENSE` në `ENV`/`ARG` të Dockerfile ose në
  `env_file` të runtime-it;
- të printosh sekrete në log, në raporte gabimi ose në përgjigje HTTP;
- të përdorësh të njëjtin sekret për `LSPP_ADMIN_TOKEN` dhe
  `LSPP_LICENSE_SIGNING_KEY`;
- të dërgosh sekretin e emetuesit në një pajisje klienti;
- të ruash sekrete në një workspace klienti ose brenda `LSPP_DATA_DIR`.

**DETYROHET:**

- sekrete të rastësishme ≥ 32 bajt (`openssl rand -hex 32`);
- skedar sekreti me leje `0600`, pronar i llogarisë së shërbimit;
- rotacion i menjëhershëm i çdo sekreti që ka hyrë në git, në log ose në një
  imazh container; fshirja e tekstit nuk e heq nga historia;
- sekrete të ndryshme ndërmjet mjediseve (dev/staging/prod nuk ndajnë çelës).

## 6.3 Identiteti dhe aksesi

- Fjalëkalimet ruhen vetëm si hash PBKDF2 (Identity V3, 600 000 iteracione);
  nuk ekziston rikthim fjalëkalimi në tekst të qartë.
- Sesionet revokohen në ndryshim fjalëkalimi dhe në dalje.
- Token-at e verifikimit/rikthimit janë të hash-uar, njëpërdorimshëm, të lidhur
  me qëllimin dhe skadojnë.
- Rrugët e llogarisë kërkojnë CSRF, validim origjine dhe kufizim norme.
- Gabimet e hyrjes nuk zbulojnë nëse email-i ekziston: përgjigje e njëtrajtshme
  për llogari të panjohur.
- Statusi "i paverifikuar" kthehet vetëm pasi fjalëkalimi verifikohet saktë —
  përndryshe hapet enumerim llogarish.

**Rregull operativ:** nëse një klient raporton "licenca ime u zhduk", mos prek
fjalëkalimin ose statusin e verifikimit për të "rregulluar" gjendjen. Ndarja
është e qartë: çelës i paguar i vlefshëm ≠ llogari e verifikuar ≠ regjistrim i
shfletuesit. Kontrollo secilën veç e veç.

## 6.4 Licencimi — gjendja reale dhe kufizimi i hapur

E vërteta e këtij checkout-i, e regjistruar në `Docs/ACCOUNT_SECURITY_REVIEW.md`:

- Verifikimi lokal i çelësit përdor HMAC me sekretin e emetuesit.
- Gjendja e ruajtur lokalisht lexohet pa ri-verifikuar nënshkrimin.
- Për rrjedhojë, **aktivizimi komercial offline nuk është i pranuar**.

**NDALOHET:**

- të shpërndash sekretin e emetuesit si "hap aktivizimi";
- të paraqitësh enkriptimin e skedarit lokal si provë autenticiteti;
- të migrosh çelësat e klientëve pa shqyrtim dhe pa njoftim;
- të pretendosh "server-side validation" ose "device fingerprinting" në
  production pa e treguar në ekzekutim.

**DETYROHET:** deri sa të ekzistojë verifikim me çelës publik ose shërbim
aktivizimi autoritativ, aktivizimi ofrohet vetëm përmes shërbimit të hostuar të
emetuesit, dhe ky kufizim shkruhet në materialin e klientit.

## 6.5 Sipërfaqja e rrjetit

- Vetëm 80/443 ekspozohen publikisht; porta e aplikacionit (13080/11400) mbetet
  e lidhur në loopback ose në rrjetin privat.
- TLS ofrohet nga proxy-i; aplikacioni nuk terminon TLS vetë.
- `LSPP_TRUST_PROXY_HEADERS=true` **vetëm** bashkë me `LSPP_TRUSTED_PROXY_IPS`.
  Pa të dyja, kufizimi i normës bëhet global dhe një klient bllokon të gjithë.
- `client_max_body_size` i proxy-t duhet të përputhet me kufijtë e aplikacionit
  (1 MiB i përgjithshëm, 8 MiB për `/api/try/*`).
- CORS vetëm me domenet reale në `LSPP_ALLOWED_ORIGINS`.
- Mos e ekspozo një instancë të pakonfiguruar lokale si shërbim klienti.

## 6.6 Të dhënat e dokumenteve

- Dokumentet e klientit janë konfidenciale. Mos i përdor për benchmark, mostra
  ose teste pa miratim me shkrim.
- Raportet e `benchmark-documents` mund të përmbajnë hash-e dhe madhësi:
  trajtoji si private.
- Skedarët e ndërmjetëm (artefakte `.br`, PNG/TIFF/PDF të ndërmjetëm) mund të
  përmbajnë përmbajtje të plotë: fshiji sipas politikës së ruajtjes.
- `/api/try/*` përpunon input të vizitorit: mos e ruaj më gjatë se sa duhet për
  funksionimin dhe mos e përziej me të dhëna klienti.
- Kopjet e `LSPP_DATA_DIR` dhe Longo përmbajnë identitet dhe entitlement:
  trajto si sekrete operimi.

## 6.7 AI dhe modelet

- Përgjigjja e modelit është **pretendim**, jo fakt. Kodi e shënon vetë
  (`MODEL_CLAIM_REQUIRES_REVIEW`).
- Një model nuk jep leje. Teksti i modelit dhe i dokumentit nuk mund të zgjerojë
  `permissions`.
- Rrugët e modeleve janë konfigurim i operatorit; klienti nuk sjell URL ose
  çelës.
- Modelet remote kërkojnë pëlqim eksplicit të përdoruesit **dhe**
  `LSPP_WORKFLOW_ALLOW_REMOTE_MODELS=true`.
- Pa `LSPP_ONNX_MODEL_PATH` nuk ka inferencë reale; mos e quaj "AI-powered".
- Prova e aftësisë së një modeli është vlerësim i regjistruar (shih
  `Docs/AI_MODEL_EVALUATION_2026-11-03.md`), jo fakti që modeli u ngarkua. Në
  atë vlerësim, një model lokal 3B nuk plotësoi kriteret për këshillim teknik.
  Përfundimi operativ: mos e vendos një model të vogël si autoritet mbi
  konfigurimin ose sigurinë.

## 6.8 Workflow — kufijtë e ekzekutimit

- Plani propozohet, ruhet dhe **shqyrtohet**; ruajtja nuk ekzekuton.
- Çdo hap deklaron varësi dhe një mjet të regjistruar. Mjete, argumente, leje,
  varësi që mungojnë dhe cikle refuzohen.
- Lejet (`files.read`, `reports.write`, `model.invoke`, `network.remote`,
  `devices.print`, `services.read`) jepen nga operatori, jo nga input-i.
- Izolim pronari: një klient nuk dorëzon rrënjën e një klienti tjetër.
- Anulimi regjistrohet; nuk raportohet si sukses.
- Efekt i jashtëm me rezultat të panjohur (timeout pas shkrimi) kërkon rakordim
  manual — mos e ripërsërit verbërisht.
- Kufijtë: input web 8 MiB, plan ≤ 256 hapësira, evidencë job-i 16 MiB,
  `LSPP_WORKFLOW_WORKERS` 1–32.

## 6.9 Bridges — rregulla SSRF dhe shkrimi

- Vetëm host-e dhe rrugë të konfiguruara nga operatori; asnjë URL nga
  përdoruesi.
- HTTPS i detyrueshëm, përveç loopback në teste të izoluara.
- URL-të nuk pranojnë kredenciale, query ose fragment.
- Pa redirect automatik, pa retry automatik, deadline 30 s.
- Kufij: JSON 2 MiB, workbook 10 MiB, telemetri 64 KiB.
- Një timeout pas shkrimi = rezultat i panjohur. Lexo nodin para se ta
  ripërsëritësh; përndryshe krijon dublikatë.
- Mock-et provojnë kontratën, jo lidhjen. Pranimi kërkon shërbim real.

## 6.10 Pajisjet dhe printimi

- `device print` dorëzon në radhë; **nuk** vërteton printim fizik.
- `device print-status` kthen `physicalCompletionVerified: false` me qëllim.
- Pas një ndërprerjeje, kontrollo radhën para ripërsëritjes.
- Skanimi fizik (`device scan`) kërkon SANE të konfiguruar; nuk është verifikuar
  në harduer në këtë repository.
- Mos e përshkruaj "printimin Stigma" si printim: është artefakt lossless `.br`.

## 6.11 Përdorimi mjekësor dhe pretendimet rregullatore

**Përdorimi mjekësor kërkon validim klinik dhe certifikime për tregun ku
shpërndahet.** Ky repository nuk përmban provë auditimi për ISO 13485, CE/MDR,
FDA 510(k) ose HIPAA.

Kontradiktë e njohur dokumentacioni: `SECURITY.md` dhe `COMPLIANCE.md`
deklarojnë certifikime dhe numra certifikatash (ISO 9001, ISO 27001, ISO 13485
"in progress", MDR, FDA, HIPAA) që nuk mbështeten nga evidencë në këtë
repository, ndërsa `README.md` thotë shprehimisht se këto nuk deklarohen pa
evidencë të auditueshme. **Deri sa të zgjidhet, mos i përdor ato deklarata në
material klienti.** Prioriteti i korrigjimit: rreshtat e certifikimeve në
`COMPLIANCE.md` dhe `SECURITY.md`.

## 6.12 Incidentet

1. **Ndalo.** Nëse dyshohet kompromis, ndërpre trafikun ose rrotullo sekretin
   para çdo hetimi tjetër.
2. **Regjistro** kohën, çka u pa, çka u bë — pa sekrete në tekst.
3. **Kufizo** përhapjen: çaktivizo çelësin e prekur, revoko sesionet.
4. **Rikupero** sipas `07-OPERIMI-DHE-RIKUPERIMI.md`.
5. **Njofto** sipas detyrimeve ligjore kur të dhëna personale janë prekur; mos
   njofto me spekulime.
6. **Mëso**: shto një kontroll ose test që e bën të përsëritshëm.
7. **Mos fshij provat** dhe mos e "rregullo" duke ndryshuar gjendjen e klientit.

Raportim i dobësive: `security@aba-gmbh.de` (jo issue publike), sipas
`SECURITY.md`.

## 6.13 Lista e verifikimit para çdo release-i

- [ ] Build pa warnings/errors; suite i plotë i testuar, jo vetëm smoke
- [ ] `/api/health` OK; `/api/metrics` dhe `/api/alerts` lexohen
- [ ] Kërkesë admin pa token → 401 (provë e ruajtur)
- [ ] Kufizim norme i provuar; CORS me origin të palejuar refuzohet
- [ ] Asnjë sekret i ri në git, në imazh, në log
- [ ] `LSPP_DATA_DIR` dhe backup-i i Longo ekzistojnë dhe janë testuar
- [ ] Rollback i provuar ose i dokumentuar me pronar
- [ ] Pretendimet publike përputhen me aftësitë e provuara
- [ ] Verifikimet e papërfunduara janë shënuar si të papërfunduara

Nëse një pikë mungon, release-i është **NO-GO**.
