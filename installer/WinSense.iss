; WinSense installer (Inno Setup 6). Built by scripts\publish.ps1, which passes /DAppVersion.
; Installs into Program Files and registers the uninstaller in "Programas e Recursos" / Configurações > Aplicativos.

#ifndef AppVersion
  #error "Passe a versão: ISCC /DAppVersion=1.0.0 WinSense.iss"
#endif

#define AppName "WinSense"
#define AppExe "WinSense.exe"
#define AppPublisher "Rodrigo Bechara"

[Setup]
; NEVER change AppId: it is how new versions find and upgrade an existing install.
AppId={{CFA18DC6-27BE-4BE9-994E-B393E7645485}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppCopyright=© 2026 {#AppPublisher}
VersionInfoVersion={#AppVersion}
VersionInfoProductName={#AppName}
VersionInfoDescription=Instalador do {#AppName}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
; Entry shown in Programas e Recursos.
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExe}
SetupIconFile=src\WinSense.App\Assets\WinSense.ico
; Closes a running WinSense before upgrading or uninstalling.
CloseApplications=yes
SourceDir=..
OutputDir=dist
OutputBaseFilename={#AppName}-{#AppVersion}-setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ShowLanguageDialog=no

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "startup"; Description: "Iniciar o WinSense com o Windows (fica nos ícones ocultos, perto do relógio)"; GroupDescription: "Inicialização:"
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "publish\win-x64\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "CHANGELOG.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[UninstallRun]
; WinSense creates per-user tasks under \WinSense\ ("open without UAC prompt" and "start with Windows").
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -Command ""Get-ScheduledTask -TaskPath '\WinSense\' -ErrorAction SilentlyContinue | Unregister-ScheduledTask -Confirm:$false"""; Flags: runhidden; RunOnceId: "RemoveScheduledTasks"

[Run]
; Elevated (like Setup): creates or removes the per-user logon task, then exits without opening a window.
Filename: "{app}\{#AppExe}"; Parameters: "--register-startup"; Flags: runhidden waituntilterminated; Tasks: startup
Filename: "{app}\{#AppExe}"; Parameters: "--unregister-startup"; Flags: runhidden waituntilterminated; Tasks: not startup
; runascurrentuser: postinstall entries default to the original, non-elevated user, and CreateProcess
; on a requireAdministrator exe fails with error 740. The installer is already elevated, so reuse that.
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent runascurrentuser
