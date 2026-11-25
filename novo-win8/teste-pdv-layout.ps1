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
$database=Join-Path $env:RUNNER_TEMP ('pdv-layout-'+[Guid]::NewGuid().ToString('N')+'.db')
$seed=@'
import sqlite3,sys
c=sqlite3.connect(sys.argv[1])
c.execute("CREATE TABLE products(id INTEGER PRIMARY KEY, name TEXT, price REAL, stock REAL, barcode TEXT, internal_code TEXT, cost REAL DEFAULT 0, sold_by_weight INTEGER DEFAULT 0)")
c.execute("INSERT INTO products(id,name,price,stock,barcode,internal_code,cost) VALUES(1,'Produto de teste',10,100,'7890000000001','1',5)")
c.execute("INSERT INTO products VALUES(2,'Salsicha',12,100,'7890000000002','2',6,0)")
c.execute("INSERT INTO products VALUES(3,'Arroz 5 kg',25,100,'7890000000003','3',15,0)")
c.execute("INSERT INTO products VALUES(4,'Queijo colonial',40,100,'7890000000004','4',20,1)")
c.commit()
c.close()
'@
$seed | python - $database
if($LASTEXITCODE -ne 0){throw 'Falha ao criar banco de teste PDV'}
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
 if(@($pdv.Controls.Find('amzgOpenCalculatorModule',$true)).Count -ne 0){throw 'Calculadora deve estar no menu ocultavel'}
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
 $backButton=& $find 'amzgPdvBackToMenu'
 if($backButton.Parent -ne $back -or $backButton.Dock -ne [Windows.Forms.DockStyle]::None -or
    $backButton.Width -gt 150 -or $backButton.Height -gt 26 -or $backButton.Height -lt 18 -or
    $backButton.Top -lt 0 -or $backButton.Bottom -gt $back.ClientSize.Height){
  throw "PDV: botao Menu Inicial fora do padrao: $($backButton.Bounds), faixa=$($back.ClientSize)"
 }
 $natural=[Windows.Forms.TextRenderer]::MeasureText($backButton.Text,$backButton.Font).Width
 if($natural -gt $backButton.ClientSize.Width){throw 'PDV: texto do botao Menu Inicial cortado.'}
 $originalSize=$pdv.Size
 try{
  $pdv.Width=[Math]::Max($pdv.MinimumSize.Width,$pdv.Width-100)
  [Windows.Forms.Application]::DoEvents()
  if($backButton.Width -gt 150 -or $backButton.Bottom -gt $back.ClientSize.Height -or $backButton.Right -gt $back.ClientSize.Width){
   throw 'PDV: botao Menu Inicial nao se ajustou ao redimensionar.'
  }
 }finally{$pdv.Size=$originalSize;[Windows.Forms.Application]::DoEvents()}
 $search=& $find 'amzgPdvSearchBar'
 $code=& $find 'amzgPdvCodeInput'
 $list=& $find 'amzgPdvItemBox'
 $actions=& $find 'amzgPdvActions'
 $finish=& $find 'amzgPdvFinish'
 $total=& $find 'amzgPdvTotal'
 $other=& $find 'amzgPdvMoreOptions'
 $fiado=& $find 'amzgPdvFiado'
 if($fiado.Visible){throw 'Fiado deve ficar oculto no menu'}
 if(!$mainType.GetMethod('OpenFiadoFromPdv')){throw 'Navegacao Fiado ausente'}
 $grid=& $find 'amzgPdvWideGrid'
 if($null -eq $grid){throw 'Tabela de itens ausente'}
 $layout=& $find 'amzgPdvLayout'
 $ratio=$list.Width/($list.Width+$actions.Width)
 if($ratio -lt 0.68 -or $ratio -gt 0.72){throw "Proporcao 70/30 incorreta: $ratio"}
 if($list.Height -lt ($layout.ClientSize.Height*0.72)){throw 'Quadro nao ocupa altura util'}
 if($back.PointToScreen([Drawing.Point]::Empty).Y -lt $search.PointToScreen([Drawing.Point]::Empty).Y -or $back.PointToScreen([Drawing.Point]::Empty).Y -gt $search.PointToScreen([Drawing.Point]::Empty).Y+$search.Height){throw 'Menu inicial fora da linha de entrada'}
 if($finish.Parent -ne $actions -or $total.Parent.Parent -ne $actions -or $actions.Controls.Count -ne 2){throw 'Somente total e finalizar devem estar na lateral direita'}
 if($finish.Top -lt $total.Parent.Bottom -or $finish.BackColor.R -le $finish.BackColor.B){throw 'Finalizar deve ficar fixo abaixo do total'}
 if($other.Parent -ne $brand -or !$other.Visible){throw 'Menu ocultavel ausente no cabecalho'}
 if((& $find 'amzgPdvAddProduct').Visible -or (& $find 'amzgPdvSaleStats').Visible){throw 'Inserir e contador devem ficar ocultos'}
 # Cabeçalhos e largura são obrigatórios mesmo sem itens ou com apenas um produto.
 $assertGrid={
  if(!$grid.ColumnHeadersVisible -or $grid.ColumnHeadersHeight -lt 34 -or $grid.CellBorderStyle -ne [Windows.Forms.DataGridViewCellBorderStyle]::None){throw 'Cabecalho ausente/baixo ou linhas de grade indevidas'}
  $expected=@('Codigo','Produto','Unidade','Quantidade','Preco','Total','ExcluirItem')
  $visible=@($grid.Columns | Where-Object {$_.Visible} | Sort-Object DisplayIndex)
  if(($visible.Name -join ',') -ne ($expected -join ',')){throw 'Colunas obrigatorias ausentes ou fora da ordem'}
  $captions=@(('C'+[char]0x00f3+'digo de barras'),('Descri'+[char]0x00e7+[char]0x00e3+'o do produto'),'Un.','Qtd.','Valor unit. (R$)','Total (R$)','Excluir')
  if(($visible.HeaderText -join '|') -ne ($captions -join '|')){throw "Titulos de colunas incorretos: [$($visible.HeaderText -join '|')] esperado [$($captions -join '|')]"}
  $sum=($visible | Measure-Object -Property Width -Sum).Sum
  if($sum -lt ($grid.ClientSize.Width-25) -or $sum -gt ($grid.ClientSize.Width+2)){throw "Colunas nao preenchem a largura: $sum / $($grid.ClientSize.Width)"}
  $title=& $find 'amzgPdvListTitle'
  if($title.PointToScreen([Drawing.Point]::Empty).Y+$title.Height -gt $grid.PointToScreen([Drawing.Point]::Empty).Y){throw 'Titulo cobre o cabecalho'}
 }
 & $assertGrid
 # Popular uma tabela descartavel com 24 produtos para medir a capacidade real.
 $originalData=$grid.DataSource
 try{
  $sample=$originalData.Clone()
  $null=$sample.Rows.Add('7894900011609','Coca-Cola 600 ml',[decimal]1,'UN',[decimal]6.5,[decimal]6.5)
  $grid.DataSource=$sample
  [Windows.Forms.Application]::DoEvents()
  & $assertGrid
  $sample.Rows.Clear()
  for($i=1;$i -le 24;$i++){
   $null=$sample.Rows.Add([string]("789000000"+$i.ToString("0000")),("Produto de teste "+$i),[decimal]1,"UN",[decimal]5.99,[decimal]5.99)
  }
  $grid.DataSource=$sample
  [Windows.Forms.Application]::DoEvents()
  & $assertGrid
  $visibleRows=$grid.DisplayedRowCount($false)
  if($visibleRows -lt 15){throw "PDV precisa mostrar pelo menos 15 linhas completas: $visibleRows"}
  if($finish.PointToScreen([Drawing.Point]::Empty).Y -lt $total.Parent.PointToScreen([Drawing.Point]::Empty).Y+$total.Parent.Height){throw 'Finalizar encoberto pelo total.'}
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
 # Recebido e troco ficam ocultos no PDV; o modal de dinheiro abaixo testa a digitacao real.
 $receivedBox=& $find 'amzgPdvReceived'
 $changeBox=& $find 'amzgPdvChange'
 if($receivedBox.Visible -or $changeBox.Visible){throw 'Recebido e troco devem aparecer somente no pagamento'}
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
 # Exercer o editor real e validar a persistencia no banco descartavel.
 $private=[Reflection.BindingFlags]'Instance,NonPublic'
 $itemType=$pdvType.GetNestedType('Item',[Reflection.BindingFlags]::NonPublic)
 $cart=$pdvType.GetField('cart',$private).GetValue($pdv)
 $item=[Activator]::CreateInstance($itemType,$true)
 foreach($entry in @(@('Id',[long]1),@('Name','Produto de teste'),@('Barcode','7890000000001'),@('InternalCode','1'),@('Qty',[decimal]2),@('Price',[decimal]10),@('Cost',[decimal]5),@('Unit','UN'))){
  $itemType.GetField([string]$entry[0]).SetValue($item,$entry[1])
 }
 $cart.Add($item)
 $item2=[Activator]::CreateInstance($itemType,$true)
 foreach($entry in @(@('Id',[long]3),@('Name','Arroz 5 kg'),@('Barcode','7890000000003'),@('InternalCode','3'),@('Qty',[decimal]1),@('Price',[decimal]25),@('Cost',[decimal]15),@('Unit','UN'))){
  $itemType.GetField([string]$entry[0]).SetValue($item2,$entry[1])
 }
 $cart.Add($item2)
 $pdvType.GetMethod('Render',$private).Invoke($pdv,$null)|Out-Null
 $grid.CurrentCell=$grid.Rows[0].Cells['Produto']
 $edit=$pdvType.GetMethod('EditSelected',$private)
 $script:editDone=$false;$script:editError=''
 $editTimer=New-Object Windows.Forms.Timer
 $editTimer.Interval=150
 $editTimer.Add_Tick({
  $dlg=@([Windows.Forms.Application]::OpenForms | Where-Object {$_.Text -eq 'Editar produto da venda'})|Select-Object -First 1
  if(!$dlg){return}
  $editTimer.Stop()
  try{
   $table=$dlg.Controls.Find('amzgEditCartGrid',$true)[0];$save=$dlg.Controls.Find('amzgEditCartSave',$true)[0]
   if($table.Rows.Count -ne 2){throw 'Editor deve listar todos os produtos do carrinho'}
   $table.EndEdit()|Out-Null
   $table.Rows[0].Cells['Quantidade'].Value=[decimal]3
   $table.Rows[0].Cells['Preco'].Value=[decimal]12.5
   $table.Rows[0].Cells['Desconto'].Value=[decimal]2.5
   $table.Rows[1].Cells['Produto'].Value='Arroz corrigido 5 kg'
   $table.Rows[1].Cells['Tipo'].Value='%'
   $table.Rows[1].Cells['Desconto'].Value=[decimal]10
   $save.PerformClick();$script:editDone=$true
  }catch{$script:editError=$_.Exception.Message;$dlg.DialogResult=[Windows.Forms.DialogResult]::Cancel;$dlg.Close()}
 })
 try{$editTimer.Start();$edit.Invoke($pdv,$null)|Out-Null}finally{$editTimer.Stop();$editTimer.Dispose()}
 if($script:editError){throw $script:editError}
 if(!$script:editDone -or $itemType.GetField('Price').GetValue($item) -ne [decimal]12.5 -or $itemType.GetProperty('Total').GetValue($item,$null) -ne [decimal]35){throw 'Edicao do carrinho ou desconto incorretos'}
 if($itemType.GetProperty('Total').GetValue($item2,$null) -ne [decimal]22.5 -or $itemType.GetField('Name').GetValue($item2) -ne 'Arroz corrigido 5 kg'){throw 'Segundo produto nao foi atualizado'}
 if(!($grid.Columns['ExcluirItem'] -is [Windows.Forms.DataGridViewButtonColumn])){throw 'Botao de exclusao individual ausente'}
 $check=@'
import sqlite3,sys
c=sqlite3.connect(sys.argv[1])
assert c.execute("SELECT price,stock FROM products WHERE id=1").fetchone()==(12.5,100)
assert c.execute("SELECT previous_price,new_price FROM product_price_audit WHERE product_id=1").fetchone()==(10,12.5)
assert c.execute("SELECT name,price,stock FROM products WHERE id=3").fetchone()==("Arroz corrigido 5 kg",25,100)
c.close()
'@
 $check | python - $database
 if($LASTEXITCODE -ne 0){throw 'Preco nao persistiu com auditoria ou estoque mudou antes da venda'}
 $cart.Clear();$pdvType.GetMethod('Render',$private).Invoke($pdv,$null)|Out-Null
 $weightMethod=$pdvType.GetMethod('IsWeightSale',[Reflection.BindingFlags]'Static,NonPublic')
 foreach($ex in @(
  @{name='Salsicha';flag=$false;expected=$true},
  @{name='Linguiça';flag=$false;expected=$true},
  @{name='Calabresa';flag=$false;expected=$true},
  @{name='Frango inteiro';flag=$false;expected=$true},
  @{name='Linguiça calabresa quilo';flag=$false;expected=$true},
  @{name='Queijo / kg';flag=$false;expected=$true},
  @{name='Queijo colonial';flag=$true;expected=$true},
  @{name='Arroz 5 kg';flag=$false;expected=$false},
  @{name='Calabresa pacote 1 kg';flag=$false;expected=$false},
  @{name='Salsicha 500 g';flag=$false;expected=$false},
  @{name='Coca-Cola 600 ml';flag=$false;expected=$false}
 )){
  if([bool]$weightMethod.Invoke($null,[object[]]@([string]$ex.name,[bool]$ex.flag)) -ne [bool]$ex.expected){throw "Unidade incorreta: $($ex.name)"}
 }
 $unit=& $find 'amzgPdvUnitInput'
 $qty=& $find 'amzgPdvQtyInput'
 $lookup=$pdvType.GetMethod('FindSuggestions',$private)
 $addProduct=$pdvType.GetMethod('AddProduct',$private)
 $code.Text='7890000000002'
 $lookup.Invoke($pdv,$null)|Out-Null
 if($unit.SelectedItem -ne 'KG' -or $qty.DecimalPlaces -ne 3){throw 'Busca nao selecionou KG e tres decimais'}
 $qty.Value=[decimal]0.375
 $addProduct.Invoke($pdv,[object[]]@($false))|Out-Null
 if($cart.Count -ne 1 -or $itemType.GetField('Unit').GetValue($cart[0]) -ne 'KG' -or $itemType.GetProperty('Total').GetValue($cart[0],$null) -ne [decimal]4.50){throw 'Venda por peso incorreta'}
 if($unit.SelectedItem -ne 'UN'){throw 'Unidade nao retornou ao padrao'}
 $code.Text='7890000000003';$lookup.Invoke($pdv,$null)|Out-Null
 if($unit.SelectedItem -ne 'UN'){throw 'Pacote de arroz foi interpretado como peso'}
 $code.Text='7890000000004';$lookup.Invoke($pdv,$null)|Out-Null
 if($unit.SelectedItem -ne 'KG'){throw 'Cadastro por peso ignorado'}
 $code.Clear();$cart.Clear();$pdvType.GetMethod('Render',$private).Invoke($pdv,$null)|Out-Null
 Write-Host 'PDV PESO OK: descricao, cadastro, pacote e 375 g.'
 Write-Host 'PDV OK: quadro 70%, entrada superior, menu oculto, total e finalizar fixos; edicao e preco persistente testados.'
 Write-Host "PREVIA REAL PDV: $preview"
}finally{
 if($null -ne $pdv){try{$pdv.Close()}catch{};$pdv.Dispose()}
 if($null -ne $main){try{$main.Close()}catch{};$main.Dispose()}
}
