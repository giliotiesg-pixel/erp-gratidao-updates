#define MyAppName "Armazem Gratidao Novo"
#define MyShortcutName "Armazém Gratidão"
#ifndef BuildNumber
#define BuildNumber "592"
#endif
#define MyAppVersion "1.0.0." + BuildNumber
[Setup]
AppId={{8B8E23C2-9EB0-4E9C-9B7A-65B9A1E3AB70}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName=C:\ERP-Gratidao
UsePreviousAppDir=no
DisableDirPage=yes
DefaultGroupName={#MyAppName}
OutputDir=..\dist
OutputBaseFilename=Instalador-Armazem-Gratidao
AppPublisher=Armazem Gratidao
AppPublisherURL=https://github.com/giliotiesg-pixel/erp-gratidao-updates
VersionInfoCompany=Armazem Gratidao
VersionInfoDescription=Instalador do Armazem Gratidao - Nao e o programa de caixa
VersionInfoProductName=Instalador - Armazem Gratidao
SetupIconFile=Instalador-Armazem-Gratidao.ico
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
[InstallDelete]
; Remove APENAS atalhos com o nome antigo gerados pelo proprio instalador.
; O novo atalho unico se chama "Armazém Gratidão" e abre o EXE do PDV.
Type: files; Name: "{group}\Armazem Gratidao Novo.lnk"
Type: files; Name: "{commondesktop}\Armazem Gratidao Novo.lnk"
Type: files; Name: "{userdesktop}\Armazem Gratidao Novo.lnk"
[Icons]
Name: "{group}\{#MyShortcutName}"; Filename: "{app}\ArmazemGratidaoNovo.exe"; WorkingDir: "{app}"; IconFilename: "{app}\ArmazemGratidaoNovo.exe"
Name: "{autodesktop}\{#MyShortcutName}"; Filename: "{app}\ArmazemGratidaoNovo.exe"; WorkingDir: "{app}"; IconFilename: "{app}\ArmazemGratidaoNovo.exe"
Name: "{group}\Reparar Armazem Gratidao"; Filename: "{app}\Reparar-Armazem-Gratidao.exe"; Comment: "Repara os arquivos do programa, com backup e sem apagar os dados"
Name: "{group}\Arquivos e manutencao"; Filename: "{app}\Arquivos"
[Run]
Filename: "{app}\ArmazemGratidaoNovo.exe"; Description: "Abrir Armazem Gratidao"; Flags: nowait postinstall skipifsilent
[Code]
// Esta etapa ocorre ANTES de qualquer substituição de arquivos.
// O PowerShell faz backup numa tarefa independente, e a janela permanece
// responsiva. Interromper a cópia BLOQUEIA a instalação inteira.
const
  AMZG_WAIT_OBJECT_0 = 0;
  AMZG_WAIT_TIMEOUT = $00000102;
  AMZG_SEE_MASK_NOCLOSEPROCESS = $00000040;
  AMZG_BACKUP_TIMEOUT_CYCLES = 6000;  // 20 minutos, 200ms por ciclo

type
  TAMZGShellExecuteInfo = record
    cbSize: DWORD;
    fMask: Cardinal;
    Wnd: HWND;
    lpVerb: string;
    lpFile: string;
    lpParameters: string;
    lpDirectory: string;
    nShow: Integer;
    hInstApp: THandle;
    lpIDList: DWORD;
    lpClass: string;
    hkeyClass: THandle;
    dwHotKey: DWORD;
    hMonitor: THandle;
    hProcess: THandle;
  end;

function AMZGShellExecuteEx(var Info: TAMZGShellExecuteInfo): BOOL;
  external 'ShellExecuteExW@shell32.dll stdcall';
function AMZGWaitForSingleObject(Handle: THandle; Milliseconds: Cardinal): Cardinal;
  external 'WaitForSingleObject@kernel32.dll stdcall';
function AMZGGetExitCodeProcess(Handle: THandle; var ExitCode: Cardinal): BOOL;
  external 'GetExitCodeProcess@kernel32.dll stdcall';
function AMZGTerminateProcess(Handle: THandle; ExitCode: Cardinal): BOOL;
  external 'TerminateProcess@kernel32.dll stdcall';
function AMZGCloseHandle(Handle: THandle): BOOL;
  external 'CloseHandle@kernel32.dll stdcall';

var
  AMZGBackupInProgress: Boolean;
  AMZGBackupCancelRequested: Boolean;

procedure AMZGCancelBackup(Sender: TObject);
begin
  AMZGBackupCancelRequested := True;
end;

procedure CancelButtonClick(CurPageID: Integer; var Cancel, Confirm: Boolean);
begin
  if AMZGBackupInProgress then
  begin
    // Cancelamento seguro: interromper o backup antes de tocar nos executáveis.
    AMZGBackupCancelRequested := True;
    Cancel := False;
    Confirm := False;
  end;
end;

function AMZGRunBackup(const ProgramPath, ProgramArgs, StatusFile: string;
  var ErrorDetails: string): Boolean;
var
  Info: TAMZGShellExecuteInfo;
  Page: TOutputProgressWizardPage;
  AbortButton: TNewButton;
  Response, ExitCode: Cardinal;
  Cycle, Cut, Step: Integer;
  PhaseText, Data: string;
  Lines: TArrayOfString;
  Launched, Completed, Cancelled: Boolean;
begin
  Result := False;
  ErrorDetails := '';
  Cycle := 0;
  Step := 5;
  PhaseText := 'Preparando o backup completo...';
  Launched := False;
  Completed := False;
  Cancelled := False;
  AMZGBackupCancelRequested := False;

  Page := CreateOutputProgressPage(
    'Armazem Gratidao - Backup de seguranca',
    'A instalacao so continua depois que o backup estiver completo e validado.');
  Page.SetText(PhaseText, 'As vendas, fiados, estoque e configuracoes serao preservados.');
  Page.SetProgress(Step, 100);
  Page.Show;
  try
    AbortButton := TNewButton.Create(WizardForm);
    AbortButton.Parent := Page.Surface;
    AbortButton.Caption := 'Cancelar backup';
    AbortButton.Width := ScaleX(138);
    AbortButton.Height := ScaleY(25);
    AbortButton.Left := 0;
    AbortButton.Top := Page.SurfaceHeight - AbortButton.Height - ScaleY(6);
    AbortButton.OnClick := @AMZGCancelBackup;
    AMZGBackupInProgress := True;

    Info.cbSize := SizeOf(Info);
    Info.fMask := AMZG_SEE_MASK_NOCLOSEPROCESS;
    Info.Wnd := 0;
    Info.lpVerb := '';
    Info.lpFile := ProgramPath;
    Info.lpParameters := ProgramArgs;
    Info.lpDirectory := ExpandConstant('{tmp}');
    Info.nShow := SW_HIDE;
    Info.hInstApp := 0;
    Info.lpIDList := 0;
    Info.lpClass := '';
    Info.hkeyClass := 0;
    Info.dwHotKey := 0;
    Info.hMonitor := 0;
    Info.hProcess := 0;

    if not AMZGShellExecuteEx(Info) then
    begin
      ErrorDetails := 'Nao foi possivel iniciar o backup em PowerShell.';
      Exit;
    end;
    if Info.hProcess = 0 then
    begin
      ErrorDetails := 'Processo de backup sem identificador valido.';
      Exit;
    end;
    Launched := True;
    try
      while not Completed do
      begin
        Response := AMZGWaitForSingleObject(Info.hProcess, 200);
        Cycle := Cycle + 1;
        if (Cycle mod 5 = 0) or (Response <> AMZG_WAIT_TIMEOUT) then
        begin
          if FileExists(StatusFile) and LoadStringsFromFile(StatusFile, Lines) then
          begin
            if GetArrayLength(Lines) > 0 then
            begin
              Data := Lines[0];
              Cut := Pos('|', Data);
              if Cut > 0 then
              begin
                Step := StrToIntDef(Copy(Data, 1, Cut - 1), Step);
                PhaseText := Copy(Data, Cut + 1, Length(Data));
              end;
            end;
          end;
        end;
        // SetProgress tambem processa mensagens da janela.
        if Cycle mod 5 = 0 then
        begin
          Page.SetText(PhaseText,
            'Tempo decorrido: ' + IntToStr(Cycle div 5) +
            ' segundos. Nao desligue o computador.');
        end;
        Page.SetProgress(Step, 100);

        if Response = AMZG_WAIT_OBJECT_0 then
          Completed := True
        else if Response <> AMZG_WAIT_TIMEOUT then
        begin
          ErrorDetails := 'Falha ao consultar o estado do backup.';
          Completed := True;
        end;
        if AMZGBackupCancelRequested then
        begin
          Cancelled := True;
          ErrorDetails := 'Backup cancelado pelo usuario. Nenhum arquivo do programa foi substituido.';
          AMZGTerminateProcess(Info.hProcess, 31);
          Completed := True;
        end;
        if (Cycle >= AMZG_BACKUP_TIMEOUT_CYCLES) and (not Completed) then
        begin
          ErrorDetails := 'Tempo limite do backup atingido. Atualizacao bloqueada sem alterar o programa.';
          AMZGTerminateProcess(Info.hProcess, 31);
          Completed := True;
        end;
      end;
      if (ErrorDetails = '') and (not Cancelled) then
      begin
        if not AMZGGetExitCodeProcess(Info.hProcess, ExitCode) then
          ErrorDetails := 'Nao foi possivel conferir a finalizacao do backup.'
        else if ExitCode <> 0 then
          ErrorDetails := 'Backup falhou (codigo ' + IntToStr(ExitCode) + ').'
        else if Step <> 100 then
          ErrorDetails := 'Backup nao confirmou todas as etapas de verificacao.'
        else
          Result := True;
      end;
    finally
      AMZGCloseHandle(Info.hProcess);
    end;
  finally
    AMZGBackupInProgress := False;
    Page.Hide;
    Page.Free;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  DataDir, DbFile, PsExe, Args, BackupDir, BackupConfig, StatusFile, ErrorDetails: string;
  BackupLines: TArrayOfString;
begin
  Result := '';
  DataDir := ExpandConstant('{localappdata}\ArmazemGratidaoNovo');
  DbFile := DataDir + '\armazem-gratidao.db';
  if not FileExists(DbFile) then
  begin
    if FileExists(ExpandConstant('{app}\ArmazemGratidaoNovo.exe')) then
      Result := 'Reparo bloqueado: banco do usuario nao encontrado.' + #13#10 +
        'Verifique o perfil do Windows antes de modificar o programa.';
    Exit;
  end;

  BackupDir := ExpandConstant('{app}\Arquivos\Backups');
  BackupConfig := DataDir + '\backup-location.txt';
  if FileExists(BackupConfig) then
  begin
    if not LoadStringsFromFile(BackupConfig, BackupLines) then
    begin
      Result := 'Atualizacao bloqueada: erro ao ler a configuracao de backup.';
      Exit;
    end;
    if GetArrayLength(BackupLines) > 0 then
      BackupDir := Trim(BackupLines[0])
    else
      BackupDir := '';
    if BackupDir = '' then
    begin
      Result := 'Atualizacao bloqueada: pasta de backup configurada esta vazia.';
      Exit;
    end;
  end;

  try
    ExtractTemporaryFile('backup-antes-instalacao.ps1');
    ExtractTemporaryFile('System.Data.SQLite.dll');
    ExtractTemporaryFile('SQLite.Interop.dll');
  except
    Result := 'Atualizacao bloqueada: nao foi possivel preparar o backup.';
    Exit;
  end;

  StatusFile := ExpandConstant('{tmp}\amzg-backup-progresso.txt');
  if FileExists(StatusFile) then DeleteFile(StatusFile);
  PsExe := ExpandConstant('{sysnative}\WindowsPowerShell\v1.0\powershell.exe');
  Args := '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File ' +
    AddQuotes(ExpandConstant('{tmp}\backup-antes-instalacao.ps1')) +
    ' -Database ' + AddQuotes(DbFile) +
    ' -TargetDirectory ' + AddQuotes(BackupDir) +
    ' -ProgressFile ' + AddQuotes(StatusFile);
  if not AMZGRunBackup(PsExe, Args, StatusFile, ErrorDetails) then
  begin
    Result := 'Instalacao interrompida: backup obrigatorio nao concluido.' + #13#10 +
      ErrorDetails + #13#10 + 'Dados e instalacao anterior preservados.';
    Exit;
  end;
end;

// O instalador completo fica guardado uma vez e oferece reparacao local,
// sem nova descarga, sem desinstalacao e sem mexer nos dados do usuario.
// Verifica o EXE real no destino; nunca concluir uma instalação que deixou
// a versão anterior. Corrige somente atalhos com o mesmo nome do aplicativo.
procedure AMZGUpdateShortcutIfExists(const LinkPath, Executable, AppDir: String);
begin
  if FileExists(LinkPath) then
  begin
    try
      CreateShellLink(LinkPath, 'Armazem Gratidao - Abrir programa instalado',
        Executable, '', AppDir, Executable, 0, SW_SHOWNORMAL);
      Log('Atalho atualizado: ' + LinkPath + ' => ' + Executable);
    except
      Log('Atalho nao atualizado: ' + LinkPath + ' - ' + GetExceptionMessage);
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  SourceSetup, RepairSetup, Executable, ActualVersion, ExpectedVersion: string;
  AppDir, LogDir, ResultLog: string;
begin
  if CurStep = ssPostInstall then
  begin
    AppDir := ExpandConstant('{app}');
    Executable := AppDir + '\ArmazemGratidaoNovo.exe';
    // Nao marcar instalacao como concluida sem o atualizador independente.
    if not FileExists(AppDir + '\AtualizadorGratidao.exe') then
      RaiseException('Instalacao incompleta: AtualizadorGratidao.exe nao foi instalado em ' + AppDir);
    ExpectedVersion := '{#MyAppVersion}';
    if not FileExists(Executable) then
      RaiseException('Instalacao incompleta: programa nao encontrado em ' + Executable);
    if not GetVersionNumbersString(Executable, ActualVersion) then
      RaiseException('Instalacao incompleta: nao foi possivel conferir a versao do EXE: ' + Executable);
    if CompareText(ActualVersion, ExpectedVersion) <> 0 then
      RaiseException('Instalacao INCOMPLETA: esperado ' + ExpectedVersion +
        ', mas encontrado ' + ActualVersion + ' em ' + Executable +
        '. O instalador nao vai informar sucesso incorretamente.');

    // Atalho do Menu Iniciar e do desktop padrao ja sao atualizados por [Icons].
    // Aqui corrigimos eventual atalho HOMONIMO no desktop do usuario atual,
    // que pode coexistir com o atalho comum e continuar abrindo o EXE antigo.
    AMZGUpdateShortcutIfExists(ExpandConstant('{userdesktop}\{#MyShortcutName}.lnk'),
      Executable, AppDir);
    AMZGUpdateShortcutIfExists(ExpandConstant('{commondesktop}\{#MyShortcutName}.lnk'),
      Executable, AppDir);

    LogDir := AppDir + '\Arquivos\Logs';
    ForceDirectories(LogDir);
    ResultLog := 'Versao instalada: ' + ActualVersion + #13#10 +
      'Executavel conferido: ' + Executable + #13#10 +
      'Pasta unica: ' + AppDir + #13#10 +
      'Atalhos conferidos: ' + ExpandConstant('{userdesktop}') + #13#10;
    SaveStringToFile(LogDir + '\instalacao-verificada.txt', ResultLog, False);
    Log('AMZG instalacao VERIFICADA: ' + ResultLog);

    // O instalador permanece como reparador independente, sem perder o banco.
    SourceSetup := ExpandConstant('{srcexe}');
    RepairSetup := AppDir + '\Reparar-Armazem-Gratidao.exe';
    if CompareText(SourceSetup, RepairSetup) <> 0 then
    begin
      if not FileCopy(SourceSetup, RepairSetup, False) then
        MsgBox('O programa foi instalado, mas nao foi possivel guardar o reparador. Guarde uma copia do instalador para reparar futuramente.',
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
