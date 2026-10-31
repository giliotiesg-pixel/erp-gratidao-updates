#define MyAppName "Armazem Gratidao Novo"
#define MyAppVersion "0.1.0"
[Setup]
AppId={{8B8E23C2-9EB0-4E9C-9B7A-65B9A1E3AB70}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={autopf}\Armazem Gratidao Novo
DefaultGroupName={#MyAppName}
OutputDir=installer
OutputBaseFilename=Armazem-Gratidao-Novo-Windows8-Setup
Compression=lzma
SolidCompression=yes
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
[Files]
Source: "bin\x64\Release\ArmazemGratidaoNovo.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\x64\Release\ArmazemGratidaoNovo.exe.config"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "bin\x64\Release\QRCoder.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\x64\Release\System.Data.SQLite.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\x64\Release\x64\SQLite.Interop.dll"; DestDir: "{app}\x64"; Flags: ignoreversion
[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\ArmazemGratidaoNovo.exe"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\ArmazemGratidaoNovo.exe"
[Run]
Filename: "{app}\ArmazemGratidaoNovo.exe"; Description: "Abrir Armazem Gratidao"; Flags: nowait postinstall skipifsilent