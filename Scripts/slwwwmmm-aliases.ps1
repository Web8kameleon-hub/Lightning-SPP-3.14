# Lightning SPP - PowerShell short commands
# Load with:
#   . "$PSScriptRoot\slwwwmmm-aliases.ps1"

$global:SLWWWMMM_ROOT = "C:\Users\Admin\source\repos\wwwmmm"

# Remove conflicting built-in aliases so function names take effect
foreach ($name in @('cli','cd','clo','ci','sli','slo','slx','slwwwmmm')) {
    if (Test-Path "Alias:$name") {
        Remove-Item "Alias:$name" -Force -ErrorAction SilentlyContinue
    }
}

function global:cd {
    param([string]$Path = $global:SLWWWMMM_ROOT)
    Set-Location -Path $Path
}

function global:ci {
    param([Parameter(Mandatory = $true)][string]$Message)
    git commit -m $Message
}

function global:cli {
    git log --oneline --decorate --graph -n 30
}

function global:clo {
    param([Parameter(Mandatory = $true)][string]$Branch)
    git checkout $Branch
}

function global:sli {
    dotnet run -- speed
}

function global:slo {
    dotnet run -- demo
}

function global:slx {
    dotnet run -- real
}

function global:slwwwmmm {
    Set-Location -Path $global:SLWWWMMM_ROOT
}

Write-Host "Aliases loaded: cd, ci, cli, clo, sli, slo, slx, slwwwmmm" -ForegroundColor Green
