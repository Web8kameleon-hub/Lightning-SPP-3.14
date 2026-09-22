# Lightning SPP 3.14 - Quick Start Guide

Ky udhëzues ofron një rrugë të shkurtër për instalim, verifikim funksional dhe përdorim bazë të API-së.

Në terminal kopjo vetëm komandat brenda blloqeve të kodit, pa shenjat ``` dhe pa tekstin shpjegues të udhëzuesit.

## Kërkesat

- .NET SDK 10.0+
- Git (opsionale, për clone)
- Docker (opsionale, për ekzekutim containerized)

## 1. Instalimi

```bash
# Clone
 git clone https://github.com/Web8kameleon-hub/Lightning-SPP-3.14.git
 cd Lightning-SPP-3.14

# Build
 dotnet build
```

Opsionale (NuGet):

```bash
dotnet add package LightningSPP --version 3.14.0
```

## 2. Verifikim i shpejtë

```bash
dotnet run speed
```

Rezultatet ndryshojnë sipas hardware, por komanda duhet të ekzekutohet pa gabime dhe të prodhojë metrika latency.

## 3. Demo funksionale

```bash
dotnet run -- demo
```

Opsionale:

```bash
dotnet run universal
```

## 4. Integrim bazë në kod

```csharp
using wwwmmm.SDK;

// Ekzekutim i shpejtë për input arbitrar
var result = await Lightning314.Instant(data);
```

## 5. Përdorim sipas profilit të pajisjes

```csharp
// Medical profile
var medicalSdk = UniversalSDK.Initialize(DeviceProfile.Medical);
var medicalResult = await medicalSdk.ProcessAsync(scanData);

// Mobile profile
var mobileResult = await MobileAPI.ProcessMobile(data, batteryLevel: 75);

// IoT profile
var iotResult = await IoTAPI.ProcessConstrained(data, maxMemoryKB: 64);

// Cloud profile
var cloudResult = await CloudAPI.ProcessMassive(items, maxParallelism: 1000);
```

## 6. Ekzekutim me Docker

```bash
docker build -t lightning-spp .
docker run lightning-spp demo
```

Për serverin me Docker Compose, krijo fillimisht `.env` në dosjen e projektit.
Në Git Bash, komandat më poshtë gjenerojnë dy sekrete të ndryshme dhe ruajnë çdo `.env` ekzistues:

```bash
if [ ! -e .env ]; then
  admin_token=$(openssl rand -hex 32) &&
  signing_key=$(openssl rand -hex 32) &&
  printf 'LSPP_ADMIN_TOKEN=%s\nLSPP_LICENSE_SIGNING_KEY=%s\nHOST_PORT=13080\n' "$admin_token" "$signing_key" > .env
fi
docker compose up -d --build
docker compose ps
curl --fail http://localhost:13080/api/health
```

Nëse `.env` ekziston me vlera bosh, plotëso `LSPP_ADMIN_TOKEN` dhe
`LSPP_LICENSE_SIGNING_KEY` me dy vlera të ndryshme të gjeneruara me `openssl rand -hex 32`.
Mos e publiko `.env` dhe ruaje çelësin e nënshkrimit gjatë përditësimeve.
Serveri hapet në http://localhost:13080. Për log-et përdor `docker compose logs --tail=100`;
për ndalim përdor `docker compose down`.

## 7. Hapat e radhës

1. Lexo dokumentimin kryesor në [README.md](README.md).
2. Shiko API reference në [Docs/API_Documentation.md](Docs/API_Documentation.md).
3. Shiko udhëzuesin e deployment-it në [Docs/DEPLOYMENT.md](Docs/DEPLOYMENT.md).
4. Ekzekuto testet nëse kërkohet validim para integrimit:

```bash
dotnet test
```

## Diagnostikim i shpejtë

- `bash: ... command not found` pas kopjimit të udhëzuesit: ekzekuto vetëm komandat, jo tekstin Markdown.
- `required variable ... is missing a value`: krijo ose plotëso `.env` sipas seksionit Docker Compose më sipër.
- Nëse `dotnet build` dështon: verifiko versionin e SDK-së.
- Nëse latency është e lartë: provo input më të vogël dhe profile të ndryshme.
- Nëse ka probleme me memoria në batch: redukto madhësinë e grupit të input-eve.
