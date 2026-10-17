# Prueba que escanear-binario.ps1 caza datos plantados a propósito en todas las formas en que un binario
# los guarda (UTF-8, Latin-1, UTF-16 LE y BE en posición par o impar, cadenas larguísimas, un .dll y un
# .pdb compilados sin PathMap, fuentes comprimidas dentro del .pdb y un PDB comprimido dentro del .dll),
# que no salta con ficheros limpios ni con casi-coincidencias, que PathMap deja limpio lo que limpia y que
# falla cerrado cuando no puede comprobar (también ante un zip). Las reglas plantadas extienden las de
# gitleaks de serie, con sus listas de rutas permitidas, como podrían hacerlo las tuyas. Los datos
# plantados se inventan al ejecutarla: aquí no hay nada que proteger. Sale con 0 si todo va como debe y
# 1 si no.
$ErrorActionPreference = 'Stop'
$scanner = Join-Path $PSScriptRoot 'escanear-binario.ps1'
$repo = Split-Path $PSScriptRoot
$pwsh = (Get-Process -Id $PID).Path
$failures = [Collections.Generic.List[string]]::new()

function Assert-True([bool] $Condition, [string] $Name) {
    if ($Condition) { "  ok     $Name" } else { "  FALLO  $Name"; $failures.Add($Name) }
}

function Get-RandomBytes([int] $Count) {
    $bytes = [byte[]]::new($Count)
    [Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    $bytes
}

# El dato entre bytes al azar; $Before decide si cae en posición par o impar.
function New-Planted([byte[]] $Payload, [int] $Before) {
    [byte[]]((Get-RandomBytes $Before) + $Payload + (Get-RandomBytes 301))
}

function Invoke-Scanner([string[]] $Arguments) {
    $output = & $pwsh -NoProfile -File $scanner @Arguments 2>&1
    [pscustomobject]@{ Code = $LASTEXITCODE; Output = ($output -join "`n") }
}

$work = Join-Path ([IO.Path]::GetTempPath()) ('cryptwire-prueba-escaner-' + [Guid]::NewGuid().ToString('N'))
$scan = Join-Path $work 'escaneo'
New-Item -ItemType Directory -Path $scan | Out-Null
try {
    $tag = [Guid]::NewGuid().ToString('N').Substring(0, 10)
    $email = "cw-plantado-$tag@example.invalid"
    $name = "Ñandú Plantado $tag"
    $folder = "cw-carpeta-$tag"
    $alnum = [char[]]'0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ'
    $token = 'ghp_' + -join (Get-RandomBytes 36 | ForEach-Object { $alnum[$_ % $alnum.Length] })
    $rules = Join-Path $work 'reglas.toml'
    Set-Content -LiteralPath $rules -Encoding utf8 -Value @"
title = 'Reglas plantadas'
[extend]
useDefault = true
[[rules]]
id = 'plantado-comentario'
regex = '''(?i)cw-comentario-$tag'''
[[rules]]
id = 'plantado-email'
regex = '''(?i)cw-plantado-$tag@example\.invalid'''
[[rules]]
id = 'plantado-nombre'
regex = '''(?i)ñandú plantado $tag'''
[[rules]]
id = 'plantado-carpeta'
regex = '''(?i)cw-carpeta-$tag'''
[[rules]]
id = 'plantado-fuente'
regex = '''(?i)cw-carpeta-$tag[\w-]*[\\/]+Plantado\.cs'''
"@

    $utf8 = [Text.UTF8Encoding]::new($false)
    $profilePath = [Environment]::GetFolderPath('UserProfile')
    $values = [ordered]@{
        email = @($email, 'plantado-email')
        nombre = @($name, 'plantado-nombre')
        token = @($token, 'github-pat')
        repo = @("$repo\src\Programa.cs", 'ruta-del-repo')
        perfil = @("$profilePath\.nuget\packages", 'ruta-del-perfil')
    }
    $encodings = [ordered]@{
        'UTF-8' = @($utf8, 0)
        'Latin-1' = @([Text.Encoding]::Latin1, 0)
        'UTF-16LE' = @([Text.Encoding]::Unicode, 0)
        'UTF-16LE+1' = @([Text.Encoding]::Unicode, 1)
        'UTF-16BE' = @([Text.Encoding]::BigEndianUnicode, 0)
        'UTF-16BE+1' = @([Text.Encoding]::BigEndianUnicode, 1)
    }

    # Un repo dentro del perfil también es una ruta del perfil.
    $repoInProfile = $repo.StartsWith("$profilePath\", [StringComparison]::OrdinalIgnoreCase)

    # Qué tiene que encontrar en cada fichero: regla => lectura en la que tiene que verla.
    $expected = @{}
    foreach ($v in $values.GetEnumerator()) {
        foreach ($e in $encodings.GetEnumerator()) {
            # Latin-1 solo dice algo con letras fuera de ASCII; las rutas con «ó» o «ñ» también valen.
            if ($e.Key -eq 'Latin-1' -and $v.Value[0] -cmatch '^[\x00-\x7F]*$') { continue }
            $file = "$($v.Key)-$($e.Key).bin"
            $payload = $e.Value[0].GetBytes($v.Value[0])
            [IO.File]::WriteAllBytes((Join-Path $scan $file), (New-Planted $payload (64 + $e.Value[1])))
            $expected[$file] = @{ $v.Value[1] = $e.Key }
            if ($v.Key -eq 'repo' -and $repoInProfile) { $expected[$file]['ruta-del-perfil'] = $e.Key }
        }
    }

    $mixed = "$($repo.ToLowerInvariant().Replace('\', '/'))/obj/Release"
    [IO.File]::WriteAllBytes((Join-Path $scan 'repo-barras-y-minusculas.bin'), (New-Planted $utf8.GetBytes($mixed) 64))
    $expected['repo-barras-y-minusculas.bin'] = @{ 'ruta-del-repo' = 'UTF-8' }
    if ($repoInProfile) { $expected['repo-barras-y-minusculas.bin']['ruta-del-perfil'] = 'UTF-8' }
    $upper = $utf8.GetBytes($email.ToUpperInvariant())
    [IO.File]::WriteAllBytes((Join-Path $scan 'email-mayusculas.bin'), (New-Planted $upper 64))
    $expected['email-mayusculas.bin'] = @{ 'plantado-email' = 'UTF-8' }

    # Una cadena legible enorme, con el dato a caballo de donde gitleaks corta sus bloques (100 KB) y de
    # donde el escáner trocea las cadenas largas (cada 1800 caracteres, trozos de 2000).
    $letters = [char[]](Get-RandomBytes 250000 | ForEach-Object { [char](97 + $_ % 26) })
    $long = [Text.StringBuilder]::new([string]::new($letters))
    [void]$long.Insert(1985, $email).Insert(99990, $email).Insert(199995, $token)
    [IO.File]::WriteAllText((Join-Path $scan 'cadena-larga.txt'), $long.ToString(), $utf8)
    $expected['cadena-larga.txt'] = @{ 'plantado-email' = 'UTF-8'; 'github-pat' = 'UTF-8' }

    # Limpios: bytes al azar y casi-coincidencias que no deben saltar.
    [IO.File]::WriteAllBytes((Join-Path $scan 'limpio-azar.bin'), (Get-RandomBytes 65536))
    $near = @(
        $email.Replace("$tag@", "$($tag.Substring(1))x@")
        "${profilePath}x\.nuget"
        if (-not $repoInProfile) { "${repo}x\src" }
    ) -join "`n"
    [IO.File]::WriteAllText((Join-Path $scan 'limpio-casi.txt'), $near, $utf8)
    [IO.File]::WriteAllBytes((Join-Path $scan 'limpio-casi-utf16.bin'), [Text.Encoding]::Unicode.GetBytes($near))
    foreach ($clean in 'limpio-azar.bin', 'limpio-casi.txt', 'limpio-casi-utf16.bin') { $expected[$clean] = @{} }

    # Compilado de verdad: la ruta de la carpeta acaba en el .dll y troceada en el .pdb, el correo en UTF-16
    # (cadena del código), el nombre en UTF-8 (atributo) y el comentario en ningún sitio, salvo si las
    # fuentes van incrustadas: entonces va comprimido dentro del .pdb, o del PDB comprimido dentro del .dll.
    # Con PathMap, la ruta desaparece y el resto no.
    $variants = @(
        @('sin-pathmap', @('-p:Mapear=false')),
        @('fuentes-en-pdb', @('-p:Mapear=true', '-p:EmbedAllSources=true')),
        @('pdb-en-dll', @('-p:Mapear=true', '-p:EmbedAllSources=true', '-p:DebugType=embedded'))
    )
    foreach ($variant in $variants) {
        $proj = Join-Path $work "$folder-$($variant[0])"
        New-Item -ItemType Directory -Path $proj | Out-Null
        Set-Content -LiteralPath (Join-Path $proj 'Plantado.csproj') -Encoding utf8 -Value @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <PathMap Condition="'$(Mapear)' == 'true'">$(MSBuildProjectDirectory)=/_/</PathMap>
  </PropertyGroup>
</Project>
'@
        # El relleno hace que el compilador comprima la fuente al incrustarla (las pequeñas van tal cual).
        Set-Content -LiteralPath (Join-Path $proj 'Plantado.cs') -Encoding utf8 -Value @"
// cw-comentario-$tag
// $('relleno ' * 300)
[assembly: System.Reflection.AssemblyMetadata("plantado", "$name")]
public static class Plantado { public static string Correo() => "$email"; }
"@
        $build = dotnet build $proj -c Release -nologo -v q @($variant[1]) `
            -p:ImportDirectoryBuildProps=false -p:ImportDirectoryBuildTargets=false 2>&1
        if ($LASTEXITCODE -ne 0) { throw "no compila el proyecto plantado ($($variant[0])): $build" }
        $out = Join-Path $scan "compilado-$($variant[0])"
        New-Item -ItemType Directory -Path $out | Out-Null
        Get-ChildItem (Join-Path $proj 'bin\Release\net10.0') -Filter 'Plantado.*' | Where-Object Extension -in '.dll', '.pdb' |
            Copy-Item -Destination $out
    }
    $expected['compilado-sin-pathmap\Plantado.dll'] = @{ 'plantado-carpeta' = 'rutas de depuración'; 'plantado-email' = 'UTF-16LE'; 'plantado-nombre' = 'UTF-8' }
    # Entera, la ruta de la fuente solo está en las rutas de depuración del .pdb: en sus bytes va troceada.
    $expected['compilado-sin-pathmap\Plantado.pdb'] = @{ 'plantado-carpeta' = 'rutas de depuración'; 'plantado-fuente' = 'rutas de depuración' }
    $expected['compilado-fuentes-en-pdb\Plantado.dll'] = @{ 'plantado-email' = 'UTF-16LE'; 'plantado-nombre' = 'UTF-8' }
    $expected['compilado-fuentes-en-pdb\Plantado.pdb'] = @{ 'plantado-comentario' = 'depuración (UTF-8)'; 'plantado-email' = 'depuración (UTF-8)'; 'plantado-nombre' = 'depuración (UTF-8)' }
    $expected['compilado-pdb-en-dll\Plantado.dll'] = @{ 'plantado-comentario' = 'depuración (UTF-8)'; 'plantado-email' = 'UTF-16LE'; 'plantado-nombre' = 'UTF-8' }

    "Datos plantados ($($expected.Count) ficheros):"
    $report = Join-Path $work 'informe.json'
    $run = Invoke-Scanner @('-Ruta', $scan, '-Reglas', $rules, '-Informe', $report)
    Assert-True ($run.Code -eq 1) "sale con 1 al encontrar algo (salió con $($run.Code))"
    $found = if (Test-Path -LiteralPath $report) { @(Get-Content -LiteralPath $report -Raw | ConvertFrom-Json) } else { @() }
    Assert-True (-not ($run.Output -match [regex]::Escape($tag))) 'la salida no enseña ningún dato plantado'
    foreach ($case in $expected.GetEnumerator() | Sort-Object Key) {
        $hits = @($found | Where-Object Fichero -eq $case.Key)
        # Las rutas de esta máquina pueden saltar además en lo compilado (la carpeta temporal está en tu
        # perfil); en lo demás, nada que no se haya plantado.
        $rulesFound = @($hits | Where-Object { -not ($case.Key -like 'compilado-sin-pathmap*' -and $_.Reglas -eq 'maquina') } |
            ForEach-Object Regla | Sort-Object -Unique)
        $ok = ($rulesFound -join ',') -eq (($case.Value.Keys | Sort-Object) -join ',')
        foreach ($rule in $case.Value.GetEnumerator()) {
            $hit = $hits | Where-Object Regla -eq $rule.Key | Select-Object -First 1
            $ok = $ok -and $hit -and (($hit.Lecturas -split ', ') -contains $rule.Value)
        }
        $what = if ($case.Value.Count) { ($case.Value.GetEnumerator() | ForEach-Object { "$($_.Key) en $($_.Value)" }) -join '; ' } else { 'nada' }
        Assert-True $ok "$($case.Key): $what$(if (-not $ok) { " (encontrado: $(($hits | ForEach-Object { "$($_.Regla) en $($_.Lecturas)" }) -join '; '))" })"
    }

    "Falla cerrado:"
    $empty = Join-Path $work 'vacia'
    New-Item -ItemType Directory -Path $empty | Out-Null
    Assert-True ((Invoke-Scanner @('-Ruta', $empty, '-Reglas', $rules)).Code -eq 2) 'carpeta vacía'
    Assert-True ((Invoke-Scanner @('-Ruta', (Join-Path $work 'no-existe'), '-Reglas', $rules)).Code -eq 2) 'carpeta que no existe'
    Assert-True ((Invoke-Scanner @('-Ruta', $scan, '-Reglas', (Join-Path $work 'no-existe.toml'))).Code -eq 2) 'reglas que no existen'
    $broken = Join-Path $work 'rota.toml'
    Set-Content -LiteralPath $broken -Encoding utf8 -Value "[[rules]]`nid = 'rota'`nregex = '''(?i)[cw-rota-$tag'''"
    $run = Invoke-Scanner @('-Ruta', $scan, '-Reglas', $broken)
    Assert-True ($run.Code -eq 2) 'regla que no compila'
    Assert-True (-not ($run.Output -match "cw-rota-$tag")) 'y no enseña la regla (podría ser tu dato)'
    $zipped = Join-Path $work 'comprimido'
    New-Item -ItemType Directory -Path $zipped | Out-Null
    [IO.File]::WriteAllText((Join-Path $work 'dentro.txt'), $email, $utf8)
    Compress-Archive -LiteralPath (Join-Path $work 'dentro.txt') -DestinationPath (Join-Path $zipped 'paquete.zip')
    Assert-True ((Invoke-Scanner @('-Ruta', $zipped, '-Reglas', $rules)).Code -eq 2) 'un zip (no se ve dentro)'
    # Un .dll con su PDB incrustado estropeado: no se puede leer lo comprimido, luego no está limpio.
    $corrupt = Join-Path $work 'estropeado'
    New-Item -ItemType Directory -Path $corrupt | Out-Null
    $dll = [IO.File]::ReadAllBytes((Join-Path $scan 'compilado-pdb-en-dll\Plantado.dll'))
    $at = [Text.Encoding]::Latin1.GetString($dll).IndexOf('MPDB', [StringComparison]::Ordinal)
    for ($k = $at + 8; $k -lt $at + 72; $k++) { $dll[$k] = $dll[$k] -bxor 0x5A }
    [IO.File]::WriteAllBytes((Join-Path $corrupt 'Plantado.dll'), $dll)
    Assert-True ($at -gt 0 -and (Invoke-Scanner @('-Ruta', $corrupt, '-Reglas', $rules)).Code -eq 2) 'un .dll con el PDB incrustado ilegible'
    $path = $env:PATH
    try {
        $env:PATH = ($path -split ';' | Where-Object { $_ -and -not (Test-Path -LiteralPath (Join-Path $_ 'gitleaks.exe')) }) -join ';'
        Assert-True ((Invoke-Scanner @('-Ruta', $scan, '-Reglas', $rules)).Code -eq 2) 'sin gitleaks'
    } finally {
        $env:PATH = $path
    }
    Assert-True (-not (Get-ChildItem ([IO.Path]::GetTempPath()) -Filter 'cryptwire-escaneo-*' -ErrorAction SilentlyContinue)) 'no deja copias temporales'
} finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}

if ($failures.Count) {
    "RESULTADO: $($failures.Count) comprobaciones fallidas."
    exit 1
}
'RESULTADO: el escáner caza todo lo plantado, no salta con lo limpio y falla cerrado.'
exit 0
