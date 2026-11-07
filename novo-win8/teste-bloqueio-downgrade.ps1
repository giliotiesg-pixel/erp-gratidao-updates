param(
 [Parameter(Mandatory=$true)][string]$Updater,
 [Parameter(Mandatory=$true)][string]$ProgramExe
)
$ErrorActionPreference='Stop'
$updaterFull=[IO.Path]::GetFullPath($Updater)
$appFull=[IO.Path]::GetFullPath($ProgramExe)
$asm=[Reflection.Assembly]::LoadFrom($updaterFull)
$t=$asm.GetType('ArmazemGratidaoAtualizador.AtualizadorWindow',$true)
$flags=[Reflection.BindingFlags]::NonPublic -bor [Reflection.BindingFlags]::Public -bor [Reflection.BindingFlags]::Static
$read=$t.GetMethod('ReadInstalledBuild',$flags)
$guard=$t.GetMethod('AssertUpgrade',$flags)
if($null -eq $read -or $null -eq $guard){throw 'Funcoes anti-downgrade ausentes no executavel compilado.'}
$actual=[int]$read.Invoke($null,@($appFull))
$expected=[int]$env:AMZG_BUILD
if($actual -ne $expected){throw "Versao lida incorreta: $actual, esperada $expected"}
$guard.Invoke($null,@([int]591,[int]592)) | Out-Null
foreach($pair in @(@([int]592,[int]582),@([int]592,[int]592),@([int]0,[int]592))){
 $blocked=$false
 try{$guard.Invoke($null,@($pair[0],$pair[1])) | Out-Null}
 catch {
  # Invoke() e encapsulado em MethodInvocationException no PowerShell 5.1.
  $blocked=$true
  $message=$_.Exception.ToString()
  if($message -notmatch 'BLOQUEADA|inválida|inv.lida'){
    throw "Rejeição inesperada: $message"
  }
 }
 if(!$blocked){throw "FALHA: Atualizador aceitou instalar a build $($pair[1]) sobre $($pair[0])."}
}
Write-Host "TESTE ANTI-DOWNGRADE OK: instalado=$actual; downgrade 592->582 e repeticao 592->592 bloqueados."
