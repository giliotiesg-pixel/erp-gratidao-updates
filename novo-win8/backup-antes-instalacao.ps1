param(
 [Parameter(Mandatory=$true)][string]$Database,
 [Parameter(Mandatory=$true)][string]$TargetDirectory,
 [Parameter(Mandatory=$true)][string]$ProgressFile
)
$ErrorActionPreference = "Stop"
function Set-BackupStatus([int]$Step,[string]$Description) {
 # Log pequeno, regravado a cada fase. O instalador mostra o estágio atual
 # sem abrir console e sem bloquear os cliques do usuário.
 try {
  [System.IO.File]::WriteAllText($ProgressFile,("$Step|"+$Description),(New-Object System.Text.UTF8Encoding($false)))
 } catch {
  # O backup em si continua; a validação final do instalador confere o resultado.
 }
}
Set-BackupStatus 5 'Preparando o backup obrigatório'
try {
 if (!(Test-Path -LiteralPath $Database -PathType Leaf)) { throw "Banco SQLite não encontrado." }
 if (!(Test-Path -LiteralPath $TargetDirectory -PathType Container)) {
  New-Item -ItemType Directory -Path $TargetDirectory -Force | Out-Null
 }
 Set-BackupStatus 10 'Verificando arquivos e permissões'
 $provider=Join-Path $PSScriptRoot "System.Data.SQLite.dll"
 if (!(Test-Path -LiteralPath $provider)) { throw "Biblioteca SQLite para backup ausente." }
 Add-Type -TypeDefinition @'
using System.Runtime.InteropServices;
public static class BackupNativeDll {
 [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
 public static extern bool SetDllDirectory(string path);
}
'@
 [BackupNativeDll]::SetDllDirectory($PSScriptRoot) | Out-Null
 Add-Type -Path $provider
 $name="armazem-gratidao-antes-atualizacao-"+(Get-Date -Format "yyyyMMdd-HHmmssfff")+"-"+[guid]::NewGuid().ToString("N").Substring(0,6)+".db"
 $out=Join-Path $TargetDirectory $name
 $src=$null;$dst=$null;$test=$null
 try {
  $src=New-Object System.Data.SQLite.SQLiteConnection("Data Source=$Database;Read Only=True;FailIfMissing=True;")
  $dst=New-Object System.Data.SQLite.SQLiteConnection("Data Source=$out;FailIfMissing=False;")
  Set-BackupStatus 25 'Copiando vendas, fiados, produtos e clientes'
  $src.Open();$dst.Open()
  $src.BackupDatabase($dst,"main","main",-1,$null,0)
  $dst.Close();$dst.Dispose();$dst=$null
  $src.Close();$src.Dispose();$src=$null
  Set-BackupStatus 50 'Conferindo integridade da cópia do banco'
  $test=New-Object System.Data.SQLite.SQLiteConnection("Data Source=$out;Read Only=True;FailIfMissing=True;")
  $test.Open()
  $check=$test.CreateCommand()
  $check.CommandText="PRAGMA integrity_check"
  $result=[string]$check.ExecuteScalar()
  $check.Dispose()
  if($result -ne "ok") { throw "Falha na verificacao de integridade: $result" }
  if((Get-Item -LiteralPath $out).Length -le 0){ throw "Backup vazio." }
  $test.Close();$test.Dispose();$test=$null
  # Gerar tambem um backup completo com banco consistente e configuracoes.
  # A pasta de backup/atualizacoes nao pode ser incluída dentro dela propria.
  Set-BackupStatus 65 'Compactando dados e configurações'
  Add-Type -AssemblyName System.IO.Compression
  Add-Type -AssemblyName System.IO.Compression.FileSystem
  $archiveFinal=Join-Path $TargetDirectory ("armazem-gratidao-completo-"+(Get-Date -Format "yyyyMMdd-HHmmssfff")+"-"+[guid]::NewGuid().ToString("N").Substring(0,6)+".zip")
  # Arquivo só ganha a extensão .zip após passar nas verificações.
  # Cancelamentos/travamentos nunca deixam um backup incompleto como válido.
  $archivePath=$archiveFinal+".partial"
  $archive=$null
  try {
    $archive=[System.IO.Compression.ZipFile]::Open($archivePath,[System.IO.Compression.ZipArchiveMode]::Create)
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$out,"Dados/armazem-gratidao.db",[System.IO.Compression.CompressionLevel]::Fastest) | Out-Null
    $dataDir=[System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath($Database))
    # Não entrar em pastas com versões antigas, backups ou atualizações:
    # compactar um backup dentro de outro causava crescimento e lentidão.
    $skipDirs=@()
    foreach($folder in @($TargetDirectory,(Join-Path $dataDir 'Backups'),
      (Join-Path $dataDir 'Backup'),(Join-Path $dataDir 'Atualizacoes'))){
      $full=[System.IO.Path]::GetFullPath($folder).TrimEnd([char]92)
      if(!$full.Equals($dataDir,[System.StringComparison]::OrdinalIgnoreCase)){
        $skipDirs+=($full+[char]92)
      }
    }
    $stack=New-Object 'System.Collections.Generic.Stack[string]'
    $stack.Push($dataDir)
    while($stack.Count -gt 0){
      $directory=$stack.Pop()
      foreach($sub in [System.IO.Directory]::GetDirectories($directory)){
        $full=[System.IO.Path]::GetFullPath($sub).TrimEnd([char]92)+[char]92
        $skip=$false
        foreach($excluded in $skipDirs){
          if($full.StartsWith($excluded,[System.StringComparison]::OrdinalIgnoreCase)){
            $skip=$true;break
          }
        }
        if($skip){continue}
        if(([System.IO.File]::GetAttributes($sub) -band [System.IO.FileAttributes]::ReparsePoint) -ne 0){continue}
        $stack.Push($sub)
      }
      foreach($path in [System.IO.Directory]::GetFiles($directory)){
        $full=[System.IO.Path]::GetFullPath($path)
        $name=[System.IO.Path]::GetFileName($full)
        if($full.Equals([System.IO.Path]::GetFullPath($Database),[System.StringComparison]::OrdinalIgnoreCase) -or
          $full.Equals([System.IO.Path]::GetFullPath($out),[System.StringComparison]::OrdinalIgnoreCase) -or
          $full.Equals([System.IO.Path]::GetFullPath($archivePath),[System.StringComparison]::OrdinalIgnoreCase) -or
          $name.EndsWith('.db-wal',[System.StringComparison]::OrdinalIgnoreCase) -or
          $name.EndsWith('.db-shm',[System.StringComparison]::OrdinalIgnoreCase) -or
          $name.StartsWith('armazem-gratidao-completo-',[System.StringComparison]::OrdinalIgnoreCase) -or
          $name.StartsWith('armazem-gratidao-antes-atualizacao-',[System.StringComparison]::OrdinalIgnoreCase)){
          continue
        }
        $rel=$full.Substring($dataDir.Length).TrimStart([char]92).Replace([char]92,[char]47)
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
          $archive,$full,('Dados/'+$rel),
          [System.IO.Compression.CompressionLevel]::Fastest) | Out-Null
      }
    }
  } finally { if($archive){$archive.Dispose()} }
  Set-BackupStatus 90 'Verificando o arquivo ZIP do backup'
  $verify=[System.IO.Compression.ZipFile]::OpenRead($archivePath)
  try {
    $entry=$verify.GetEntry("Dados/armazem-gratidao.db")
    if(!$entry -or $entry.Length -le 0){ throw "Backup completo nao contem banco de dados." }
  } finally { $verify.Dispose() }
  if((Get-Item -LiteralPath $archivePath).Length -le 0){throw "Backup completo vazio."}
  [System.IO.File]::Move($archivePath,$archiveFinal)
  # O ZIP verificado já guarda a cópia SQLite consistente. Não manter uma
  # duplicata solta em disco a cada reparo; somente limpar após validar o ZIP.
  try { Remove-Item -LiteralPath $out -Force -ErrorAction Stop } catch { }
  Set-BackupStatus 100 'Backup completo validado'
  Write-Output "Backup completo validado: $archiveFinal"
  exit 0
 } finally {
  if($test){$test.Dispose()}
  if($dst){$dst.Dispose()}
  if($src){$src.Dispose()}
 }
} catch {
 $msg=$_.Exception.ToString()
 Set-BackupStatus 0 ('ERRO: '+$_.Exception.Message)
 Write-Error "INSTALACAO BLOQUEADA: o backup automatico nao foi concluido. $msg"
 exit 31
}
