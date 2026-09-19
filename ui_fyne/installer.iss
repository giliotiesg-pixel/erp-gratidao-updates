[Setup]
AppId={{B6D0B6F2-7A5D-4F77-9B68-ERPGRATIDAO}}
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
SetupLogging=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Files]
Source: "ERP_Gratidao_Interface_Teste.exe"; DestDir: "{app}"; DestName: "ERP_Gratidao.exe"; Flags: ignoreversion
Source: "data\*"; DestDir: "{app}\data"; Flags: ignoreversion recursesubdirs createallsubdirs onlyifdoesntexist; Check: DirExists(ExpandConstant('{src}\data'))

[Dirs]
Name: "{app}\data"; Permissions: users-modify

[Icons]
Name: "{group}\ERP Gratidão"; Filename: "{app}\ERP_Gratidao.exe"
Name: "{autodesktop}\ERP Gratidão"; Filename: "{app}\ERP_Gratidao.exe"

[Run]
Filename: "{app}\ERP_Gratidao.exe"; Description: "Abrir ERP Gratidão"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: files; Name: "{app}\ERP_Gratidao.exe"

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    MsgBox('A pasta de dados não é removida automaticamente para proteger o banco erp.sqlite e seus registros.', mbInformation, MB_OK);
end;
