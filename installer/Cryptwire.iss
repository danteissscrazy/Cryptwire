; Instalador de Cryptwire para Windows 10/11 x64, con Inno Setup 6.7 o posterior.
;
; No se compila a mano: tools\empaquetar.ps1 publica el commit (publicar.ps1 deja artifacts\publicar ya
; escaneado), trae el instalador de WebView2 de Microsoft con su firma comprobada y pasa aquí las rutas.
;
; - Por usuario, sin administrador, en %LOCALAPPDATA%\Programs\Cryptwire. La carpeta no se puede
;   cambiar: en una donde otros usuarios del PC puedan escribir (C:\Cryptwire), podrían cambiar las DLL
;   que arranca la entrada de inicio con Windows.
; - Lleva .NET dentro. Si al PC le falta WebView2, instala el de Microsoft (por usuario, sin administrador).
; - Tus datos (sesión de WhatsApp, ajustes, tonos y fondos) viven aparte, en %LOCALAPPDATA%\Cryptwire:
;   actualizar no los toca y desinstalar pregunta antes de borrarlos.

#if Ver < EncodeVer(6, 7, 0)
  #error Hace falta Inno Setup 6.7 o posterior.
#endif
#ifndef Publicado
  #error Compila con tools\empaquetar.ps1.
#endif

#define AppExe "Cryptwire.exe"

[Setup]
; El AppId identifica la instalación para actualizarla y desinstalarla: no se cambia nunca.
AppId={{B6609599-7FC5-4080-A0E2-9E064A37C041}
AppName=Cryptwire
AppVersion={#Version}
AppVerName=Cryptwire {#Version}
AppPublisher=Cryptwire
AppComments=Cliente de escritorio para WhatsApp Web con pieles. Proyecto independiente, sin relación con WhatsApp ni con Meta.
VersionInfoVersion={#Version}
VersionInfoProductVersion={#Version}
VersionInfoProductTextVersion={#Version}+{#Commit}
VersionInfoDescription=Instalador de Cryptwire
PrivilegesRequired=lowest
DefaultDirName={autopf}\Cryptwire
DisableDirPage=yes
DisableWelcomePage=no
DefaultGroupName=Cryptwire
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.17763
SetupIconFile=..\src\Cryptwire.App\Assets\logo.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName=Cryptwire
; Oscuro y con los colores del Relicario. Las imágenes salen de tools\generar-imagenes-instalador.ps1.
WizardStyle=modern dark
WizardSizePercent=110
WizardBackColor=#070C19
WizardImageBackColor=#070C19
WizardSmallImageBackColor=#070C19
WizardImageFile=imagenes\lateral-164.png,imagenes\lateral-328.png
WizardSmallImageFile=imagenes\cabecera-55.png,imagenes\cabecera-110.png
ShowLanguageDialog=no
OutputDir={#Salida}
OutputBaseFilename=Cryptwire-{#Version}-instalador
#ifdef SinComprimir
; Copia de comprobación: ni los ficheros ni la cabecera (textos, código y lista de ficheros) van
; comprimidos, así que el escáner lee el instalador entero. La de verdad sale de lo mismo, comprimida.
Compression=none
InternalCompressLevel=none
#else
Compression=lzma2/max
SolidCompression=yes
#endif

[Languages]
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"

[Messages]
; De tú, como la app: los textos de las páginas que se ven al instalar y desinstalar.
WelcomeLabel1=[name/ver]
WelcomeLabel2=Esto instalará Cryptwire para tu usuario de Windows, sin pedir permisos de administrador.%n%nCryptwire es un cliente de escritorio para WhatsApp Web. Proyecto independiente, sin relación con WhatsApp ni con Meta.
ClickNext=Pulsa Siguiente para continuar o Cancelar para salir.
WizardSelectTasks=Tareas adicionales
SelectTasksDesc=¿Algo más?
SelectTasksLabel2=Elige qué más quieres y pulsa Siguiente.
WizardReady=Todo listo
ReadyLabel1=Cryptwire está lista para instalarse.
ReadyLabel2a=Pulsa Instalar para seguir, o Atrás para cambiar algo.
ReadyLabel2b=Pulsa Instalar para seguir.
ReadyMemoDir=Carpeta:
ReadyMemoTasks=Además:
WizardPreparing=Preparando la instalación
PreparingDesc=Preparando la instalación de Cryptwire.
InstallingLabel=Espera mientras se instala Cryptwire.
FinishedHeadingLabel=Cryptwire está instalada
FinishedLabel=Al abrirla, vincúlala con tu móvil: en WhatsApp, Dispositivos vinculados › Vincular un dispositivo, y escanea el código QR. Cada PC se vincula por separado.
FinishedLabelNoIcons=Al abrirla, vincúlala con tu móvil: en WhatsApp, Dispositivos vinculados › Vincular un dispositivo, y escanea el código QR. Cada PC se vincula por separado.
ClickFinish=Pulsa Finalizar para cerrar el asistente.
ExitSetupMessage=La instalación no ha terminado: si sales ahora, Cryptwire no se instala.%n%n¿Salir?
ConfirmUninstall=¿Desinstalar %1? Tus datos (la sesión de WhatsApp y tus ajustes) se quedan, salvo que digas lo contrario al terminar.
UninstallStatusLabel=Espera mientras se desinstala %1.
UninstalledAll=%1 se ha desinstalado.

[Tasks]
Name: "escritorio"; Description: "Crear un acceso directo en el escritorio"; Flags: unchecked

[InstallDelete]
; Al actualizar, fuera lo de la versión anterior: una piel quitada no debe seguir saliendo.
Type: filesandordirs; Name: "{app}\themes"
Type: filesandordirs; Name: "{app}\runtimes"
Type: filesandordirs; Name: "{app}\es"
Type: files; Name: "{app}\*.dll"
Type: files; Name: "{app}\*.json"

[Files]
Source: "{#Publicado}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#WebView2}"; DestName: "MicrosoftEdgeWebview2Setup.exe"; Flags: dontcopy

[Icons]
Name: "{autoprograms}\Cryptwire"; Filename: "{app}\{#AppExe}"; Comment: "WhatsApp Web con pieles"
Name: "{autodesktop}\Cryptwire"; Filename: "{app}\{#AppExe}"; Comment: "WhatsApp Web con pieles"; Tasks: escritorio

[Run]
Filename: "{app}\{#AppExe}"; Description: "Abrir Cryptwire"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: dirifempty; Name: "{app}"

[Code]
const
  WebView2Client = 'Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';
  ApprovedKey = 'Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run';
  StartupValue = 'Cryptwire';
  EVENT_MODIFY_STATE = $0002;

function OpenEvent(DesiredAccess: Cardinal; InheritHandle: Integer; Name: String): THandle;
  external 'OpenEventW@kernel32.dll stdcall';
function SetEvent(Event: THandle): Integer;
  external 'SetEvent@kernel32.dll stdcall';
function CloseHandle(Handle: THandle): Integer;
  external 'CloseHandle@kernel32.dll stdcall';

{ WebView2 está si su versión está apuntada, para todo el PC o para tu usuario (como lo mira Microsoft). }
function HasWebView2Version(RootKey: Integer; SubKey: String): Boolean;
var
  Version: String;
begin
  Result := RegQueryStringValue(RootKey, SubKey, 'pv', Version) and (Version <> '') and (Version <> '0.0.0.0');
end;

function WebView2Installed: Boolean;
begin
  Result := HasWebView2Version(HKLM32, 'SOFTWARE\' + WebView2Client) or HasWebView2Version(HKCU, 'Software\' + WebView2Client);
end;

procedure InstallWebView2;
var
  ResultCode: Integer;
begin
  if WebView2Installed then
    Exit;
  WizardForm.StatusLabel.Caption := 'Instalando Microsoft Edge WebView2…';
  ExtractTemporaryFile('MicrosoftEdgeWebview2Setup.exe');
  if not Exec(ExpandConstant('{tmp}\MicrosoftEdgeWebview2Setup.exe'), '/silent /install', '', SW_HIDE,
      ewWaitUntilTerminated, ResultCode) or (ResultCode <> 0) or not WebView2Installed then
    SuppressibleMsgBox('No se ha podido instalar Microsoft Edge WebView2 (código ' + IntToStr(ResultCode) + '). ' +
      'Cryptwire lo necesita: instálalo desde la web de Microsoft y vuelve a abrirla.', mbError, MB_OK, IDOK);
end;

{ El SID de tu usuario, como lo usa Cryptwire en el nombre de su candado. Vacío si no se puede saber. }
function CurrentUserSid: String;
var
  Output: TExecOutput;
  ResultCode, P: Integer;
  Line: String;
begin
  Result := '';
  if ExecAndCaptureOutput(ExpandConstant('{sys}\whoami.exe'), '/user /fo csv /nh', '', SW_HIDE,
      ewWaitUntilTerminated, ResultCode, Output) and (ResultCode = 0) and (GetArrayLength(Output.StdOut) > 0) then
  begin
    { "equipo\usuario","S-1-5-21-…" }
    Line := Trim(Output.StdOut[0]);
    P := Pos('","S-', Line);
    if (P > 0) and (Copy(Line, Length(Line), 1) = '"') then
      Result := Copy(Line, P + 3, Length(Line) - P - 3);
  end;
end;

{ Cierra la Cryptwire abierta (también una compilada a mano, en otra carpeta) igual que --salir: con el
  aviso de salir de su instancia única (SingleInstance.cs, «Local\Cryptwire-<SID>»; si cambia allí, cambia
  aquí). True si no queda ninguna abierta. }
function CloseCryptwire: Boolean;
var
  Name: String;
  Event: THandle;
  Waited: Integer;
begin
  Result := True;
  Name := CurrentUserSid;
  if Name = '' then
    Exit;
  Name := 'Local\Cryptwire-' + Name;
  if not CheckForMutexes(Name) then
    Exit;
  Event := OpenEvent(EVENT_MODIFY_STATE, 0, Name + '-salir');
  if Event <> 0 then
  begin
    SetEvent(Event);
    CloseHandle(Event);
  end;
  Waited := 0;
  while CheckForMutexes(Name) and (Waited < 15000) do
  begin
    Sleep(250);
    Waited := Waited + 250;
  end;
  Result := not CheckForMutexes(Name);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not CloseCryptwire then
    Result := 'Cryptwire sigue abierta y no se ha podido cerrar. Ciérrala (clic derecho en su icono de la bandeja › Salir) y vuelve a abrir el instalador.';
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    InstallWebView2;
end;

function InitializeUninstall: Boolean;
begin
  Result := CloseCryptwire;
  if not Result then
    SuppressibleMsgBox('Cryptwire sigue abierta y no se ha podido cerrar. Ciérrala (clic derecho en su icono de la bandeja › Salir) y vuelve a desinstalar.',
      mbError, MB_OK, IDOK);
end;

{ La entrada de inicio con Windows, solo si apunta a esta instalación. }
procedure RemoveStartupEntry;
var
  Command: String;
begin
  if RegQueryStringValue(HKCU, RunKey, StartupValue, Command) and
      (Pos(Lowercase(ExpandConstant('{app}\{#AppExe}')), Lowercase(Command)) > 0) then
  begin
    RegDeleteValue(HKCU, RunKey, StartupValue);
    RegDeleteValue(HKCU, ApprovedKey, StartupValue);
  end;
end;

{ Tus datos solo se borran si lo dices. WebView2 puede tardar un poco en soltar los suyos. }
procedure DeleteUserData;
var
  Data: String;
  Tries: Integer;
begin
  Data := ExpandConstant('{localappdata}\Cryptwire');
  if UninstallSilent or not DirExists(Data) then
    Exit;
  if MsgBox('¿Borrar también tus datos de Cryptwire?' + #13#10#13#10 +
      'Se borran la sesión de WhatsApp (tendrías que volver a escanear el QR), tus ajustes, tonos, fondos y pieles propias. ' +
      'Si vas a reinstalar, pulsa No.', mbConfirmation, MB_YESNO or MB_DEFBUTTON2) <> IDYES then
    Exit;
  for Tries := 1 to 10 do
  begin
    DelTree(Data, True, True, True);
    if not DirExists(Data) then
      Exit;
    Sleep(1000);
  end;
  MsgBox('No se ha podido borrar todo: queda ' + Data + '.', mbError, MB_OK);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    RemoveStartupEntry;
    DeleteUserData;
  end;
end;
