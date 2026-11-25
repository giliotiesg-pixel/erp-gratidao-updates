param(
 [Parameter(Mandatory=$true)][string]$Executable,
 [Parameter(Mandatory=$true)][string]$PreviewFile
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$exe=[IO.Path]::GetFullPath($Executable)
$preview=[IO.Path]::GetFullPath($PreviewFile)
[Environment]::CurrentDirectory=[IO.Path]::GetDirectoryName($exe)
[System.Windows.Forms.Application]::EnableVisualStyles()
$asm=[System.Reflection.Assembly]::LoadFrom($exe)
$pdvType=$asm.GetType('ArmazemGratidao.PdvForm',$true)
$mainType=$asm.GetType('ArmazemGratidao.MainForm',$true)
$main=$null;$pdv=$null
$database=Join-Path $env:RUNNER_TEMP 'pdv-layout-dados-descartaveis.db'
try{
 $main=[Activator]::CreateInstance($mainType)
 # Indicador visual persistente, independente do rodape e sem caixas de mensagem.
 $indicator=$mainType.GetMethod('SetUpdateIndicator',[Reflection.BindingFlags]'Instance,NonPublic')
 $installed=$mainType.GetProperty('InstalledBuild',[Reflection.BindingFlags]'Instance,NonPublic').GetValue($main,$null)
 $headerUpdate=@($main.Controls.Find('amzgUpdateHeader',$true))[0]
 $homeUpdate=@($main.Controls.Find('amzgDashboardTile9',$true))[0]
 if(!$indicator -or !$headerUpdate -or !$homeUpdate){throw 'Indicador de atualizacao ausente no cabecalho ou painel.'}
 $indicator.Invoke($main,[object[]]@([int]($installed+1)))|Out-Null
 if($headerUpdate.Text -notlike '*dispon*' -or $homeUpdate.Text -notlike '*dispon*' -or $headerUpdate.BackColor -ne $homeUpdate.BackColor){throw 'Atualizacao disponivel nao foi sinalizada.'}
 $indicator.Invoke($main,[object[]]@([int]$installed))|Out-Null
 if($headerUpdate.Text -like '*dispon*' -or $homeUpdate.Text -like '*dispon*'){throw 'Indicador permaneceu ativo sem versao mais nova.'}
 # Usar o construtor publico de um argumento elimina ambiguidades do binder PowerShell 5.1.
 $ctor=$pdvType.GetConstructor([type[]]@([string]))
 if($null -eq $ctor){throw 'Construtor publico PdvForm(string) ausente.'}
 $invokePdv=New-Object 'System.Object[]' 1
 $invokePdv[0]=[string]$database
 $pdv=$ctor.Invoke($invokePdv)
 # O clique em FIADO deve usar uma selecao de cliente obrigatoria distinta.
 $private=[Reflection.BindingFlags]::Instance -bor [Reflection.BindingFlags]::NonPublic
 $fiadoPicker=$pdvType.GetMethod('ChooseCustomerInWindow',$private,$null,[type[]]@([bool]),$null)
 if($null -eq $fiadoPicker -or $fiadoPicker.ReturnType -ne [bool]){
  throw 'Fiado sem lista de clientes obrigatoria ao finalizar.'
 }
 # Reproduz a mesma injecao de navegacao feita ao abrir PDV via menu Iniciar.
 $flags=[System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::NonPublic
 $backMethod=$mainType.GetMethod('AddBackToMenu',$flags)
 if($null -eq $backMethod){throw 'MainForm sem botao Voltar.'}
 $invokeBack=New-Object 'System.Object[]' 1
 $invokeBack[0]=[System.Windows.Forms.Form]$pdv
 $backMethod.Invoke([object]$main,$invokeBack) | Out-Null
 $pdv.StartPosition='Manual'
 $pdv.Location=[System.Drawing.Point]::new(20,20)
 $pdv.Size=[System.Drawing.Size]::new(1180,740)
 $pdv.Show()
 [System.Windows.Forms.Application]::DoEvents()
 if(@($pdv.Controls.Find('amzgOpenCalculatorModule',$true)).Count -ne 1){throw 'Calculadora ausente no PDV'}
 $binding=[Reflection.BindingFlags]::Static -bor [Reflection.BindingFlags]::NonPublic
 $detect=$pdvType.GetMethod('IsCocaReturnableBottle',$binding)
 if($null -eq $detect){throw 'Detector de cascos ausente.'}
 foreach($example in @(
  @{p='Coca Cola 1 L retornavel vidro';b=$true},
  @{p='Coca-Cola Retornável 2 Litros';b=$true},
  @{p='Coca Cola 1 litro vidro';b=$true},
  @{p='Coca Cola 2L RET';b=$true},
  @{p='Coca Cola 1000 ml retornavel';b=$true},
  @{p='Coca Cola 2000ml retornavel';b=$true},
  @{p='Coca Cola 200 ml';b=$false},
  @{p='Coca Cola 600 ml';b=$false},
  @{p='Coca Cola 350 ml lata';b=$false},
  @{p='Coca Cola 2 L PET';b=$false},
  @{p='Coca Cola 2 L descartavel';b=$false},
  @{p='Coca Cola 2.5 L retornavel';b=$false},
  @{p='Guarana retornavel 2 L';b=$false},
  @{p='Coca Cola 1 L nao retornavel';b=$false}
 )){
  $actual=[bool]$detect.Invoke($null,[object[]]@([string]$example.p))
  if($actual -ne $example.b){throw "Casco incorreto: $($example.p) = $actual"}
 }
 $find={param([string]$name)
  $controls=@($pdv.Controls.Find($name,$true))
  if($controls.Count -ne 1){throw "Controle obrigatorio ausente ou duplicado: $name ($($controls.Count))"}
  return $controls[0]
 }
 $brand=& $find 'amzgPdvBrand'
 $back=& $find 'amzgBackBar'
 $search=& $find 'amzgPdvSearchBar'
 $code=& $find 'amzgPdvCodeInput'
 $list=& $find 'amzgPdvItemBox'
 $actions=& $find 'amzgPdvActions'
 $finish=& $find 'amzgPdvFinish'
 $summary=& $find 'amzgPdvSummary'
 $total=& $find 'amzgPdvTotal'
 $other=& $find 'amzgPdvMoreOptions'
 $fiado=& $find 'amzgPdvFiado'
 if(!$fiado.Visible -or !$fiado.Enabled -or $fiado.Text -ne 'Fiado' -or $fiado.Parent.Name -ne 'amzgPdvActionStack'){throw 'Botao Fiado precisa estar visivel nas acoes do PDV.'}
 if(!$mainType.GetMethod('OpenFiadoFromPdv')){throw 'Navegacao Fiado a partir do PDV ausente.'}
 $grid=@($pdv.Controls.Find('amzgPdvItemBox',$true)[0].Controls |
    Where-Object {$_ -is [System.Windows.Forms.DataGridView] -and $_.Visible}) |
    Select-Object -First 1
 if($null -eq $grid){throw 'Tabela de itens ausente no novo PDV.'}
 $screenY={param($control)
  return $control.PointToScreen([System.Drawing.Point]::Empty).Y
 }
 if((& $screenY $brand) -ge (& $screenY $back) -or
    (& $screenY $back) -ge (& $screenY $search) -or
    (& $screenY $search) -ge (& $screenY $list)){
  throw 'Ordem incorreta: marca, Voltar, Codigo/Nome e lista.'
 }
 if($actions.PointToScreen([System.Drawing.Point]::Empty).X -le
    $list.PointToScreen([System.Drawing.Point]::Empty).X+$list.Width){
  throw 'Botoes principais nao foram colocados na lateral direita.'
 }
 if((& $screenY $summary) -ge (& $screenY $search) -or
    (& $screenY $summary) -le (& $screenY $back)){throw 'Cliente, recebido e desconto devem ficar no topo.'}
 if($total.Parent.Name -ne 'amzgPdvTotalFrame' -or (& $screenY $total) -le (& $screenY $finish)){throw 'Total deve ficar abaixo do botao finalizar.'}
 $layout=& $find 'amzgPdvLayout'
 # O Windows limita a janela ao monitor real (inclusive o monitor virtual do CI).
 # Exigir proporcao de area, alem da altura minima que permita 15 linhas completas.
 $minHeight=[Math]::Max(392,[int][Math]::Floor($layout.ClientSize.Height*0.66))
 if($list.ClientSize.Width -lt 700 -or $actions.ClientSize.Width -gt 220 -or $list.Height -lt $minHeight -or $grid.RowTemplate.Height -gt 25){
  throw "Lista nao foi ampliada: $($list.Width)x$($list.Height), minimo=$minHeight, acoes=$($actions.Width), janela=$($layout.ClientSize)"
 }
 if($search.Height -gt 60){throw 'Busca de produtos alta demais.'}
 $stack=& $find 'amzgPdvActionStack'
 foreach($btn in $stack.Controls){
  if($btn.Height -ne $finish.Height -or $btn.Width -ne $finish.Width){throw "Botoes laterais devem ter tamanho identico: $($btn.Name)"}
 }
 if($finish.BackColor.R -le $finish.BackColor.B -or $stack.Controls.GetChildIndex($finish) -ne ($stack.Controls.Count-1)){throw 'Finalizar precisa ficar vermelho e por ultimo.'}
 # Popular uma tabela descartavel com 24 produtos para medir a capacidade real.
 $originalData=$grid.DataSource
 try{
  $sample=$originalData.Clone()
  for($i=1;$i -le 24;$i++){
   $null=$sample.Rows.Add([string]("789000000"+$i.ToString("0000")),("Produto de teste "+$i),[decimal]1,"UN",[decimal]5.99,[decimal]5.99)
  }
  $grid.DataSource=$sample
  [Windows.Forms.Application]::DoEvents()
  $visibleRows=$grid.DisplayedRowCount($false)
  if($visibleRows -lt 15){throw "PDV precisa mostrar pelo menos 15 linhas completas: $visibleRows"}
  if($finish.PointToScreen([Drawing.Point]::Empty).Y+$finish.Height -gt $total.Parent.PointToScreen([Drawing.Point]::Empty).Y){throw 'Finalizar encoberto pelo total.'}
  Write-Host "PDV VISUAL: $visibleRows linhas completas; lista $($list.Width)x$($list.Height)."
 }finally{$grid.DataSource=$originalData}
 foreach($name in @('amzgPdvOtherPayments','amzgPdvNewSale','amzgPdvWhatsApp','amzgPdvSaleStats')){
  $null=& $find $name
 }
 foreach($expected in @('amzgPdvAddProduct','amzgPdvMoreOptions',
    'amzgPdvCustomer','amzgPdvReceived','amzgPdvChange','amzgPdvTotal')){
  $null=& $find $expected
 }
 [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($preview)) | Out-Null
 $bitmap=[System.Drawing.Bitmap]::new($pdv.ClientSize.Width,$pdv.ClientSize.Height)
 try{
  $pdv.DrawToBitmap($bitmap,[System.Drawing.Rectangle]::new(
   [System.Drawing.Point]::Empty,$pdv.ClientSize))
  $bitmap.Save($preview,[System.Drawing.Imaging.ImageFormat]::Png)
 }finally{$bitmap.Dispose()}
 if((Get-Item -LiteralPath $preview).Length -lt 7000){
  throw 'Preview da tela do PDV incompleta.'
 }
 # Troco deve atualizar durante a digitacao, antes de confirmar ou sair do campo.
 $receivedBox=& $find 'amzgPdvReceived'
 $changeBox=& $find 'amzgPdvChange'
 $receivedBox.Text='20,50'
 if($changeBox.Text -ne 'TROCO: R$ 20,50'){throw 'Troco no rodape nao atualizou enquanto digitava.'}
 $receivedBox.Text='0,00'
 $cashMethod=$pdvType.GetMethod('ConfirmCashReceived',[Reflection.BindingFlags]'Instance,NonPublic')
 if(!$cashMethod){throw 'Modal de dinheiro ausente.'}
 $script:cashError='';$script:cashDone=$false
 $cashTimer=New-Object System.Windows.Forms.Timer
 $cashTimer.Interval=150
 $cashTimer.Add_Tick({
  $dlg=@([Windows.Forms.Application]::OpenForms | Where-Object {$_.Text -eq 'PDV | Pagamento em dinheiro'}) | Select-Object -First 1
  if(!$dlg){return}
  $cashTimer.Stop()
  try{
   $input=$dlg.Controls.Find('amzgCashReceived',$true)[0]
   $change=$dlg.Controls.Find('amzgCashChange',$true)[0]
   $button=$dlg.Controls.Find('amzgCashConfirm',$true)[0]
   $input.Text=''
   if($button.Enabled -or $change.Text -ne 'FALTA: R$ 37,50'){throw 'Campo vazio nao informa falta.'}
   $input.Text='5'
   if($button.Enabled -or $change.Text -ne 'FALTA: R$ 32,50'){throw 'Falta nao atualizou na primeira tecla.'}
   $input.Text='50'
   if(!$button.Enabled -or $change.Text -ne 'TROCO: R$ 12,50'){throw 'Troco nao atualizou durante digitacao.'}
   $input.Text='50,25'
   if($change.Text -ne 'TROCO: R$ 12,75'){throw 'Troco com centavos incorreto.'}
   $input.Text='37,50'
   if(!$button.Enabled -or $change.Text -ne 'TROCO: R$ 0,00'){throw 'Pagamento exato incorreto.'}
   $input.Text='abc'
   if($button.Enabled){throw 'Valor invalido aceito.'}
   $script:cashDone=$true
  }catch{$script:cashError=$_.Exception.Message}
  finally{$dlg.DialogResult=[Windows.Forms.DialogResult]::Cancel;$dlg.Close()}
 })
 try{
  $cashTimer.Start()
  $null=$cashMethod.Invoke($pdv,[object[]]@([decimal]37.50))
  if($script:cashError){throw $script:cashError}
  if(!$script:cashDone){throw 'Modal de dinheiro nao testado.'}
 }finally{$cashTimer.Stop();$cashTimer.Dispose()}
 # Testar de verdade o modal de pagamento dividido, inclusive a terceira linha.
 $splitMethod=$pdvType.GetMethod('SelectSplitPayments',[Reflection.BindingFlags]'Instance,NonPublic')
 if(!$splitMethod){throw 'Modal de pagamento dividido ausente.'}
 $script:splitError='';$script:splitDone=$false
 $splitTimer=New-Object System.Windows.Forms.Timer
 $splitTimer.Interval=150
 $splitTimer.Add_Tick({
  $dlg=@([Windows.Forms.Application]::OpenForms | Where-Object {$_.Text -eq 'Pagamento dividido (2 ou 3 formas)'}) | Select-Object -First 1
  if(!$dlg){return}
  $splitTimer.Stop()
  try{
   $two=$dlg.Controls.Find('amzgSplitTwo',$true)[0]
   $three=$dlg.Controls.Find('amzgSplitThree',$true)[0]
   $third=$dlg.Controls.Find('amzgSplitMethod3',$true)[0]
   $v1=$dlg.Controls.Find('amzgSplitValue1',$true)[0]
   $v2=$dlg.Controls.Find('amzgSplitValue2',$true)[0]
   $v3=$dlg.Controls.Find('amzgSplitValue3',$true)[0]
   $confirm=$dlg.Controls.Find('amzgSplitConfirm',$true)[0]
   if(!$two.Checked -or $third.Visible -or $v3.Visible){throw 'Terceira linha precisa ficar oculta em duas formas.'}
   $v1.Text='3'
   if($v2.Text -ne '97,00'){throw 'Primeira tecla nao atualizou o restante.'}
   $v1.Text='30,25'
   if($v2.Text -ne '69,75' -or !$v2.ReadOnly){throw 'Duas formas: restante incorreto.'}
   $three.Checked=$true
   [Windows.Forms.Application]::DoEvents()
   if(!$third.Visible -or !$third.Enabled -or $v2.ReadOnly -or !$v3.ReadOnly){throw 'Tres formas: terceira linha ou campos incorretos.'}
   if($v2.Text -ne '69,75'){throw 'Segunda forma deve iniciar com o restante sugerido.'}
   $v2.Text='20,10'
   if($v3.Text -ne '49,65'){throw 'Tres formas: restante incorreto durante digitacao.'}
   $v1.Text='95'
   if($confirm.Enabled){throw 'Pagamento acima do total foi aceito.'}
   $v1.Text='30,25'
   $two.Checked=$true
   if($third.Visible -or $v3.Visible -or $v2.Text -ne '69,75'){throw 'Retorno a duas formas incorreto.'}
   $script:splitDone=$true
  }catch{$script:splitError=$_.Exception.Message}
  finally{$dlg.DialogResult=[Windows.Forms.DialogResult]::Cancel;$dlg.Close()}
 })
 try{
  $splitTimer.Start()
  $null=$splitMethod.Invoke($pdv,[object[]]@([decimal]100))
  if($script:splitError){throw $script:splitError}
  if(!$script:splitDone){throw 'Modal dividido nao testado.'}
 }finally{$splitTimer.Stop();$splitTimer.Dispose()}
 Write-Host 'PDV OK: cliente, recebido e desconto no topo; lista ampla, botoes iguais e total na lateral.'
 Write-Host "PREVIA REAL PDV: $preview"
}finally{
 if($null -ne $pdv){try{$pdv.Close()}catch{};$pdv.Dispose()}
 if($null -ne $main){try{$main.Close()}catch{};$main.Dispose()}
}
