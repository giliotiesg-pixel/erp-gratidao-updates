param(
 [Parameter(Mandatory=$true)][string]$ProgramExe,
 [Parameter(Mandatory=$true)][string]$UpdaterExe
)
$ErrorActionPreference='Stop'
$program=[IO.Path]::GetFullPath($ProgramExe)
$updater=[IO.Path]::GetFullPath($UpdaterExe)
$assembly=[Reflection.Assembly]::LoadFrom($program)
$type=$assembly.GetType('ArmazemGratidao.MainForm',$true)
$guard=$type.GetMethod('EnsureInstalledUpdaterFree',
 [Reflection.BindingFlags]::Static -bor [Reflection.BindingFlags]::NonPublic)
if(!$guard){throw 'Verificacao antecipada de arquivo bloqueado ausente no PDV'}
$folder=Join-Path $env:RUNNER_TEMP 'AMZG-Preflight-PDV'
New-Item -ItemType Directory -Path $folder -Force | Out-Null
$destination=Join-Path $folder 'AtualizadorGratidao.exe'
Copy-Item -LiteralPath $updater -Destination $destination -Force
$hash=(Get-FileHash $destination -Algorithm SHA256).Hash
$stream=[IO.File]::Open($destination,[IO.FileMode]::Open,
 [IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
try{
 $blocked=$false
 try{$guard.Invoke($null,@([string]$folder)) | Out-Null}
 catch {
  if($_.Exception.ToString() -notmatch 'ainda esta aberto ou bloqueado'){
   throw "Bloqueio inesperado: $($_.Exception)"
  }
  $blocked=$true
 }
 if(!$blocked){throw 'PDV nao detectou atualizador em uso antes do backup.'}
 Write-Host 'PREFLIGHT DO PDV OK: bloqueio detectado antes de download, backup ou alteracao.'
}finally{$stream.Dispose()}
$guard.Invoke($null,@([string]$folder)) | Out-Null
if((Get-FileHash $destination -Algorithm SHA256).Hash -ne $hash){
 throw 'Arquivo foi alterado durante verificacao de disponibilidade'
}
# Duplo clique no atualizador auxiliar nao pode abrir janela modal que bloqueie o proprio EXE.
$standalone=Start-Process -FilePath $updater -PassThru -WindowStyle Hidden
try{
 if(!$standalone.WaitForExit(3500)){
   throw 'Atualizador sem parametros permanece aberto e bloqueia as proximas atualizacoes.'
 }
 Write-Host 'ATUALIZADOR OK: abertura por engano termina sem deixar janela bloqueada.'
}finally{
 if(!$standalone.HasExited){Stop-Process -Id $standalone.Id -Force}
 $standalone.Dispose()
}
