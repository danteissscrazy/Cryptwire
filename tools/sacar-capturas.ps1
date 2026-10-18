param(
    # Cryptwire.exe compilado en Debug: es la versión que escucha WEBVIEW2_* y deja entrar por el puerto de
    # depuración, que es por donde se tapa el QR y se lanza el aviso de ejemplo.
    [Parameter(Mandatory)] [string] $Exe,
    # Carpeta donde quedan las capturas (PNG).
    [Parameter(Mandatory)] [string] $Salida,
    # Solo la ventana y los ajustes: el aviso obliga a esperar casi una hora (ver abajo).
    [switch] $SinAviso
)
# Las capturas del README, en una máquina limpia de GitHub: Cryptwire recién instalada, sin vincular a
# ninguna cuenta, así que no sale ningún chat ni contacto de nadie. El QR de vincular se difumina (es de
# una sesión que se tira al acabar, pero un QR no pinta nada en un README) y el aviso es de un contacto
# inventado con una foto dibujada aquí.
#
# Abre un puerto de depuración a la sesión de WebView2 y cambia la pantalla y el escritorio, así que solo
# corre en una máquina desechable de GitHub Actions.
$ErrorActionPreference = 'Stop'
trap {
    [Console]::Error.WriteLine("sacar-capturas: $_")
    exit 1
}

if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted') {
    [Console]::Error.WriteLine('sacar-capturas: solo corre en una máquina de GitHub Actions (abre un puerto de depuración y cambia la pantalla).')
    exit 2
}

Add-Type -AssemblyName System.Drawing, UIAutomationClient, UIAutomationTypes
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;

public static class Win
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
        public int dmFields, dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
        public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
    }

    public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int cmd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc callback, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int max);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] public static extern bool SetSysColors(int count, int[] elements, int[] colors);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool SystemParametersInfo(uint action, uint param, string value, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool EnumDisplaySettings(string device, int mode, ref DEVMODE devMode);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int ChangeDisplaySettings(ref DEVMODE devMode, int flags);
    [DllImport("shell32.dll")] public static extern int SHQueryUserNotificationState(out int state);
}
'@

$null = [Win]::SetProcessDPIAware()
$null = New-Item -ItemType Directory -Path $Salida -Force
$Salida = (Resolve-Path $Salida).Path
$port = 9222

function Step([string] $Title) { "`n== $Title" }

function Wait-Until([scriptblock] $Condition, [int] $Seconds, [string] $What) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        $value = & $Condition
        if ($value) { return $value }
        Start-Sleep -Milliseconds 500
    }
    throw "pasados $Seconds s, $What."
}

function Get-Rect([IntPtr] $Hwnd) {
    $r = New-Object Win+RECT
    $null = [Win]::GetWindowRect($Hwnd, [ref] $r)
    $r
}

# Las ventanas visibles de un proceso, con su título y su tamaño.
function Get-ProcessWindows([int] $ProcessId) {
    $found = [Collections.Generic.List[object]]::new()
    $callback = [Win+EnumProc] {
        param($hwnd, $lparam)
        $windowPid = 0
        $null = [Win]::GetWindowThreadProcessId($hwnd, [ref] $windowPid)
        if ($windowPid -eq $ProcessId -and [Win]::IsWindowVisible($hwnd)) {
            $text = [Text.StringBuilder]::new(256)
            $null = [Win]::GetWindowText($hwnd, $text, 256)
            $r = Get-Rect $hwnd
            $found.Add([pscustomobject]@{ Hwnd = $hwnd; Title = $text.ToString(); Width = $r.Right - $r.Left; Height = $r.Bottom - $r.Top; Rect = $r })
        }
        $true
    }
    $null = [Win]::EnumWindows($callback, [IntPtr]::Zero)
    $found
}

# La ventana tal como la pinta ella misma (PrintWindow con PW_RENDERFULLCONTENT: incluye lo de WebView2).
function Save-Window([IntPtr] $Hwnd, [string] $Name) {
    $r = Get-Rect $Hwnd
    $bitmap = [Drawing.Bitmap]::new($r.Right - $r.Left, $r.Bottom - $r.Top)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $hdc = $graphics.GetHdc()
    $ok = [Win]::PrintWindow($Hwnd, $hdc, 2)
    $graphics.ReleaseHdc($hdc)
    $graphics.Dispose()
    if (-not $ok) { throw "PrintWindow ha fallado con $Name" }
    $bitmap.Save((Join-Path $Salida $Name), [Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
    "$Name ($($r.Right - $r.Left)x$($r.Bottom - $r.Top))"
}

# Un trozo de la pantalla tal como se ve (con las transparencias de los avisos sobre el escritorio).
function Save-Screen([int] $X, [int] $Y, [int] $Width, [int] $Height, [string] $Name) {
    $bitmap = [Drawing.Bitmap]::new($Width, $Height)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $graphics.CopyFromScreen($X, $Y, 0, 0, [Drawing.Size]::new($Width, $Height))
    $graphics.Dispose()
    $bitmap.Save((Join-Path $Salida $Name), [Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
    "$Name (${Width}x$Height)"
}

# Evalúa JavaScript en la página de WhatsApp por el protocolo de depuración de Chromium (CDP).
function Invoke-Page([string] $Expression) {
    $socket = [Net.WebSockets.ClientWebSocket]::new()
    $none = [Threading.CancellationToken]::None
    try {
        $null = $socket.ConnectAsync([Uri] $script:pageSocket, $none).GetAwaiter().GetResult()
        $request = @{ id = 1; method = 'Runtime.evaluate'; params = @{ expression = $Expression; returnByValue = $true; awaitPromise = $true } } |
            ConvertTo-Json -Depth 5 -Compress
        $bytes = [Text.Encoding]::UTF8.GetBytes($request)
        $null = $socket.SendAsync([ArraySegment[byte]]::new($bytes), [Net.WebSockets.WebSocketMessageType]::Text, $true, $none).GetAwaiter().GetResult()
        $buffer = [byte[]]::new(1MB)
        while ($true) {
            $message = [IO.MemoryStream]::new()
            do {
                $result = $socket.ReceiveAsync([ArraySegment[byte]]::new($buffer), $none).GetAwaiter().GetResult()
                $message.Write($buffer, 0, $result.Count)
            } while (-not $result.EndOfMessage)
            $reply = [Text.Encoding]::UTF8.GetString($message.ToArray()) | ConvertFrom-Json
            if ($reply.id -eq 1) {
                if ($reply.result.exceptionDetails) { throw "la página ha fallado: $($reply.result.exceptionDetails.exception.description)" }
                return $reply.result.result.value
            }
        }
    } finally {
        $socket.Dispose()
    }
}

# Una foto de perfil inventada: luna, estrellas y agujas góticas. Nada sale de ningún sitio.
function New-AvatarDataUri {
    $size = 256
    $bitmap = [Drawing.Bitmap]::new($size, $size)
    $g = [Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $sky = [Drawing.Drawing2D.LinearGradientBrush]::new([Drawing.Point]::new(0, 0), [Drawing.Point]::new(0, $size),
        [Drawing.Color]::FromArgb(255, 58, 22, 74), [Drawing.Color]::FromArgb(255, 8, 6, 20))
    $g.FillRectangle($sky, 0, 0, $size, $size)
    $random = [Random]::new(7)
    for ($i = 0; $i -lt 40; $i++) {
        $d = $random.Next(1, 4)
        $g.FillEllipse([Drawing.SolidBrush]::new([Drawing.Color]::FromArgb($random.Next(90, 230), 230, 225, 255)), $random.Next(0, $size), $random.Next(0, 150), $d, $d)
    }
    $g.FillEllipse([Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255, 238, 232, 214)), 140, 34, 78, 78)
    $g.FillEllipse([Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255, 46, 18, 62)), 160, 24, 78, 78)
    $spires = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255, 4, 3, 10))
    foreach ($s in @(@(18, 150, 34), @(62, 118, 40), @(112, 168, 30), @(150, 132, 44), @(204, 160, 34))) {
        $x = $s[0]; $top = $s[1]; $w = $s[2]
        $g.FillPolygon($spires, [Drawing.Point[]] @(
                [Drawing.Point]::new($x, $size), [Drawing.Point]::new($x, $top + 40), [Drawing.Point]::new($x + $w / 2, $top),
                [Drawing.Point]::new($x + $w, $top + 40), [Drawing.Point]::new($x + $w, $size)))
    }
    $g.FillRectangle($spires, 0, 220, $size, 36)
    $g.Dispose()
    $stream = [IO.MemoryStream]::new()
    $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
    'data:image/png;base64,' + [Convert]::ToBase64String($stream.ToArray())
}

Step 'Pantalla y escritorio'
$mode = New-Object Win+DEVMODE
$mode.dmSize = [Runtime.InteropServices.Marshal]::SizeOf([type] [Win+DEVMODE])
$null = [Win]::EnumDisplaySettings($null, -1, [ref] $mode)
"antes: $($mode.dmPelsWidth)x$($mode.dmPelsHeight)"
if ($mode.dmPelsWidth -lt 1920 -or $mode.dmPelsHeight -lt 1080) {
    $mode.dmPelsWidth = 1920
    $mode.dmPelsHeight = 1080
    $mode.dmFields = 0x80000 -bor 0x100000  # DM_PELSWIDTH | DM_PELSHEIGHT
    "ChangeDisplaySettings: $([Win]::ChangeDisplaySettings([ref] $mode, 0))"
}
$screenWidth = [Win]::GetSystemMetrics(0)
$screenHeight = [Win]::GetSystemMetrics(1)
"pantalla: ${screenWidth}x$screenHeight"
# Escritorio liso en el azul noche del Relicario, para que los avisos se vean sobre algo de la piel.
$null = [Win]::SystemParametersInfo(0x14, 0, '', 3)
$null = [Win]::SetSysColors(1, [int[]] @(1), [int[]] @(0x000A0402))


function Get-PageSocket {
    try {
        $json = (Invoke-WebRequest "http://127.0.0.1:$port/json/list" -TimeoutSec 2 -UseBasicParsing).Content
    } catch {
        return $null
    }
    foreach ($target in @($json | ConvertFrom-Json)) {
        if ($target.type -eq 'page' -and "$($target.url)".StartsWith('https://web.whatsapp.com/')) { return $target.webSocketDebuggerUrl }
    }
    $null
}

# El puerto de depuración y la página en español, por la variable que el Debug suma a sus argumentos.
$env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = "--remote-debugging-port=$port --lang=es"

# El proceso principal de WebView2 (el que no lleva --type=): sus argumentos dicen si ha llegado el puerto.
function Show-BrowserProcess {
    Get-CimInstance Win32_Process -Filter "Name = 'msedgewebview2.exe'" | Where-Object { $_.CommandLine -notmatch '--type=' } | ForEach-Object {
        "proceso principal de WebView2: $($_.CommandLine -replace '^"[^"]*"\s*', '')"
    }
}

# Abre Cryptwire sin vincular, con la ventana en su sitio, y espera al QR con la piel puesta.
function Open-Cryptwire {
    $script:process = Start-Process -FilePath $Exe -PassThru
    $script:main = Wait-Until { Get-ProcessWindows $script:process.Id | Where-Object { $_.Width -ge 700 } | Select-Object -First 1 } 60 'no aparece la ventana de Cryptwire'
    "ventana: '$($script:main.Title)'"
    $null = [Win]::SetWindowPos($script:main.Hwnd, [IntPtr]::Zero, 120, 60, 1280, 820, 0x0040)  # SWP_SHOWWINDOW
    try {
        $script:pageSocket = Wait-Until { Get-PageSocket } 90 'no responde el puerto de depuración'
    } catch {
        Show-BrowserProcess
        Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | Where-Object { $_.LocalAddress -in '127.0.0.1', '::1' } |
            ForEach-Object { "escuchando: $($_.LocalAddress):$($_.LocalPort) ($((Get-Process -Id $_.OwningProcess -ErrorAction SilentlyContinue).Name))" }
        throw
    }
    Show-BrowserProcess
    'página de WhatsApp: conectada'
    # El QR de vincular es un <canvas> con tamaño; hasta entonces la web enseña su pantalla de carga.
    Wait-Until { (Invoke-Page "[...document.querySelectorAll('canvas')].some(c => c.width > 100) && document.readyState === 'complete'") -eq $true } 120 'no sale el QR de WhatsApp Web' | Out-Null
    Start-Sleep -Seconds 3
    "idioma de la página: $(Invoke-Page 'document.documentElement.lang'); estilos de Cryptwire: $(Invoke-Page '[...document.querySelectorAll(''style[id^=cryptwire]'')].map(s => s.id).join('', '')')"
    Hide-Qr
}

function Hide-Qr {
    "QR difuminados: $(Invoke-Page "(() => { const c = [...document.querySelectorAll('canvas')]; c.forEach(e => { e.style.filter = 'blur(9px)'; }); return c.length; })()")"
    Start-Sleep -Seconds 1
}

function Close-Cryptwire {
    Start-Process -FilePath $Exe -ArgumentList '--salir' -Wait
    Wait-Until { $script:process.HasExited } 30 'Cryptwire no se ha cerrado' | Out-Null
    'cerrada'
}

Step 'Ventana y ajustes'
Open-Cryptwire
Save-Window $main.Hwnd '01-ventana.png'

# Ajustes, con el botón de la barra de título (UI Automation).
$root = [Windows.Automation.AutomationElement]::FromHandle($main.Hwnd)
$button = $root.FindFirst([Windows.Automation.TreeScope]::Descendants,
    [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty, 'Ajustes de Cryptwire'))
if (-not $button) { throw 'no encuentro el botón de Ajustes' }
$button.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
$settings = Wait-Until { Get-ProcessWindows $process.Id | Where-Object { $_.Hwnd -ne $main.Hwnd -and $_.Width -ge 800 } | Select-Object -First 1 } 20 'no se abren los ajustes'
Start-Sleep -Seconds 2
Save-Window $settings.Hwnd '02-ajustes.png'
[Windows.Automation.AutomationElement]::FromHandle($settings.Hwnd).GetCurrentPattern([Windows.Automation.WindowPattern]::Pattern).Close()
Start-Sleep -Seconds 1
Close-Cryptwire
if ($SinAviso) {
    'OK (sin aviso)'
    exit 0
}

# Windows no deja salir avisos en la primera hora tras el primer inicio de sesión (horas tranquilas, estado 6)
# y Cryptwire lo respeta. Una máquina de GitHub acaba de iniciar sesión por primera vez: se espera a que acaben.
Step 'Esperar a que Windows acepte avisos'
$start = Get-Date
$lastReport = [datetime]::MinValue
while ($true) {
    $state = 0
    $null = [Win]::SHQueryUserNotificationState([ref] $state)
    if ($state -eq 5) { break }
    if ((Get-Date) - $start -gt [TimeSpan]::FromMinutes(80)) { throw "Windows no acepta avisos (estado $state) tras 80 minutos." }
    if ((Get-Date) - $lastReport -gt [TimeSpan]::FromMinutes(5)) {
        "estado $state (5 = los acepta), $([int]((Get-Date) - $start).TotalMinutes) min"
        $lastReport = Get-Date
    }
    Start-Sleep -Seconds 15
}
"Windows acepta avisos (esperados $([int]((Get-Date) - $start).TotalMinutes) min)"

Step 'Aviso'
Open-Cryptwire
# El aviso solo sale si no estás en Cryptwire: se minimiza (minimizada no cuenta como estar en ella).
$null = [Win]::ShowWindow($main.Hwnd, 6)  # SW_MINIMIZE
Start-Sleep -Seconds 1
$avatar = New-AvatarDataUri
$body = '¿Nos vemos a medianoche en la cripta?'
"aviso lanzado: $(Invoke-Page "new Notification('Lilith', { body: '$body', icon: '$avatar', tag: 'cryptwire-captura' }); Notification.permission")"
try {
    $toast = Wait-Until { Get-ProcessWindows $process.Id | Where-Object { $_.Hwnd -ne $main.Hwnd -and $_.Width -lt 700 -and $_.Height -lt 400 } | Select-Object -First 1 } 15 'no sale el aviso'
} catch {
    Get-ProcessWindows $process.Id | ForEach-Object { "ventana de Cryptwire: '$($_.Title)' $($_.Width)x$($_.Height)" }
    Save-Screen 0 0 $screenWidth $screenHeight 'pantalla-sin-aviso.png'
    throw
}
Start-Sleep -Milliseconds 1500
$t = Get-Rect $toast.Hwnd
$pad = 24
Save-Screen ($t.Left - $pad) ($t.Top - $pad) ($t.Right - $t.Left + 2 * $pad) ($t.Bottom - $t.Top + 2 * $pad) '03-aviso.png'
Close-Cryptwire
'OK'