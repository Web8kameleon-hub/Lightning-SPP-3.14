# 01 — Instalimi dhe nisja e parë

Ky dokument përshkruan instalimin real të Lightning SPP 3.14.1 nga ky repository.
Çdo hap ka një **provë pranimi**: nëse prova nuk kalon, hapi nuk konsiderohet i
kryer. Nuk ka hapa "opsionalë që supozohen të suksesshëm".

## 1.1 Kërkesat

| Kërkesë | Detaj | Pse duhet |
| --- | --- | --- |
| .NET SDK | 10.0 ose më i ri (`TargetFramework` = `net10.0`) | Build dhe ekzekutim |
| Licencë SixLabors | `SIXLABORS_LICENSE` në mjedisin e build-it | ImageSharp 4.x komercial; build-i refuzon pa të |
| Poppler (`pdfinfo`, `pdftoppm`) | Vetëm për PDF real | Rendering i faqeve PDF |
| `pdftotext` | Vetëm për workflow me tekst PDF | Nxjerrje teksti pa shpikje |
| Docker | Vetëm për deployment container | Hosting i shërbimit web |
| Longo | Vetëm për llogari/entitlement | Persistencë e identitetit dhe licencave |

Poppler nuk është i detyrueshëm për `--help`, `demo`, `pdf-create` dhe konvertim
imazhi. Ai është i detyrueshëm për `pdf-render` dhe për çdo rrugë që lexon tekst
PDF. Nëse mungon, komanda dështon — nuk kthehet në përpunim të rremë.

## 1.2 Përgatitja e build-it (PowerShell, Windows)

Sekreti i build-it nuk shkon kurrë në repository. Vendoset në `.env.build`
(nuk versionohet, duhet të mbetet jashtë git) dhe ngarkohet vetëm në seancën
aktuale:

```powershell
# .env.build përmban: SIXLABORS_LICENSE=<vlera reale>
. ./Scripts/Load-BuildEnvironment.ps1
dotnet build wwwmmm.csproj -c Release
```

Nëse politika e ekzekutimit bllokon skriptin, zgjidhja e saktë është ngarkimi i
variablit në seancë ose një politikë e miratuar nga operatori — jo vendosja e
sekretit në një skedar të versionuar dhe jo çaktivizimi i përhershëm i politikës.

**Provë pranimi:** build-i kalon me `0 Warning(s)`, `0 Error(s)`.

## 1.3 Përgatitja e build-it (Docker)

Sekreti kalon si BuildKit secret, jo si `ENV`/`ARG`:

```bash
docker build --secret id=sixlabors_license,env=SIXLABORS_LICENSE -t lightning-spp .
```

Vlera nuk duhet të shfaqet në `docker history`, në Dockerfile, as në
konfigurimin runtime të kontejnerit.

**Provë pranimi:** imazhi ndërtohet; `docker history` nuk përmban sekretin.

## 1.4 Verifikimi i parë pas build-it

Renditja ka rëndësi: së pari komanda që nuk kërkon as PDF as rrjet, pastaj
përpunimi real, pastaj shërbimi.

```bash
dotnet run --project wwwmmm.csproj -- --help
dotnet run --project wwwmmm.csproj -- demo
dotnet run --project wwwmmm.csproj -- convert imazh.png jpeg 85
```

- `--help` liston komandat reale të CLI-t.
- `demo` ekzekuton validimin real të përpunimit të imazhit me fixture të
  identifikuara shprehimisht si fixture.
- `convert` lexon një imazh real, e skanon dhe shkruan skedarin e konvertuar
  pranë input-it.

**Provë pranimi:** `convert` prodhon skedar me madhësi > 0 dhe CLI shtyp
`✓ Converted ...`.

## 1.5 Instalimi nga arkiva runtime

Arkivat (`artifacts/distribution`) përmbajnë aplikacionin, runtime-in .NET,
skedarët statikë dhe njoftimet e licencave. Ato **nuk** përmbajnë çelësa
aktivizimi, kredenciale providerësh, të dhëna klientësh, modele AI të
konfiguruara ose shërbim Longo në punë.

Windows x64:

```powershell
Get-FileHash .\lightning-spp-3.14.1-win-x64.zip -Algorithm SHA256
# krahasoje me skedarin .sha256 shoqërues
Expand-Archive .\lightning-spp-3.14.1-win-x64.zip -DestinationPath .\lightning-3.14.1
cd .\lightning-3.14.1
.\wwwmmm.exe --help
.\wwwmmm.exe demo
```

Linux x64 (glibc; jo Alpine/musl):

```bash
sha256sum -c lightning-spp-3.14.1-linux-x64.tar.gz.sha256
tar -xzf lightning-spp-3.14.1-linux-x64.tar.gz
cd lightning-spp-3.14.1-linux-x64
./wwwmmm --help
./wwwmmm demo
sudo apt-get install -y poppler-utils fonts-dejavu-core   # vetëm nëse duhet PDF
```

**Provë pranimi:** hash-i përputhet; `--help` dhe `demo` ekzekutohen.

## 1.6 Nisja e shërbimit web

```bash
dotnet run --project wwwmmm.csproj -- serve 13080
# ose në container:
docker run --rm -p 127.0.0.1:13080:13080 --env-file .env lightning-spp
```

Porti merret nga argumenti, përndryshe nga `PORT`, përndryshe `13080`.
Host-i i bind-it merret nga `LSPP_BIND_HOST`; brenda container-it default është
`+`, jashtë tij `localhost`.

**Provë pranimi:**

```bash
curl -fsS http://localhost:13080/api/health
```

Duhet të kthejë status 200 me `ok=true`. Nëse kthen 500 ose mbyllet lidhja,
shiko `08-ZGJIDHJA-E-PROBLEMEVE.md` — mos e shpall shërbimin "në punë".

## 1.7 Konfigurimi minimal para çdo ekspozimi

Para se shërbimi të shihet nga jashtë, duhet të ekzistojnë të paktën:

```bash
LSPP_ADMIN_TOKEN=<sekret i rastësishëm ≥32 bajt>
LSPP_LICENSE_SIGNING_KEY=<sekret i rastësishëm ≥32 bajt>
LSPP_DATA_DIR=/var/lib/lightning          # rrugë ABSOLUTE
LSPP_ENVIRONMENT=Production
```

Procedura e plotë, përfshirë rotacionin e çelësave dhe proxy-t e besuar, është
te `02-KONFIGURIMI.md`. Rregullat e detyrueshme janë te
`06-RREGULLAT-E-SIGURISE.md`.

## 1.8 Përditësimi pa humbje të dhënash

1. Ndal procesin e vjetër; mos e fshi drejtorinë e vjetër.
2. Ekstrakto versionin e ri në një drejtori **të re**.
3. Ruaj të paprekur: drejtorinë e vjetër, mjedisin privat, të dhënat e
   klientëve, `LSPP_DATA_DIR` dhe kopjet e Longo.
4. Verifiko `--help`, një dokument përfaqësues dhe `/api/health`.
5. Vetëm pas kësaj kalo trafikun.

Rikthimi nga një dështim aplikacioni bëhet duke rikthyer drejtorinë e
ekzekutuesit. Të dhënat e klientëve nuk kthehen mbrapsht pa një procedurë
rikuperimi të shqyrtuar.

## 1.9 Çka instalimi nuk provon

Instalimi i suksesshëm provon që aplikacioni niset dhe përpunon atë që u
testua. Ai **nuk** provon:

- skanim fizik nga skaner harduerik (nuk është verifikuar në këtë repository);
- printim fizik (vetëm Linux, përmes radhës së sistemit, i paverifikuar në
  harduer);
- inferencë ONNX me model real të klientit;
- pranim nga klient i jashtëm ose përputhshmëri mjekësore.

Këto mbeten të hapura deri sa të ekzistojë provë e regjistruar; shih
`10-KUFIJTE-DHE-PRETENDIMET.md` dhe `11-MATRICA-E-PRANIMIT.md`.
