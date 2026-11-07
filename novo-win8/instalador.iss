#define MyAppName "Armazem Gratidao Novo"
#ifndef BuildNumber
#define BuildNumber "586"
#endif
#define MyAppVersion "1.0.0." + BuildNumber
[Setup]
AppId={{8B8E23C2-9EB0-4E9C-9B7A-65B9A1E3AB70}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName=C:\ERP-Gratidao
DefaultGroupName={#MyAppName}
OutputDir=..\dist
OutputBaseFilename=Armazem-Gratidao-Novo-Setup
AppPublisher=Armazem Gratidao
AppPublisherURL=https://github.com/giliotiesg-pixel/erp-gratidao-updates
VersionInfoCompany=Armazem Gratidao
VersionInfoDescription=Instalador oficial do Armazem Gratidao Novo
VersionInfoProductName=Armazem Gratidao Novo
SetupIconFile=Armazem-Gratidao.ico
UninstallDisplayIcon={app}\ArmazemGratidaoNovo.exe
CloseApplications=yes
RestartApplications=no
Compression=zip/5
SolidCompression=no
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
[Dirs]
; Uma unica pasta de manutencao. Dados de vendas continuam no perfil atual.
Name: "{app}\Arquivos"
Name: "{app}\Arquivos\Atualizacoes"; Permissions: users-modify
Name: "{app}\Arquivos\Backups"; Permissions: users-modify
Name: "{app}\Arquivos\Logs"; Permissions: users-modify
Name: "{app}\Arquivos\Rollback"; Permissions: users-modify
[Files]
Source: "bin\x64\Release\ArmazemGratidaoNovo.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\x64\Release\ArmazemGratidaoNovo.exe.config"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "bin\x64\Release\QRCoder.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\x64\Release\zxing.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\x64\Release\AtualizadorGratidao.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\x64\Release\System.Data.SQLite.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\x64\Release\x64\SQLite.Interop.dll"; DestDir: "{app}\x64"; Flags: ignoreversion
; Auxiliares extraidos apenas antes da instalacao: nao alteram o banco de dados.
Source: "backup-antes-instalacao.ps1"; DestDir: "{tmp}"; Flags: dontcopy
Source: "bin\x64\Release\System.Data.SQLite.dll"; DestDir: "{tmp}"; Flags: dontcopy
Source: "bin\x64\Release\x64\SQLite.Interop.dll"; DestDir: "{tmp}"; Flags: dontcopy
[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\ArmazemGratidaoNovo.exe"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\ArmazemGratidaoNovo.exe"
Name: "{group}\Reparar Armazem Gratidao"; Filename: "{app}\Reparar-Armazem-Gratidao.exe"; Comment: "Repara os arquivos do programa, com backup e sem apagar os dados"
Name: "{group}\Arquivos e manutencao"; Filename: "{app}\Arquivos"
[Run]
Filename: "{app}\ArmazemGratidaoNovo.exe"; Description: "Abrir Armazem Gratidao"; Flags: nowait postinstall skipifsilent
[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  DataDir, DbFile, PsExe, Args, BackupDir, BackupConfig: string;
  Code: Integer;
  BackupLines: TArrayOfString;
begin
  Result := '';
  { O banco permanece na pasta de dados, nunca no diretorio do aplicativo. }
  DataDir := ExpandConstant('{localappdata}\ArmazemGratidaoNovo');
  DbFile := DataDir + '\armazem-gratidao.db';
  if not FileExists(DbFile) then
  begin
    { Instalacao nova pode nao ter banco; reparo de uma instalacao existente nao. }
    if FileExists(ExpandConstant('{app}\ArmazemGratidaoNovo.exe')) then
      Result := 'Reparo bloqueado: o banco do usuario atual nao foi localizado.'+#13#10+
        'Confira o perfil do Windows e o banco antes de modificar o programa.';
    Exit;
  end;

  { Usar a mesma pasta de backup definida no menu do Armazem. }
  BackupDir := ExpandConstant('{app}\Arquivos\Backups');
  BackupConfig := DataDir + '\backup-location.txt';
  if FileExists(BackupConfig) then
  begin
    if not LoadStringsFromFile(BackupConfig, BackupLines) then
    begin
      Result := 'Atualizacao cancelada: nao foi possivel ler a pasta de backup configurada.';
      Exit;
    end;
    if GetArrayLength(BackupLines) > 0 then
      BackupDir := Trim(BackupLines[0])
    else
      BackupDir := '';
    if BackupDir = '' then
    begin
      Result := 'Atualizacao cancelada: pasta de backup configurada esta vazia.';
      Exit;
    end;
  end;

  try
    ExtractTemporaryFile('backup-antes-instalacao.ps1');
    ExtractTemporaryFile('System.Data.SQLite.dll');
    ExtractTemporaryFile('SQLite.Interop.dll');
  except
    Result := 'Atualizacao cancelada: nao foi possivel preparar o backup do banco. Nenhum arquivo do sistema foi substituido.';
    Exit;
  end;

  PsExe := ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe');
  Args := '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File ' +
    AddQuotes(ExpandConstant('{tmp}\backup-antes-instalacao.ps1')) +
    ' -Database ' + AddQuotes(DbFile) +
    ' -TargetDirectory ' + AddQuotes(BackupDir);
  if (not Exec(PsExe, Args, '', SW_HIDE, ewWaitUntilTerminated, Code)) or (Code <> 0) then
  begin
    Result := 'Atualizacao cancelada: falhou o backup automatico do banco de dados.' + #13#10 +
      'Verifique o espaco em disco e as permissoes da pasta: ' + BackupDir + #13#10 +
      'O instalador nao continuara sem uma copia de seguranca valida.';
    Exit;
  end;
end;

// O instalador completo fica guardado uma vez e oferece reparacao local,
// sem nova descarga, sem desinstalacao e sem mexer nos dados do usuario.
procedure CurStepChanged(CurStep: TSetupStep);
var
  SourceSetup, RepairSetup: string;
begin
  if CurStep = ssPostInstall then
  begin
    SourceSetup := ExpandConstant('{srcexe}');
    RepairSetup := ExpandConstant('{app}\Reparar-Armazem-Gratidao.exe');
    if CompareText(SourceSetup, RepairSetup) <> 0 then
    begin
      if not FileCopy(SourceSetup, RepairSetup, False) then
        MsgBox('O programa foi instalado, mas nao foi possivel guardar o reparador. Guarde uma copia deste instalador para reparar os arquivos futuramente.',
          mbError, MB_OK);
    end;
  end;
end;

procedure InitializeWizard();
begin
  WizardForm.WelcomeLabel2.Caption :=
   'Este instalador completo instala ou repara os arquivos do Armazem Gratidao.'+#13#10+
   'Nenhuma venda, fiado, estoque ou configuracao sera apagada.'+#13#10+
   'Antes de atualizar ou reparar, o backup automatico deve ser validado.'+#13#10+
   'Para reparar futuramente, use o atalho Reparar Armazem Gratidao.';
end;
