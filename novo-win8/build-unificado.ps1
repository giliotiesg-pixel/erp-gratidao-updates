param(
 [ValidateRange(588,65000)][int]$Build=588
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
function Assert-Exit([string]$Step) {
 if ($LASTEXITCODE -ne 0) { throw "$Step falhou com codigo $LASTEXITCODE" }
}
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project=$PSScriptRoot
$bin=Join-Path $project 'bin\x64\Release'
$stage=Join-Path $root 'dist\pacote'
$dist=Join-Path $root 'dist'
$packageDir=Join-Path $project 'packages'
Push-Location $root
try {
 Write-Host "Construindo Armazem Gratidao Novo - build $Build"
 if(Test-Path $dist){Remove-Item $dist -Recurse -Force}
 New-Item -ItemType Directory -Path $stage -Force|Out-Null
 if(!(Get-Command msbuild -ErrorAction SilentlyContinue)){throw 'MSBuild nao instalado / nao encontrado.'}
 if(!(Get-Command nuget -ErrorAction SilentlyContinue)){throw 'NuGet CLI nao instalado / nao encontrado.'}
 if(!(Get-Command python -ErrorAction SilentlyContinue)){throw 'Python nao instalado / nao encontrado.'}
 nuget restore (Join-Path $project 'packages.config') -PackagesDirectory $packageDir -NonInteractive
 Assert-Exit 'Restaurar pacotes'
 # GitHub Windows 2025 nao inclui o SDK legado .NET 4.5.
 # As referencias oficiais abaixo servem SOMENTE para compilacao; nao sao instaladas no PC do cliente.
 nuget install Microsoft.NETFramework.ReferenceAssemblies.net45 -Version 1.0.3 -OutputDirectory $packageDir -NonInteractive
 Assert-Exit 'Baixar assembly de referencia oficial .NET 4.5'
 $refSource=Join-Path $packageDir 'Microsoft.NETFramework.ReferenceAssemblies.net45.1.0.3\build\.NETFramework\v4.5'
 if(!(Test-Path (Join-Path $refSource 'mscorlib.dll'))){throw "Pacote oficial .NET 4.5 incompleto: $refSource"}
 $refTarget=Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5'
 if(!(Test-Path (Join-Path $refTarget 'mscorlib.dll'))){
  New-Item -ItemType Directory -Force -Path $refTarget | Out-Null
  Copy-Item (Join-Path $refSource '*') $refTarget -Recurse -Force
 }
 if(!(Test-Path (Join-Path $refTarget 'mscorlib.dll'))){throw 'Assemblies .NET 4.5 nao preparados para MSBuild'}
 Write-Host 'Compilacao validara APIs antigas de .NET 4.5, sem exigir .NET 4.8 no Windows 8.'
 # O pacote System.Data.SQLite.Core e somente um metapacote.
 # Procurar a DLL real do Stub NetFramework, sem presumir pasta net46 fixa.
 $managed=Get-ChildItem -Path $packageDir -Recurse -File -Filter 'System.Data.SQLite.dll' |
  Where-Object {$_.FullName -match 'Stub.System.Data.SQLite.Core.NetFramework'} |
  Sort-Object @{Expression={if($_.FullName -match 'net45[\\/]'){0}elseif($_.FullName -match 'net40[\\/]'){1}else{2}}} |
  Select-Object -First 1
 if($null -eq $managed){
  Get-ChildItem -Path $packageDir -Recurse -File -Filter '*.dll' |
   Select-Object -First 30 -ExpandProperty FullName | ForEach-Object {Write-Host $_}
  throw 'System.Data.SQLite.dll nao encontrada. Nao distribuir um aplicativo incompleto.'
 }
 $deps=Join-Path $project 'dependencias'
 New-Item -ItemType Directory -Path $deps -Force|Out-Null
 Copy-Item $managed.FullName (Join-Path $deps 'System.Data.SQLite.dll') -Force
 Write-Host "Assembly SQLite: $($managed.FullName)"
 python (Join-Path $project 'gerar-icone.py')
 Assert-Exit 'Gerar icone'
 $version="1.0.0.$Build"
 $versionFile=Join-Path $project 'BuildVersion.cs'
 "[assembly: System.Reflection.AssemblyVersion(`"$version`")]`r`n[assembly: System.Reflection.AssemblyFileVersion(`"$version`")]" |
  Set-Content $versionFile -Encoding ASCII
 foreach($name in @('ArmazemGratidao.csproj','AtualizadorGratidao.csproj')){
  msbuild (Join-Path $project $name) /t:Rebuild /m:1 /p:Configuration=Release /p:Platform=x64 /v:minimal
  Assert-Exit "Compilar $name"
 }
 # Não distribuir pacote se o executável NÃO contiver a versão prometida.
 # Esse erro passava despercebido nos testes anteriores, que só verificavam existência.
 $exe=Join-Path $bin 'ArmazemGratidaoNovo.exe'
 $actualFile=(Get-Item $exe).VersionInfo.FileVersion
 $actualAssembly=[System.Reflection.AssemblyName]::GetAssemblyName($exe).Version.ToString()
 if($actualFile -ne $version -or $actualAssembly -ne $version){
  throw "Build incorreta no EXE. Esperado=$version arquivo=$actualFile assembly=$actualAssembly"
 }
 Write-Host "EXE verificado: $exe, FileVersion=$actualFile, AssemblyVersion=$actualAssembly"
 $native=Get-ChildItem -Path $packageDir -Recurse -File -Filter 'SQLite.Interop.dll' |
  Where-Object {$_.FullName -match '[\\/]x64[\\/]'} |
  Sort-Object @{Expression={if($_.FullName -match 'net45[\\/]'){0}elseif($_.FullName -match 'net40[\\/]'){1}else{2}}} |
  Select-Object -First 1
 if($null -eq $native){throw 'DLL SQLite.Interop.dll (x64) nao encontrada nos pacotes NuGet.'}
 $x64=Join-Path $bin 'x64'
 New-Item -ItemType Directory -Path $x64 -Force|Out-Null
 Copy-Item $native.FullName (Join-Path $x64 'SQLite.Interop.dll') -Force
 $payload=@('ArmazemGratidaoNovo.exe','AtualizadorGratidao.exe',
  'QRCoder.dll','zxing.dll','System.Data.SQLite.dll')
 foreach($name in $payload){
  $src=Join-Path $bin $name
  if(!(Test-Path $src)){throw "Arquivo compilado ausente: $src"}
 }
 # O pacote incremental contem somente executaveis e DLLs autorizados.
 foreach($name in $payload){
  if($name -eq 'AtualizadorGratidao.exe'){continue}
  Copy-Item (Join-Path $bin $name) (Join-Path $stage $name) -Force
 }
 $conf=Join-Path $bin 'ArmazemGratidaoNovo.exe.config'
 if(Test-Path $conf){Copy-Item $conf (Join-Path $stage 'ArmazemGratidaoNovo.exe.config') -Force}
 New-Item -ItemType Directory -Path (Join-Path $stage 'x64') -Force|Out-Null
 Copy-Item (Join-Path $x64 'SQLite.Interop.dll') (Join-Path $stage 'x64\SQLite.Interop.dll') -Force
 $bad=@(Get-ChildItem $stage -Recurse -File | Where-Object {$_.Extension -match '^\.(db|sqlite|sqlite3|bak|wal|shm)$'})
 if($bad.Count -gt 0){throw 'Banco de dados encontrado no pacote. Publicacao bloqueada!'}
 $zip=Join-Path $dist 'Armazem-Gratidao-Atualizacao.zip'
 Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal -Force
 Add-Type -AssemblyName System.IO.Compression.FileSystem
 $archive=[System.IO.Compression.ZipFile]::OpenRead($zip)
 try{
  $names=@($archive.Entries | Where-Object {$_.Name} | ForEach-Object {$_.FullName})
  if(-not ($names -contains 'ArmazemGratidaoNovo.exe')){throw 'Executavel principal ausente no ZIP.'}
  if($names.Count -lt 5){throw 'ZIP de atualizacao incompleto.'}
 }finally{$archive.Dispose()}
 $inno=@('C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
         'C:\Program Files\Inno Setup 6\ISCC.exe') |
  Where-Object {Test-Path $_} | Select-Object -First 1
 if(-not $inno){throw 'Inno Setup 6 nao encontrado.'}
 & $inno "/DBuildNumber=$Build" (Join-Path $project 'instalador.iss')
 Assert-Exit 'Gerar instalador completo'
 $setup=Join-Path $dist 'Armazem-Gratidao-Novo-Setup.exe'
 if(!(Test-Path $setup)){throw 'Instalador unico nao foi gerado.'}
 $hash=(Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
 $repo='https://github.com/giliotiesg-pixel/erp-gratidao-updates'
 $tag="amzg-novo-win8-build-$Build"
 $manifest=[ordered]@{
  build=$Build; tag=$tag; asset='Armazem-Gratidao-Atualizacao.zip'
  url="$repo/releases/download/$tag/Armazem-Gratidao-Atualizacao.zip"
  sha256=$hash
 }
 ($manifest|ConvertTo-Json -Compress) | Set-Content (Join-Path $dist 'manifesto-proposto.json') -Encoding UTF8
 $setupHash=(Get-FileHash $setup -Algorithm SHA256).Hash.ToLowerInvariant()
 @("Build: $Build","Instalador: $setup","Instalador SHA-256: $setupHash",
  "Atualizacao ZIP SHA-256: $hash","Manifesto: manifesto-proposto.json") |
  Set-Content (Join-Path $dist 'VALIDACAO-SHA256.txt') -Encoding UTF8
 Write-Host "Pacote unico e ZIP de atualizacao gerados em $dist"
}finally{
 Pop-Location
}
