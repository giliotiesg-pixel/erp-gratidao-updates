param(
 [Parameter(Mandatory=$true)][string]$Updater
)
$ErrorActionPreference='Stop'
$updater=[IO.Path]::GetFullPath($Updater)
$assembly=[Reflection.Assembly]::LoadFrom($updater)
$t=$assembly.GetType('ArmazemGratidaoAtualizador.AtualizadorWindow',$true)
$flags=[Reflection.BindingFlags]::Static -bor [Reflection.BindingFlags]::NonPublic
$method=$t.GetMethod('EnsureTargetsWritable',$flags)
if($null -eq $method){throw 'Protecao contra arquivo em uso nao encontrada'}
$dir=Join-Path $env:RUNNER_TEMP 'AMZG-lock-test'
New-Item -ItemType Directory -Path $dir -Force | Out-Null
$target=Join-Path $dir 'AtualizadorGratidao.exe'
Copy-Item -LiteralPath $updater -Destination $target -Force
$before=(Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
$names=[string[]]@('AtualizadorGratidao.exe')
function Check([int]$ms){
 $params=New-Object 'object[]' 3
 $params[0]=[string]$dir
 $params[1]=$names
 $params[2]=[int]$ms
 $method.Invoke($null,[object[]]$params) | Out-Null
}
$handle=[System.IO.File]::Open($target,[System.IO.FileMode]::Open,
 [System.IO.FileAccess]::ReadWrite,[System.IO.FileShare]::None)
try{
 $blocked=$false
 try{Check 500}
 catch {
  if($_.Exception.ToString() -notmatch 'ATUALIZACAO NAO INICIADA'){
   throw "Bloqueio inesperado: $($_.Exception)"
  }
  $blocked=$true
 }
 if(!$blocked){throw 'ERRO GRAVE: atualizador tentou substituir executavel que estava bloqueado.'}
 $during=(Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
 if($during -ne $before){throw 'Arquivo mudou durante bloqueio, nao houve protecao transacional.'}
 Write-Host 'LOCK TEST OK: EXE aberto por outro processo foi detectado ANTES de copiar arquivos.'
}finally{$handle.Dispose()}
Check 500
$after=(Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
if($after -ne $before){throw 'Arquivo foi alterado no teste de preflight.'}
Write-Host 'LOCK TEST OK: depois do fechamento o alvo ficou disponivel e sem alteracoes.'
