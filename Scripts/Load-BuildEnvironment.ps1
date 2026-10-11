param([string]$PrivateEnvPath = (Join-Path $PSScriptRoot '../.env.build'))

$licenseEntries = @(Get-Content -LiteralPath $PrivateEnvPath -ErrorAction Stop |
    Where-Object { $_ -match '^SIXLABORS_LICENSE=' })
if ($licenseEntries.Count -ne 1) {
    throw 'The private build environment must contain one SIXLABORS_LICENSE entry.'
}
$buildLicense = $licenseEntries[0].Substring('SIXLABORS_LICENSE='.Length).Trim()
if ([string]::IsNullOrWhiteSpace($buildLicense)) {
    throw 'SIXLABORS_LICENSE is empty.'
}
$env:SIXLABORS_LICENSE = $buildLicense
$env:SixLaborsLicenseKey = $buildLicense
Remove-Variable buildLicense, licenseEntries
Write-Output 'Private build environment loaded; license value is not displayed.'
