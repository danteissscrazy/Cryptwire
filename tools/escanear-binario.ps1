param(
    # La carpeta que se va a distribuir: la salida de publicar.ps1, un bin\Release…
    [Parameter(Mandatory)] [string] $Ruta,
    # Reglas de datos personales al estilo de gitleaks. Por defecto, las mismas del pre-commit:
    # ..\.privado\gitleaks-personal.toml, fuera del repo.
    [string] $Reglas,
    # Si se da, deja aquí los hallazgos en JSON (fichero, juego de reglas, regla y lecturas).
    [string] $Informe
)
# Busca secretos y datos personales dentro de lo que se distribuye, no solo en el código: .NET guarda
# cadenas en UTF-8 y en UTF-16, y una ruta de compilación se cuela en el .dll o el .pdb si falla PathMap.
# gitleaks no sirve tal cual: se salta los .exe, .dll y .ttf por su tipo y lee por bloques que pueden
# partir una coincidencia. Así que de cada fichero se sacan sus cadenas legibles en seis lecturas (UTF-8,
# Latin-1 y UTF-16 LE y BE, alineado y desalineado). Si es un .NET, además, su depuración: las rutas de
# las fuentes enteras (el .pdb las guarda troceadas) y lo que va comprimido (el PDB incrustado en el .dll
# y las fuentes incrustadas en el .pdb), en las mismas seis lecturas. gitleaks lo revisa todo como texto
# con tres juegos de reglas: los secretos del repo (.gitleaks.toml), tus datos personales y las rutas de
# esta máquina (el repo y tu perfil de usuario, también en su forma corta 8.3).
# Nunca enseña lo que encuentra ni las reglas, solo dónde y con qué regla: la salida puede acabar en un
# log público. Sale con 0 si está limpio, 1 si encuentra algo y 2 si no ha podido comprobarlo todo:
# falla cerrado, también ante un fichero comprimido (zip, woff…) cuyo interior no sabe leer.
$ErrorActionPreference = 'Stop'
trap {
    [Console]::Error.WriteLine("escanear-binario: $_")
    exit 2
}

function Stop-Scan([string] $Message) {
    [Console]::Error.WriteLine("escanear-binario: $Message")
    exit 2
}

# Escapa una ruta para RE2 (gitleaks): solo los metacaracteres. RE2 no acepta «\ » ni «\ñ», así que
# [regex]::Escape no sirve.
function ConvertTo-Re2Literal([string] $Text) {
    [regex]::Replace($Text, '[\\.+*?()|\[\]{}^$]', { param($m) '\' + $m.Value })
}

# Una ruta en cualquier forma: barras de los dos tipos y repetidas, mayúsculas o minúsculas, y sin
# confundir C:\Users\ana con C:\Users\anabel.
function New-PathRule([string] $Id, [string] $Path) {
    $parts = $Path -split '[\\/]+' | Where-Object { $_ }
    $regex = '(?i)' + (($parts | ForEach-Object { ConvertTo-Re2Literal $_ }) -join '[\\/]+') + '(?:[^\p{L}\p{N}_.~-]|$)'
    "[[rules]]`nid = '$Id'`ndescription = 'Ruta de esta maquina'`nregex = '''$regex'''`n"
}

function Get-ShortPath([string] $Path) {
    try { (New-Object -ComObject Scripting.FileSystemObject).GetFolder($Path).ShortPath } catch { $null }
}

# Descomprime un bloque Deflate con un tope: un .pdb no puede inflarse hasta llenar la memoria.
function Expand-Deflate([byte[]] $Data, [int] $Offset, [int] $Size) {
    if ($Size -le 0 -or $Size -gt 256MB) { throw "un bloque comprimido dice medir $Size bytes" }
    $out = [byte[]]::new($Size)
    $deflate = [IO.Compression.DeflateStream]::new([IO.MemoryStream]::new($Data, $Offset, $Data.Length - $Offset), [IO.Compression.CompressionMode]::Decompress)
    try {
        $done = 0
        while ($done -lt $Size -and ($n = $deflate.Read($out, $done, $Size - $done)) -gt 0) { $done += $n }
        if ($done -ne $Size) { throw 'un bloque comprimido no tiene lo que dice' }
    } finally {
        $deflate.Dispose()
    }
    $out
}

# La depuración de un .NET: Paths son las rutas enteras (la del .pdb que apunta el .dll y la de cada
# fuente) y Bytes, lo que va comprimido o troceado (el PDB incrustado entero, las fuentes incrustadas, el
# Source Link y las opciones de compilación). Null si el fichero no es un .NET ni un .pdb portátil; si lo
# es y no se puede leer, lanza: mejor no escanear que dar por limpio lo que no se ha visto.
$EmbeddedSource = [Guid]'0e8a571b-6926-466e-b4ad-8ab04611f5fe'
function Get-DebugInfo([string] $Path, [byte[]] $Bytes) {
    $paths = [Collections.Generic.List[string]]::new()
    $blobs = [IO.MemoryStream]::new()
    $pdbs = [Collections.Generic.List[byte[]]]::new()
    if ($Bytes.Length -ge 4 -and [Text.Encoding]::ASCII.GetString($Bytes, 0, 4) -eq 'BSJB') {
        $pdbs.Add($Bytes)
    } elseif ($Bytes.Length -ge 2 -and $Bytes[0] -eq 0x4D -and $Bytes[1] -eq 0x5A) {
        $pe = [Reflection.PortableExecutable.PEReader]::new([IO.MemoryStream]::new($Bytes))
        try {
            try { $null = $pe.PEHeaders } catch { return $null }  # «MZ» y nada más: no es un programa.
            foreach ($entry in $pe.ReadDebugDirectory()) {
                if ($entry.Type -eq 'CodeView') {
                    $paths.Add($pe.ReadCodeViewDebugDirectoryData($entry).Path)
                } elseif ($entry.Type -eq 'EmbeddedPortablePdb') {
                    # «MPDB», el tamaño descomprimido y el PDB en Deflate.
                    $at = $entry.DataPointer
                    if ($entry.DataSize -lt 8 -or [Text.Encoding]::ASCII.GetString($Bytes, $at, 4) -ne 'MPDB') { throw 'PDB incrustado sin firma' }
                    $pdb = Expand-Deflate $Bytes ($at + 8) ([BitConverter]::ToInt32($Bytes, $at + 4))
                    $pdbs.Add($pdb)
                    $blobs.Write($pdb, 0, $pdb.Length)
                }
            }
        } finally {
            $pe.Dispose()
        }
    } else {
        return $null
    }
    foreach ($pdb in $pdbs) {
        $provider = [Reflection.Metadata.MetadataReaderProvider]::FromPortablePdbStream([IO.MemoryStream]::new($pdb))
        try {
            $reader = $provider.GetMetadataReader()
            foreach ($doc in $reader.Documents) { $paths.Add($reader.GetString($reader.GetDocument($doc).Name)) }
            foreach ($handle in $reader.CustomDebugInformation) {
                $info = $reader.GetCustomDebugInformation($handle)
                $blob = $reader.GetBlobBytes($info.Value)
                # Fuente incrustada: un entero (0 = tal cual; si no, tamaño descomprimido) y el texto.
                if ($reader.GetGuid($info.Kind) -eq $EmbeddedSource -and $blob.Length -ge 4) {
                    $size = [BitConverter]::ToInt32($blob, 0)
                    if ($size -eq 0) {
                        $text = [byte[]]::new($blob.Length - 4)
                        [Array]::Copy($blob, 4, $text, 0, $text.Length)
                        $blob = $text
                    } else {
                        $blob = Expand-Deflate $blob 4 $size
                    }
                }
                $blobs.Write($blob, 0, $blob.Length)
                $blobs.WriteByte(0)
            }
        } finally {
            $provider.Dispose()
        }
    }
    [pscustomobject]@{ Paths = $paths; Bytes = $blobs.ToArray() }
}

# Las seis lecturas de unos bytes.
function Get-Readings([byte[]] $Bytes, [string] $Prefix) {
    $odd = [Math]::Max($Bytes.Length - 1, 0)
    $from = [Math]::Min(1, $Bytes.Length)
    [ordered]@{
        "${Prefix}utf8" = [Text.Encoding]::UTF8.GetString($Bytes)
        "${Prefix}latin1" = [Text.Encoding]::Latin1.GetString($Bytes)
        "${Prefix}u16le" = [Text.Encoding]::Unicode.GetString($Bytes)
        "${Prefix}u16le1" = [Text.Encoding]::Unicode.GetString($Bytes, $from, $odd)
        "${Prefix}u16be" = [Text.Encoding]::BigEndianUnicode.GetString($Bytes)
        "${Prefix}u16be1" = [Text.Encoding]::BigEndianUnicode.GetString($Bytes, $from, $odd)
    }
}

# Contenedores comprimidos por sus primeros bytes: zip (y lo que va en zip), gzip, 7z, cab, rar, xz,
# bzip2, zstd y fuentes woff/woff2. Su interior no se ve: no se puede dar por limpio.
$containers = @(
    [byte[]](0x50, 0x4B, 0x03, 0x04), [byte[]](0x50, 0x4B, 0x05, 0x06), [byte[]](0x1F, 0x8B),
    [byte[]](0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C), [byte[]](0x4D, 0x53, 0x43, 0x46), [byte[]](0x52, 0x61, 0x72, 0x21),
    [byte[]](0xFD, 0x37, 0x7A, 0x58, 0x5A, 0x00), [byte[]](0x42, 0x5A, 0x68), [byte[]](0x28, 0xB5, 0x2F, 0xFD),
    [byte[]](0x77, 0x4F, 0x46, 0x46), [byte[]](0x77, 0x4F, 0x46, 0x32)
)
function Test-Container([byte[]] $Bytes) {
    foreach ($magic in $containers) {
        if ($Bytes.Length -lt $magic.Length) { continue }
        $same = $true
        for ($k = 0; $k -lt $magic.Length; $k++) { if ($Bytes[$k] -ne $magic[$k]) { $same = $false; break } }
        if ($same) { return $true }
    }
    $false
}

$repo = Split-Path $PSScriptRoot
$gitleaks = Get-Command gitleaks -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $gitleaks) { Stop-Scan 'gitleaks no está instalado (winget install Gitleaks.Gitleaks).' }
if (-not (Test-Path -LiteralPath $Ruta -PathType Container)) { Stop-Scan "no existe la carpeta $Ruta." }
$Ruta = (Resolve-Path -LiteralPath $Ruta).Path.TrimEnd('\')
$files = @(Get-ChildItem -LiteralPath $Ruta -Recurse -File -Force)
if ($files.Count -eq 0) { Stop-Scan "la carpeta $Ruta está vacía: no hay nada que dar por limpio." }

# Juegos de reglas. Los secretos y las rutas de la máquina siempre; los datos personales, como en el pre-commit.
$sets = [ordered]@{ secretos = Join-Path $repo '.gitleaks.toml' }
if (-not (Test-Path -LiteralPath $sets.secretos)) { Stop-Scan "faltan las reglas de secretos ($($sets.secretos))." }
if ($Reglas) {
    if (-not (Test-Path -LiteralPath $Reglas -PathType Leaf)) { Stop-Scan "no existen las reglas $Reglas." }
    $sets.personales = (Resolve-Path -LiteralPath $Reglas).Path
} else {
    $personal = Join-Path (Split-Path $repo) '.privado\gitleaks-personal.toml'
    if (Test-Path -LiteralPath $personal -PathType Leaf) {
        $sets.personales = $personal
    } elseif ((git -C $repo config --bool cryptwire.personalRules) -eq 'true') {
        Stop-Scan "faltan las reglas de datos personales ($personal)."
    } else {
        [Console]::Error.WriteLine("escanear-binario: aviso, sin reglas de datos personales en $personal.")
    }
}

$work = Join-Path ([IO.Path]::GetTempPath()) ('cryptwire-escaneo-' + [Guid]::NewGuid().ToString('N'))
$views = Join-Path $work 'vistas'
New-Item -ItemType Directory -Path $views | Out-Null
try {
    $toml = "title = 'Rutas de esta maquina'`n"
    foreach ($place in @(@('repo', $repo), @('perfil', [Environment]::GetFolderPath('UserProfile')))) {
        $toml += New-PathRule "ruta-del-$($place[0])" $place[1]
        $short = Get-ShortPath $place[1]
        if ($short -and $short -ne $place[1]) { $toml += New-PathRule "ruta-del-$($place[0])-8.3" $short }
    }
    $sets.maquina = Join-Path $work 'maquina.toml'
    [IO.File]::WriteAllText($sets.maquina, $toml, [Text.UTF8Encoding]::new($false))

    # Las cadenas legibles de cada lectura, una por párrafo, en ficheros con nombres neutros y sin
    # extensión: así ninguna lista de rutas permitidas de gitleaks (.dll, .pdb, .png…) se salta nada, el
    # tipo de fichero siempre es texto y gitleaks corta sus bloques entre cadenas. Las muy largas van en
    # trozos que se solapan.
    $utf8 = [Text.UTF8Encoding]::new($false)
    $printable = [regex]::new('[^\p{C}' + [char]0xFFFD + ']{4,}')
    $labels = @{ rutas = 'rutas de depuración' }
    foreach ($e in @(@('utf8', 'UTF-8'), @('latin1', 'Latin-1'), @('u16le', 'UTF-16LE'), @('u16le1', 'UTF-16LE+1'), @('u16be', 'UTF-16BE'), @('u16be1', 'UTF-16BE+1'))) {
        $labels[$e[0]] = $e[1]
        $labels["d$($e[0])"] = "depuración ($($e[1]))"
    }
    for ($i = 0; $i -lt $files.Count; $i++) {
        $bytes = [IO.File]::ReadAllBytes($files[$i].FullName)
        $relative = $files[$i].FullName.Substring($Ruta.Length + 1)
        if (Test-Container $bytes) { Stop-Scan "$relative va comprimido y no sé ver dentro: escanea la carpeta antes de comprimirla." }
        try {
            $debug = Get-DebugInfo $files[$i].FullName $bytes
        } catch {
            Stop-Scan "no he podido leer la depuración de $relative ($($_.Exception.Message))."
        }
        $texts = Get-Readings $bytes ''
        if ($debug) {
            $texts.rutas = $debug.Paths -join "`n"
            if ($debug.Bytes.Length) { foreach ($r in (Get-Readings $debug.Bytes 'd').GetEnumerator()) { $texts[$r.Key] = $r.Value } }
        }
        foreach ($t in $texts.GetEnumerator()) {
            $sb = [Text.StringBuilder]::new("cryptwire: cadenas legibles`n`n")
            foreach ($m in $printable.Matches($t.Value)) {
                for ($at = 0; ; $at += 1800) {
                    [void]$sb.Append($m.Value, $at, [Math]::Min(2000, $m.Length - $at)).Append("`n`n")
                    if ($at + 2000 -ge $m.Length) { break }
                }
            }
            [IO.File]::WriteAllText((Join-Path $views ('{0:D5}-{1}' -f $i, $t.Key)), $sb.ToString(), $utf8)
        }
    }

    $hits = foreach ($set in $sets.GetEnumerator()) {
        $report = Join-Path $work "informe-$($set.Key).json"
        # --gitleaks-ignore-path a la carpeta temporal: ningún .gitleaksignore puede silenciar el escaneo.
        # Lo que diga gitleaks no se enseña: con una regla rota, repetiría la regla, que es el dato.
        $output = & $gitleaks.Source dir $views --config $set.Value --no-banner --redact --exit-code 99 `
            --report-format json --report-path $report --gitleaks-ignore-path $work --log-level error 2>&1
        if ($LASTEXITCODE -notin 0, 99 -or $output) {
            Stop-Scan "gitleaks no ha podido revisarlo todo con las reglas $($set.Key) (código $LASTEXITCODE)."
        }
        if ($LASTEXITCODE -eq 0) { continue }
        foreach ($f in @(Get-Content -LiteralPath $report -Raw | ConvertFrom-Json)) {
            $index, $view = [IO.Path]::GetFileName($f.File) -split '-', 2
            [pscustomobject]@{
                Fichero = $files[[int]$index].FullName.Substring($Ruta.Length + 1)
                Reglas = $set.Key
                Regla = $f.RuleID
                Lectura = $labels[$view]
            }
        }
    }
} finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}

$found = @($hits | Group-Object Fichero, Reglas, Regla | ForEach-Object {
    $first = $_.Group[0]
    [pscustomobject]@{
        Fichero = $first.Fichero
        Reglas = $first.Reglas
        Regla = $first.Regla
        Lecturas = ($_.Group.Lectura | Select-Object -Unique) -join ', '
    }
} | Sort-Object Fichero, Regla)
if ($Informe) { ConvertTo-Json -InputObject $found -Depth 3 | Set-Content -LiteralPath $Informe -Encoding utf8 }
if ($found.Count) { $found | Format-Table -AutoSize | Out-String -Width 220 }
"TOTAL: $($found.Count) hallazgos en $($files.Count) ficheros revisados (reglas: $($sets.Keys -join ', '))"
exit ([int]($found.Count -gt 0))
