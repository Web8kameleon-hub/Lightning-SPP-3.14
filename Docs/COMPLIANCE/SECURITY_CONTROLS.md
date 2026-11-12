# Kontrollet e sigurisë — me provën e tyre

Çdo kontroll emërtohet me vendin në kod dhe me provën që e verifikon. Një
kontroll pa provë nuk llogaritet.

## Kriptografia

| Kontrolli | Implementimi | Prova |
| --- | --- | --- |
| Nënshkrim asimetrik i licencës | `Licensing/LicenseSignature.cs` — ECDSA P-256 / SHA-256, format IEEE P1363 | `LicenseAsymmetricTests` (5 teste) |
| Verifikim pa sekret në pajisje | `LicenseKey.Validate` dega `LSP3` përdor vetëm çelësin publik | `AsymmetricKey_ValidatesWithPublicKeyOnly` |
| Ri-validim i gjendjes së ruajtur | `LicenseManager.GetActiveLicense` verifikon nënshkrimin në çdo ngarkim | `AsymmetricKey_TamperedTier_IsRejected` |
| Nënshkrim HMAC (legacy, vetëm server) | `LicenseKey.ComputeSignature` | `LicenseKeyRotationTests` |
| Ruajtje me AES-256-GCM | `LicenseManager.EncryptLicense` / `DecryptLicense` | Testet e ciklit të klientit |
| Fjalëkalim PBKDF2 600k | `CustomerAccounts` (Identity V3) | `CustomerLifecycleTests` |
| Krahasim sekreti në kohë konstante | `RuntimeSecurity.SecretEquals` (SHA-256 + `FixedTimeEquals`) | Përdoret në kontrollin e admin token |

## Autentikimi dhe autorizimi

| Kontrolli | Implementimi | Prova |
| --- | --- | --- |
| Admin Bearer i detyrueshëm | `LicenseApiServer` — `/api/license/issue`, `/activate`, `/signing-status` | Teste integrimi: 401 pa token |
| Sesione me cookie + CSRF | `CustomerAccounts`, `CustomerStore` | `AccountFlowTests`, `ApiCorsIntegrationTests` |
| Revokim sesioni në ndryshim fjalëkalimi | `CustomerStore` | `CustomerLifecycleTests` |
| Token verifikimi/rikthimi i hash-uar, njëpërdorimshëm | `CustomerStore` | Testet e ripërdorimit |
| Izolim pronari në workspace | `WorkflowApi.WorkspaceAllowed` | Testet e autorizimit |
| Leje të deklaruara nga operatori, jo nga input-i | `Workflow/WorkflowRunner` | Testet e planit |

## Sipërfaqja e rrjetit

| Kontrolli | Implementimi | Prova |
| --- | --- | --- |
| Kufi trupi 1 MiB (8 MiB te `/api/try/*`) | `LicenseApiServer.MaxBodyBytes`, `TryMaxUploadBytes` | Teste HTTP |
| Kufi norme 30/min për IP | `RequestRateLimiter`, `RateLimitMaxRequests` | Provë manuale + testet e limiter-it |
| Kufi i veçantë 10/5 min për `/api/try/*` | `TryRateLimitMaxRequests` | Po |
| Një përpunim i vetëm njëkohësisht për try | `SemaphoreSlim(1,1)` | Po |
| `/api/health` i përjashtuar nga limiter-i | Kushti në `HandleRequest` | Po |
| CORS vetëm me origjina të konfiguruara | `LSPP_ALLOWED_ORIGINS` | `ApiCorsIntegrationTests` |
| Proxy headers vetëm nga IP të besuara | `LSPP_TRUSTED_PROXY_IPS` | `TrustedProxyTests` |
| Path traversal i bllokuar në statike | Verifikim `StartsWith(wwwroot)` | Kontroll kodi |

## Bridges

| Kontrolli | Implementimi | Prova |
| --- | --- | --- |
| URL vetëm nga mjedisi i operatorit | `BridgeCommands.Connection` | Kontroll kodi |
| HTTPS i detyrueshëm (loopback vetëm teste) | `BridgeConnection` | Testet e kontratës |
| Pa redirect automatik | `AllowAutoRedirect = false` | Testet e kontratës |
| Deadline 30 s | `BridgeConnection` | Testet e timeout-it |
| Kufij përgjigjeje 2/10 MiB dhe 64 KiB | `ReadBoundedFileAsync`, lexues i kufizuar | Testet e kufijve |
| Pa retry automatik për shkrime | Dizajn | `Docs/BRIDGES.md` |

## Dështim i mbyllur (fail closed)

| Skenari | Sjellja |
| --- | --- |
| Konfigurim production i mangut | Nisja dështon (`RuntimeSecurity.ValidateProduction`) |
| Pa çelës publik dhe pa sekret | Licenca `LSP3` refuzohet |
| Nënshkrim i pavlefshëm ose i ndryshuar | Refuzohet |
| Workflow i fikur në production | `503` |
| Email i paverifikuar, fjalëkalim i saktë | `401` me `code`, pa sesion |
| Plan me mjet të panjohur | Refuzohet para ekzekutimit |

## Çka NUK është kontroll i provuar

- Terminimi i TLS: bëhet nga proxy-i, jo nga aplikacioni.
- Printimi fizik: vetëm dorëzim në radhë.
- Skanimi fizik: i paverifikuar në harduer.
- Monitorim i vazhdueshëm: metrikat janë në memorie.
- Zbulim mashtrimi me karta: i delegon Stripe.

## Lidhja

- Rreziqet përkatëse: `RISK_REGISTER.md`
- Rregullat operativë: `../MANUAL/06-RREGULLAT-E-SIGURISE.md`
- Rishikimi i llogarive: `../ACCOUNT_SECURITY_REVIEW.md`
