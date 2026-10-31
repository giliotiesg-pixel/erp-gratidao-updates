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
; Auxiliares extraidos apenas antes da instalacao: nao alteram o banco de dados.
Source: "backup-antes-instalacao.ps1"; DestDir: "{tmp}"; Flags: dontcopy
Source: "bin\x64\Release\System.Data.SQLite.dll"; DestDir: "{tmp}"; Flags: dontcopy
Source: "bin\x64\Release\x64\SQLite.Interop.dll"; DestDir: "{tmp}"; Flags: dontcopy
[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\ArmazemGratidaoNovo.exe"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\ArmazemGratidaoNovo.exe"
[Run]
Filename: "{app}\ArmazemGratidaoNovo.exe"; Description: "Abrir Armazem Gratidao"; Flags: nowait postinstall skipifsilent
[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  DataDir, DbFile, PsExe, Args: string;
  Code: Integer;
begin
  Result := '';
  { O banco permanece na pasta de dados, nunca no diretorio do aplicativo. }
  DataDir := ExpandConstant('{localappdata}\ArmazemGratidaoNovo');
  DbFile := DataDir + '\armazem-gratidao.db';
  if not FileExists(DbFile) then
    Exit;

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
    ' -TargetDirectory ' + AddQuotes(DataDir);
  if (not Exec(PsExe, Args, '', SW_HIDE, ewWaitUntilTerminated, Code)) or (Code <> 0) then
  begin
    Result := 'Atualizacao cancelada: falhou o backup automatico do banco de dados.' + #13#10 +
      'Verifique o espaco em disco e as permissoes da pasta: ' + DataDir + #13#10 +
      'O instalador nao continuara sem uma copia de seguranca valida.';
    Exit;
  end;
end;
