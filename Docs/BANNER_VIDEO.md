# Banner Video Guide

Ky udhezues mbulon preset-in kinematik 10s, titrat e animuara dhe eksportin MP4.

## URL te gatshme

- Banner normal: `/banner.html`
- Banner cinematic 10s: `/banner.html?preset=cinematic10`
- Banner cinematic capture mode: `/banner.html?preset=cinematic10&capture=1&bpm=96`

## Cfare ben preset-i cinematic10

- Aktivizon capture mode automatikisht.
- Ekzekuton timeline 10 sekonda me ritem te qendrueshem.
- Shfaq titra sipas fazave:
  - `SCAN: Harmonic Wave Lock`
  - `PROCESS: Resonance Compression`
  - `PRINT: Stigma Fluid Imprint`
  - `wwwmmm // LIGHTNING SPP 3.14`

## Eksport MP4 me ffmpeg (Windows)

Script i gatshem: `Scripts/capture-banner-video.ps1`

Script-i tani kerkon `ffmpeg` ne kete renditje:

- `-FfmpegPath` (nese e jep manualisht)
- `ffmpeg` nga PATH
- `tools/ffmpeg-*-essentials_build/bin/ffmpeg.exe` brenda repo

Nese nuk ke ffmpeg global, mjafton build lokal ne `tools/`.

Shembull:

```powershell
./Scripts/capture-banner-video.ps1 -X 0 -Y 0 -Width 1920 -Height 1080 -Duration 10 -Framerate 60 -Output "banner-cinematic-10s.mp4"
```

Opsionale (rruge manuale):

```powershell
./Scripts/capture-banner-video.ps1 -FfmpegPath "C:\path\to\ffmpeg.exe" -Duration 10 -Output "banner-cinematic-10s.mp4"
```

Komande direkte ffmpeg:

```powershell
ffmpeg -y -f gdigrab -framerate 60 -offset_x 0 -offset_y 0 -video_size 1920x1080 -i desktop -t 10 -c:v libx264 -pix_fmt yuv420p -preset slow -crf 18 -movflags +faststart banner-cinematic-10s.mp4
```
