# Lightning SPP — portofoli i proceseve për bashkëpunim

Përditësuar: 2026-11-03. Audienca: banka, kompani, administratë qendrore dhe
vendore. Fusha: përzgjedhje dhe vlerësim pilotësh. Dokumenti nuk është ofertë
kontraktuale, certifikim ose deklaratë se të gjitha rrjedhat janë pranuar në prodhim.

## Pozicionimi për partnerët

Lightning SPP bashkon përpunimin e dokumenteve me mjete të kontrolluara për
kërkim, krahasim, pajtim të të dhënave, kontrolle sipas rregullave dhe raportim.
Partneri përcakton procesin dhe jep të dhëna të autorizuara; piloti verifikon
rezultatin, referencat, kufijtë dhe nevojën për shqyrtim njerëzor.

Faturat janë një rast përdorimi. E njëjta infrastrukturë mund të përdoret si
bazë për procese të tjera, por çdo rrjedhë kërkon kontratën, konfigurimin dhe
provat e saj. Ekzistenca e një mjeti nuk provon zgjidhjen e plotë të një procesi.

Vendimi i pranimit dhe pengesat mbahen te
[evidenca e release-it](RELEASE_EVIDENCE.md). Ky portofol nuk krijon një vendim
paralel pranimi. [Udhërrëfyesi](TECHNOLOGY_EVOLUTION.md) përcakton rendin e punës.

## Proceset dhe kufijtë e ofertës për pilot

| ID dhe procesi | Hyrja e nevojshme | Përpunimi që ekziston në kod | Rezultati i synuar i pilotit | Çfarë duhet plotësuar/provuar |
| --- | --- | --- | --- | --- |
| P01 — shqyrtim dosjesh dhe versionesh | Dokumente UTF-8/PDF me tekst, dosje të autorizuara dhe kërkesë e përcaktuar | Import, origjinale, hash-e, lexim, kërkim literal dhe krahasim skedarësh | Raport dallimesh dhe gjetjesh me burimet përkatëse | Kuptimi semantik, klasifikimi automatik dhe rregullat e plotësisë së dosjes nuk provohen nga kërkimi literal |
| P02 — përgatitje dokumentesh dhe PDF | Imazhe ose PDF brenda kufijve të rrugës së zgjedhur | Përpunim imazhesh/faqesh, nxjerrje teksti PDF ekzistues, output PDF | Dokument i vlefshëm dhe raport i përpunimit pa humbje të heshtur faqesh | OCR i skanimeve, cilësia e nxjerrjes dhe pranimi i pajisjeve; demo PDF e rasterizuar nuk garanton ruajtjen e tekstit/vetorëve |
| P03 — pajtim financiar | Regjistra të normalizuar faturash, pagesash, kredish/rimbursimesh dhe alokime të shqyrtuara | Decimal, kontroll monedhe/pale, kufij alokimi dhe llogaritje gjendjesh | Lista të paguara, pjesërisht të paguara, të hapura/të vonuara, kredite dhe transaksione të palidhura | Nxjerrja nga dokumenti dhe lidhja bankare janë faza më vete; kontestimet kërkojnë model dhe evidencë të posaçme |
| P04 — kontroll sipas rregullave të partnerit | Vlera të strukturuara të verifikuara, njësi dhe protokoll i versionuar me përgjegjës | Krahasim me kufij min/max, shënim devijimesh ose nevoje për shqyrtim | Listë përjashtimesh me rregullin, versionin, burimin dhe përgjegjësin | Rregullat duhet të miratohen nga partneri; motori aktual nuk është interpretues i përgjithshëm ligjor apo kontrollues i çdo lloj kushti |
| P05 — gjurmueshmëri kërkesash dhe provash | Kërkesa me ID/version, regjistra testesh dhe skedarë provues | Lidhje kërkesë–test–burim, version i papërputhur, provë e munguar dhe rezultat i raportuar | Matricë gjurmueshmërie dhe listë boshllëqesh për shqyrtim | Regjistri i një testi të kaluar nuk është inspektim fizik, certifikim apo verifikim i pavarur |
| P06 — monitorim treguesish | Seri vlerash me data unike, njësi dhe burime | Ndryshime absolute dhe përqindje; trajtim i bazës zero | Raport ndryshimesh për shqyrtim analitik | Nuk është parashikim recesioni ose provë shkakësie; marrja automatike nga burimet kërkon integrim real |
| P07 — raporte dhe shpërndarje e kontrolluar | Rezultate të mjeteve me referenca dhe leje përkatëse | Eksport JSON/CSV/PDF; dërgim në CUPS dhe lexim gjendjeje kur konfigurohet | Raport i hapshëm, me burime, dhe regjistër dorëzimi kur provohet | Dorëzimi në queue nuk provon printim fizik; dokumentet e mëdha kërkojnë ndarje dhe verifikim |
| P08 — lidhje me ekosistemin | Shërbime të identifikuara, kontrata, kredenciale dhe destinacione të miratuara | Bridges Excel/NodeDB në CLI; adapterë HTTP read-only të konfiguruar nga operatori | Operacion real i provuar me sukses, dështim dhe gjurmueshmëri | Bridges ende pa pranim të plotë prodhimi; adapteri gjenerik nuk provon lidhje me bankë, mailbox apo regjistër institucional |

P01/P04/P05 mund të shërbejnë si bazë për shqyrtim dokumentacioni administrativ,
dosjesh projektesh dhe kontrolle të brendshme. Këto janë zbatime të propozuara për
pilot; nuk deklarohen sot si module të plota për prokurim, kredi, leje apo vendime
administrative. Çdo proces kërkon fushat, rregullat dhe përgjegjësit e tij.

## Lidhja me implementimin

| Proceset | Burimi dhe kontratat |
| --- | --- |
| P01, P02, P07 | [FileTools](../Workflow/FileTools.cs): `files.list`, `files.read`, `files.search`, `files.compare`, `documents.pdf-text`, `reports.export`; [TryDocumentProcessor](../Web/TryDocumentProcessor.cs) për rrugën demo PDF |
| P03, P04, P05, P06 | [DomainTools](../Workflow/DomainTools.cs): `finance.reconcile`, `review.rules`, `requirements.trace`, `economy.indicators` |
| Tabela hyrëse | [TableTools](../Workflow/TableTools.cs): `tables.read-csv`; leximi i fushave nuk përcakton vetë kuptimin kontabël |
| Pajisje P07 | [DeviceTools](../Workflow/DeviceTools.cs): printerë, queue dhe status; varen nga Linux/CUPS dhe pajisjet |
| Integrime P08 | [Kontratat e bridges](BRIDGES.md), [ServiceAdapters](../Workflow/ServiceAdapters.cs) dhe [StripeWorkspaceTools](../Workflow/StripeWorkspaceTools.cs); Stripe këtu lexon faturat e klientit të autentikuar të shërbimit, jo llogaritë e tij bankare |
| Orkestrimi i përbashkët | [WorkflowRunner](../Workflow/WorkflowRunner.cs), [WorkspaceScope](../Workflow/WorkspaceScope.cs) dhe [runtime](WORKFLOW_RUNTIME.md): pronësi, leje, varësi, hash-e, anulim dhe rezultate të ruajtura |

## Kontrata e rezultatit të pilotit

Për çdo gjetje, rresht raporti ose përfundim kërkohen:

1. Identiteti i dokumentit/regjistrit dhe hash-i i burimit origjinal.
2. Fusha/vlera e përdorur, njësia/monedha kur zbatohen dhe faqja/rreshti kur
   është i disponueshëm. Nëse vendndodhja nuk është nxjerrë, shënohet si mungesë.
3. Rregulli, versioni, mjeti dhe transformimet që prodhuan rezultatin.
4. Evidenca mbështetëse, gjendja e shqyrtimit dhe personi që e konfirmoi kur kërkohet.
5. Pasiguritë, gabimet, të dhënat e munguar dhe hapi i nevojshëm pasues.

Kjo është kontrata e synuar e pranimit për pilotin. Kodi aktual ruan referenca
dhe hash-e në nivel rezultati; nuk pretendohet se të gjitha mjetet sot japin
gjurmueshmëri për çdo fushë, faqe dhe zonë të dokumentit. Ajo duhet verifikuar
dhe plotësuar për procesin e zgjedhur.

“Dokument i lexueshëm”, “të dhëna të verifikuara”, “brenda rregullit të dhënë”,
“pagesë e konfirmuar” dhe “pranuar nga shqyrtuesi” janë gjendje të ndryshme.
Një etiketë e vetme “OK” nuk i zëvendëson. Fatura nuk provon pagesën dhe një
pretendim në dokument nuk provon se ekziston një kontestim i vlefshëm.

## Si fillon një pilot

Përpara përdorimit të të dhënave, partneri dhe përgjegjësi i pilotit regjistrojnë:

- Procesin e zgjedhur P01–P08, hyrjet, rezultatet e kërkuara dhe veprimet e lejuara.
- Pronarin e procesit, përgjegjësin teknik, përgjegjësin e sigurisë/të dhënave dhe
  shqyrtuesin që ka të drejtë të pranojë rezultatet. Këto role ende nuk janë caktuar.
- Corpus-in e autorizuar, gjuhët, formatet, dokumentet e vështira dhe rezultatin
  referencë të kontrolluar nga partneri; shembujt e gjeneruar mbeten vetëm teste.
- Mjedisin, kufijtë lokal/remote, akseset, ruajtjen/fshirjen, backup-in dhe auditimin.
- Pragjet e pranimit për saktësinë për fushë, gabimet e rënda, mbulimin e referencave,
  kohën, burimet dhe rikuperimin. Pragjet caktohen përpara matjes; ky dokument
  nuk shpik vlera dhe nuk thotë se ato janë arritur.

Rruga e propozuar e parë është P01 → P07: import i autorizuar, tekst/lexim,
kërkim ose krahasim i përcaktuar dhe raport me referenca. Kjo lejon demonstrim
të kontrolluar pa e bërë AI-në kusht për korrektësinë. P03 shtohet kur hyrjet
financiare dhe alokimet janë të normalizuara dhe të shqyrtuara. OCR dhe
interpretimi semantik shtohen vetëm pasi të kalojnë vlerësimin e tyre.

Për objektivin 30.000 dokumente kërkohet test i veçantë i rrjedhës së plotë:
import, nxjerrje, përpunim, referenca, raportim, ndërprerje/rinisje, integritet
dhe rikuperim. Kufiri i lexuesit CSV prej 30.000 rreshtash nuk është kjo provë.

## Provat që mbyllin pilotin

| Kontrolli | Prova e kërkuar për procesin e zgjedhur |
| --- | --- |
| Korrektësia | Rezultatet krahasohen me referencën e partnerit; maten gabimet dhe dërgimet për shqyrtim |
| Gjurmueshmëria | Çdo përfundim i pranuar lidhet me burimin; mungesat nuk plotësohen nga modeli |
| Siguria | Rrjedhë e autentikuar, përdorues pa leje, pronar i gabuar dhe përmbajtje keqdashëse; pa ekspozim sekretesh |
| Dështimet | Dokument i dëmtuar, format/njësi e gabuar, shërbim i padisponueshëm, anulim dhe timeout pa sukses të fabrikuar |
| Operimi | Versioni konkret, mjedisi, kohët/burimet, checkpoint-et, restore dhe rollback kur zbatohet |
| Pranimi | Shqyrtuesi i partnerit regjistron çfarë u ushtrua, çfarë kaloi dhe çfarë mbetet e bllokuar te evidenca e release-it |

Modeli `llama3.2:3b` i testuar lokalisht nuk u pranua për këshillim teknik ose
planifikim autonom: shih [vlerësimin real](AI_MODEL_EVALUATION_2026-11-03.md).
Modelet kandidate kërkojnë vlerësim për detyrën, gjuhën dhe corpus-in e pilotit;
emri, madhësia ose përgjigjja jo bosh nuk janë prova përshtatshmërie.

## Formulim i përdorshëm në takim

“Propozojmë një pilot të kontrolluar të Lightning SPP për procesin tuaj të
dokumenteve. Do të përcaktojmë të dhënat, rregullat dhe rezultatet që duhen
verifikuar, do të demonstrojmë përpunimin real dhe do të masim saktësinë,
gjurmueshmërinë dhe kufijtë operacionalë përpara zgjerimit.”

Ky material përgatit bisedën dhe shqyrtimin e pilotit. Nuk është dërguar te
asnjë organizatë dhe nuk autorizon kontaktim, publikim ose deploy.
