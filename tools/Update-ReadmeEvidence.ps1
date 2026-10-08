param(
    [string]$EvidencePath = "evidence/evidence.json",
    [string]$OutputPath = "evidence/current-status.md",
    [string]$ReadmePath = "README.md"
)

$ErrorActionPreference = "Stop"

$startMarker = "<!-- LIGHTNING-EVIDENCE:START -->"
$endMarker   = "<!-- LIGHTNING-EVIDENCE:END -->"

if (-not (Test-Path -LiteralPath $EvidencePath)) {
    throw "Evidence file not found: $EvidencePath"
}

if (-not (Test-Path -LiteralPath $ReadmePath)) {
    throw "README file not found: $ReadmePath"
}

$e = Get-Content -LiteralPath $EvidencePath -Raw | ConvertFrom-Json

$required = @(
    "product",
    "commit",
    "recordedAt",
    "evidenceStatus",
    "build",
    "tests",
    "verificationGaps"
)

foreach ($name in $required) {
    if (-not ($e.PSObject.Properties.Name -contains $name)) {
        throw "Missing required evidence field: $name"
    }
}

foreach ($name in @("total", "passed", "failed", "skipped")) {
    if (-not ($e.tests.PSObject.Properties.Name -contains $name)) {
        throw "Missing required tests field: $name"
    }
}

$total   = [int]$e.tests.total
$passed  = [int]$e.tests.passed
$failed  = [int]$e.tests.failed
$skipped = [int]$e.tests.skipped

if ($total -le 0) {
    throw "Zero tests cannot establish evidence."
}

if ($total -ne ($passed + $failed + $skipped)) {
    throw "Inconsistent test totals."
}

if ($failed -gt 0 -and $e.evidenceStatus -eq "GREEN") {
    throw "Evidence cannot be GREEN while tests are failing."
}

if ($skipped -gt 0 -and $e.evidenceStatus -eq "GREEN") {
    throw "Evidence cannot be GREEN while verification gaps remain skipped."
}

if ($e.build.status -ne "GREEN" -and $e.evidenceStatus -eq "GREEN") {
    throw "Evidence cannot be GREEN when the build is not GREEN."
}

$head = (git rev-parse HEAD).Trim()

if ($LASTEXITCODE -ne 0) {
    throw "Unable to resolve Git HEAD."
}

if ($e.commit -ne $head) {
    throw "Evidence commit $($e.commit) does not match HEAD $head."
}

if ($e.PSObject.Properties.Name -contains "productionRuntime") {
    if ($e.productionRuntime.verified -eq $true) {
        if ($e.productionRuntime.commit -ne $e.commit) {
            throw "Production runtime commit does not match evidence commit."
        }

        if ($e.productionRuntime.activeReleaseId -ne $e.commit) {
            throw "Production active release does not match evidence commit."
        }
    }
}

$shortCommit = $e.commit.Substring(
    0,
    [Math]::Min(7, $e.commit.Length)
)

$lines = [System.Collections.Generic.List[string]]::new()

$lines.Add($startMarker)
$lines.Add("## Current verified status")
$lines.Add("")
$lines.Add("This status is generated from recorded verification evidence. It does not replace product documentation or architecture history.")
$lines.Add("")
$lines.Add("| Check | Result |")
$lines.Add("| --- | --- |")
$lines.Add("| Evidence | $($e.evidenceStatus) |")
$lines.Add("| Commit | ``$shortCommit`` |")
$lines.Add("| Build | $($e.build.status) ($($e.build.warnings) warnings, $($e.build.errors) errors) |")
$lines.Add("| Tests | $passed passed, $failed failed, $skipped skipped, $total total |")
$lines.Add("| Recorded | $($e.recordedAt) |")
$lines.Add("")
$lines.Add("### Verification gaps")
$lines.Add("")

foreach ($gap in $e.verificationGaps) {
    $lines.Add("- $gap")
}

$lines.Add("")
$lines.Add("**Evidence status is not the same as production status.**")
$lines.Add($endMarker)

$blockLf = $lines -join "`n"

# Deterministic preview artifact.
[System.IO.File]::WriteAllText(
    (Join-Path (Get-Location) $OutputPath),
    $blockLf + "`n",
    [System.Text.UTF8Encoding]::new($false)
)

$readmeFullPath = (Resolve-Path -LiteralPath $ReadmePath).Path
$readmeBytes = [System.IO.File]::ReadAllBytes($readmeFullPath)
$readmeText = [System.Text.Encoding]::UTF8.GetString($readmeBytes)

$startCount = ([regex]::Matches(
    $readmeText,
    [regex]::Escape($startMarker)
)).Count

$endCount = ([regex]::Matches(
    $readmeText,
    [regex]::Escape($endMarker)
)).Count

if ($startCount -ne $endCount) {
    throw "README evidence markers are unbalanced."
}

if ($startCount -gt 1) {
    throw "README contains duplicate evidence blocks."
}

# Preserve README newline convention.
$newline = if ($readmeText.Contains("`r`n")) {
    "`r`n"
} else {
    "`n"
}

$block = $blockLf -replace "`n", $newline

if ($startCount -eq 0) {
    # INSTALL: append only; existing README content remains unchanged.
    $separator = ""

    if (-not $readmeText.EndsWith("`n")) {
        $separator += $newline
    }

    $separator += $newline
    $updated = $readmeText + $separator + $block + $newline
}
else {
    # UPDATE: replace only the controlled block.
    $pattern =
        "(?s)" +
        [regex]::Escape($startMarker) +
        ".*?" +
        [regex]::Escape($endMarker)

    $updated = [regex]::Replace(
        $readmeText,
        $pattern,
        [System.Text.RegularExpressions.MatchEvaluator]{
            param($m)
            return $block
        },
        1
    )
}

[System.IO.File]::WriteAllText(
    $readmeFullPath,
    $updated,
    [System.Text.UTF8Encoding]::new($false)
)

$verify = [System.IO.File]::ReadAllText($readmeFullPath)

$verifyStart = ([regex]::Matches(
    $verify,
    [regex]::Escape($startMarker)
)).Count

$verifyEnd = ([regex]::Matches(
    $verify,
    [regex]::Escape($endMarker)
)).Count

if ($verifyStart -ne 1 -or $verifyEnd -ne 1) {
    throw "README generation did not produce exactly one evidence block."
}

Write-Host "GREEN: generated $OutputPath"
Write-Host "GREEN: README evidence block synchronized"