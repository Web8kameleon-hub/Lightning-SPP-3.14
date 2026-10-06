# Publishing to NuGet Gallery

## Package Information

**Package Name:** LightningSPP.Universal
**Version:** 3.14.0
**Publisher:** ABA GmbH
**Author:** Ledjan Ahmati
**License:** MIT

---

## Prerequisites

### 1. NuGet Account

1. Create account at https://www.nuget.org
2. Generate API key: https://www.nuget.org/account/apikeys
3. Store API key securely

### 2. Required Tools

```bash
# .NET SDK (already installed with VS 2026)
dotnet --version

# NuGet CLI (optional, if not using dotnet pack)
choco install nuget.commandline
```

---

## Package Configuration

### 1. Update .csproj

The `wwwmmm.csproj` file should include:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <!-- Basic Info -->
    <PackageId>LightningSPP.Universal</PackageId>
    <Version>3.14.0</Version>
    <Authors>Ledjan Ahmati</Authors>
    <Company>ABA GmbH</Company>

    <!-- Description -->
    <Title>Lightning SPP 3.14 - Universal Scan-Process-Print System</Title>
    <Description>
      Ultra-fast universal scan, process, and print system optimized for ANY device.
      Includes real image processing (ImageSharp), AI/ML (Microsoft.ML), and supports
      medical devices, mobile, IoT, embedded, desktop, web, and cloud deployments.

      Technologies: TideWave, CLX, CLX.i, XCL, Resonance, NanoDecibel, Clisonix AI, Stigma.
    </Description>
    <Summary>Universal scan-process-print system with real processing for all devices</Summary>

    <!-- Project Info -->
    <PackageProjectUrl>https://github.com/Web8kameleon-hub/Lightning-SPP-3.14</PackageProjectUrl>
    <RepositoryUrl>https://github.com/Web8kameleon-hub/Lightning-SPP-3.14</RepositoryUrl>
    <RepositoryType>git</RepositoryType>

    <!-- Legal -->
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <Copyright>Copyright © 2024 ABA GmbH. All rights reserved.</Copyright>
    <PackageRequireLicenseAcceptance>true</PackageRequireLicenseAcceptance>

    <!-- README -->
    <PackageReadmeFile>README.md</PackageReadmeFile>

    <!-- Icon -->
    <PackageIcon>icon.png</PackageIcon>

    <!-- Tags -->
    <PackageTags>
      lightning;spp;scan;process;print;universal;medical;iot;mobile;
      embedded;imagesharp;ml;ai;real-processing;tidewave;resonance;
      stigma;clisonix;bochum;aba-gmbh;healthcare;medical-device
    </PackageTags>

    <!-- Release Notes -->
    <PackageReleaseNotes>
      v3.14.0 - REAL Processing Update
      - REAL image processing with SixLabors.ImageSharp
      - REAL AI/ML with Microsoft.ML and Brotli compression
      - REAL file output with actual I/O operations
      - Universal SDK for ALL devices (medical, mobile, IoT, embedded, desktop, web, cloud)
      - Lightning 3.14 API with instant (&lt;1ms), ultra-fast (&lt;5ms), and lightning (&lt;10ms) modes
      - Configurable REAL vs SIMULATED processing
      - Complete documentation and compliance files
      - Production-ready for massive deployment
    </PackageReleaseNotes>

    <!-- Build Settings -->
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Library</OutputType>
    <GeneratePackageOnBuild>false</GeneratePackageOnBuild>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>

    <!-- Documentation -->
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <DocumentationFile>bin\$(Configuration)\$(TargetFramework)\$(AssemblyName).xml</DocumentationFile>
  </PropertyGroup>

  <!-- Include Files in Package -->
  <ItemGroup>
    <None Include="README.md" Pack="true" PackagePath="\" />
    <None Include="LICENSE" Pack="true" PackagePath="\" />
    <None Include="icon.png" Pack="true" PackagePath="\" />
    <None Include="COMPLIANCE.md" Pack="true" PackagePath="docs\" />
    <None Include="SECURITY.md" Pack="true" PackagePath="docs\" />
    <None Include="PRIVACY.md" Pack="true" PackagePath="docs\" />
  </ItemGroup>

  <!-- Dependencies are auto-included -->
</Project>
```

### 2. Create Package Icon

Create a 128x128 PNG icon and save as `icon.png` in the project root.

---

## Building the Package

### Method 1: Using dotnet CLI (Recommended)

```bash
# Navigate to project directory
cd C:\Users\Admin\source\repos\wwwmmm

# Clean previous builds
dotnet clean

# Restore dependencies
dotnet restore

# Build in Release mode
dotnet build -c Release

# Create NuGet package
dotnet pack -c Release -o .\nupkg

# Output will be in: .\nupkg\LightningSPP.Universal.3.14.0.nupkg
```

### Method 2: Using Visual Studio

1. Right-click project in Solution Explorer
2. Select "Pack"
3. Package created in `bin\Release\`

### Method 3: Using NuGet CLI

```bash
# Create .nuspec if needed
nuget spec

# Pack
nuget pack wwwmmm.csproj -Properties Configuration=Release
```

---

## Publishing to NuGet.org

### Step 1: Test Locally

```bash
# Verify package contents
dotnet nuget locals all --list
dotnet nuget locals http-cache --clear

# Test install locally
dotnet add package LightningSPP.Universal --source .\nupkg
```

### Step 2: Publish

```bash
# Set your API key (one-time)
dotnet nuget push ".\nupkg\LightningSPP.Universal.3.14.0.nupkg" \
  --api-key YOUR_API_KEY_HERE \
  --source https://api.nuget.org/v3/index.json

# Or with stored API key
dotnet nuget setapikey YOUR_API_KEY_HERE --source https://api.nuget.org/v3/index.json
dotnet nuget push ".\nupkg\LightningSPP.Universal.3.14.0.nupkg" --source https://api.nuget.org/v3/index.json
```

### Step 3: Wait for Indexing

- **Indexing Time:** 10-30 minutes
- **Validation:** Automatic by NuGet
- **Status:** Check at https://www.nuget.org/packages/LightningSPP.Universal

---

## Publish Symbols Package

For better debugging experience:

```bash
# Symbols package is auto-created with IncludeSymbols=true
dotnet nuget push ".\nupkg\LightningSPP.Universal.3.14.0.snupkg" \
  --api-key YOUR_API_KEY_HERE \
  --source https://api.nuget.org/v3/index.json
```

---

## Versioning Strategy

### Semantic Versioning (SemVer)

```
MAJOR.MINOR.PATCH
  3  . 14  .  0

MAJOR: Breaking changes (incompatible API changes)
MINOR: New features (backward compatible)
PATCH: Bug fixes (backward compatible)
```

### Our Versions

- `3.14.0` - Current (REAL processing update)
- `3.15.0` - Next minor (new features planned)
- `4.0.0` - Future major (if breaking changes)

### Pre-release Versions

```
3.14.0-alpha
3.14.0-beta
3.14.0-rc1
3.14.0      (stable)
```

---

## Package Validation

Before publishing, verify:

### 1. Package Contents

```bash
# List contents
dotnet nuget verify .\nupkg\LightningSPP.Universal.3.14.0.nupkg

# Or extract and inspect
Expand-Archive .\nupkg\LightningSPP.Universal.3.14.0.nupkg -DestinationPath .\temp
```

**Should contain:**
- `lib/net10.0/` - Compiled DLLs
- `README.md`
- `LICENSE`
- `icon.png`
- `docs/` - Compliance documents
- `_rels/` - Package metadata

### 2. Dependencies

Verify all dependencies are listed correctly:
- SixLabors.ImageSharp 3.1.5
- Microsoft.ML 3.0.1
- Microsoft.ML.OnnxRuntime 1.19.2

### 3. Metadata

Check NuGet Package Explorer:

```bash
# Install NuGet Package Explorer
choco install nugetpackageexplorer

# Open package
NuGetPackageExplorer.exe .\nupkg\LightningSPP.Universal.3.14.0.nupkg
```

---

## Continuous Deployment with GitHub Actions

Create `.github/workflows/nuget-publish.yml`:

```yaml
name: Publish to NuGet

on:
  push:
    tags:
      - 'v*.*.*'

jobs:
  publish:
    runs-on: windows-latest

    steps:
    - uses: actions/checkout@v3

    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '10.0.x'

    - name: Restore dependencies
      run: dotnet restore

    - name: Build
      run: dotnet build -c Release --no-restore

    - name: Pack
      run: dotnet pack -c Release --no-build -o ./nupkg

    - name: Publish to NuGet
      run: dotnet nuget push ./nupkg/*.nupkg --api-key ${{ secrets.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json

    - name: Publish Symbols
      run: dotnet nuget push ./nupkg/*.snupkg --api-key ${{ secrets.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json
```

**Setup:**
1. Go to GitHub repository settings
2. Secrets and variables → Actions
3. Add secret: `NUGET_API_KEY`

**Trigger:**
```bash
git tag v3.14.0
git push origin v3.14.0
```

---

## Package Management

### Unlisting a Package

If you need to unlist (not delete):

```bash
# Unlist on NuGet.org
nuget delete LightningSPP.Universal 3.14.0 YOUR_API_KEY -Source https://api.nuget.org/v3/index.json -NonInteractive
```

**Note:** Unlisting doesn't delete, just hides from search. Existing users can still restore.

### Deprecating a Package

Mark as deprecated on NuGet.org:
1. Go to package page
2. Click "Deprecate"
3. Select reason and provide alternative

---

## Multi-Target Packages

For broader compatibility:

```xml
<TargetFrameworks>net10.0;net8.0;net6.0</TargetFrameworks>
```

**Benefits:**
- Wider adoption
- Support older frameworks
- Gradual migration path

**Current:** We target only `net10.0` for cutting-edge features.

---

## Localization

Add localized README files:

```
README.md
README.de.md (German)
README.sq.md (Albanian)
README.fr.md (French)
```

Reference in .csproj:
```xml
<None Include="README.de.md" Pack="true" PackagePath="\" />
```

---

## Documentation Links

### Package Page

Once published, your package will be at:
```
https://www.nuget.org/packages/LightningSPP.Universal
```

### Installation Command

Users install with:
```bash
dotnet add package LightningSPP.Universal --version 3.14.0
```

Or in Package Manager Console:
```powershell
Install-Package LightningSPP.Universal -Version 3.14.0
```

---

## Support & Maintenance

### After Publishing

1. **Monitor downloads:** Check NuGet.org statistics
2. **Watch issues:** GitHub Issues for bug reports
3. **Update regularly:** Security patches, bug fixes
4. **Respond to feedback:** Community engagement

### Version Updates

```bash
# Update version in .csproj
# Commit changes
git commit -am "Bump version to 3.15.0"
git tag v3.15.0
git push origin main --tags

# Rebuild and publish
dotnet pack -c Release
dotnet nuget push ./nupkg/*.nupkg --source https://api.nuget.org/v3/index.json
```

---

## Checklist Before Publishing

- [ ] Version number updated in .csproj
- [ ] README.md updated with latest info
- [ ] LICENSE file included
- [ ] All tests passing
- [ ] Documentation complete
- [ ] Icon image added (128x128 PNG)
- [ ] Release notes written
- [ ] Dependencies up to date
- [ ] Security vulnerabilities addressed
- [ ] Package built successfully
- [ ] Package contents verified
- [ ] API key ready
- [ ] Git tagged with version

---

## Contact

**Package Maintainer:**
Ledjan Ahmati
ABA GmbH
Email: clisonix@pm.me

**Issues:**
https://github.com/Web8kameleon-hub/Lightning-SPP-3.14/issues

---

**Lightning SPP 3.14 - Ready for NuGet!**
**© 2024 ABA GmbH. All rights reserved.**
