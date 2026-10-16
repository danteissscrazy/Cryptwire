param(
    # Dónde dejar marca.png (por defecto, los recursos de la app).
    [string] $OutDir = (Join-Path $PSScriptRoot '..\src\Cryptwire.App\Assets')
)
# Dibuja la palabra «Cryptwire» en Grenze Gotisch (peso 600, oro) como imagen para la barra de título.
# WPF no sabe usar fuentes variables, así que la dibuja Microsoft Edge sin ventana, a 4 aumentos para
# que se vea nítida, con fondo transparente, y se recorta al borde de las letras.
$ErrorActionPreference = 'Stop'
$font = (Resolve-Path (Join-Path $PSScriptRoot 'fuentes\GrenzeGotisch-Variable.ttf')).Path
$edge = Join-Path ${env:ProgramFiles(x86)} 'Microsoft\Edge\Application\msedge.exe'
$work = Join-Path ([IO.Path]::GetTempPath()) ("cryptwire-marca-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
try {
    Copy-Item -LiteralPath $font (Join-Path $work 'grenze.ttf')
    $html = @'
<!doctype html><meta charset="utf-8">
<style>
  @font-face { font-family: "Grenze Gotisch"; src: url("grenze.ttf"); font-weight: 100 900; }
  body { margin: 0; background: transparent; }
  p { margin: 0; padding: 8px 12px; display: inline-block; font: 600 92px/1.15 "Grenze Gotisch"; color: #dcb663; }
</style>
<p>Cryptwire</p>
'@
    Set-Content -LiteralPath (Join-Path $work 'marca.html') -Value $html -Encoding utf8
    $shot = Join-Path $work 'marca-bruta.png'
    & $edge --headless=new --disable-gpu --no-first-run "--user-data-dir=$(Join-Path $work 'perfil')" --hide-scrollbars `
        --force-device-scale-factor=1 --default-background-color=00000000 --window-size=700,160 `
        "--screenshot=$shot" ([Uri](Join-Path $work 'marca.html')).AbsoluteUri 2>&1 | Out-Null
    if (-not (Test-Path $shot)) { throw 'Edge no ha generado la captura.' }

    # Recorte al borde de lo pintado (alfa > 0), con 2 px de margen.
    Add-Type -AssemblyName System.Drawing
    $img = [Drawing.Bitmap]::FromFile($shot)
    try {
        $minX = $img.Width; $minY = $img.Height; $maxX = -1; $maxY = -1
        for ($y = 0; $y -lt $img.Height; $y++) {
            for ($x = 0; $x -lt $img.Width; $x++) {
                if ($img.GetPixel($x, $y).A -gt 0) {
                    if ($x -lt $minX) { $minX = $x }; if ($x -gt $maxX) { $maxX = $x }
                    if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y }
                }
            }
        }
        if ($maxX -lt 0) { throw 'La captura ha salido vacía.' }
        $rect = [Drawing.Rectangle]::FromLTRB([Math]::Max(0, $minX - 2), [Math]::Max(0, $minY - 2), [Math]::Min($img.Width, $maxX + 3), [Math]::Min($img.Height, $maxY + 3))
        $crop = $img.Clone($rect, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $out = Join-Path (Resolve-Path $OutDir).Path 'marca.png'
        $crop.Save($out, [Drawing.Imaging.ImageFormat]::Png)
        $crop.Dispose()
        "marca.png ($($rect.Width)x$($rect.Height) px) en $OutDir"
    }
    finally { $img.Dispose() }
}
finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}
