# Dosja e përputhshmërisë — Lightning SPP

Kjo dosje përgatit **rrugën** drejt certifikimit. Nuk është certifikim.

## Deklarata e statusit

| Pyetje | Përgjigje |
| --- | --- |
| A është Lightning SPP i certifikuar ISO 13485? | **Jo.** Nuk ka auditim nga organ i certifikimit. |
| A ka CE markë (MDR 2017/745)? | **Jo.** Nuk ka deklaratë konformiteti të nënshkruar nga organ i njoftuar. |
| A ka FDA 510(k) clearance? | **Jo.** Nuk ka parashtresë të aprovuar. |
| A është HIPAA compliance verified? | **Jo si certifikim.** Kontrollet teknike ekzistojnë; marrëveshjet dhe auditimi mbeten të operatorit. |
| A është GDPR compliant? | **Jo si certifikim.** Kontrollet ekzistojnë; detyrimet ligjore mbeten të operatorit. |
| A është ISO 27001 i certifikuar? | **Jo.** Ka kontrolle të dokumentuara, jo certifikatë. |

Çdo deklaratë tjetër në repository që thotë të kundërtën është e pasaktë dhe duhet
korrigjuar. Shih `../MANUAL/10-KUFIJTE-DHE-PRETENDIMET.md` §10.4.

## Çka kjo dosje ofron realisht

1. **Analizë boshllëqesh** ndaj standardeve që prekin produktin
   (`GAP_ANALYSIS.md`): çka kërkohet, çka ekziston në kod, çka mungon.
2. **Regjistër rreziqesh** (`RISK_REGISTER.md`) sipas logjikës ISO 14971:
   rrezik → shkak → kontroll → rrezik i mbetur.
3. **Inventar SOUP** (`SOUP_INVENTORY.md`): çdo varësi e palës së tretë me
   version, licencë dhe rol, sipas IEC 62304.
4. **Kontrolle sigurie** (`SECURITY_CONTROLS.md`): çdo kontroll me provën
   përkatëse në kod ose test.
5. **Gjurmueshmëri** (`TRACEABILITY.md`): kërkesë → kontroll → provë.

## Rendi i punës për t'u bërë i certifikueshëm

Fazat nuk mund të kalohen me dokumentacion; secila kërkon provë.

| Fazë | Puna | Dalja |
| --- | --- | --- |
| C0 | Kjo dosje: inventar, rreziqe, boshllëqe | Dokumente të shqyrtuara |
| C1 | Klasifikim i pajisjes (SaMD) dhe tregu i synuar | Vendim i shkruar, i arsyetuar |
| C2 | Sistemi i menaxhimit të cilësisë (QMS) | Procedura, trajnim, auditim i brendshëm |
| C3 | Zhvillim sipas IEC 62304 (Klasa B ose C) | Plane, arkitekturë, teste, gjurmim |
| C4 | Menaxhim rreziku ISO 14971 i plotë | Skedar rreziku i miratuar |
| C5 | Usability IEC 62366-1 | Vlerësim formues + përmbledhës |
| C6 | Vlerësim klinik / performancë | Raport klinik ose i performancës |
| C7 | Auditim nga organ i njoftuar / FDA | Certifikatë ose clearance |

**Asnjë fazë nuk provohet nga ky repository vetëm.** Fazat C1–C7 kërkojnë njerëz,
procedura dhe auditorë të jashtëm.

## Kush e mban këtë dosje

Dosja mbahet nga pronari i sigurisë dhe pronari i release-it. Çdo rresht i
regjistrit të rreziqeve ka pronar. Një rrezik pa pronar është rrezik i
pamenaxhuar.

## Lidhje

- [Manuali i sigurisë](../MANUAL/06-RREGULLAT-E-SIGURISE.md)
- [Matrica e pranimit](../MANUAL/11-MATRICA-E-PRANIMIT.md)
- [Rishikimi i sigurisë së llogarive](../ACCOUNT_SECURITY_REVIEW.md)
- [SLA/SLO](../SLA_SLO.md)
