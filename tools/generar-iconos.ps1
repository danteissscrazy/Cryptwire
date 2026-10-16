param(
    # El logo en SVG (cuadrado). Las imágenes salen en la misma carpeta.
    [Parameter(Mandatory)] [string] $Svg
)
# Genera desde el SVG del logo: logo-64.png (barra de título) y logo.ico (ejecutable, ventana y barra de
# tareas) con tamaños de 16 a 256 px. Dibuja con Microsoft Edge sin ventana, con un perfil temporal, y
# arma el .ico a mano: cabecera ICO con una imagen PNG por tamaño.
$ErrorActionPreference = 'Stop'
$svgPath = (Resolve-Path -LiteralPath $Svg).Path
$outDir = Split-Path $svgPath
$edge = Join-Path ${env:ProgramFiles(x86)} 'Microsoft\Edge\Application\msedge.exe'
$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256

$work = Join-Path ([IO.Path]::GetTempPath()) ("cryptwire-iconos-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
try {
    Copy-Item -LiteralPath $svgPath (Join-Path $work 'logo.svg')
    $x = 0
    $imgs = foreach ($s in $sizes) { "<img src='logo.svg' style='position:absolute;left:${x}px;top:0;width:${s}px;height:${s}px'>"; $x += $s + 8 }
    $html = "<!doctype html><body style='margin:0;background:transparent'>$($imgs -join '')</body>"
    Set-Content -LiteralPath (Join-Path $work 'tira.html') -Value $html -Encoding utf8
    $shot = Join-Path $work 'tira.png'
    & $edge --headless=new --disable-gpu --no-first-run "--user-data-dir=$(Join-Path $work 'perfil')" --hide-scrollbars `
        --force-device-scale-factor=1 --default-background-color=00000000 "--window-size=$($x),256" `
        "--screenshot=$shot" ([Uri](Join-Path $work 'tira.html')).AbsoluteUri 2>&1 | Out-Null
    if (-not (Test-Path $shot)) { throw 'Edge no ha generado la captura.' }

    Add-Type -AssemblyName System.Drawing
    $strip = [Drawing.Bitmap]::FromFile($shot)
    $pngs = @{}
    try {
        $x = 0
        foreach ($s in $sizes) {
            $bmp = $strip.Clone([Drawing.Rectangle]::new($x, 0, $s, $s), [Drawing.Imaging.PixelFormat]::Format32bppArgb)
            $ms = [IO.MemoryStream]::new()
            $bmp.Save($ms, [Drawing.Imaging.ImageFormat]::Png)
            $pngs[$s] = $ms.ToArray()
            $bmp.Dispose(); $ms.Dispose()
            $x += $s + 8
        }
    }
    finally { $strip.Dispose() }

    [IO.File]::WriteAllBytes((Join-Path $outDir 'logo-64.png'), $pngs[64])

    # ICO: cabecera de 6 bytes, una entrada de 16 bytes por imagen y luego los PNG seguidos.
    $ico = [IO.MemoryStream]::new()
    $w = [IO.BinaryWriter]::new($ico)
    $w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    foreach ($s in $sizes) {
        $dim = if ($s -ge 256) { 0 } else { $s }
        $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
        $w.Write([uint16]1); $w.Write([uint16]32)
        $w.Write([uint32]$pngs[$s].Length); $w.Write([uint32]$offset)
        $offset += $pngs[$s].Length
    }
    foreach ($s in $sizes) { $w.Write($pngs[$s]) }
    $w.Flush()
    [IO.File]::WriteAllBytes((Join-Path $outDir 'logo.ico'), $ico.ToArray())
    $w.Dispose()
    "logo.ico ($($sizes -join ', ') px) y logo-64.png en $outDir"
}
finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}
