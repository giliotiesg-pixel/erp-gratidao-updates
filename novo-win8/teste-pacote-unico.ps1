param(
 [Parameter(Mandatory=$true)][string]$Updater,
 [Parameter(Mandatory=$true)][string]$Zip,
 [Parameter(Mandatory=$true)][string]$ExpectedVersion
)
$ErrorActionPreference='Stop'
$assembly=[Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($Updater))
$extractor=$assembly.GetType('ArmazemGratidaoAtualizador.ZipCompat',$true)
$window=$assembly.GetType('ArmazemGratidaoAtualizador.AtualizadorWindow',$true)
$flags=[Reflection.BindingFlags]::Static -bor [Reflection.BindingFlags]::NonPublic
$method=$extractor.GetMethod('Extract',$flags)
$allowed=$window.GetMethod('Allowed',$flags)
if($null -eq $method -or $null -eq $allowed){throw 'Atualizador sem extrator ZIP ou lista de arquivos autorizados'}
$allow=[System.Delegate]::CreateDelegate([System.Func[string,bool]],$allowed)
$root=Join-Path $env:RUNNER_TEMP 'AMZG-Teste-ZIP-Integral'
New-Item -ItemType Directory -Path $root -Force | Out-Null
try {
 $entries=$method.Invoke($null,@([IO.Path]::GetFullPath($Zip),$root,$allow))
 $required=@('ArmazemGratidaoNovo.exe','AtualizadorGratidao.exe','QRCoder.dll',
   'System.Data.SQLite.dll','zxing.dll','x64/SQLite.Interop.dll')
 foreach($name in $required){
  if($entries -notcontains $name){throw "Pacote faltando arquivo essencial: $name"}
  $path=Join-Path $root ($name.Replace('/',[IO.Path]::DirectorySeparatorChar))
  if(!(Test-Path -LiteralPath $path)){throw "Arquivo nao extraido: $name"}
 }
 $program=Join-Path $root 'ArmazemGratidaoNovo.exe'
 $updater=Join-Path $root 'AtualizadorGratidao.exe'
 $pv=[Reflection.AssemblyName]::GetAssemblyName($program).Version.ToString()
 $uv=[Reflection.AssemblyName]::GetAssemblyName($updater).Version.ToString()
 if($pv -ne $ExpectedVersion -or $uv -ne $ExpectedVersion){
  throw "Versoes diferentes apos extrair o pacote: programa=$pv, atualizador=$uv, esperado=$ExpectedVersion"
 }
 Write-Host "PACOTE UNICO OK: programa e atualizador $ExpectedVersion extraidos do mesmo ZIP; 6 arquivos essenciais conferidos."
}finally{
 try{Remove-Item $root -Recurse -Force}catch{}
}
