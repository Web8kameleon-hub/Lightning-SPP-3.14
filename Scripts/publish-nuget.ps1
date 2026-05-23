param(
    [string]$Configuration = "Release",
    [string]$Version = "3.14.0"
)

$ErrorActionPreference = "Stop"

if (-not $env:NUGET_API_KEY) {
    Write-Error "NUGET_API_KEY environment variable is required."
    exit 1
}

dotnet restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet build --configuration $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$outDir = Join-Path $PSScriptRoot "..\nupkg"
dotnet pack --configuration $Configuration --no-build -p:PackageVersion=$Version --output $outDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet nuget push "$outDir\*.nupkg" --api-key $env:NUGET_API_KEY --source https://api.nuget.org/v3/index.json --skip-duplicate
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "NuGet publish completed." -ForegroundColor Green
