param(
    # El instalador que se prueba (artifacts\instalador\Cryptwire-<versión>-instalador.exe).
    [Parameter(Mandatory)] [string] $Instalador,
    # usuario: el instalador corre sin permisos de administrador, como lo abre casi todo el mundo (WebView2
    # se instala solo para ese usuario). administrador: corre elevado (WebView2 se instala para todo el PC).
    [Parameter(Mandatory)] [ValidateSet('usuario', 'administrador')] [string] $Modo,
    # Carpeta para el log del instalador.
    [Parameter(Mandatory)] [string] $Salida
)
# Prueba la rama del instalador que instala WebView2 en un Windows que no lo tiene: quita WebView2 de la
# máquina, instala Cryptwire en silencio y no la da por buena hasta que WebView2 vuelve a estar y Cryptwire
# arranca con él (sus procesos de WebView2 corren con el perfil de Cryptwire).
#
# Quita WebView2 del PC entero, así que solo corre en una máquina desechable de GitHub Actions.
$ErrorActionPreference = 'Stop'
trap {
    [Console]::Error.WriteLine("probar-instalador: $_")
    # Para ver qué había en pantalla (un diálogo de error de Cryptwire, por ejemplo): en la máquina de
    # GitHub no hay nada tuyo que salga en la captura.
    try {
        Get-Process -Name Cryptwire -ErrorAction SilentlyContinue | ForEach-Object { "Cryptwire abierta: '$($_.MainWindowTitle)'" }
        Add-Type -AssemblyName System.Windows.Forms, System.Drawing
        $bounds = [Windows.Forms.Screen]::PrimaryScreen.Bounds
        $bitmap = [Drawing.Bitmap]::new($bounds.Width, $bounds.Height)
        [Drawing.Graphics]::FromImage($bitmap).CopyFromScreen($bounds.Location, [Drawing.Point]::Empty, $bounds.Size)
        $bitmap.Save((Join-Path $Salida 'pantalla.png'), [Drawing.Imaging.ImageFormat]::Png)
    } catch {
        [Console]::Error.WriteLine("probar-instalador: sin captura de pantalla ($_)")
    }
    exit 1
}

if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted') {
    [Console]::Error.WriteLine('probar-instalador: solo corre en una máquina de GitHub Actions, porque quita WebView2 del PC.')
    exit 2
}

$clientKey = 'Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}'
$machineKey = "HKLM:\SOFTWARE\WOW6432Node\$clientKey"
$userKey = "HKCU:\Software\$clientKey"
$exe = Join-Path $env:LOCALAPPDATA 'Programs\Cryptwire\Cryptwire.exe'
$profileDir = Join-Path $env:LOCALAPPDATA 'Cryptwire\WebView2'
$null = New-Item -ItemType Directory -Path $Salida -Force
$Salida = (Resolve-Path $Salida).Path
$Instalador = (Resolve-Path $Instalador).Path
$log = Join-Path $Salida 'instalador.log'

# runas recibe una sola línea de órdenes y no admite comillas dentro: en las máquinas de GitHub ninguna de
# estas rutas lleva espacios, y si alguna los llevara, mejor parar que lanzar otra cosa.
foreach ($path in @($Instalador, $log, $exe)) {
    if ($path -match '\s') { throw "una ruta lleva espacios y runas no la aceptaría: $path" }
}

function Step([string] $Title) { "`n== $Title" }

# La versión de WebView2 donde la mira el instalador de Cryptwire (y Microsoft): vacía o 0.0.0.0 es que no está.
function Get-WebView2Version([string] $Key) {
    $pv = (Get-ItemProperty -LiteralPath $Key -Name pv -ErrorAction SilentlyContinue).pv
    if ($pv -and $pv -ne '0.0.0.0') { $pv } else { $null }
}

function Get-WebView2 {
    [pscustomobject]@{ Machine = Get-WebView2Version $machineKey; User = Get-WebView2Version $userKey }
}

function Format-WebView2($State) {
    "WebView2 para todo el PC: $(if ($State.Machine) { $State.Machine } else { 'no' }); para el usuario: $(if ($State.User) { $State.User } else { 'no' })"
}

# Lanza una orden con el token del modo. En «usuario», runas con el nivel «usuario básico» le quita el
# grupo de administradores. runas no espera a que termine: quien llama espera aparte. Se espera solo a
# runas con WaitForExit: Start-Process -Wait espera también a sus descendientes, y Cryptwire no acaba.
function Start-AsMode([string] $File, [string] $Arguments) {
    if ($Modo -eq 'usuario') {
        $p = Start-Process -FilePath runas.exe -ArgumentList "/trustlevel:0x20000 `"$File $Arguments`"" -NoNewWindow -PassThru
        $p.WaitForExit()
        if ($p.ExitCode -ne 0) { throw "runas no ha podido lanzar $File (salida $($p.ExitCode))." }
    } elseif ($Arguments) {
        $null = Start-Process -FilePath $File -ArgumentList $Arguments
    } else {
        $null = Start-Process -FilePath $File
    }
}

function Wait-Until([scriptblock] $Condition, [int] $Seconds, [string] $What) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        if (& $Condition) { return }
        Start-Sleep -Seconds 2
    }
    throw "pasados $Seconds s, $What."
}

function Get-CryptwireWebViews {
    $pattern = [regex]::Escape($profileDir)
    @(Get-CimInstance Win32_Process -Filter "Name = 'msedgewebview2.exe'" | Where-Object { $_.CommandLine -match $pattern })
}

Step 'Estado inicial'
$before = Get-WebView2
Format-WebView2 $before

Step 'Quitar WebView2'
foreach ($scope in @(
        @{ Name = 'todo el PC'; Root = ${env:ProgramFiles(x86)}; Flags = @('--system-level'); Present = $before.Machine },
        @{ Name = 'el usuario'; Root = $env:LOCALAPPDATA; Flags = @(); Present = $before.User })) {
    if (-not $scope.Present) { continue }
    $setup = Get-ChildItem -Path (Join-Path $scope.Root 'Microsoft\EdgeWebView\Application\*\Installer\setup.exe') -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if (-not $setup) { throw "WebView2 está para $($scope.Name), pero no encuentro su desinstalador." }
    $p = Start-Process -FilePath $setup.FullName -ArgumentList (@('--uninstall', '--msedgewebview', '--verbose-logging', '--force-uninstall') + $scope.Flags) -Wait -PassThru
    "desinstalador de WebView2 para $($scope.Name): salida $($p.ExitCode)"
}
$removed = Get-WebView2
Format-WebView2 $removed
if ($removed.Machine -or $removed.User) { throw 'WebView2 sigue instalado: la prueba no serviría.' }

if ($Modo -eq 'usuario') {
    Step 'El token sin permisos de administrador'
    $groups = Join-Path $Salida 'grupos.txt'
    if ($groups -match '\s') { throw "una ruta lleva espacios y runas no la aceptaría: $groups" }
    Start-AsMode 'cmd.exe' "/c whoami /groups /fo csv > $groups"
    Wait-Until { (Test-Path $groups) -and (Get-Item $groups).Length -gt 0 } 30 'whoami no ha escrito los grupos'
    Start-Sleep -Seconds 1
    $admins = @(Get-Content $groups | Where-Object { $_ -match 'S-1-5-32-544' })
    if (-not $admins -or $admins[0] -notmatch 'deny only') { throw "el token sigue siendo de administrador: $admins" }
    'administradores: solo para denegar (el instalador no puede elevarse)'
}

Step "Instalar Cryptwire ($Modo)"
Start-AsMode $Instalador "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /LOG=$log"
# Inno Setup cierra el log al terminar, también cuando falla.
Wait-Until { (Test-Path $log) -and (Select-String -Path $log -Pattern 'Log closed.' -SimpleMatch -Quiet) } 600 'el instalador no ha terminado'
Select-String -Path $log -Pattern 'WebView2|Exit code|Installation process succeeded|error' | ForEach-Object { $_.Line }
if (-not (Select-String -Path $log -Pattern 'Installation process succeeded' -SimpleMatch -Quiet)) { throw 'el instalador no ha terminado bien (ver instalador.log).' }

Step 'WebView2 después de instalar'
$after = Get-WebView2
Format-WebView2 $after
if (-not ($after.Machine -or $after.User)) { throw 'el instalador ha terminado, pero WebView2 no está.' }
$expected = if ($Modo -eq 'usuario') { 'User' } else { 'Machine' }
if (-not $after.$expected) { "aviso: WebView2 ha quedado instalado, pero no donde se esperaba ($expected)." }

Step 'Cryptwire arranca con ese WebView2'
if (-not (Test-Path $exe)) { throw "no está $exe" }
Start-AsMode $exe ''
Wait-Until { (Get-CryptwireWebViews).Count -gt 0 } 120 'no hay procesos de WebView2 con el perfil de Cryptwire'
$webviews = Get-CryptwireWebViews
"$($webviews.Count) procesos de WebView2 $(Split-Path (Split-Path $webviews[0].ExecutablePath -Parent) -Leaf) con el perfil de Cryptwire"
# Que siga en pie un rato: un motor que arranca y se cae también deja procesos un momento.
Start-Sleep -Seconds 15
$webviews = Get-CryptwireWebViews
"a los 15 s: $($webviews.Count) procesos ($(($webviews | ForEach-Object { if ($_.CommandLine -match '--type=([\w-]+)') { $Matches[1] } else { 'principal' } }) -join ', '))"
if ($webviews.Count -eq 0) { throw 'el WebView2 de Cryptwire se ha cerrado.' }
if (-not (Get-Process -Name Cryptwire -ErrorAction SilentlyContinue)) { throw 'Cryptwire no sigue abierta.' }

Step 'Cerrar Cryptwire'
Start-AsMode $exe '--salir'
Wait-Until { -not (Get-Process -Name Cryptwire -ErrorAction SilentlyContinue) } 30 'Cryptwire no se ha cerrado con --salir'
'cerrada con --salir'

# La carpeta no se puede cambiar ni a mano (/DIR=): en una compartida, otros podrían cambiar lo que arranca.
Step 'Otra carpeta se rechaza'
$other = Join-Path $Salida 'otra-carpeta'
$otherLog = Join-Path $Salida 'instalador-otra-carpeta.log'
Start-AsMode $Instalador "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /DIR=$other /LOG=$otherLog"
Wait-Until { (Test-Path $otherLog) -and (Select-String -Path $otherLog -Pattern 'Log closed.' -SimpleMatch -Quiet) } 300 'el instalador no ha terminado'
if (Select-String -Path $otherLog -Pattern 'Installation process succeeded' -SimpleMatch -Quiet) { throw "el instalador ha aceptado otra carpeta ($other)." }
if (Test-Path (Join-Path $other 'Cryptwire.exe')) { throw "hay una Cryptwire en $other." }
if (-not (Select-String -Path $otherLog -Pattern 'se instala siempre en' -SimpleMatch -Quiet)) { throw 'el instalador ha fallado, pero no por la carpeta (ver instalador-otra-carpeta.log).' }
"rechazada: $other"

"`nOK: sin WebView2, el instalador ($Modo) lo instala, Cryptwire arranca con él y no se instala en otra carpeta."
