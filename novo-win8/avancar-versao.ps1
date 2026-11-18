param([switch]$SomentePrevia)
$ErrorActionPreference='Stop'
$path=Join-Path $PSScriptRoot 'VERSAO.txt'
if(!(Test-Path -LiteralPath $path)){throw 'VERSAO.txt nao encontrado'}
$current=(Get-Content -LiteralPath $path -Raw).Trim()
if($current -notmatch '^([1-9][0-9]?)\.([0-9]{1,2})\.([1-9][0-9]{0,4})$'){
 throw 'VERSAO.txt deve seguir o formato 1.4.678'
}
$major=[int]$Matches[1]
$minor=[int]$Matches[2]
$build=[int]$Matches[3]
if($build -ge 65000){throw 'Limite do numero interno da build'}
# O numero do build e a unica parte que cresce a cada atualizacao.
# A versao major.minor so muda em atualizacoes planejadas, nunca implicitamente.
$next="$major.$minor.$($build+1)"
if($SomentePrevia){Write-Output $next;exit 0}
[System.IO.File]::WriteAllText($path,$next+"`n",[System.Text.Encoding]::ASCII)
Write-Host "Versao anterior: $current"
Write-Host "Proxima versao: $major.$minor (build $($build+1))"
