[Setup]
AppId=ERP-Gratidao-Fyne
AppName=ERP Gratidão
AppVersion=0.1.0
AppPublisher=Armazém Gratidão
DefaultDirName={autopf}\ERP Gratidão
DefaultGroupName=ERP Gratidão
OutputDir=dist
OutputBaseFilename=ERP_Gratidao_Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
UninstallDisplayName=ERP Gratidão
CreateUninstallRegKey=yes
Uninstallable=yes
SetupLogging=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no

[Files]
Source: "ERP_Gratidao_Interface_Teste.exe"; DestDir: "{app}"; DestName: "ERP_Gratidao.exe"; Flags: ignoreversion

[Dirs]
Name: "{app}\data"; Permissions: users-modify
Name: "{app}\backup"; Permissions: users-modify
Name: "{app}\logs"; Permissions: users-modify

[Icons]
Name: "{group}\ERP Gratidão"; Filename: "{app}\ERP_Gratidao.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\ERP Gratidão"; Filename: "{app}\ERP_Gratidao.exe"; WorkingDir: "{app}"

[Run]
Filename: "{app}\ERP_Gratidao.exe"; Description: "Abrir ERP Gratidão"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: files; Name: "{app}\ERP_Gratidao.exe"
Type: files; Name: "{app}\*.log"
Type: dirifempty; Name: "{app}\logs"
Type: dirifempty; Name: "{app}\backup"

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    MsgBox('Os dados do ERP Gratidão foram preservados. A pasta data e o banco erp.sqlite não são apagados pelo desinstalador.', mbInformation, MB_OK);
end;
