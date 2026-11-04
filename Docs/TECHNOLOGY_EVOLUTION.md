# Lightning SPP — programi i ngritjes teknologjike

Data e vlerësimit: 2026-11-04. Fusha: organizim, prodhim, siguri,
integrime dhe demonstrim me prova. Ky është udhërrëfyes pune, jo deklaratë
se aftësitë e propozuara janë implementuar apo pranuar në prodhim.

## Drejtimi

Lightning SPP synon të jetë pika e kontrolluar e punës me dokumente dhe
orkestrimit të aftësive të ekosistemit: identitet i verifikuar, kontrata të
versionuara, ekzekutim i autorizuar dhe rezultat i gjurmueshëm. Ruhet pipeline-i
ekzistues dhe ripërdoret regjistri i mjeteve; nuk krijohet një implementim paralel.
Lidershipi botëror mbetet ambicie që kërkon krahasime të riprodhueshme,
pranim nga klientë dhe evidencë operimi. Nuk është pretendim aktual produkti.

Ky program rendit punën ndërmjet moduleve. Detajet e workflow mbeten te
[plani i integrimit](fabric-p0/INTEGRATION_PLAN.md), kontratat aktuale te
[bridges](BRIDGES.md), ndërsa vendimi i pranimit të release-it regjistrohet te
[evidenca e release-it](RELEASE_EVIDENCE.md). Nuk krijohet status paralel pranimi.

[Portofoli i proceseve për partnerët](PARTNER_PROCESS_PORTFOLIO.md) lidh
rastet e pilotit me kodin, rezultatet dhe provat e kërkuara për pranimin.

## Baza e vëzhguar dhe kufijtë

Source revisions të Clisonix, OS-CLX dhe Kloud u lexuan më 2026-11-04. Kjo
verifikon kodin në degët e shqyrtuara, jo revision-in e instaluar tani në
prodhim. Evidenca historike e shëndetit ose e deploy-it kërkon rikonfirmim para
pranimit të integrimit.

| Fusha | Baza konkrete | Çfarë duhet provuar më tej |
| --- | --- | --- |
| Përpunimi | Pipeline, dokumente/PDF, SDK dhe API ekzistuese | Cilësia dhe kapaciteti në corpus të autorizuar të klientit |
| Orkestrimi | Regjistër, workspace, model gateway dhe plane në `Workflow/` | Rrjedhë e plotë me përdorues real të autorizuar dhe izolim ndërmjet pronarëve |
| Bridges | Excel Core, Excel parse/validate dhe OS-CLX NodeDB në `Integrations/`; kodet dhe kufijtë e upstream krahasohen më poshtë | Rikonfirmim i revision-it të instaluar; provë e izoluar e NodeDB dhe pranimi i kontratës së plotë |
| Identiteti/licencat | Llogari, Stripe dhe persistence Longo | Dërgim te recipient i jashtëm dhe zgjidhje e verifikimit offline pa sekrete të emetuesit |
| Operimi | Procedura deploy/recovery dhe workflow release-readiness | Evidencë mbi versionin konkret të instaluar dhe rikuperimin |
| Metrikat | `Services/LightningMetrics.cs`; kufij të dokumentuar te SLO | Histori e qëndrueshme, percentile dhe dritare matjeje përtej rinisjeve |

Burime për kufijtë: [siguria e llogarive](ACCOUNT_SECURITY_REVIEW.md),
[pranimi i klientit](CUSTOMER_ACCEPTANCE.md), [SLA/SLO](SLA_SLO.md) dhe
[rikuperimi i workspace](WORKSPACE_PRODUCTION_RECOVERY.md).

## Rendi i zbatimit

Çdo fazë prodhon kandidat të shqyrtueshëm. Kalimi varet nga prova, jo nga afati.
Përgjegjësit më poshtë janë role të propozuara; operatori duhet të caktojë
persona përpara ekzekutimit të fazës përkatëse.

| Faza | Puna dhe përgjegjësia | Kushti i daljes |
| --- | --- | --- |
| P0 — baza e besimit | Release owner + security owner: rikonfirmojnë release SHA, pengesat e llogarive/licencimit dhe gjendjen e bridges; caktojnë pronar për çdo pengesë | Build/testet e kërkuara kalojnë; pengesat e prekura mbyllen me prova ose aftësia mbetet jashtë prodhimit |
| P1 — operim i matshëm | Operations owner: histori metrikash, correlation IDs pa sekrete, alarme, backup dhe ushtrim restore në mjedis të izoluar | Alarm i provuar, histori që mbijeton rinisjen, RPO/RTO të miratuara dhe të matura; rollback me kontroll integriteti |
| P2 — kontrata ekosistemi | Integration owner + pronari upstream: inventar projektesh dhe pranimi i një bridge-i në një kohë | Kontratë versioni, autentikim, kufij dhe prova reale suksesi/dështimi për çdo lidhje |
| P3 — workflow referencë | Product owner + quality owner: import dokumenti, plan i shqyrtuar, mjet i autorizuar, rezultat dhe eksport | Izolim pronari, anulim, dështim i rikuperueshëm, hash-e dhe output i vlefshëm në rrjedhën e plotë |
| P4 — demonstrim dhe shpërndarje | Release owner: paketim, instalim i pastër dhe benchmark i riprodhueshëm | Artefakte me hash, prova të mjedisit dhe kufij të deklaruar; krahasime vetëm kur metodologjia i mbështet |

P0 nuk lejon migrim të heshtur të licencave ose dobësim të mbrojtjes ndaj malware.
Gjetja e regjistruar për OS-CLX kërkon shqyrtim sigurie dhe testim në mjedis të
miratuar, pa përjashtime antivirus si rrugë për ta quajtur testin të kaluar.

## Kontrata e çdo bridge-i

Për të lidhur të gjitha projektet duhet fillimisht inventari i plotë i
ekosistemit. Ky checkout evidenton lidhjet më poshtë; nuk provon se ato janë
lista e plotë e projekteve të organizatës.

| Objektivi | Pika e nisjes | Hapi i ardhshëm |
| --- | --- | --- |
| Clisonix Excel Core | `Integrations/ClisonixExcelCoreBridge.cs` | Export LAGTER; konfirmim i revision-it të instaluar. Nuk është parser faturash |
| Clisonix Excel service | `Integrations/ClisonixExcelBridge.cs` | Upstream release record raporton fix të deploy-uar; `main` ende nuk e përmban. Verifikim i revision-it aktiv dhe testim i izoluar |
| OS-CLX NodeDB | `Integrations/OsClxNodeDbBridge.cs` | Runtime store i shqyrtuar është në memorie, kufi 512 node; ingest/readback me konfigurimin real të auth |
| Kloud NodeDB control plane | Repo `Kloud`, jo adapter i SPP | Vlerësim i lease/WAL sipas nevojës; WAL nuk rikuperon state pas nisjes dhe gabimet e shkrimit nuk bllokojnë API-në |
| Kërkesë fature → printim | Workflow dhe `DeviceTools` ekzistues | Plotësimi i inbox-it, OCR/nxjerrjes, klasifikimit dhe lidhjes me printimin kërkon prova/implementim; propozimi i modelit nuk e zëvendëson miratimin |
| Shërbime të tjera Clisonix | Konfigurimi i adapterëve në workflow | Vetëm endpoint-e read-only të provuara; leje të veçanta për shkrime |
| Projekte të tjera | Ende pa inventar të konfirmuar në këtë vlerësim | Regjistrim repo/service, pronari, qëllimi dhe kontrata para implementimit |

### Krahasimi i moduleve në repos e organizatës

- **Excel Core:** `clisonix.com`, `clisonix-evolution` dhe `LONGO-` kanë të
  njëjtin blob për `excel-core/lagter_v1_api.py` (`90fd8523`). Konfigurimi
  Compose i shërbimit është te `clisonix.com`; kopjet identike nuk provojnë se
  revision-i i njëjtë është instaluar. Ky modul gjeneron workbook-e LAGTER.
- **Excel parse/validate:** `clisonix-evolution` dhe `LONGO-` kanë të njëjtin
  blob `services/excel/main.py` (`7de54036`); `clisonix.com@main` ka blob tjetër
  (`e04aa2ef`). Të gjitha këto `main` të shqyrtuara mungojnë workbook-close,
  ruajtjen e vlerave zero/false, header-at unikë dhe gabimin për sheet që mungon.
  Upstream release record raporton deploy të correction nga
  `f7ab88b64aecf063b6d7987432ea04dc11fc00bd`, por PR #143 u mbyll pa merge.
  Prandaj kodi në `main` dhe kodi i raportuar si i deploy-uar nuk duhen barazuar.
- **NodeDB:** SPP lidhet me endpoint-et e runtime-it në OS-CLX. `OS-CLX@master`
  mban node-t në map në memorie me kufi 512. `Kloud@main` shton leases dhe
  append-only WAL, por node/lease state mbetet në memorie, WAL nuk replay-ohet
  pas restart-it dhe gabimet e shkrimit në WAL kapen pa u kthyer si dështim.
  OS-CLX është kontrata e tashme e SPP-së për telemetry; Kloud ka funksione
  control-plane shtesë, por nuk është zëvendësim durable.
- **Autentikimi:** qasja e GitHub CLI në kod privat nuk përdor kredencialet e
  shërbimit. Guard-at API-key/HMAC të OS-CLX aktivizohen kur konfigurohen
  sekretet; testet e integrimit të SPP vendosin kredenciale vetëm për të provuar
  atë regjim. Përdorimi real varet nga konfigurimi i runtime-it/ingress-it.
- **Faturë → printim:** Model Gateway mund të propozojë plane nga kërkesa në
  gjuhë natyrore, por i kthen si `PROPOSED_REQUIRES_REVIEW`. Nuk ka intake inbox,
  OCR ose nxjerrje të përgjithshme të fushave të faturës; `finance.reconcile`
  kërkon të dhëna të strukturuara me alokime të shqyrtuara. Printimi fizik i
  workflow-it kërkon Linux/CUPS dhe gjendja e queue nuk provon dalje letre.

Për çdo hyrje, review duhet të përmbajë:

1. Identitetin e projektit, revision-in upstream, pronarin, mjedisin dhe
   operacionet reale; një repository nuk është vetvetiu një shërbim i lidhshëm.
2. Skemën e versionuar të hyrjes/daljes, gabimet, kufijtë e madhësisë dhe
   klasifikimin e të dhënave; kontroll të pronësisë në të dy anët.
3. Destinacione të lejuara nga operatori, TLS, kredenciale shërbimi me privilegj
   minimal, rotacion dhe redaktim sekretesh në log-e.
4. Deadline, anulim dhe backpressure. Retry vetëm me semantikë të provuar;
   timeout pas një shkrimi mund të lërë rezultat të panjohur. Deduplikimi dhe
   readback përcaktohen nga kontrata upstream, jo nga supozimi i klientit.
5. Teste kontrate dhe teste me shërbimin real: auth i munguar/i gabuar,
   payload i pavlefshëm, përgjigje e dëmtuar, kufij, timeout, ndërprerje dhe
   izolim. Mock-et provojnë kontratën, jo lidhjen në prodhim.
6. Procedurë çaktivizimi/rollback, përputhshmëri versionesh dhe evidencë të
   release-it. Mungesa e shërbimit shfaqet si mungesë aftësie.

Transporti ekzistues tashmë kufizon përgjigjet, përdor deadline dhe ndalon
redirect-et automatike; këto mbrojtje ruhen. Regjistrimi i një bridge-i në CLI
nuk e regjistron automatikisht si mjet të autorizuar në workflow.

## Demonstrimi i forcës teknologjike

Demonstrimi referencë duhet të lidhë vetëm aftësi të pranuara: dokument i
autorizuar → hash origjinali → plan i shqyrtuar → përpunim real → bridge real
i miratuar → eksport i validuar → raport provash. Përfshihet një dështim i
kontrolluar në mjedisin e testit dhe rikuperimi pa humbje të origjinalit.

Për çdo demonstrim ruhen commit SHA, image digest kur përdoret container,
OS/hardware/runtime, konfigurimi pa sekrete, identitetet hash të input/output,
komandat, rezultatet dhe kohët reale. Matjet përfshijnë latency p50/p95/p99 kur
numri i mostrave e justifikon, throughput, peak memory, dështime dhe cilësi
sipas detyrës. Pragjet caktohen përpara matjes me corpus dhe ngarkesë të
përcaktuar; nuk shpiken rezultate ose objektiva universale.

Rezultatet e OCR, ruajtjes së tekstit PDF, AI dhe pajisjeve fizike deklarohen
vetëm kur janë ushtruar. Queue submission nuk provon printim fizik. Ndiqet
[protokolli i benchmark-ut](BENCHMARK_PROTOCOL.md).

## Pranimi dhe veprimi i parë

Veprimi i parë inxhinierik është P0: riprodhimi i baseline-it dhe zgjedhja e
pengesës së parë për korrigjim sipas rrezikut të konfirmuar. Nuk ndryshohet
arkitektura e prodhimit me një migrim të vetëm të madh.

Për ndryshime kodi kërkohen `dotnet build`, testet përkatëse dhe kontrollet
e workflow/failure cases të prekura sipas [rregullave të repository-t](../AGENTS.md).
Deploy kërkon rrugë rikuperimi dhe verifikim shëndeti. Release evidence duhet
të dallojë qartë: provë lokale, integrim real i izoluar dhe pranim prodhimi.

Programi nisi me dokumentacion. Ekzekutimet dhe korrigjimet pasuese të P0
regjistrohen te [evidenca e release-it](RELEASE_EVIDENCE.md).
Ky udhërrëfyes nuk ndryshon vetë vendimin e pranimit të produktit.
