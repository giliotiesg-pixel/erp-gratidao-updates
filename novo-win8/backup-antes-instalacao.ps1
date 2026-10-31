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
  Write-Output "Backup de atualizacao validado: $out"
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
