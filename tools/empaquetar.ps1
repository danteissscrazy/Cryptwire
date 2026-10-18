param(
    # El instalador de WebView2 de Microsoft (MicrosoftEdgeWebview2Setup.exe) si ya lo tienes. Si no, se
    # descarga de Microsoft. En los dos casos se comprueba su firma.
    [string] $WebView2,
    # ISCC.exe de Inno Setup 6.7 o posterior. Por defecto se busca en el PATH y donde lo deja su instalador.
    [string] $Iscc
)
# Hace el instalador de Cryptwire (installer\Cryptwire.iss) y no lo da por bueno hasta que:
#   1. publicar.ps1 deja en artifacts\publicar el commit compilado, probado y escaneado;
#   2. el instalador de WebView2 que va dentro lo firma Microsoft;
#   3. una copia del instalador sin comprimir (ficheros y cabecera: textos, código y lista de ficheros) pasa
#      el escáner. El instalador comprime y el escáner no ve dentro de lo comprimido: por eso se escanea
#      esa copia, hecha de lo mismo, y después se comprime.
# Deja el instalador en artifacts\instalador.
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot
$artifacts = Join-Path $repo 'artifacts'
$published = Join-Path $artifacts 'publicar'
$pwsh = (Get-Process -Id $PID).Path
$webView2Url = 'https://go.microsoft.com/fwlink/p/?LinkId=2124703'

function Step([string] $Title) { "`n== $Title" }

function Stop-Package([string] $Message) {
    [Console]::Error.WriteLine("empaquetar: $Message No se empaqueta.")
    exit 1
}

function Find-Iscc {
    if ($Iscc) { return $Iscc }
    $found = Get-Command ISCC.exe -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($found) { return $found.Source }
    foreach ($dir in @("$env:LOCALAPPDATA\Programs", ${env:ProgramFiles(x86)}, $env:ProgramFiles)) {
        if ($dir -and (Test-Path -LiteralPath (Join-Path $dir 'Inno Setup 6\ISCC.exe'))) { return Join-Path $dir 'Inno Setup 6\ISCC.exe' }
    }
    Stop-Package 'no encuentro Inno Setup 6 (ISCC.exe).'
}

$compiler = Find-Iscc
$script = Join-Path $repo 'installer\Cryptwire.iss'

& $pwsh -NoProfile -File (Join-Path $PSScriptRoot 'publicar.ps1')
if ($LASTEXITCODE -ne 0) { Stop-Package 'publicar.ps1 no ha terminado bien.' }

Step 'Instalador de WebView2, firmado por Microsoft'
$bootstrapper = Join-Path $artifacts 'webview2\MicrosoftEdgeWebview2Setup.exe'
New-Item -ItemType Directory -Path (Split-Path $bootstrapper) -Force | Out-Null
if ($WebView2) {
    Copy-Item -LiteralPath $WebView2 -Destination $bootstrapper
} else {
    Invoke-WebRequest -Uri $webView2Url -OutFile $bootstrapper -UseBasicParsing
}
$signature = Get-AuthenticodeSignature -LiteralPath $bootstrapper
$size = (Get-Item -LiteralPath $bootstrapper).Length
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch '^CN=Microsoft Corporation,' -or
    $signature.SignerCertificate.Issuer -notmatch '^CN=Microsoft Code Signing PCA') {
    Stop-Package "el instalador de WebView2 no lleva una firma válida de Microsoft ($($signature.Status))."
}
# No vale cualquier programa firmado por Microsoft: tiene que ser el instalador pequeño de Edge Update (el de
# WebView2 lo es, con su etiqueta), y su cadena tiene que acabar en la raíz de Microsoft.
$chain = [Security.Cryptography.X509Certificates.X509Chain]::new()
$null = $chain.Build($signature.SignerCertificate)
$root = $chain.ChainElements[$chain.ChainElements.Count - 1].Certificate.Subject
$info = (Get-Item -LiteralPath $bootstrapper).VersionInfo
if ($root -notmatch '^CN=Microsoft Root Certificate Authority' -or $info.OriginalFilename -ne 'MicrosoftEdgeUpdateSetup.exe' -or
    $info.ProductName -ne 'Microsoft Edge Update') {
    Stop-Package "el instalador de WebView2 no es el de Microsoft Edge Update ($($info.OriginalFilename), $($info.ProductName))."
}
if ($size -gt 10MB) { Stop-Package "el instalador de WebView2 mide $size bytes: no es el pequeño de Microsoft." }
"firma válida de Microsoft Corporation, $([Math]::Round($size / 1KB)) KB"

Step 'Versión'
# La de la app: «0.5.0+<commit>». El commit tiene que ser el que se ha publicado.
$product = (Get-Item -LiteralPath (Join-Path $published 'Cryptwire.exe')).VersionInfo.ProductVersion
$version, $commit = $product -split '\+', 2
$head = git -C $repo rev-parse HEAD
if ($version -notmatch '^\d+\.\d+\.\d+$' -or $commit -ne $head) {
    Stop-Package "la versión de la app ($product) no es la del commit publicado ($head)."
}
$commit = $commit.Substring(0, 7)
"Cryptwire $version ($commit)"

$defines = @("/DPublicado=$published", "/DWebView2=$bootstrapper", "/DVersion=$version", "/DCommit=$commit")

Step 'Copia sin comprimir: nada personal ni secreto en el instalador entero'
$plain = Join-Path $artifacts 'instalador-sin-comprimir'
& $compiler /Q @defines "/DSalida=$plain" /DSinComprimir $script
if ($LASTEXITCODE -ne 0) { Stop-Package "Inno Setup no ha compilado la copia sin comprimir (código $LASTEXITCODE)." }
& $pwsh -NoProfile -File (Join-Path $PSScriptRoot 'escanear-binario.ps1') -Ruta $plain
if ($LASTEXITCODE -ne 0) { Stop-Package 'el escáner encuentra algo en el instalador o no ha podido comprobarlo.' }
Remove-Item -LiteralPath $plain -Recurse -Force

Step 'Instalador'
$output = Join-Path $artifacts 'instalador'
& $compiler /Q @defines "/DSalida=$output" $script
if ($LASTEXITCODE -ne 0) { Stop-Package "Inno Setup no ha compilado el instalador (código $LASTEXITCODE)." }
Get-ChildItem -LiteralPath $output -Filter '*.exe' | ForEach-Object {
    '{0}  {1}  ({2:N1} MB)' -f (Get-FileHash -LiteralPath $_.FullName).Hash, $_.Name, ($_.Length / 1MB)
}
