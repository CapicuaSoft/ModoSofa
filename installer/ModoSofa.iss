; Instalador de ModoSofá (Inno Setup 6). Lo compila release.ps1:
;   ISCC.exe /DVersion=0.1.0 installer\ModoSofa.iss   →   dist\ModoSofa-Setup-0.1.0.exe
; Necesita dist\ModoSofa (tools\publicar.ps1).
;
; Se instala para el usuario actual, sin permisos de administrador (como el propio ModoSofá).
; Solo la regla de firewall pide elevación, y únicamente si se marca esa tarea.

#ifndef Version
  #define Version "0.0.0"
#endif
#define Nombre "ModoSofá"
#define Exe "ModoSofa.exe"
#define Repo "https://github.com/jbautistamartin/ModoSofa"

[Setup]
AppId={{6E3C2A51-8F0B-4D2E-9C67-1B5A0D7E4F93}
AppName={#Nombre}
AppVersion={#Version}
AppVerName={#Nombre} {#Version}
AppPublisher=José Luis Bautista Martín
AppPublisherURL={#Repo}
AppSupportURL={#Repo}/issues
AppUpdatesURL={#Repo}/releases
VersionInfoVersion={#Version}
DefaultDirName={autopf}\ModoSofa
DisableProgramGroupPage=yes
DisableDirPage=auto
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir=..\dist
OutputBaseFilename=ModoSofa-Setup-{#Version}
SetupIconFile=..\src\ModoSofa.Agente\ModoSofa.ico
UninstallDisplayIcon={app}\{#Exe}
UninstallDisplayName={#Nombre}
LicenseFile=..\LICENSE
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; ModoSofá se cierra en PrepareToInstall; el Restart Manager no sabe cerrar una app de bandeja.
CloseApplications=no

[Languages]
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "inicio"; Description: "Iniciar ModoSofá con Windows"
Name: "firewall"; Description: "Permitir que el móvil se conecte por la Wi-Fi de casa (solo redes privadas; pedirá permiso de administrador una vez)"
Name: "retroarch"; Description: "Activar los comandos de red de RetroArch para cerrarlo y pausarlo sin perder partidas (guarda una copia de retroarch.cfg)"
Name: "autologon"; Description: "Inicio de sesión automático de Windows, para usar ModoSofá nada más encender el PC desde el móvil. Menos seguro: quien encienda el PC entra en tu sesión"; Flags: unchecked

[Files]
Source: "..\dist\ModoSofa\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "comun.ps1"; DestDir: "{app}\instalador"; Flags: ignoreversion
Source: "activar-retroarch.ps1"; DestDir: "{app}\instalador"; Flags: ignoreversion
Source: "firewall.ps1"; DestDir: "{app}\instalador"; Flags: ignoreversion
Source: "quitar-firewall.ps1"; DestDir: "{app}\instalador"; Flags: ignoreversion
Source: "configurar-autologon.ps1"; DestDir: "{app}\instalador"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#Nombre}"; Filename: "{app}\{#Exe}"; Comment: "Mando a distancia del PC para jugar desde el sofá"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "ModoSofa"; ValueData: """{app}\{#Exe}"""; Flags: uninsdeletevalue; Tasks: inicio

[Run]
; RetroArch antes que el firewall: así la regla de bloqueo ya encuentra su ejecutable.
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\instalador\activar-retroarch.ps1"""; Flags: runhidden waituntilterminated; Tasks: retroarch; StatusMsg: "Activando los comandos de red de RetroArch…"
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File ""{app}\instalador\firewall.ps1"" -Programa ""{app}\{#Exe}"""; Verb: runas; Flags: shellexec runhidden waituntilterminated; Tasks: firewall; StatusMsg: "Configurando el firewall…"
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File ""{app}\instalador\configurar-autologon.ps1"""; Flags: runhidden waituntilterminated; Tasks: autologon; StatusMsg: "Configurando el inicio de sesión automático…"
Filename: "{app}\{#Exe}"; Description: "Abrir ModoSofá"; Flags: nowait postinstall skipifsilent
; En una actualización silenciosa también se vuelve a abrir.
Filename: "{app}\{#Exe}"; Flags: nowait; Check: WizardSilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
procedure CerrarModoSofa;
var
  Codigo: Integer;
begin
  { Se cierra sin deshacer el modo sofá: el estado queda en disco y la versión nueva lo recupera. }
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#Exe}', '', SW_HIDE, ewWaitUntilTerminated, Codigo);
  Sleep(800);
end;

function PrepareToInstall(var NecesitaReiniciar: Boolean): String;
begin
  CerrarModoSofa;
  Result := '';
end;

function InitializeUninstall: Boolean;
begin
  CerrarModoSofa;
  Result := True;
end;

function HayReglaDeFirewall: Boolean;
var
  Codigo: Integer;
begin
  { «show rule» no necesita elevación y devuelve 0 si la regla existe. }
  Result := Exec(ExpandConstant('{sys}\netsh.exe'), 'advfirewall firewall show rule name="ModoSofá"', '',
    SW_HIDE, ewWaitUntilTerminated, Codigo) and (Codigo = 0);
end;

procedure CurUninstallStepChanged(Paso: TUninstallStep);
var
  Codigo: Integer;
begin
  if Paso = usUninstall then
  begin
    if HayReglaDeFirewall then
      ShellExec('runas', 'powershell.exe',
        '-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "' + ExpandConstant('{app}\instalador\quitar-firewall.ps1') + '"',
        '', SW_HIDE, ewWaitUntilTerminated, Codigo);
  end;

  if (Paso = usPostUninstall) and not UninstallSilent then
  begin
    if MsgBox('¿Borrar también la configuración, los tokens del móvil y de Alexa, y los logs?' + #13#10#13#10 +
              'Si vas a volver a instalar ModoSofá, responde No y conservarás la vinculación.',
              mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
    begin
      DelTree(ExpandConstant('{userappdata}\ModoSofa'), True, True, True);
      DelTree(ExpandConstant('{localappdata}\ModoSofa'), True, True, True);
    end;
  end;
end;
