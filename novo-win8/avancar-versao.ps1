param([switch]$SomentePrevia)
$ErrorActionPreference='Stop'
$path=Join-Path $PSScriptRoot 'VERSAO.txt'
if(!(Test-Path $path)){throw 'VERSAO.txt nao encontrado'}
$current=(Get-Content -LiteralPath $path -Raw).Trim()
if($current -notmatch '^([1-9][0-9]?)\.([0-9]{1,2})\.([1-9][0-9]{0,4})$'){
 throw 'VERSAO.txt deve seguir o formato 1.1.593'
}
$major=[int]$Matches[1];$minor=[int]$Matches[2];$build=[int]$Matches[3]
if($build -ge 65534){throw 'Limite do numero interno da build'}
if($minor -ge 9){$major++;$minor=0}else{$minor++}
$next="$major.$minor.$($build+1)"
if($SomentePrevia){Write-Output $next;exit 0}
# Um unico arquivo e atualizado por versao; nenhum instalador precisa ser renumerado manualmente.
[System.IO.File]::WriteAllText($path,$next+"`n",[System.Text.Encoding]::ASCII)
Write-Host "Versao anterior: $current"
Write-Host "Proxima versao unificada: $major.$minor (build $($build+1))"
