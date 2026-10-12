# Gera os ícones do app a partir de assets\logo.svg (o mesmo logo do site):
#   src\WinSense.App\Assets\WinSense.ico   (exe, janela e instalador; 16 a 256 px)
#   src\WinSense.App\Assets\WinSense.png   (logo da barra lateral, 256 px)
# Uso: .\scripts\make-icon.ps1   (requer o Microsoft Edge, usado para renderizar o SVG)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$svg = Join-Path $root 'assets\logo.svg'
$outDir = Join-Path $root 'src\WinSense.App\Assets'
$tmp = Join-Path ([IO.Path]::GetTempPath()) "winsense-icon-$PID"
New-Item -ItemType Directory -Force $tmp, $outDir | Out-Null

$edge = @("${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
          "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $edge) { throw 'Microsoft Edge não encontrado.' }

# Render once at 512 px on a transparent background, then downscale to each icon size.
$html = Join-Path $tmp 'logo.html'
$svgUri = ([Uri]$svg).AbsoluteUri
"<html><body style='margin:0;background:transparent'><img src='$svgUri' width='512' height='512'></body></html>" |
    Set-Content -Encoding utf8 $html
$big = Join-Path $tmp 'logo-512.png'
Start-Process $edge -Wait -ArgumentList '--headless=new', '--disable-gpu', '--hide-scrollbars',
    '--default-background-color=00000000', '--window-size=512,512', "`"--screenshot=$big`"", ([Uri]$html).AbsoluteUri
if (-not (Test-Path $big)) { throw 'Falha ao renderizar o SVG.' }

$source = [Drawing.Image]::FromFile($big)
function Resize([int]$size) {
    $bmp = New-Object Drawing.Bitmap $size, $size, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.DrawImage($source, 0, 0, $size, $size)
    $g.Dispose()
    $ms = New-Object IO.MemoryStream
    $bmp.Save($ms, [Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    , $ms.ToArray()
}

# ICO with PNG-compressed frames (supported since Windows Vista).
$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$frames = $sizes | ForEach-Object { , (Resize $_) }
$ico = New-Object IO.MemoryStream
$w = New-Object IO.BinaryWriter $ico
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$frames[$i].Length); $w.Write([uint32]$offset)
    $offset += $frames[$i].Length
}
foreach ($f in $frames) { $w.Write($f) }
$w.Flush()
[IO.File]::WriteAllBytes((Join-Path $outDir 'WinSense.ico'), $ico.ToArray())
[IO.File]::WriteAllBytes((Join-Path $outDir 'WinSense.png'), (Resize 256))

$source.Dispose()
Remove-Item -Recurse -Force $tmp
Write-Host "Ícones gerados em $outDir"
