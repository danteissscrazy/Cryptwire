param(
    # Dónde dejar las imágenes (por defecto, junto al script del instalador).
    [string] $OutDir = (Join-Path $PSScriptRoot '..\installer\imagenes')
)
# Dibuja las imágenes del asistente de instalación con los colores del Relicario: la lateral (bienvenida y
# final) con el logo y la marca, y la de la cabecera (las demás páginas) con el logo. Cada una a 100 % y a
# 200 %: Inno Setup elige la que mejor encaja con el escalado de la pantalla. Las dibuja Microsoft Edge sin
# ventana, con un perfil temporal, a partir de logo.svg, el adorno de filigrana y las fuentes del repo.
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot
$edge = Join-Path ${env:ProgramFiles(x86)} 'Microsoft\Edge\Application\msedge.exe'
New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
$OutDir = (Resolve-Path -LiteralPath $OutDir).Path
$work = Join-Path ([IO.Path]::GetTempPath()) ("cryptwire-instalador-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
try {
    Copy-Item -LiteralPath (Join-Path $repo 'src\Cryptwire.App\Assets\logo.svg') (Join-Path $work 'logo.svg')
    Copy-Item -LiteralPath (Join-Path $repo 'themes\relicario\adornos\filigrana.svg') (Join-Path $work 'filigrana.svg')
    Copy-Item -LiteralPath (Join-Path $repo 'tools\fuentes\GrenzeGotisch-Variable.ttf') (Join-Path $work 'grenze.ttf')
    Copy-Item -LiteralPath (Join-Path $repo 'themes\relicario\fuentes\ShareTechMono-Regular.ttf') (Join-Path $work 'mono.ttf')

    $style = @'
<meta charset="utf-8">
<style>
  @font-face { font-family: "Grenze Gotisch"; src: url("grenze.ttf"); font-weight: 100 900; }
  @font-face { font-family: "Share Tech Mono"; src: url("mono.ttf"); }
  html, body { margin: 0; overflow: hidden; }
</style>
'@
    $pages = @{
        lateral = @{
            Size = '164,314'
            Html = $style + @'
<style>
  body { position: relative; width: 164px; height: 314px; }
  .fondo { position: absolute; left: 0; top: 0; width: 164px; height: 314px;
           background: radial-gradient(circle at 50% 33%, rgba(220,182,99,.16), rgba(220,182,99,0) 46%),
                       linear-gradient(180deg, #02040a 0%, #070c19 58%, #0b1530 100%); }
  .borde { position: absolute; left: 6px; top: 6px; width: 150px; height: 300px; box-sizing: border-box; border: 1px solid rgba(220,182,99,.22); }
  .fil { position: absolute; width: 26px; height: 26px; }
  .logo { position: absolute; left: 32px; top: 58px; width: 100px; height: 100px; }
  .marca { position: absolute; top: 166px; width: 100%; margin: 0; text-align: center;
           font: 600 34px/1 "Grenze Gotisch"; color: #dcb663; }
  .filo { position: absolute; top: 214px; left: 34px; right: 34px; height: 1px;
          background: linear-gradient(90deg, transparent, #dcb663, transparent); }
  .lema { position: absolute; top: 224px; width: 100%; margin: 0; text-align: center;
          font: 8.5px/1 "Share Tech Mono"; letter-spacing: .18em; color: #45d4ff; }
</style>
<div class="fondo"></div>
<div class="borde"></div>
<img class="fil" src="filigrana.svg" style="left:9px;top:9px">
<img class="fil" src="filigrana.svg" style="right:9px;bottom:9px;transform:rotate(180deg)">
<img class="logo" src="logo.svg">
<p class="marca">Cryptwire</p>
<div class="filo"></div>
<p class="lema">WHATSAPP · PIELES</p>
'@
        }
        cabecera = @{
            Size = '55,58'
            Html = $style + @'
<style>
  body { width: 55px; height: 58px; background: transparent; }
  img { position: absolute; left: 4px; top: 5px; width: 48px; height: 48px; }
</style>
<img src="logo.svg">
'@
        }
    }

    Add-Type -AssemblyName System.Drawing
    foreach ($page in $pages.GetEnumerator()) {
        $html = Join-Path $work "$($page.Key).html"
        Set-Content -LiteralPath $html -Value $page.Value.Html -Encoding utf8
        $width, $height = [int[]]($page.Value.Size -split ',')
        foreach ($scale in 1, 2) {
            # Edge sin ventana se cuelga con ventanas muy pequeñas: se dibuja en una de 200 × 200 como poco
            # y se recorta la esquina de arriba a la izquierda. Con tiempo virtual, para que las fuentes
            # estén cargadas antes de la captura (si no, el texto sale invisible).
            $shot = Join-Path $work "$($page.Key)-$scale.png"
            & $edge --headless=new --disable-gpu --no-first-run "--user-data-dir=$(Join-Path $work "perfil-$($page.Key)-$scale")" `
                --hide-scrollbars --virtual-time-budget=5000 "--force-device-scale-factor=$scale" --default-background-color=00000000 `
                "--window-size=$([Math]::Max($width, 200)),$([Math]::Max($height, 200))" `
                "--screenshot=$shot" ([Uri]$html).AbsoluteUri 2>&1 | Out-Null
            if (-not (Test-Path $shot)) { throw "Edge no ha generado $($page.Key) a $scale aumentos." }
            $name = "$($page.Key)-$($width * $scale).png"
            $canvas = [Drawing.Bitmap]::FromFile($shot)
            try {
                $crop = $canvas.Clone([Drawing.Rectangle]::new(0, 0, $width * $scale, $height * $scale), [Drawing.Imaging.PixelFormat]::Format32bppArgb)
                $crop.Save((Join-Path $OutDir $name), [Drawing.Imaging.ImageFormat]::Png)
                "$name  $($crop.Width)x$($crop.Height)"
                $crop.Dispose()
            } finally {
                $canvas.Dispose()
            }
        }
    }
} finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}
