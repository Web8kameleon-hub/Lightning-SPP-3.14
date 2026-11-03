# Regjistri i rreziqeve

Strukturë sipas ISO 14971: rrezik → shkak → kontroll → rrezik i mbetur.
**Ky regjistër nuk zëvendëson një skedar rreziku të miratuar.** Është baza mbi
të cilën një pronar rreziku vendos dhe nënshkruan.

## Shkallët

**Ashpërsia (S):** 1 = shqetësim; 2 = humbje të dhënash/kohe; 3 = gabim
përpunimi i dukshëm; 4 = dëmtim financiar/ligjor; 5 = dëmtim i personit.

**Probabiliteti (P):** 1 = i rrallë; 2 = i mundshëm; 3 = i mundshëm shpesh;
4 = i zakonshëm.

**Rezultati** = S × P. Prag: ≥ 12 kërkon kontroll para prodhimit; ≥ 8 kërkon
kontroll të dokumentuar dhe provë; < 8 monitorohet.

## Rreziqet

| ID | Rreziku | Shkak | S | P | Rez. | Kontrolli | Rrezik i mbetur | Pronari |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| R-01 | Licencë e falsifikuar pranohet | Ruajtja lokale lexohej pa verifikim nënshkrimi | 4 | 3 | 12 | Ri-validim me ECDSA në çdo ngarkim (`LicenseManager.GetActiveLicense`) | I ulët — provë: `LicenseAsymmetricTests` | Security |
| R-02 | Sekreti i emetuesit shpërndahet me klientin | Aktivizimi offline kërkonte HMAC | 5 | 2 | 10 | Format LSP3 me çelës publik; HMAC mbetet vetëm në server | I ulët — nuk kërkohet sekret në pajisje | Security |
| R-03 | Input keqdashës dëmton përpunimin | Dekodim imazhi nga payload arbitrar | 3 | 3 | 9 | `SKBitmap.Decode` + refuzim i PDF/binar; kufi 8M pikselë | Mesatar — teste dekodimi | Engine |
| R-04 | Shterje memorie nga input i madh | Rezolucion i pakufizuar | 3 | 3 | 9 | Kufi pikselësh dhe anësh; batch ndahet | I ulët | Engine |
| R-05 | Shërbimi bllokohet nga një klient | Limiter global kur proxy-t headers janë të gabuar | 3 | 3 | 9 | `LSPP_TRUSTED_PROXY_IPS` i detyrueshëm me headers; dokumentuar | Mesatar nëse konfigurimi gabon | Platform |
| R-06 | Dokument i klientit përzihet me tjetrin | Rrugë e përbashkët output-i | 4 | 2 | 8 | `LSPP_DATA_DIR` + workspace për përdorues; `PrintOutputDirectory` i veçantë | I ulët me konfigurim të saktë | Platform |
| R-07 | Printim fizik i dubluar | Ripërsëritje pas timeout-it të radhës | 2 | 3 | 6 | `print-status` raporton `physicalCompletionVerified=false`; procedurë readback | Mesatar — kërkon disiplinë operative | Operations |
| R-08 | Shkrim i dyfishtë në upstream | Retry pas timeout-it të bridge-it | 3 | 3 | 9 | Pa retry automatik; readback i detyrueshëm; dokumentuar | Mesatar | Integrations |
| R-09 | SSRF përmes bridge-it | URL nga përdoruesi | 4 | 2 | 8 | Vetëm URL nga mjedisi; pa redirect; HTTPS | I ulët | Security |
| R-10 | Modeli jep udhëzim të gabuar si fakt | Përgjigje LLM pa verifikim | 3 | 4 | 12 | Etiketë `MODEL_CLAIM_REQUIRES_REVIEW`; modeli nuk jep leje | Mesatar — kërkon shqyrtim njerëzor | Product |
| R-11 | Metrikat keqinterpretohen si SLO historik | Numërues në memorie | 2 | 3 | 6 | Dokumentuar në `SLA_SLO.md` dhe manual | I ulët | Operations |
| R-12 | Email-i i verifikimit nuk arrin | Provider i paverifikuar | 3 | 3 | 9 | Gabim i raportuar, nuk supozohet sukses | Mesatar — varet nga provider | Accounts |
| R-13 | Pagesë e përpunuar dy herë | Event i përsëritur webhook | 4 | 2 | 8 | Dedup eventesh; gjendje e ruajtur | I ulët | Billing |
| R-14 | Certifikim i pretenduar pa provë | Dokumente marketing | 4 | 3 | 12 | Korrigjuar në `../MANUAL/10`, `Wiki/Home.md`; kërkon rishikim të vazhdueshëm | I ulët nëse mbahet | Product |
| R-15 | Backup i paprovuar | Restore nuk testohet | 4 | 2 | 8 | Procedurë javore restore në `../MANUAL/07` | Mesatar deri në ushtrimin e parë | Operations |
| R-16 | Shërbim i vjetëruar mbetet aktiv | Deploy pa kullim | 3 | 2 | 6 | Blue-green me drain; rollback i provuar | I ulët | Operations |
| R-17 | Përdorim mjekësor pa validim | Produkti përdoret klinikisht | 5 | 2 | 10 | Deklaratë e qartë e kufirit; certifikim i papranuar | **I lartë — i papranuar** | Product |

## Rreziqet që NUK janë zbutur

| ID | Pse mbetet i hapur |
| --- | --- |
| R-17 | Kërkon vlerësim klinik dhe certifikim; asnjë kontroll softuerik nuk e zëvendëson |
| R-07 | Kërkon pajisje reale dhe procedurë operative; nuk provohet në kod |
| R-12 | Varet nga një provider i jashtëm i verifikuar |
| R-15 | Kërkon ushtrim të regjistruar restore |

## Rregullat e regjistrit

1. Çdo rrezik me rezultat ≥ 12 bllokon release-in derisa kontrolli të provohet.
2. Një rrezik pa pronar është i pamenaxhuar — konsiderohet i hapur.
3. Kontrolli shkruhet me provën e tij (test, komandë, dokument), jo me qëllim.
4. Rreziku i mbetur rivlerësohet pas çdo ndryshimi që prek kontrollin.
5. Ky regjistër rishikohet në çdo release; ndryshimi i statusit kërkon provë.
