$ErrorActionPreference='Stop'
# Impede que verificacoes antigas de tamanho fixo retornem e escondam defeitos reais.
$checks=@(
 @{File='teste-tema-todos-modulos.ps1'; Pattern='\$form\.Width\s+-gt\s+950|\$form\.Height\s+-gt\s+640'},
 @{File='teste-cadastro-produtos.ps1'; Pattern='\$dlg\.Width\s+-gt\s+790|\$dlg\.Height\s+-gt\s+660'}
)
foreach($check in $checks){
 $file=Join-Path $PSScriptRoot $check.File
 if(!(Test-Path $file)){throw "Teste obrigatorio ausente: $($check.File)"}
 $source=[IO.File]::ReadAllText($file)
 if($source -match $check.Pattern){throw "Regressao: limite antigo de janela em $($check.File)"}
 if(!$source.Contains('WorkingArea')){throw "Falta validar area util do monitor em $($check.File)"}
}
$pdv=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'teste-pdv-layout.ps1'))
foreach($name in @('amzgPdvBrand','amzgPdvSearchBar','amzgPdvItemBox','amzgPdvActions','amzgPdvTotal')){
 if(!$pdv.Contains($name)){throw "Teste PDV perdeu controle obrigatorio: $name"}
}
Write-Host 'Contratos de regressao visual OK.'
