$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot | Split-Path
$temp = Join-Path ([IO.Path]::GetTempPath()) ('lightning-product-tests-' + [guid]::NewGuid().ToString('N'))
New-Item $temp -ItemType Directory | Out-Null
try {
    $registry = Join-Path $temp 'products.json'
    $output = Join-Path $temp 'pdf.html'
    $try = Join-Path $temp 'try.html'
    $original = Get-Content (Join-Path $root 'evidence/products.json') -Raw
    function Generate([string]$json) {
        [IO.File]::WriteAllText($registry, $json)
        Copy-Item -LiteralPath (Join-Path $root 'Web/wwwroot/try.html') -Destination $try
        & (Join-Path $root 'tools/Update-ProductUi.ps1') -RegistryPath $registry -TemplatePath (Join-Path $root 'tools/templates/pdf.html') -OutputPath $output -TryPath $try
    }
    Generate $original
    if ([IO.File]::ReadAllText($output) -cne [IO.File]::ReadAllText((Join-Path $root 'Web/wwwroot/pdf.html'))) { throw 'Generated page is stale' }
    if ([IO.File]::ReadAllText($try) -cne [IO.File]::ReadAllText((Join-Path $root 'Web/wwwroot/try.html'))) { throw 'Generated entry link is stale' }
    $first = [IO.File]::ReadAllText($output)
    Generate $original
    if ([IO.File]::ReadAllText($output) -cne $first) { throw 'Generation is not deterministic' }
    foreach ($field in @('customerVisible','maturity')) {
        $r = $original | ConvertFrom-Json
        if ($field -eq 'customerVisible') { $r.products[0].customerVisible = $false } else { $r.products[0].maturity = 'IMPLEMENTED' }
        Generate ($r | ConvertTo-Json -Depth 15)
        if ((Get-Content $output -Raw) -match '<form' -or (Get-Content $try -Raw) -match 'href="/pdf"') { throw 'Unavailable product was advertised' }
    }
    foreach ($fault in @('profile','duplicate','entrypoint','boolean')) {
        $r = $original | ConvertFrom-Json
        switch ($fault) {
            'profile' { $r.products[0].profiles[0].id = 'fake-ai' }
            'duplicate' { $r.products += $r.products[0] }
            'entrypoint' { $r.products[0].entrypoints = @('/fake') }
            'boolean' { $r.products[0].customerVisible = 'false' }
        }
        $before = [IO.File]::ReadAllText($output)
        $rejected = $false
        try { Generate ($r | ConvertTo-Json -Depth 15) } catch { $rejected = $true }
        if (!$rejected -or [IO.File]::ReadAllText($output) -cne $before) { throw "Invalid registry accepted or output changed: $fault" }
    }
    $r = $original | ConvertFrom-Json
    $r.products[0].name = '<script>alert(1)</script>'
    Generate ($r | ConvertTo-Json -Depth 15)
    if ((Get-Content $output -Raw) -match '<script>alert' -or (Get-Content $try -Raw) -match '<script>alert') { throw 'Registry text was not escaped' }
    $r.products[0].name = "PDF `"name`" \ path`n</script><script>alert(1)</script> & text"
    Generate ($r | ConvertTo-Json -Depth 15)
    $html = Get-Content $output -Raw
    $metadata = [regex]::Match($html, '(?s)<script type="application/ld\+json">(.*?)</script>').Groups[1].Value | ConvertFrom-Json
    if ($metadata.name -cne ($r.products[0].name + ' | Lightning SPP') -or $metadata.breadcrumb.itemListElement[1].name -cne $r.products[0].name -or $html.Contains('</script><script>alert')) { throw 'JSON metadata was not safely serialized' }
    Write-Host 'PASS: deterministic assets, availability/maturity gating, unsupported profiles, duplicate IDs, entrypoints, booleans and HTML escaping.'
} finally {
    # Only this explicitly created temporary directory is removed.
    $resolvedTemp = [IO.Path]::GetFullPath($temp)
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (!$resolvedTemp.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($resolvedTemp) -notmatch '^lightning-product-tests-[a-f0-9]{32}$') { throw 'Unsafe test cleanup path' }
    Remove-Item -LiteralPath $temp -Recurse -Force
}
