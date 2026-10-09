param(
    [string]$RegistryPath = "evidence/products.json",
    [string]$TemplatePath = "tools/templates/pdf.html",
    [string]$OutputPath = "Web/wwwroot/pdf.html",
    [string]$TryPath = "Web/wwwroot/try.html"
)
$ErrorActionPreference = "Stop"
$registry = Get-Content -LiteralPath $RegistryPath -Raw | ConvertFrom-Json
$ids = @{}
foreach ($product in $registry.products) {
    foreach ($field in @('id','name','kind','maturity','customerVisible','commercialReady','customerAction','capabilities','entrypoints','evidence','limitations')) {
        if ($null -eq $product.$field) { throw "Missing product field: $field" }
    }
    if ($ids.ContainsKey($product.id)) { throw "Duplicate product id: $($product.id)" }
    $ids[$product.id] = $true
    if ($product.kind -notin @('PRODUCT','CAPABILITY','INTERNAL_SUBSYSTEM','COMMERCIAL_INFRASTRUCTURE','INTEGRATION')) { throw 'Invalid product kind' }
    if ($product.maturity -notin @('INTENT','PROTOTYPE','IMPLEMENTED','VERIFIED','PRODUCTION')) { throw 'Invalid maturity' }
    if ($product.customerVisible -isnot [bool] -or $product.commercialReady -isnot [bool]) { throw 'Availability must be boolean' }
    if ($product.customerVisible -and ($product.kind -ne 'PRODUCT' -or !$product.customerAction -or !$product.entrypoints.Count -or !$product.evidence.Count)) { throw 'Visible products require an executable action and evidence' }
}
$pdf = @($registry.products | Where-Object id -eq 'pdf-processor')
if ($pdf.Count -ne 1) { throw 'Exactly one PDF Processor entry is required' }
$pdf = $pdf[0]
function Encode([string]$value) { [System.Net.WebUtility]::HtmlEncode($value) }
function JsonString([string]$value) {
    # JSON script contents are raw text: HTML entities cannot escape JSON strings.
    ($value | ConvertTo-Json -Compress).Replace('<', '\u003c').Replace('>', '\u003e').Replace('&', '\u0026')
}
$available = $pdf.customerVisible -and $pdf.maturity -in @('VERIFIED','PRODUCTION')
$link = ''
if ($available) {
    if (@($pdf.entrypoints) -notcontains '/pdf' -or @($pdf.entrypoints) -notcontains '/api/try/process') { throw 'PDF entrypoints do not match the existing pipeline' }
    $profileIds = @($pdf.profiles | ForEach-Object id)
    if ($profileIds.Count -ne 1 -or $profileIds[0] -ne 'lightning') { throw 'Only the verified Lightning PDF product profile is currently supported' }
    $page = Get-Content -LiteralPath $TemplatePath -Raw
    $values = @{
        NAME = Encode $pdf.name; ACTION = Encode $pdf.customerAction; MATURITY = Encode $pdf.maturity
        NAME_JSON = JsonString $pdf.name; TITLE_JSON = JsonString ($pdf.name + ' | Lightning SPP')
        PROFILE_IDS = Encode ($profileIds -join ',')
        PROFILES = (@($pdf.profiles | ForEach-Object { '<option value="' + (Encode $_.id) + '">' + (Encode $_.name) + '</option>' }) -join "`n")
        LIMITATIONS = (@($pdf.limitations | ForEach-Object { '<li>' + (Encode $_) + '</li>' }) -join "`n")
    }
    foreach ($key in $values.Keys) { $page = $page.Replace('{{' + $key + '}}', $values[$key]) }
    if ($page -match '\{\{') { throw 'Unresolved template field' }
    $link = '<p><a class="button secondary" href="/pdf">' + (Encode $pdf.name) + '</a> — ' + (Encode $pdf.customerAction) + '</p>'
} else {
    $page = '<!doctype html><html lang="en"><head><meta charset="utf-8"><title>Product unavailable | Lightning SPP</title><link rel="stylesheet" href="/site.css"><meta name="robots" content="noindex"></head><body><main><h1>Product unavailable</h1><p>This workflow is not currently listed as available.</p><a href="/try">Document processing demo</a></main></body></html>'
}
$try = Get-Content -LiteralPath $TryPath -Raw
$start = '<!-- LIGHTNING-PRODUCT:START -->'; $end = '<!-- LIGHTNING-PRODUCT:END -->'
if ([regex]::Matches($try, [regex]::Escape($start)).Count -ne 1 -or [regex]::Matches($try, [regex]::Escape($end)).Count -ne 1 -or $try.IndexOf($start) -ge $try.IndexOf($end)) { throw 'Invalid product markers' }
$block = $start + "`n" + $link + "`n" + $end
$try = [regex]::Replace($try, '(?s)' + [regex]::Escape($start) + '.*?' + [regex]::Escape($end), [System.Text.RegularExpressions.MatchEvaluator]{param($m) $block})
foreach ($item in @(@($OutputPath,$page), @($TryPath,$try))) {
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($item[0], $PWD.Path), $item[1], [Text.UTF8Encoding]::new($false))
}
Write-Host 'Product UI synchronized from registry (no runtime repository reads).'
