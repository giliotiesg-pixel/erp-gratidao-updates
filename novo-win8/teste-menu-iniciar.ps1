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
 if(@($form.Controls.Find('amzgHomeCalculator',$true)).Count -ne 1){
  throw 'Calculadora deixou de aparecer na pagina principal.'
 }
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
