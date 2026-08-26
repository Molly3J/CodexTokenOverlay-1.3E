#define MyAppName "Codex Token Overlay"
#ifndef MyAppArch
  #define MyAppArch "x64"
#endif
#ifndef MyAppDisplayVersion
  #define MyAppDisplayVersion "1.4.0"
#endif
#ifndef MyAppNumericVersion
  #define MyAppNumericVersion "1.4.0.0"
#endif
#define MyAppExeName "CodexTokenOverlay.exe"

[Setup]
AppId={{7B85DC9B-0E03-4C11-97AA-2159F83E5E75}
AppName={#MyAppName}
AppVersion={#MyAppDisplayVersion}
AppPublisher=CodexTokenOverlay Project
DefaultDirName={localappdata}\Programs\CodexTokenOverlay
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
#if MyAppArch == "x64"
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif
OutputDir=..\dist\release
OutputBaseFilename=CodexTokenOverlay-{#MyAppDisplayVersion}-windows-{#MyAppArch}-Setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern dynamic
CloseApplications=yes
RestartApplications=no
SetupIconFile=..\assets\Codex.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
VersionInfoVersion={#MyAppNumericVersion}
VersionInfoCompany=CodexTokenOverlay Project
VersionInfoDescription=Codex Token Overlay Setup
VersionInfoProductName=Codex Token Overlay
LicenseFile=..\LICENSE
InfoBeforeFile=..\PRIVACY.txt

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式 CODEX(tokenoverlay)"; GroupDescription: "快捷方式："; Flags: checkedonce

[Files]
Source: "..\dist\win-{#MyAppArch}\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\scripts\Start-CodexTokenOverlay.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\assets\Codex.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.zh-CN.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\PRIVACY.txt"; DestDir: "{app}"; Flags: ignoreversion

[InstallDelete]
Type: files; Name: "{autodesktop}\CODEX.lnk"
Type: files; Name: "{autodesktop}\Codex Token Overlay.lnk"
Type: files; Name: "{autodesktop}\CODEX(tokenoverlay).lnk"

[Icons]
Name: "{autodesktop}\CODEX(tokenoverlay)"; Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File ""{app}\Start-CodexTokenOverlay.ps1"""; WorkingDir: "{app}"; IconFilename: "{app}\Codex.ico"; IconIndex: 0; Comment: "启动 Codex 并启用 Token 状态栏"; Tasks: desktopicon
Name: "{group}\CODEX(tokenoverlay)"; Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File ""{app}\Start-CodexTokenOverlay.ps1"""; WorkingDir: "{app}"; IconFilename: "{app}\Codex.ico"; IconIndex: 0
Name: "{group}\仅启动 Token Overlay"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{group}\卸载 {#MyAppName}"; Filename: "{uninstallexe}"

[Run]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File ""{app}\Start-CodexTokenOverlay.ps1"""; Description: "立即启动 Codex + Token Overlay"; Flags: nowait postinstall skipifsilent
