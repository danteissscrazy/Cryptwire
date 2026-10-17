# Prepara lo que se distribuye y no lo da por bueno hasta que pasa todos los controles:
#   1. el árbol de git está limpio: se publica el commit, no la carpeta;
#   2. el escáner caza datos plantados (si no, su «limpio» no vale nada);
#   3. el commit se clona en artifacts\fuente y se compila ahí: ni los cambios sin guardar ni lo que git
#      ignora (un Thumbs.db, una captura en Assets\…) entran en lo publicado;
#   4. paquetes exactos (packages.lock.json) y sin vulnerabilidades conocidas (NuGetAudit), con los tests
#      en Release;
#   5. el escáner no encuentra nada en lo publicado.
# Deja la app en artifacts\publicar. No toca bin\ ni obj\ de tu carpeta, así que se puede ejecutar con
# Cryptwire abierta desde un bin\Release.
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot
$artifacts = Join-Path $repo 'artifacts'
$source = Join-Path $artifacts 'fuente'
$output = Join-Path $artifacts 'publicar'
$pwsh = (Get-Process -Id $PID).Path

function Step([string] $Title) { "`n== $Title" }

function Assert-Exit([string] $What) {
    if ($LASTEXITCODE -ne 0) {
        [Console]::Error.WriteLine("publicar: $What (código $LASTEXITCODE). No se publica.")
        exit 1
    }
}

Step 'Árbol de git'
if (git -C $repo status --porcelain) {
    [Console]::Error.WriteLine('publicar: hay cambios sin guardar en git. Se publica un commit: haz commit primero.')
    exit 1
}
"commit $(git -C $repo rev-parse --short HEAD)"

Step 'El escáner caza datos plantados'
& $pwsh -NoProfile -File (Join-Path $PSScriptRoot 'probar-escaner.ps1') | Select-Object -Last 1
Assert-Exit 'el escáner no ha pasado su prueba'

Step 'Copia limpia del commit'
if (Test-Path -LiteralPath $artifacts) { Remove-Item -LiteralPath $artifacts -Recurse -Force }
git clone --quiet $repo $source
Assert-Exit 'no se ha podido clonar el commit'
$common = @('-c', 'Release', '-nologo', '-v', 'q', '-p:RestoreLockedMode=true', '-p:RestoreForce=true')

Step 'Paquetes bloqueados y sin vulnerabilidades, tests en Release'
dotnet test (Join-Path $source 'tests\Cryptwire.Core.Tests\Cryptwire.Core.Tests.csproj') @common 2>&1 | Select-Object -Last 3
Assert-Exit 'fallan la restauración o los tests'

Step 'Publicación (Windows x64, con .NET dentro)'
dotnet publish (Join-Path $source 'src\Cryptwire.App\Cryptwire.App.csproj') @common -r win-x64 --self-contained -o $output 2>&1 | Select-Object -Last 2
Assert-Exit 'falla la publicación'

Step 'Nada personal ni secreto en lo publicado'
& $pwsh -NoProfile -File (Join-Path $PSScriptRoot 'escanear-binario.ps1') -Ruta $output
Assert-Exit 'el escáner encuentra algo o no ha podido comprobarlo'

Step 'Listo'
"Publicado en $output"
Get-ChildItem -LiteralPath $output -Filter 'Cryptwire*.dll' | ForEach-Object {
    '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName).Hash, $_.Name
}
