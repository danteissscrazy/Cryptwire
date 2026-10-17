param(
    # Publica aunque haya cambios sin guardar en git (para probar). Lo normal es publicar un commit.
    [switch] $PermitirCambios
)
# Prepara lo que se distribuye y no lo da por bueno hasta que pasa todos los controles:
#   1. el escáner caza datos plantados (si no, su «limpio» no vale nada);
#   2. paquetes exactos (packages.lock.json) y sin vulnerabilidades conocidas (NuGetAudit), con los
#      tests en Release;
#   3. publicación en artifacts\ (dentro del repo: PathMap solo limpia las rutas que cuelgan de él);
#   4. el escáner no encuentra nada en lo publicado.
# Deja la app en artifacts\publish\Cryptwire.App\release. No toca bin\ ni obj\ de los proyectos, así
# que se puede ejecutar con Cryptwire abierta desde un bin\Release.
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot
$artifacts = Join-Path $repo 'artifacts'
$output = Join-Path $artifacts 'publish\Cryptwire.App\release'
$pwsh = (Get-Process -Id $PID).Path

function Step([string] $Title) { "`n== $Title" }

function Assert-Exit([string] $What) {
    if ($LASTEXITCODE -ne 0) {
        [Console]::Error.WriteLine("publicar: $What (código $LASTEXITCODE). No se publica.")
        exit 1
    }
}

Step 'Árbol de git'
$dirty = git -C $repo status --porcelain
if ($dirty -and -not $PermitirCambios) {
    [Console]::Error.WriteLine("publicar: hay cambios sin guardar en git. Haz commit o usa -PermitirCambios.")
    exit 1
}
"commit $(git -C $repo rev-parse --short HEAD)$(if ($dirty) { ' con cambios sin guardar' })"

Step 'El escáner caza datos plantados'
& $pwsh -NoProfile -File (Join-Path $PSScriptRoot 'probar-escaner.ps1') | Select-Object -Last 1
Assert-Exit 'el escáner no ha pasado su prueba'

if (Test-Path -LiteralPath $artifacts) { Remove-Item -LiteralPath $artifacts -Recurse -Force }
$common = @('-c', 'Release', '--artifacts-path', $artifacts, '-nologo', '-v', 'q',
    '-p:RestoreLockedMode=true', '-p:RestoreForce=true')

Step 'Paquetes bloqueados y sin vulnerabilidades, tests en Release'
dotnet test (Join-Path $repo 'tests\Cryptwire.Core.Tests\Cryptwire.Core.Tests.csproj') @common 2>&1 | Select-Object -Last 3
Assert-Exit 'fallan la restauración o los tests'

Step 'Publicación'
dotnet publish (Join-Path $repo 'src\Cryptwire.App\Cryptwire.App.csproj') @common 2>&1 | Select-Object -Last 2
Assert-Exit 'falla la publicación'

Step 'Nada personal ni secreto en lo publicado'
& $pwsh -NoProfile -File (Join-Path $PSScriptRoot 'escanear-binario.ps1') -Ruta $output
Assert-Exit 'el escáner encuentra algo o no ha podido comprobarlo'

Step 'Listo'
"Publicado en $output"
Get-ChildItem -LiteralPath $output -Filter 'Cryptwire*.dll' | ForEach-Object {
    '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName).Hash, $_.Name
}
