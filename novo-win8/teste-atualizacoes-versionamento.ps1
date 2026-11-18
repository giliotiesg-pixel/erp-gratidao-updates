$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$version=(Get-Content (Join-Path $PSScriptRoot 'VERSAO.txt') -Raw).Trim()
if($version -notmatch '^([1-9][0-9]?)\.([0-9]{1,2})\.([1-9][0-9]{0,4})$'){
 throw "Fonte unica de versao invalida: $version"
}
$localBuild=[int]$Matches[3]
$localVersion="$($Matches[1]).$($Matches[2])"
$manifest=(Get-Content (Join-Path $repo 'atualizacao-novo-win8-pacote.json') -Raw | ConvertFrom-Json)
if([int]$manifest.build -gt $localBuild){
 throw "O manifesto anuncia build $($manifest.build) superior a fonte de compilacao $localBuild"
}
if($manifest.tag -ne "amzg-novo-win8-build-$($manifest.build)"){
 throw 'Manifesto e tag nao correspondem'
}
if($manifest.asset -ne 'Armazem-Gratidao-Atualizacao.zip'){
 throw 'Pacote de atualizacao incorreto'
}
$expectedUrl="https://github.com/giliotiesg-pixel/erp-gratidao-updates/releases/download/$($manifest.tag)/$($manifest.asset)"
if($manifest.url -ne $expectedUrl){throw 'URL oficial nao corresponde ao build'}
if($manifest.sha256 -notmatch '^[0-9a-fA-F]{64}$'){throw 'SHA-256 ausente ou invalido'}
$source=(Get-Content (Join-Path $PSScriptRoot 'Program.cs') -Raw)
if(-not $source.Contains('if(manual||release==null)')){
 throw 'Verificacao manual deve consultar as releases mesmo com manifesto em cache'
}
if($source.Contains('/main/atualizacao-novo-win8.json')){
 throw 'Fonte antiga de atualizacao voltou a ser utilizada'
}
$workflow=(Get-Content (Join-Path $repo '.github/workflows/amzg-novo-unificado.yml') -Raw)
$jobCount=([regex]::Matches($workflow,'(?m)^  publicar_release:$')).Count
if($jobCount -ne 1){throw "Pipeline tem $jobCount jobs duplicados de publicacao"}
$manualOnly="if: github.ref == 'refs/heads/main' && github.event_name == 'workflow_dispatch' && inputs.publicar_release == true"
if(-not $workflow.Contains($manualOnly)){throw 'Publicacao exige disparo manual e testes aprovados'}
if($workflow.Contains("github.event_name == 'push' ||")){
 throw 'Publicar release ao enviar codigo nao e permitido'
}
Write-Host "OK: fonte $localVersion build $localBuild; manifesto publicado build $($manifest.build); release so manual."
