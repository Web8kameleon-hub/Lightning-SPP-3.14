param(
    [int]$X = 0,
    [int]$Y = 0,
    [int]$Width = 1920,
    [int]$Height = 1080,
    [int]$Duration = 10,
    [int]$Framerate = 60,
    [string]$Output = "banner-cinematic-10s.mp4",
    [string]$FfmpegPath = ""
)

function Resolve-FfmpegPath {
    param([string]$ExplicitPath)

    if ($ExplicitPath -and (Test-Path $ExplicitPath)) {
        return (Resolve-Path $ExplicitPath).Path
    }

    $cmd = Get-Command ffmpeg -ErrorAction SilentlyContinue
    if ($cmd -and $cmd.Source) {
        return $cmd.Source
    }

    $repoRoot = Split-Path $PSScriptRoot -Parent
    $localCandidates = @(
        (Join-Path $repoRoot "tools\ffmpeg\bin\ffmpeg.exe"),
        (Join-Path $repoRoot "tools\ffmpeg.exe")
    )

    $latestExtracted = Get-ChildItem -Path (Join-Path $repoRoot "tools") -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -like "ffmpeg*-essentials_build" } |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

    if ($latestExtracted) {
        $localCandidates += (Join-Path $latestExtracted.FullName "bin\ffmpeg.exe")
    }

    foreach ($candidate in $localCandidates) {
        if (Test-Path $candidate) {
            return (Resolve-Path $candidate).Path
        }
    }

    return $null
}

$ffmpegExe = Resolve-FfmpegPath -ExplicitPath $FfmpegPath
if (-not $ffmpegExe) {
    Write-Error "ffmpeg nuk u gjet ne PATH ose ne ./tools. Perdore -FfmpegPath 'C:\path\to\ffmpeg.exe' ose shkarko build lokal ne ./tools."
    exit 1
}

$videoSize = "${Width}x${Height}"

Write-Host "Using ffmpeg: $ffmpegExe"
Write-Host "Capture settings: ${videoSize} @ ${Framerate}fps, duration ${Duration}s"
Write-Host "Tip: Hape /banner.html?preset=cinematic10&capture=1&bpm=96 ne browser dhe vendose ne zonen e capture."

& $ffmpegExe -y `
    -f gdigrab `
    -framerate $Framerate `
    -offset_x $X `
    -offset_y $Y `
    -video_size $videoSize `
    -i desktop `
    -t $Duration `
    -c:v libx264 `
    -pix_fmt yuv420p `
    -preset slow `
    -crf 18 `
    -movflags +faststart `
    $Output

if ($LASTEXITCODE -eq 0) {
    Write-Host "Video u krijua: $Output"
}
else {
    Write-Error "Capture deshtoi me kod: $LASTEXITCODE"
    exit $LASTEXITCODE
}
