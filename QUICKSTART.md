# Lightning SPP 3.14 - Quick Start Guide

Ky udhëzues ofron një rrugë të shkurtër për instalim, verifikim funksional dhe përdorim bazë të API-së.

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
dotnet run demo
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

## 7. Hapat e radhës

1. Lexo dokumentimin kryesor në [README.md](README.md).
2. Shiko API reference në [Docs/API_Documentation.md](Docs/API_Documentation.md).
3. Shiko udhëzuesin e deployment-it në [Docs/DEPLOYMENT.md](Docs/DEPLOYMENT.md).
4. Ekzekuto testet nëse kërkohet validim para integrimit:

```bash
dotnet test
```

## Diagnostikim i shpejtë

- Nëse `dotnet build` dështon: verifiko versionin e SDK-së.
- Nëse latency është e lartë: provo input më të vogël dhe profile të ndryshme.
- Nëse ka probleme me memoria në batch: redukto madhësinë e grupit të input-eve.
