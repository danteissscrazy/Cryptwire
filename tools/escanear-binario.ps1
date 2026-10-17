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
# Latin-1 y UTF-16 LE y BE, alineado y desalineado), más sus rutas de depuración si es un .NET, y gitleaks
# las revisa como texto con tres juegos de reglas: los secretos del repo (.gitleaks.toml), tus datos
# personales y las rutas de esta máquina (el repo y tu perfil de usuario, también en su forma corta 8.3).
# Nunca enseña lo que encuentra, solo dónde y con qué regla: la salida puede acabar en un log público.
# Sale con 0 si está limpio, 1 si encuentra algo y 2 si no ha podido comprobarlo: falla cerrado.
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

# Las rutas de depuración de un .NET: la del .pdb que apunta el .dll y la de cada fuente. El .pdb guarda
# esas rutas troceadas por carpetas, y un PDB incrustado va comprimido dentro del .dll: ninguna lectura de
# bytes las ve enteras.
function Get-DebugPaths([string] $Path) {
    $stream = [IO.File]::OpenRead($Path)
    try {
        $head = [byte[]]::new(4)
        $read = $stream.Read($head, 0, 4)
        $stream.Position = 0
        $pdbs = @()
        if ($read -eq 4 -and [Text.Encoding]::ASCII.GetString($head) -eq 'BSJB') {
            $pdbs += [Reflection.Metadata.MetadataReaderProvider]::FromPortablePdbStream($stream, 'LeaveOpen')
        } elseif ($read -ge 2 -and $head[0] -eq 0x4D -and $head[1] -eq 0x5A) {
            $pe = [Reflection.PortableExecutable.PEReader]::new($stream, 'LeaveOpen')
            foreach ($entry in $pe.ReadDebugDirectory()) {
                if ($entry.Type -eq 'CodeView') { $pe.ReadCodeViewDebugDirectoryData($entry).Path }
                if ($entry.Type -eq 'EmbeddedPortablePdb') { $pdbs += $pe.ReadEmbeddedPortablePdbDebugDirectoryData($entry) }
            }
        }
        foreach ($pdb in $pdbs) {
            $reader = $pdb.GetMetadataReader()
            foreach ($doc in $reader.Documents) { $reader.GetString($reader.GetDocument($doc).Name) }
            $pdb.Dispose()
        }
    } catch {
        # No es un .NET legible: quedan las lecturas de bytes.
    } finally {
        $stream.Dispose()
    }
}

function Get-ShortPath([string] $Path) {
    try { (New-Object -ComObject Scripting.FileSystemObject).GetFolder($Path).ShortPath } catch { $null }
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

    # Las cadenas legibles de cada lectura, una por párrafo, en ficheros con nombres neutros: así ninguna
    # lista de rutas permitidas de gitleaks (.dll, .png…) se salta nada, el tipo de fichero siempre es texto
    # y gitleaks corta sus bloques entre cadenas. Las muy largas van en trozos que se solapan.
    $utf8 = [Text.UTF8Encoding]::new($false)
    $printable = [regex]::new('[^\p{C}�]{4,}')
    $labels = [ordered]@{ utf8 = 'UTF-8'; latin1 = 'Latin-1'; u16le = 'UTF-16LE'; u16le1 = 'UTF-16LE+1'; u16be = 'UTF-16BE'; u16be1 = 'UTF-16BE+1'; pdb = 'rutas de depuración' }
    for ($i = 0; $i -lt $files.Count; $i++) {
        $bytes = [IO.File]::ReadAllBytes($files[$i].FullName)
        $odd = [Math]::Max($bytes.Length - 1, 0)
        $texts = [ordered]@{
            utf8 = [Text.Encoding]::UTF8.GetString($bytes)
            latin1 = [Text.Encoding]::Latin1.GetString($bytes)
            u16le = [Text.Encoding]::Unicode.GetString($bytes)
            u16le1 = [Text.Encoding]::Unicode.GetString($bytes, [Math]::Min(1, $bytes.Length), $odd)
            u16be = [Text.Encoding]::BigEndianUnicode.GetString($bytes)
            u16be1 = [Text.Encoding]::BigEndianUnicode.GetString($bytes, [Math]::Min(1, $bytes.Length), $odd)
            pdb = @(Get-DebugPaths $files[$i].FullName) -join "`n"
        }
        foreach ($t in $texts.GetEnumerator()) {
            $sb = [Text.StringBuilder]::new("cryptwire: cadenas legibles`n`n")
            foreach ($m in $printable.Matches($t.Value)) {
                for ($at = 0; ; $at += 1800) {
                    [void]$sb.Append($m.Value, $at, [Math]::Min(2000, $m.Length - $at)).Append("`n`n")
                    if ($at + 2000 -ge $m.Length) { break }
                }
            }
            [IO.File]::WriteAllText((Join-Path $views ('{0:D5}.{1}' -f $i, $t.Key)), $sb.ToString(), $utf8)
        }
    }

    $hits = foreach ($set in $sets.GetEnumerator()) {
        $report = Join-Path $work "informe-$($set.Key).json"
        # --gitleaks-ignore-path a la carpeta temporal: ningún .gitleaksignore puede silenciar el escaneo.
        $output = & $gitleaks.Source dir $views --config $set.Value --no-banner --redact --exit-code 99 `
            --report-format json --report-path $report --gitleaks-ignore-path $work --log-level error 2>&1
        if ($LASTEXITCODE -eq 0) { continue }
        if ($LASTEXITCODE -ne 99) { Stop-Scan "gitleaks ha fallado con las reglas $($set.Key) (código $LASTEXITCODE): $output" }
        foreach ($f in @(Get-Content -LiteralPath $report -Raw | ConvertFrom-Json)) {
            $leaf = [IO.Path]::GetFileName($f.File)
            $index, $view = $leaf -split '\.', 2
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
