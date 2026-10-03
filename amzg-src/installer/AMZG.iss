#define MyAppName "AMZ.G"
#define MyAppVersion "0.8.2"
#define MyAppExeName "AMZG.exe"
[Setup]
AppId={{7A32C3E9-35DF-4A1B-A5E2-9A13DAF1A710}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={autopf}\AMZG
PrivilegesRequired=admin
OutputDir=Output
OutputBaseFilename=AMZG-Setup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion
[Dirs]
Name: "{commonappdata}\AMZG\Data"; Permissions: users-modify
Name: "C:\AMZG-Seguranca"
Name: "C:\AMZG-Seguranca\Backups"
Name: "C:\AMZG-Seguranca\Atualizacoes"
Name: "C:\AMZG-Seguranca\Versoes"
Name: "C:\AMZG-Seguranca\Logs"
Name: "C:\AMZG-Seguranca\Recuperacao"
[Icons]
Name: "{autoprograms}\AMZ.G"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\AMZ.G"; Filename: "{app}\{#MyAppExeName}"
[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Abrir AMZ.G"; Flags: nowait postinstall skipifsilent