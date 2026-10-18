# Lightning SPP 3.14 — plani aktual i integrimit

Përditësuar: 15 tetor 2026. Projekti: `wwwmmm`.
Ky plan zëvendëson planin historik “WWWMMM ultra-thin gateway”.
Kontratat Fabric/CLX mbeten integrim i veçantë; nuk përcaktojnë më të gjithë produktin.

## Qëllimi

Lightning është një multitool dinamik për dokumente, imazhe, kod dhe procese pune.
Përdoruesi jep qëllimin; agjenti zbulon aftësitë reale, propozon një plan,
ekzekuton vetëm veprimet e autorizuara dhe shpjegon rezultatet me prova.

Cikli i punës:

**Kupto → kërko prova → planifiko → shqyrto → ekzekuto → verifiko → përshtatu → shpjego.**

Nuk ndërtohen rrjedha të hardkoduara për çdo profesion. Regjistri, kontratat,
adapterët dhe rregullat e versionuara lejojnë shtimin e aftësive.
“Pa limite artificiale” nuk nënkupton burime fizike të pakufizuara:
ngarkesat e mëdha kërkojnë batch-e, radhë pune, backpressure dhe checkpoint-e.

## Arkitektura dhe kanalet

| Pjesa | Përgjegjësia |
| --- | --- |
| Identiteti | Ripërdor autentikimin ekzistues; workspace dhe rezultate vetëm për pronarin |
| Regjistri | Mjete me ID unike, argumente, leje, varësi dhe kushte riprovimi |
| Orkestruesi | Validim plani, varësi, rezultate të ndërmjetme dhe izolim dështimesh |
| Modelet | Adapterë të konfigurueshëm, zbulim identiteti/aftësish dhe inferencë reale |
| Workspace | Origjinale të ruajtura, hash-e, kontekst, raporte dhe gjendje pune |
| Evidenca | Burimi, versioni, rreshti/faqja kur dihen, hash-i dhe pasiguria |
| Ndërfaqja | Bisedë me kontekst të autorizuar, shqyrtim plani, progres dhe anulim |
| Rishikimi | Gjetje të qëndrueshme dhe pranim i regjistruar nga përdoruesi |

Dokumente, pamje ekrani dhe përgjigje modelesh trajtohen si të dhëna të pabesueshme.
Ato nuk japin leje dhe nuk autorizojnë mjete, fshirje, pagesa apo akses ndërmjet klientëve.

## Modelet dhe ripërdorimi

- Ripërdoren komponentët ekzistues të Lightning: CustomerAccounts, CustomerStore,
  Longo, dokumentet/PDF, adapterët e pajisjeve dhe infrastruktura Stripe.
- Shërbimet Clisonix/Ollama lidhen përmes konfigurimit të operatorit.
- LLaVA, Gemma, CLX, XCL dhe CLX.i janë kandidatë; emri nuk provon instalim,
  aftësi vizuale, cilësi OCR apo inferencë funksionale.
- Në runtime-in Ollama të kontrolluar gjatë kësaj pune u zbulua realisht
  `qwen2.5:7b`, me aftësi të raportuara `completion` dhe `tools`.
  Kjo nuk provon ekzistencën e modeleve të tjera apo cilësinë e përfundimeve.
- Mungesa e modelit ose shërbimit jep dështim të shprehur, pa përgjigje të fabrikuara.
- Produkti lokal ruan dokumentet dhe inferencën në serverin e klientit.
  Përpunimi remote është kanal i veçantë, kërkon konfigurim dhe autorizim eksplicit;
  nuk përdoret si fallback i heshtur për produktin lokal.

## Paketat e aftësive

| Fusha | Punë konkrete | Kriteri i përfundimit |
| --- | --- | --- |
| Skedarë dhe kod | Lexim, kërkim, krahasim, auditim kandidatësh dhe referenca | Gjetje me prova; pattern-i vetëm nuk provon mock/fake në prodhim |
| Dokumente | Import, origjinale, tekst PDF, vision/OCR, klasifikim dhe raporte | Nxjerrje dhe cilësi të verifikuara në dokumente reale |
| Fatura dhe pagesa | Decimal/currency, pagesa të pjesshme/konsoliduara, kredi dhe rimbursime | Alokime të gjurmueshme; lidhjet e pasigurta kërkojnë shqyrtim |
| Ekonomi | Tregues me burime, ndryshime, skenarë dhe sinjale të hershme rreziku | Backtesting dhe pasiguri; jo pretendim për parashikim të sigurt recesioni |
| Ndërtim | Krahasim planesh/specifikimesh, dimensione, toleranca dhe ndryshime | Referencë në dokument dhe kontroll inxhinierik për sigurinë strukturore |
| Aviacion | Gjurmueshmëri kërkesë/material/projekt/test, devijime dhe prova që mungojnë | Dokumentacioni nuk zëvendëson inspektimin fizik apo certifikimin |
| Klinikë | Identitet, datë, njësi, vlera, protokolle dhe alarme me burime | Rregulla të klinikës, kontroll nxjerrjeje dhe pranim/eskalim i regjistruar |

Nuk vendosen pragje klinike/inxhinierike, metrika besueshmërie ose përfundime
ekonomike të shpikura. Alarmi duhet të ruajë provën dhe statusin e shqyrtimit;
një popup kalimtar nuk mjafton.

## Workflow i parë i biznesit

Import → kuptim → klasifikim → krahasim → organizim logjik → raport → print/eksport.

1. Import nga dosja; mailbox vetëm me connector dhe leje të zgjedhura.
2. Origjinale të pandryshuara, hash-e, identifikues dhe checkpoint-e.
3. Tekst PDF nativ; OCR lokal vetëm pas verifikimit të modelit.
4. Skema të versionuara për fatura, kredi, kthime dhe transaksione.
5. Pajtimi përdor decimal, monedhë, palë dhe referenca; shenja e shumës
   nuk përcakton vetë kuptimin e pagesës.
6. Gjendje të shprehura: paid, partially paid, open, overdue, credited,
   refunded, unmatched dhe needs review.
7. Pamje dosjesh pa lëvizje/fshirje të heshtur të origjinaleve.
8. Raport me prova; printimi i dorëzuar në queue nuk deklarohet i përfunduar.

Objektivi prej 30 000 dokumentesh kërkon matje kapaciteti dhe prova rikuperimi;
nuk deklarohet i mbështetur vetëm nga teste të vogla.

## Gjendja e implementimit në degën aktuale

Kodi i mëposhtëm është shtuar. Kjo listë nuk është pranim për prodhim:

- `Workflow/Contracts.cs`: regjistër, kontrata, leje dhe validim varësish.
- `Workflow/WorkspaceScope.cs`: kufizim aksesi, import dhe snapshot-e me hash.
- `Workflow/FileTools.cs`: listim/lexim/kërkim/krahasim, auditim patterns,
  tekst PDF nativ dhe raporte JSON/CSV.
- `Workflow/DomainTools.cs`: pajtim i alokimeve të shqyrtuara,
  ndryshime treguesish, rregulla të dhëna dhe gjurmueshmëri kërkesash/testesh.
- `Workflow/ModelGateway.cs`: discovery, tekst/vision dhe propozim plani.
- `Workflow/WorkflowRunner.cs`: plane me varësi, snapshot-e, leje,
  kufizim worker-ësh, anulim dhe kushte riprovimi.
- `Workflow/WorkflowCommands.cs`: hyrje CLI për mjete, modele dhe plane.
- `Web/WorkflowApi.cs` dhe faqja `/workspace`: API/UI me identitetin ekzistues.

Build Release është ekzekutuar gjatë kësaj pune me **0 gabime dhe 0 paralajmërime**.
Testet funksionale, të sigurisë, regresionit dhe browser-it për ndryshimet e reja
janë ende për t'u përfunduar. Sjellja në prodhim nuk është verifikuar dhe kjo
punë nuk është deploy-uar.

## Punët dhe portat e pranimit

- [ ] Verifikim mjetesh, referencash, lejesh dhe izolimi workspace.
- [ ] Teste për anulim, ndërprerje/rinisje, konkurrencë dhe backpressure.
- [ ] Inferencë reale dhe plane të gjeneruara nga modelet e disponueshme.
- [ ] Verifikim vision/OCR me dokumente dhe referenca faqesh/zonash.
- [ ] Browser acceptance për import, bisedë, plan, progres dhe shqyrtim.
- [ ] Connector mailbox/bank i zgjedhur dhe provuar me të dhëna të autorizuara.
- [ ] Raport PDF dhe adapterë printimi të verifikuar.
- [ ] Validim i veçantë klinik, ekonomik, ndërtimor dhe aviacioni.
- [ ] Test kapaciteti me 30 000 dokumente dhe backup/restore.
- [ ] CI, evidencë aktuale, rollback dhe verifikim shëndeti para deploy.

JSON-i i datës 13 tetor me 184 teste të kaluara dhe
`acceptedForProduction: false` është evidencë historike e një implementimi tjetër.
Nuk përdoret për të deklaruar se ndryshimet aktuale kanë kaluar testet.

## Dokumentet shoqëruese

- [Scope i platformës](../../scope.md)
- [Lista aktuale e punëve](../../todo.md)
- [Plani i kanaleve](../../chanellplan.md)
- [Shënimet historike të kontratës Fabric](contracts/README.md)

**NO FAKE / PRODUCTION GRADE:** ndryshimi mbetet i papranuar për prodhim
sa kohë një kërkesë e detyrueshme verifikimi është e pambyllur.
