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
$jobCount=([regex]::Matches($workflow,'(?m)^  publicar_release:\r?$')).Count
if($jobCount -ne 1){throw "Pipeline tem $jobCount jobs duplicados de publicacao"}
$manual="github.event_name == 'workflow_dispatch' && inputs.publicar_release == true"
$approvedPush="github.event_name == 'push' && contains(github.event.head_commit.message, '[release]')"
if(-not $workflow.Contains($manual) -or -not $workflow.Contains($approvedPush)){
 throw 'Publicacao deve exigir despacho manual ou commit marcado [release], sempre apos testes'
}
if($workflow.Contains("github.event_name == 'push' ||")){
 throw 'Publicar release ao enviar codigo nao e permitido'
}
Write-Host "OK: fonte $localVersion build $localBuild; manifesto publicado build $($manifest.build); release so manual."

if(-not $workflow.Contains('--draft --target') -or -not $workflow.Contains('gh api --method PATCH') -or -not $workflow.Contains('validar-pacote-oficial.ps1')){
 throw 'Publicacao precisa homologar artefato e validar rascunho antes de liberar.'
}
if(-not $workflow.Contains('git fetch origin main') -or -not $workflow.Contains('git push origin HEAD:main') -or $workflow.Contains('git push --force')){
 throw 'Publicacao precisa sincronizar sem sobrescrever codigo.'
}
if(-not $source.Contains('WriteUpdateLog("BACKUP OK"') -or -not $source.Contains('WriteUpdateLog("DOWNLOAD SHA-256 OK"')){
 throw 'Diagnostico de atualizacao precisa registrar etapas verificadas.'
}

if(-not $source.Contains('SetUpdateIndicator(remoteBuild);') -or -not $source.Contains('if(!manual)return;') -or -not $source.Contains('amzgUpdateHeader')){
 throw 'Verificacao automatica deve apenas sinalizar a atualizacao no painel.'
}
