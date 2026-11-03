# Analiza e boshllëqeve — ndaj standardeve

Për secilin standard: çka kërkohet, çka ekziston **në këtë repository**, çka
mungon. "Ekziston" do të thotë provë e gjetur në kod ose test; "mungon" do të
thotë që kërkon punë njerëzore ose auditim të jashtëm.

## IEC 62304 — cikli i jetës së softuerit mjekësor

| Kërkesa | Gjendja | Prova ose boshllëku |
| --- | --- | --- |
| Klasifikim sigurie (A/B/C) | Mungon | Kërkon vendim të dokumentuar |
| Plan zhvillimi | Pjesërisht | AGENTS.md, TECHNOLOGY_EVOLUTION.md |
| Kërkesa softuerike | Pjesërisht | API_Documentation.md, FUNCTIONS_REFERENCE.md |
| Arkitekturë | Po | ARCHITECTURE.md, Configuration/, Services/ |
| Dizajn i detajuar | Pjesërisht | Komente XML në kod |
| Njësi testimi | Po | Tests/LightningSPP.Tests (343 teste) |
| Integrim testimi | Po | Teste HTTP, Longo, Stripe, bridges |
| Testim sistemi | Pjesërisht | hosting-smoke.sh, GO_LIVE_CHECKLIST.md |
| Gjurmueshmëri | Mungon | Fillimi te TRACEABILITY.md |
| Menaxhim konfigurimi | Po | Git, commit SHA, image digest |
| Menaxhim defektesh | Pjesërisht | GitHub Issues, pa procedurë |
| Verifikim SOUP | Mungon | Inventari ekziston, verifikimi jo |

## ISO 14971 — menaxhim rreziku

| Kërkesa | Gjendja | Prova ose boshllëku |
| --- | --- | --- |
| Identifikim rreziqesh | Po (fillestar) | RISK_REGISTER.md |
| Vlerësim rreziku | Po (fillestar) | Severity dhe probability |
| Kontroll rreziku | Pjesërisht | Kontrolle teknike të listuara |
| Rrezik i mbetur | Po (fillestar) | Kolonë në regjistër |
| Raport i menaxhimit të rrezikut | Mungon | Kërkon miratim njerëzor |
| Rishikim pas prodhimit | Mungon | Nuk ka survejim |

## IEC 62366-1 — inxhinieri e përdorshmërisë

| Kërkesa | Gjendja |
| --- | --- |
| Specifikim i përdorimit | Mungon |
| Rreziqe të ndërfaqes | Mungon |
| Vlerësim formues | Mungon |
| Vlerësim përmbledhës me përdorues realë | Mungon |

Ka UI (Web/wwwroot), por UI nuk është vlerësim përdorshmërie.

## ISO 13485 — sistemi i menaxhimit të cilësisë

| Kërkesa | Gjendja |
| --- | --- |
| Manual cilësie | Mungon |
| Kontroll dokumentesh | Pjesërisht (git, pa procedurë) |
| Auditim i brendshëm | Mungon |
| Trajnim i personelit | Mungon |
| Veprim korrigjues (CAPA) | Mungon |
| Mbikëqyrje e furnitorëve | Mungon |

## MDR 2017/745 (CE)

| Kërkesa | Gjendja |
| --- | --- |
| Klasifikim dhe rregull i zbatueshëm | Mungon |
| Dokumentacion teknik | Pjesërisht (ky repo) |
| Deklaratë konformiteti (DoC) | Mungon |
| Organ i njoftuar | I pazgjedhur |
| Vlerësim klinik | Mungon |
| PMS / PMCF | Mungon |
| UDI dhe regjistrim | Mungon |

## FDA 510(k) — Klasa II

| Kërkesa | Gjendja |
| --- | --- |
| Indications for use | Mungon |
| Krahasim me predicate | Mungon |
| Testim i performancës | Pjesërisht (matje lokale) |
| Dokumentacion softuerik | Pjesërisht |
| Dokumentacion kibernetik | Pjesërisht (SECURITY_CONTROLS.md) |
| Labeling / IFU | Mungon |

## HIPAA

| Kërkesa | Gjendja | Prova |
| --- | --- | --- |
| Kontroll aksesi | Po | PBKDF2, sesione, role |
| Auditim | Pjesërisht | InternalAuditLogger, SigningAuditTrail |
| Integritet | Po | Nënshkrim ECDSA i licencave, AesGcm për ruajtje |
| Enkriptim në transit | Jashtë aplikacionit | TLS nga proxy |
| Marrëveshje (BAA) | Mungon | Kërkon nënshkrim juridik |
| Procedura e incidenteve | Pjesërisht | MANUAL/06 §6.12 |
| Trajnim i personelit | Mungon | — |

## GDPR

| Kërkesa | Gjendja |
| --- | --- |
| Bazë ligjore për përpunim | Mungon (vendim i operatorit) |
| Regjistër i aktiviteteve të përpunimit | Mungon |
| DPIA | Mungon |
| Njoftim shkeljesh brenda 72 orësh | Mungon procedurë |
| Të drejtat e subjektit (akses, fshirje) | Pjesërisht (fshirje manuale) |
| Transferime ndërkombëtare | Mungon vlerësim |

## ISO 27001

| Domen | Gjendja |
| --- | --- |
| Inventar asetesh | Pjesërisht (SOUP_INVENTORY.md) |
| Kontroll aksesi | Po |
| Kriptografi | Po (ECDSA, AES-GCM, PBKDF2) |
| Regjistrim dhe monitorim | Pjesërisht (metrika në memorie) |
| Menaxhim incidentesh | Pjesërisht |
| Vazhdimësi biznesi / backup | Pjesërisht (MANUAL/07) |
| Auditim i brendshëm ISMS | Mungon |

## Boshllëqet kritike, sipas rendit

1. **Klasifikimi i pajisjes** (C1) — pa të, standardet e tjera janë të pacaktuara.
2. **Matricë gjurmueshmërie** kërkesë për test (shih TRACEABILITY.md).
3. **QMS dhe auditim i brendshëm** (C2) — asnjë organ nuk certifikon pa të.
4. **Vlerësim klinik / i performancës** (C6).
5. **Proces i menaxhimit të rreziqeve i miratuar**, jo vetëm regjistër.

Deri atëherë, gjuha e saktë është: "kontrolle teknike të dokumentuara,
certifikim i papranuar".
