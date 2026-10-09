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

# Recorded evidence remains attached to its tested revision, including when a
# descendant changes only documentation. Never relabel that run as current HEAD.
git merge-base --is-ancestor $e.commit $head
if ($LASTEXITCODE -ne 0) { throw "Recorded evidence revision is not an ancestor of HEAD." }

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
$lines.Add("The test row describes the recorded Windows run at ``$shortCommit``, not a combined Windows/Linux result or verification of uncommitted changes.")
if ($e.productVerification) {
    $v = Get-Content -LiteralPath $e.productVerification -Raw | ConvertFrom-Json
    if ($v.baseCommit -ne $head) { throw 'PDF product verification belongs to another worktree base' }
    foreach ($source in $v.sourceFiles) {
        if ((Get-FileHash -LiteralPath $source.path -Algorithm SHA256).Hash -ne $source.sha256) { throw "PDF evidence is stale: $($source.path)" }
    }
    foreach ($run in @($v.windowsTests, $v.linuxTests, $v.nativePdfTests)) {
        if ((Get-FileHash -LiteralPath $run.artifact -Algorithm SHA256).Hash -ne $run.sha256) { throw "Test artifact is stale: $($run.artifact)" }
        [xml]$trx = Get-Content -LiteralPath $run.artifact -Raw
        $counts = $trx.TestRun.ResultSummary.Counters
        if ([int]$counts.total -ne $run.discovered -or [int]$counts.passed -ne $run.passed -or [int]$counts.failed -ne $run.failed -or ([int]$counts.total - [int]$counts.executed) -ne $run.skipped) { throw "Test artifact totals disagree: $($run.artifact)" }
    }
    if ((Get-FileHash -LiteralPath $v.browser.artifact -Algorithm SHA256).Hash -ne $v.browser.sha256) { throw 'Browser evidence is stale' }
    $browserReport = Get-Content -LiteralPath $v.browser.artifact -Raw | ConvertFrom-Json
    if ($v.productGate -eq 'GREEN' -and ($v.build.status -ne 'PASS' -or $v.windowsTests.failed -ne 0 -or $v.linuxTests.failed -ne 0 -or $v.nativePdfTests.failed -ne 0 -or $v.nativePdfTests.skipped -ne 0 -or $v.nativePdfTests.passed -le 0 -or $v.browser.status -ne 'PASS' -or $browserReport.passed -ne $true)) { throw 'PDF product gate contradicts verification results' }
    $lines.Add("")
    $lines.Add("PDF Processor worktree verification (base ``$($v.baseCommit.Substring(0,7))``; source and artifact hashes recorded in ``$($e.productVerification)``): build $($v.build.status); Windows $($v.windowsTests.passed)/$($v.windowsTests.failed)/$($v.windowsTests.skipped), Linux $($v.linuxTests.passed)/$($v.linuxTests.failed)/$($v.linuxTests.skipped), PDF boundary suite with Poppler enabled $($v.nativePdfTests.passed)/$($v.nativePdfTests.failed)/$($v.nativePdfTests.skipped) (passed/failed/skipped). Browser workflow and downloaded PDF re-rendering: $($v.browser.status). These are local results, not a production deployment.")
}
if ($e.PSObject.Properties.Name -contains 'nativePdfEvidence') {
    $n = $e.nativePdfEvidence
    $lines.Add("")
    $lines.Add("Linux/native PDF: $($n.passed) passed, $($n.failed) failed, $($n.skipped) skipped at ``$($n.commit.Substring(0,7))`` ($($n.provenance)).")
}
if ($e.productionRuntime) {
    $lines.Add("")
    $lines.Add("Production evidence is recorded separately at ``$($e.productionRuntime.commit.Substring(0,7))``: $($e.productionRuntime.provenance). No production check or deployment is performed by this generator.")
}
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
if ($startCount -eq 1 -and $readmeText.IndexOf($startMarker) -ge $readmeText.IndexOf($endMarker)) { throw 'README evidence markers are reversed.' }

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

[System.IO.File]::WriteAllText((Join-Path (Get-Location) $OutputPath), $blockLf + "`n", [System.Text.UTF8Encoding]::new($false))
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

& (Join-Path $PSScriptRoot 'Update-ProductUi.ps1')
Write-Host "Generated $OutputPath; recorded evidence status: $($e.evidenceStatus)"
Write-Host "README evidence block synchronized"
