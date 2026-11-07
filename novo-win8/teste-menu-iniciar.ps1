param(
 [Parameter(Mandatory=$true)][string]$Executable,
 [Parameter(Mandatory=$true)][string]$PreviewFile
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$exe=[IO.Path]::GetFullPath($Executable)
$preview=[IO.Path]::GetFullPath($PreviewFile)
if(!(Test-Path -LiteralPath $exe)){throw "PDV nao encontrado: $exe"}
[Environment]::CurrentDirectory=[IO.Path]::GetDirectoryName($exe)
[System.Windows.Forms.Application]::EnableVisualStyles()
$assembly=[System.Reflection.Assembly]::LoadFrom($exe)
$type=$assembly.GetType('ArmazemGratidao.MainForm',$true)
$form=$null
try{
 $form=[Activator]::CreateInstance($type)
 $form.StartPosition='Manual'
 $form.Location=[System.Drawing.Point]::new(22,22)
 $form.Size=[System.Drawing.Size]::new(1180,750)
 $form.Show()
 [System.Windows.Forms.Application]::DoEvents()
 # Falha antiga: painel Start fixado na lateral.
 if(@($form.Controls.Find('amzgStartPanel',$true)).Count -gt 0){
  throw 'Falha: ainda existe barra lateral fixa em vez do menu flutuante.'
 }
 # Regressao v1.4 build 597: a calculadora nao pode ocupar a pagina inicial.
 if(@($form.Controls.Find('amzgHomeCalculator',$true)).Count -ne 0){
  throw 'Calculadora antiga continua fixa na pagina principal.'
 }
 $calcButton=@($form.Controls.Find('amzgOpenCalculator',$true))
 if($calcButton.Count -ne 1 -or !$calcButton[0].Visible){
  throw 'Botao especial Calculadora ausente no cabecalho.'
 }
 $homePanel=@($form.Controls.Find('homePanel',$true)) | Select-Object -First 1
 if($null -eq $homePanel -or
   @($homePanel.Controls | Where-Object {$_.Name -eq 'amzgDashboardCanvas'}).Count -ne 1){
  throw 'Dashboard sem a nova tela inicial.'
 }
 if(@($form.Controls.Find('amzgHomeCalculator',$true)).Count -gt 0){
  throw 'Calculadora fixa encontrada na tela inicial.'
 }
 if(@($homePanel.Controls | Where-Object {$_.GetType().Name -eq 'BrandedHomePanel'}).Count -gt 0){
  throw 'Nao removeram a marca dagua do fundo.'
 }
 $rankingLeft=@($form.Controls.Find('amzgRankingLeft',$true))
 $rankingRight=@($form.Controls.Find('amzgRankingRight',$true))
 if($rankingLeft.Count -ne 1 -or $rankingRight.Count -ne 1){
  throw 'Ranking com duas listas de 10 produtos nao encontrado.'
 }
 if($rankingLeft[0].Columns.Count -ne 3 -or $rankingRight[0].Columns.Count -ne 3 -or
   $rankingLeft[0].Columns[2].HeaderText -ne 'Qtd. total' -or
   $rankingRight[0].Columns[2].HeaderText -ne 'Qtd. total'){
  throw 'Ranking nao possui coluna de quantidade total.'
 }
 $rankPanel=@($form.Controls.Find('amzgDashboardRankingPanel',$true))
 if($rankPanel.Count -ne 1 -or $rankPanel[0].Height -gt 330){
  throw 'Ranking nao ficou compacto.'
 }
 $windowTitle=@($form.Controls.Find('amzgHomeTitle',$true))
 if($windowTitle.Count -ne 1 -or ($windowTitle[0].Text -notmatch 'Armaz' -or $windowTitle[0].Text -notmatch 'Gratid')){
  throw 'Titulo Armazem Gratidao desapareceu ou foi cortado.'
 }
 # Novo painel inicial: calculadora somente quando acionada no cabecalho.
 if(@($form.OwnedForms | Where-Object {$_.Name -eq 'amzgModernCalculator'}).Count -ne 0){
  throw 'A calculadora apareceu automaticamente, contrariando o modo oculto.'
 }
 $stats=@($form.Controls.Find('amzgDashboardMetrics',$true))
 if($stats.Count -ne 1 -or @($stats[0].Controls | Where-Object {
    $_.Name -like 'amzgDashboardMetric*'
   }).Count -ne 4){throw 'Painel novo nao tem os quatro indicadores.'}
 $quick=@($form.Controls.Find('amzgDashboardTiles',$true))
 if($quick.Count -ne 1 -or @($quick[0].Controls | Where-Object {
    $_.Name -like 'amzgDashboardTile*'
   }).Count -ne 10){throw 'Faltam atalhos compactos do novo painel.'}
 if(@($form.Controls.Find('amzgDashboardStart',$true)).Count -ne 1){
  throw 'Botao Iniciar do painel nao encontrado.'
 }
 $folder=[IO.Path]::GetDirectoryName($preview)
 [IO.Directory]::CreateDirectory($folder) | Out-Null
 $homePreview=Join-Path $folder 'Previa-Inicio-Marca-Dagua.png'
 $homeBitmap=[System.Drawing.Bitmap]::new($homePanel.ClientSize.Width,$homePanel.ClientSize.Height)
 try{
  $homePanel.DrawToBitmap($homeBitmap,[System.Drawing.Rectangle]::new(
    [System.Drawing.Point]::Empty,$homePanel.ClientSize))
  $homeBitmap.Save($homePreview,[System.Drawing.Imaging.ImageFormat]::Png)
 }finally{$homeBitmap.Dispose()}
 if((Get-Item -LiteralPath $homePreview).Length -lt 5000){
  throw 'Previa do Inicio incompleta.'
 }
 $calcButton[0].PerformClick()
 [System.Windows.Forms.Application]::DoEvents()
 $calculator=@($form.OwnedForms | Where-Object {
   $_.Name -eq 'amzgModernCalculator'
 }) | Select-Object -First 1
 if($null -eq $calculator -or !$calculator.Visible){
  throw 'Botao calculadora nao abriu a janela independente.'
 }
 $keypad=@($calculator.Controls.Find('amzgCalcKeys',$true)) | Select-Object -First 1
 $calcDisplay=@($calculator.Controls.Find('amzgCalcDisplay',$true)) | Select-Object -First 1
 if($null -eq $keypad -or $null -eq $calcDisplay -or $keypad.Controls.Count -ne 24){
  throw 'Calculadora moderna sem teclas ou visor.'
 }
 $press={param([string]$Key)
  $target=@($keypad.Controls | Where-Object {$_.Tag -eq $Key}) |
   Select-Object -First 1
  if($null -eq $target){throw "Tecla ausente na calculadora: $Key"}
  $target.PerformClick()
 }
 $frame=@($calculator.Controls.Find('amzgCalcDisplayFrame',$true)) |
  Select-Object -First 1
 if($null -eq $frame -or !$frame.Visible -or $frame.Height -lt 60 -or
    -not ($calcDisplay -is [System.Windows.Forms.Label])){
  throw 'Visor da calculadora continua cortado ou sem quadro proprio.'
 }
 & $press 'C'
 & $press '1'
 if($calcDisplay.Text -ne '1'){throw "Primeiro numero invisivel: $($calcDisplay.Text)"}
 & $press '2'
 if($calcDisplay.Text -ne '12'){throw "Segundo numero invisivel: $($calcDisplay.Text)"}
 # O numero deve estar pintado no visor, nao somente guardado em memoria.
 $render=[Drawing.Bitmap]::new($calcDisplay.ClientSize.Width,$calcDisplay.ClientSize.Height)
 try{
  $calcDisplay.DrawToBitmap($render,[Drawing.Rectangle]::new(
   [Drawing.Point]::Empty,$calcDisplay.ClientSize))
  $pixels=0
  for($py=12;$py -lt ($render.Height-12);$py+=2){
   for($px=12;$px -lt ($render.Width-12);$px+=2){
    $p=$render.GetPixel($px,$py)
    if($p.R -lt 160 -and $p.G -lt 170 -and $p.B -lt 210){$pixels++}
   }
  }
  if($pixels -lt 8){throw "Digitos nao foram pintados no visor ($pixels pixels)."}
 }finally{$render.Dispose()}
 & $press 'C'
 foreach($key in @('9','÷','2','=')){& $press $key}
 if($calcDisplay.Text -ne '4,5'){throw "Erro em 9/2: $($calcDisplay.Text)"}
 & $press 'C'
 foreach($key in @('6','×','7','=')){& $press $key}
 if($calcDisplay.Text -ne '42'){throw "Erro em 6x7: $($calcDisplay.Text)"}
 & $press 'C'
 foreach($key in @('1','0','−','3','=')){& $press $key}
 if($calcDisplay.Text -ne '7'){throw "Erro em 10-3: $($calcDisplay.Text)"}
 & $press 'C'
 foreach($key in @('1','0','0','+','1','0','%','=')){& $press $key}
 if($calcDisplay.Text -ne '110'){throw "Erro em 100 + 10%: $($calcDisplay.Text)"}
 $calcPreview=Join-Path $folder 'Previa-Calculadora.png'
 $calcBitmap=[System.Drawing.Bitmap]::new($calculator.ClientSize.Width,$calculator.ClientSize.Height)
 try{
  $calculator.DrawToBitmap($calcBitmap,[System.Drawing.Rectangle]::new(
   [System.Drawing.Point]::Empty,$calculator.ClientSize))
  $calcBitmap.Save($calcPreview,[System.Drawing.Imaging.ImageFormat]::Png)
 }finally{$calcBitmap.Dispose()}
 if((Get-Item -LiteralPath $calcPreview).Length -lt 5000){
  throw 'Previa da calculadora incompleta.'
 }
 $calcButton[0].PerformClick()
 [System.Windows.Forms.Application]::DoEvents()
 if(@($form.OwnedForms | Where-Object {$_.Name -eq 'amzgModernCalculator' -and
   $_.Visible}).Count -ne 1){throw 'Segundo clique abriu calculadora duplicada.'}
 $calculator.Close()
 Write-Host 'CALCULADORA OK: janela unica, 24 teclas e porcentagem comercial 100+10%=110.'
 Write-Host "PREVIA VISUAL CALCULADORA: $calcPreview"
 Write-Host "PREVIA VISUAL MARCA DAGUA: $homePreview"
 if($form.Text -notmatch [regex]::Escape($env:AMZG_VERSION) -or
    $form.Text -notmatch ('(?i)build\s+'+$env:AMZG_BUILD)){
   throw "Versao incorreta no PDV: $($form.Text)"
 }
 $toggle=$type.GetMethod('ToggleStartMenu',
  [System.Reflection.BindingFlags]::Instance -bor
  [System.Reflection.BindingFlags]::NonPublic)
 if($null -eq $toggle){throw 'Botao Iniciar nao esta ligado ao menu.'}
 $toggle.Invoke($form,@()) | Out-Null
 [System.Windows.Forms.Application]::DoEvents()
 $popup=@($form.OwnedForms | Where-Object {$_.Name -eq 'amzgFloatingStartMenu'}) |
   Select-Object -First 1
 if($null -eq $popup -or !$popup.Visible){throw 'Botao Iniciar nao abre janela flutuante.'}
 $categories=@($popup.Controls.Find('amzgStartCategories',$true))
 $branch=@($popup.Controls.Find('amzgStartFlyout',$true))
 $modules=@($popup.Controls.Find('amzgStartModules',$true))
 $search=@($popup.Controls.Find('amzgStartSearch',$true))
 if($categories.Count -ne 1 -or $branch.Count -ne 1 -or
    $modules.Count -ne 1 -or $search.Count -ne 1){
  throw 'Menu flutuante sem categorias, pesquisa ou submenu.'
 }
 $categoryCount=[int]$popup.GetType().GetProperty('CategoryCount').GetValue($popup,$null)
 $moduleCount=[int]$popup.GetType().GetProperty('ModuleCount').GetValue($popup,$null)
 if($categoryCount -ne 5 -or $moduleCount -lt 30){
  throw "Menu incompleto: $categoryCount categorias, $moduleCount modulos."
 }
 foreach($button in @($categories[0].Controls)){
  if($button.Width -lt 260 -or $button.Height -lt 64 -or
     $null -eq $button.Image -or $button.Image.Width -lt 38){
   throw 'Botao de categoria pequeno ou sem icone grande.'
  }
 }
 $isBranch=[bool]$popup.GetType().GetProperty('IsBranchOpen').GetValue($popup,$null)
 if($isBranch){throw 'Submenu deve estar fechado antes de escolher secao.'}
 $select=$popup.GetType().GetMethod('SelectSection')
 if($null -eq $select){throw 'Categorias nao podem abrir o submenu.'}
 $select.Invoke($popup,@('VENDAS E ATENDIMENTO')) | Out-Null
 [System.Windows.Forms.Application]::DoEvents()
 if(!$branch[0].Visible){throw 'Clicar em Vendas e Atendimento nao abriu submenu.'}
 $items=@($modules[0].Controls | ForEach-Object {$_.AccessibleDescription})
 # Assercoes ASCII: Windows PowerShell 5.1 interpreta acentos e separadores
 # quando recebe script UTF-8 sem BOM; as expressoes abaixo evitam falso erro.
 foreach($required in @('^PDV.*Nova venda$','^Vendas$','^Cascos retorn',
   '^Fiado$','^Clientes$','vendas anotadas$','^WhatsApp$')){
  if(@($items | Where-Object {$_ -match $required}).Count -eq 0){
   throw "Falta no submenu de vendas: $required. Encontrados: $($items -join '; ')"
  }
 }
 if(@($modules[0].Controls | Where-Object {$null -eq $_.Image -or
   $_.Image.Width -lt 38 -or $_.Width -lt 220 -or $_.Height -lt 75}).Count -gt 0){
  throw 'Modulo sem botao grande ou sem icone 40px.'
 }
 if(@($modules[0].Controls | Where-Object {
  $_.BackColor.B -lt 120 -or $_.ForeColor -ne [Drawing.Color]::White -or $null -eq $_.Region
 }).Count -gt 0){throw 'Submenu nao usa botoes azuis arredondados com icones brancos.'}

 $search[0].Text='produto'
 [System.Windows.Forms.Application]::DoEvents()
 $found=@($modules[0].Controls | ForEach-Object {$_.AccessibleDescription})
 if($found -notcontains 'Produtos'){throw 'Pesquisa nao encontrou Produtos.'}
 if($found -contains 'Fiado'){throw 'Pesquisa nao filtrou resultados.'}
 $search[0].Clear()
 [System.Windows.Forms.Application]::DoEvents()
 $select.Invoke($popup,@('PRODUTOS E ESTOQUE')) | Out-Null
 $goods=@($modules[0].Controls | ForEach-Object {$_.AccessibleDescription})
 if($goods -notcontains 'Produtos' -or $goods -notcontains 'Estoque'){
  throw 'Submenu Produtos e Estoque incompleto.'
 }
 # Capturar a janela real: popup aberto com icones, nao uma imagem inventada.
 $select.Invoke($popup,@('VENDAS E ATENDIMENTO')) | Out-Null
 [System.Windows.Forms.Application]::DoEvents()
 [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($preview)) | Out-Null
 $bitmap=[System.Drawing.Bitmap]::new($popup.ClientSize.Width,$popup.ClientSize.Height)
 try{
  $area=[System.Drawing.Rectangle]::new([System.Drawing.Point]::Empty,$popup.ClientSize)
  $popup.DrawToBitmap($bitmap,$area)
  $bitmap.Save($preview,[System.Drawing.Imaging.ImageFormat]::Png)
 }finally{$bitmap.Dispose()}
 if((Get-Item -LiteralPath $preview).Length -lt 5000){
  throw 'Previa do menu flutuante foi gerada incompleta.'
 }
 $toggle.Invoke($form,@()) | Out-Null
 [System.Windows.Forms.Application]::DoEvents()
 if(@($form.OwnedForms | Where-Object {$_.Name -eq 'amzgFloatingStartMenu' -and $_.Visible}).Count -gt 0){
  throw 'Botao Iniciar nao fechou o menu flutuante.'
 }
 Write-Host "MENU WINDOWS OK: $categoryCount categorias, $moduleCount modulos com icones, submenu Vendas/Estoque, pesquisa e fechamento."
 Write-Host "PREVIA VISUAL: $preview"
}finally{
 if($null -ne $form){try{$form.Close()}catch{};$form.Dispose()}
}
