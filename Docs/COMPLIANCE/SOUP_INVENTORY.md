# Inventari SOUP — varësitë e palës së tretë

Sipas IEC 62304, çdo komponent i palës së tretë (SOUP) duhet identifikuar,
versionuar dhe vlerësuar. Ky inventar merret **nga `wwwmmm.csproj`**, jo nga
kujtesa.

## Paketat NuGet

| Paketa | Versioni | Licenca | Roli | Kritikaliteti |
| --- | --- | --- | --- | --- |
| SixLabors.ImageSharp | 4.1.3 | Six Labors Split License (komerciale për përdorim komercial) | Motor imazhi R, kodim PNG/JPEG/BMP/TIFF | Lartë |
| SkiaSharp | 4.153.1 | MIT | Motor imazhi L (Skia) | Lartë |
| SkiaSharp.NativeAssets.Linux.NoDependencies | 4.153.1 | MIT | Biblioteka native për Linux | Lartë |
| Microsoft.ML | 5.0.0 | MIT | Kontekst ML (jo inferencë e konfiguruar) | Mesatar |
| Microsoft.ML.OnnxRuntime | 1.19.2 | MIT | Inferencë ONNX kur konfigurohet model | Mesatar |
| Stripe.net | 51.1.0 | Apache-2.0 | Pagesa dhe faturim | Lartë |
| Microsoft.VisualStudio.Azure.Containers.Tools.Targets | 1.23.0 | MIT | Mjete build container | I ulët |

## Komponentë të platformës

| Komponenti | Versioni | Rol |
| --- | --- | --- |
| .NET / ASP.NET Core (`Microsoft.AspNetCore.App` framework reference) | net10.0 | Host, Kestrel-less HttpListener, DI, antiforgery |
| `System.Security.Cryptography` | pjesë e runtime | ECDSA, AES-GCM, PBKDF2, HMAC |
| `System.IO.Compression` | pjesë e runtime | Brotli |

## Mjete të jashtme (jo biblioteka, por kërkohen në runtime)

| Mjeti | Kërkohet për | Nëse mungon |
| --- | --- | --- |
| `pdfinfo`, `pdftoppm` (Poppler) | `document pdf-render` | Komanda dështon |
| `pdftotext` (Poppler) | Tekst PDF në workflow | Nxjerrja e tekstit dështon |
| CUPS (`lp`, `lpstat`) | Printim fizik (Linux) | `PlatformNotSupportedException` ose dështim |
| SANE (`scanimage`) | Skanim fizik (Linux) | Dështim |
| Longo (shërbim i veçantë) | Llogari dhe entitlement | Regjistrimi/hyrja nuk punojnë |
| Docker | Deployment container | Vetëm instalim alternativ |

## Vlerësimi i SOUP — çka mungon

IEC 62304 kërkon për çdo SOUP:

1. **Identifikim dhe version** — ✅ i plotë më sipër.
2. **Vlerësim i funksionit dhe rrezikut** — ⚠️ pjesërisht: rreziqet R-03 dhe
   R-04 në `RISK_REGISTER.md` mbulojnë dekodimin e imazhit.
3. **Kërkesat e performancës** — ⚠️ jo të dokumentuara për ImageSharp/Skia.
4. **Verifikim i funksionimit** — ✅ pjesërisht: testet e `OutputAdapter` dhe
   `engines` ekzekutojnë të dy motorët realisht.
5. **Monitorim i dobësive** — ❌ mungon: nuk ka proces periodik për CVE.
6. **Plan për përditësime** — ❌ mungon: përditësimet bëhen ad hoc.

## Rreziku kryesor i SOUP

`SixLabors.ImageSharp` është i licencuar komercialisht: build-i **refuzon** të
kompilojë pa `SixLaborsLicenseKey`. Kjo është provë reale e varësisë — jo
dokumentim. Nëse licenca skadon, build-i dështon; kjo është sjellje e mirë, por
duhet planifikuar.

Vlera e licencës **nuk versionohet** dhe nuk duhet të shfaqet në log ose në
`docker history`. Shih `../MANUAL/02-KONFIGURIMI.md` §2.11.

## Veprimet e kërkuara

| Veprim | Pronari | Statusi |
| --- | --- | --- |
| Proces mujor i monitorimit të CVE për NuGet | Security | I hapur |
| Vlerësim i shkruar rreziku për ImageSharp/Skia | Security + Engine | I hapur |
| Plan përditësimi me testim regresioni | Release | I hapur |
| Regjistrim i skadencës së licencës SixLabors | Release | I hapur |
