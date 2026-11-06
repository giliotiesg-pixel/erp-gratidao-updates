param(
 [Parameter(Mandatory=$true)][string]$Database,
 [Parameter(Mandatory=$true)][string]$TargetDirectory
)
$ErrorActionPreference = "Stop"
try {
 if (!(Test-Path -LiteralPath $Database -PathType Leaf)) { exit 0 }
 if (!(Test-Path -LiteralPath $TargetDirectory -PathType Container)) {
  New-Item -ItemType Directory -Path $TargetDirectory -Force | Out-Null
 }
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
  $src.Open();$dst.Open()
  $src.BackupDatabase($dst,"main","main",-1,$null,0)
  $dst.Close();$dst.Dispose();$dst=$null
  $src.Close();$src.Dispose();$src=$null
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
  Add-Type -AssemblyName System.IO.Compression
  Add-Type -AssemblyName System.IO.Compression.FileSystem
  $archivePath=Join-Path $TargetDirectory ("armazem-gratidao-completo-"+(Get-Date -Format "yyyyMMdd-HHmmssfff")+"-"+[guid]::NewGuid().ToString("N").Substring(0,6)+".zip")
  $archive=$null
  try {
    $archive=[System.IO.Compression.ZipFile]::Open($archivePath,[System.IO.Compression.ZipArchiveMode]::Create)
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$out,"Dados/armazem-gratidao.db",[System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    $dataDir=[System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath($Database))
    $backPrefix=[System.IO.Path]::GetFullPath($TargetDirectory).TrimEnd('\')+'\'
    $updatesPrefix=[System.IO.Path]::GetFullPath((Join-Path $dataDir "Atualizacoes")).TrimEnd('\')+'\'
    foreach($item in @(Get-ChildItem -LiteralPath $dataDir -Recurse -File -ErrorAction Stop)){
      $path=[System.IO.Path]::GetFullPath($item.FullName)
      if($path.StartsWith($backPrefix,[System.StringComparison]::OrdinalIgnoreCase) -or
         $path.StartsWith($updatesPrefix,[System.StringComparison]::OrdinalIgnoreCase) -or
         $path -eq [System.IO.Path]::GetFullPath($Database) -or
         $path.EndsWith(".db-wal") -or $path.EndsWith(".db-shm")){continue}
      $rel=$path.Substring($dataDir.Length).TrimStart('\').Replace('\','/')
      [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$path,("Dados/"+$rel),[System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
  } finally { if($archive){$archive.Dispose()} }
  $verify=[System.IO.Compression.ZipFile]::OpenRead($archivePath)
  try {
    $entry=$verify.GetEntry("Dados/armazem-gratidao.db")
    if(!$entry -or $entry.Length -le 0){ throw "Backup completo nao contem banco de dados." }
  } finally { $verify.Dispose() }
  if((Get-Item -LiteralPath $archivePath).Length -le 0){throw "Backup completo vazio."}
  Write-Output "Backup do banco validado: $out"
  Write-Output "Backup completo validado: $archivePath"
  exit 0
 } finally {
  if($test){$test.Dispose()}
  if($dst){$dst.Dispose()}
  if($src){$src.Dispose()}
 }
} catch {
 $msg=$_.Exception.ToString()
 Write-Error "INSTALACAO BLOQUEADA: o backup automatico nao foi concluido. $msg"
 exit 31
}
