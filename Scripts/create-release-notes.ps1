param(
    [string]$Version = "v3.14.0"
)

$notes = @"
# Lightning SPP $Version

## Highlights
- REST API for cloud integration
- Web dashboard for license management
- Desktop drag-and-drop GUI scaffold
- Internal audit logging for key issuance

## Artifacts
- NuGet: LightningSPP
- Docker: lightning-spp

## Notes
- For NuGet publish set NUGET_API_KEY.
- For GitHub release use:
  gh release create $Version --title "Lightning SPP $Version" --notes-file CHANGELOG.md
"@

$target = Join-Path $PSScriptRoot "..\release-notes-$Version.md"
Set-Content -Path $target -Value $notes -Encoding UTF8
Write-Host "Release notes created at $target" -ForegroundColor Green
