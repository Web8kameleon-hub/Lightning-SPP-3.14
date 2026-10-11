# LightningSPP NuGet packaging

The project defines `LightningSPP` version `3.14.1`, targeting `.NET 10`.
The 11 October 2026 audit found no published NuGet package under `LightningSPP`
or `LightningSPP.Universal`. Packaging successfully is not publication evidence.

## License metadata

The package contains MIT SDK/client components and commercial engine/runtime
components. `PackageLicenseFile` points to `LICENSE`; the package also includes
`LICENSE-MIT.txt` and `LICENSE-COMMERCIAL.txt`. It must not be labeled MIT-only.
See [LICENSE](LICENSE) for directory scope, existing MIT grants and third-party
license boundaries. Commercial source redistribution requires an applicable
written agreement; an activation key alone does not grant it.

## Build and inspect

Configure a valid private Six Labors license using the dependency's supported
license-file configuration. Keep that license, API keys and credentials outside
the repository and package.

```powershell
dotnet build wwwmmm.csproj -c Release
dotnet test Tests/LightningSPP.Tests/LightningSPP.Tests.csproj -c Release
dotnet pack wwwmmm.csproj -c Release --no-build -o nupkg
```

Inspect the generated `.nupkg` archive and `.nuspec`: verify the package ID,
version, framework, all three license notices, SDK/runtime assemblies and
required dependencies. Verify installation and real processing in a clean
consumer environment before accepting a distribution release. A NuGet package
is not a Windows MSI/MSIX or Linux DEB/RPM installer.

## Publication

The CI `publish-nuget` job runs for a published GitHub release after its required
build/test and Docker jobs pass. It requires `NUGET_API_KEY`; never print or commit
the key. Pushing `main` triggers production deployment, not NuGet publication.
After authorized publication, verify the exact version on NuGet and retain the
build/install evidence. Do not advertise a download before that verification.
