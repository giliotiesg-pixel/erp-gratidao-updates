param([string]$Dist='dist')
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root=(Resolve-Path $Dist).Path
$manifest=Get-Content (Join-Path $root 'manifesto-proposto.json') -Raw | ConvertFrom-Json
$zip=Join-Path $root 'Armazem-Gratidao-Atualizacao.zip'
$updater=Join-Path $root 'AtualizadorGratidao.exe'
$required=@('ArmazemGratidaoNovo.exe','AtualizadorGratidao.exe','QRCoder.dll','zxing.dll','System.Data.SQLite.dll','x64/SQLite.Interop.dll')
$allowed=$required+@('ArmazemGratidaoNovo.exe.config')
if($manifest.build -lt 1 -or $manifest.build -gt 65000 -or $manifest.version -notmatch '^[0-9]{1,2}\.[0-9]{1,2}$'){throw 'Build ou versao invalida'}
$expected="$($manifest.version).0.$($manifest.build)"
$tag="amzg-novo-win8-build-$($manifest.build)"
$url="https://github.com/giliotiesg-pixel/erp-gratidao-updates/releases/download/$tag/Armazem-Gratidao-Atualizacao.zip"
if($manifest.full_version -ne $expected -or $manifest.tag -ne $tag -or $manifest.url -ne $url -or $manifest.asset -ne 'Armazem-Gratidao-Atualizacao.zip'){throw 'Manifesto divergente do canal oficial'}
if($manifest.sha256 -notmatch '^[0-9a-fA-F]{64}$' -or (Get-FileHash $zip -Algorithm SHA256).Hash -ne $manifest.sha256){throw 'SHA-256 do ZIP divergente'}
$temp=Join-Path ([IO.Path]::GetTempPath()) ('amzg-validate-'+[guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($temp)|Out-Null
try{
 $archive=[IO.Compression.ZipFile]::OpenRead($zip)
 try{
  $seen=@{}
  foreach($entry in $archive.Entries){
   $name=$entry.FullName.Replace('\','/')
   if(!$entry.Name){
    if($name -ne 'x64/'){throw "Diretorio proibido: $name"}
    continue
   }
   if($allowed -notcontains $name -or $seen.ContainsKey($name) -or $entry.Length -le 0 -or $entry.Length -gt 100MB){throw "Entrada invalida, duplicada ou proibida: $name"}
   $seen[$name]=$true
   $target=Join-Path $temp $name
   [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))|Out-Null
   [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$target)
   if((Get-Item -LiteralPath $target).Length -ne $entry.Length){throw "Entrada truncada: $name"}
   # Ler integralmente cada entrada; detectar arquivo danificado durante descompressao.
   Get-FileHash -LiteralPath $target -Algorithm SHA256 | Out-Null
  }
  foreach($name in $required){if(!$seen.ContainsKey($name)){throw "Arquivo obrigatorio ausente: $name"}}
 }finally{$archive.Dispose()}
 foreach($exe in @('ArmazemGratidaoNovo.exe','AtualizadorGratidao.exe')){
  $file=Join-Path $temp $exe
  if((Get-Item $file).VersionInfo.FileVersion.Trim() -ne $expected -or [Reflection.AssemblyName]::GetAssemblyName($file).Version.ToString() -ne $expected){throw "Versao interna divergente: $exe"}
 }
 if((Get-FileHash $updater -Algorithm SHA256).Hash -ne (Get-FileHash (Join-Path $temp 'AtualizadorGratidao.exe') -Algorithm SHA256).Hash){throw 'Atualizador externo difere do ZIP'}
 Write-Host "ZIP INTEGRAL APROVADO: $tag; $($seen.Count) arquivos autorizados, versoes e SHA-256 corretos."
}finally{Remove-Item $temp -Recurse -Force}
