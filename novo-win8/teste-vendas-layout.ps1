param(
 [Parameter(Mandatory=$true)][string]$Executable,
 [Parameter(Mandatory=$true)][string]$PreviewFile
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$exe=[IO.Path]::GetFullPath($Executable)
$preview=[IO.Path]::GetFullPath($PreviewFile)
if(!(Test-Path $exe)){throw "Executavel nao encontrado: $exe"}
$folder=[IO.Path]::GetDirectoryName($preview)
[IO.Directory]::CreateDirectory($folder) | Out-Null
$db=Join-Path $folder 'teste-vendas-descartavel.db'
if(Test-Path $db){Remove-Item -LiteralPath $db -Force}
$python=@'
import sys,sqlite3,datetime
conn=sqlite3.connect(sys.argv[1])
conn.executescript("""
CREATE TABLE sales (id INTEGER PRIMARY KEY, created_at TEXT, customer_name TEXT, payment_method TEXT, gross_total REAL);
CREATE TABLE sale_items (id INTEGER PRIMARY KEY,sale_id INTEGER,barcode TEXT,name TEXT,quantity REAL,unit_price REAL,total REAL);
""")
today=datetime.datetime.now().strftime("%Y-%m-%d")
conn.execute("INSERT INTO sales VALUES (1924,?,?,?,?)",(today+" 20:55:02","Cliente de teste","Dinheiro",56.24))
conn.execute("INSERT INTO sales VALUES (1923,?,?,?,?)",(today+" 20:21:45","Consumidor","Pix",31.48))
yesterday=(datetime.datetime.now()-datetime.timedelta(days=1)).strftime("%Y-%m-%d")
older=(datetime.datetime.now()-datetime.timedelta(days=2)).strftime("%Y-%m-%d")
conn.execute("INSERT INTO sales VALUES (1922,?,?,?,?)",(yesterday+" 10:00:00","Cliente A","Dinheiro",10.00))
conn.execute("INSERT INTO sales VALUES (1921,?,?,?,?)",(yesterday+" 15:00:00","Cliente B","Fiado",20.00))
conn.execute("INSERT INTO sales VALUES (1920,?,?,?,?)",(older+" 10:00:00","Cliente C","Pix",500.00))
conn.execute("INSERT INTO sale_items VALUES (1,1924,?,?,?,?,?)",("78901","Refrigerante de teste 2 L",2,6.50,13.00))
conn.execute("INSERT INTO sale_items VALUES (2,1924,?,?,?,?,?)",("78902","Biscoito de teste",3,14.413333,43.24))
conn.execute("INSERT INTO sale_items VALUES (3,1923,?,?,?,?,?)",("78903","Agua de teste",4,7.87,31.48))
conn.commit()
conn.close()
'@
$pythonFile=Join-Path $folder 'preparar-vendas-descartaveis.py'
[IO.File]::WriteAllText($pythonFile,$python,(New-Object System.Text.UTF8Encoding($false)))
python $pythonFile $db
if($LASTEXITCODE -ne 0 -or !(Test-Path $db)){throw 'Banco SQLite descartavel nao criado'}
[Environment]::CurrentDirectory=[IO.Path]::GetDirectoryName($exe)
[Windows.Forms.Application]::EnableVisualStyles()
$assembly=[Reflection.Assembly]::LoadFrom($exe)
$mainType=$assembly.GetType('ArmazemGratidao.MainForm',$true)
$salesType=$assembly.GetType('ArmazemGratidao.ConsultaForm',$true)
$main=$null
$form=$null
try{
 $main=[Activator]::CreateInstance($mainType)
 $constructor=$salesType.GetConstructor([Type[]]@([string],[bool]))
 if($null -eq $constructor){throw 'ConsultaForm nao possui construtor (string, bool).'}
 $form=$constructor.Invoke([object[]]@([string]$db,[bool]$false))
 $form.StartPosition='Manual'
 $form.Location=[Drawing.Point]::new(30,30)
 $form.Size=[Drawing.Size]::new(1280,780)
 $attach=$mainType.GetMethod('AddBackToMenu',[Reflection.BindingFlags]::NonPublic -bor [Reflection.BindingFlags]::Instance)
 if($null -eq $attach){throw 'Nao existe retorno padrao ao Inicio.'}
 $attach.Invoke($main,[object[]]@($form)) | Out-Null
 $form.Show()
 [Windows.Forms.Application]::DoEvents()
 foreach($name in @('amzgSalesLayout','amzgSalesBanner','amzgSalesNavigation',
  'amzgSalesFilters','amzgSalesListPanel','amzgSalesDetailPanel','amzgSalesTotalsBar',
  'amzgSalesNewSale','amzgSalesToday','amzgSalesPrevious','amzgSalesEdit',
  'amzgSalesDelete','amzgSalesReports','amzgSalesSearch','amzgSalesUpdate',
  'amzgSalesBackSlot','amzgBackBar')){
  if(@($form.Controls.Find($name,$true)).Count -ne 1){throw "Controle obrigatorio ausente: $name"}
 }
 $back=@($form.Controls.Find('amzgSalesBackSlot',$true))[0]
 if($back.Controls.Count -lt 1 -or
    $back.Parent.GetType().Name -ne 'TableLayoutPanel'){
  throw 'Voltar ao Menu Inicial nao esta na primeira linha superior.'
 }
 $grid=@($form.Controls.Find('amzgSalesListPanel',$true))[0].Controls |
  Where-Object {$_.GetType().Name -eq 'DataGridView'} | Select-Object -First 1
 $items=@($form.Controls.Find('amzgSalesDetailPanel',$true))[0].Controls |
  Where-Object {$_.GetType().Name -eq 'DataGridView'} | Select-Object -First 1
 if($null -eq $grid -or $null -eq $items -or
    $grid.Rows.Count -lt 2 -or $items.Rows.Count -lt 2){
  throw "Lista de vendas ou detalhes vazios na amostra: $($grid.Rows.Count) / $($items.Rows.Count)"
 }
 $visible=@($grid.Columns | Where-Object {$_.Visible})
 if($visible.Count -ne 5){throw "Esperadas cinco colunas de venda, encontradas: $($visible.Count)"}
 $total=@($form.Controls.Find('amzgSalesDetailSummary',$true))[0]
 $right=@($form.Controls.Find('amzgSalesDetailPanel',$true))[0]
 $left=@($form.Controls.Find('amzgSalesListPanel',$true))[0]
 if($total.Height -lt 85 -or $left.Width -lt 350 -or $right.Width -lt 350){
  throw 'Painel de venda fora do layout de duas colunas.'
 }
 $info=@($form.Controls.Find('amzgSalesInfoValue',$true))
 if($info.Count -ne 1 -or $info[0].Width -lt 165){
  throw 'Cliente, data ou pagamento estao espremidos no painel de detalhes.'
 }
 $flags=[Reflection.BindingFlags]::NonPublic -bor [Reflection.BindingFlags]::Instance
 $form.Controls.Find('amzgSalesPrevious',$true)[0].PerformClick()
 [Windows.Forms.Application]::DoEvents()
 $headerDay=$salesType.GetField('headerToday',$flags).GetValue($form)
 $labelDay=$salesType.GetField('headerDayCaption',$flags).GetValue($form)
 $footerDay=$salesType.GetField('footerToday',$flags).GetValue($form)
 if($headerDay.Text -notmatch 'R\$\s*30[,.]00' -or $labelDay.Text -ne 'Total da Data' -or
  $footerDay.Text -notmatch '30[,.]00'){throw "Data anterior incorreta: $($headerDay.Text) / $($footerDay.Text)"}
 $picker=$salesType.GetField('dateFilter',$flags).GetValue($form)
 $picker.Value=[DateTime]::Today.AddDays(-2)
 [Windows.Forms.Application]::DoEvents()
 if($headerDay.Text -notmatch 'R\$\s*500[,.]00' -or $grid.Rows.Count -ne 1){
  throw "Filtro de data nao atualizou o total: $($headerDay.Text) / linhas=$($grid.Rows.Count)"
 }
 # A data pode ser trocada repetidas vezes sem voltar ao botao Vendas Anteriores.
 $picker.Value=[DateTime]::Today.AddDays(-1)
 [Windows.Forms.Application]::DoEvents()
 if($headerDay.Text -notmatch 'R\$\s*30[,.]00' -or $grid.Rows.Count -ne 2){
  throw "Segunda selecao de data anterior incorreta: $($headerDay.Text)"
 }
 $form.Controls.Find('amzgSalesToday',$true)[0].PerformClick()
 [Windows.Forms.Application]::DoEvents()
 if($headerDay.Text -notmatch 'R\$\s*87[,.]72'){throw "Retorno ao dia atual incorreto: $($headerDay.Text)"}
 # Abrir exatamente a janela Produtos da venda e verificar cabecalhos com
 # resolucoes diferentes; o teste anterior so abria o modulo principal.
 $factory=$salesType.GetMethod('BuildSaleItemsWindow',$flags)
 if($null -eq $factory){throw 'Janela de produtos por venda nao permite teste real.'}
 $detail=$factory.Invoke($form,[object[]]@([long]1924))
 try{
  $detail.Show($form)
  [Windows.Forms.Application]::DoEvents()
  $title=@($detail.Controls.Find('amzgSalesItemsHeader',$true))
  $body=@($detail.Controls.Find('amzgSalesItemsBody',$true))
  $rows=@($detail.Controls.Find('amzgSalesItemsGrid',$true))
  $calc=@($detail.Controls.Find('amzgCalculatorShortcutBar',$false))
  if($title.Count -ne 1 -or $body.Count -ne 1 -or
     $rows.Count -ne 1 -or $calc.Count -ne 1){
   throw 'Faltam cabecalho, tabela, corpo ou barra da calculadora da venda.'
  }
  $saleGrid=$rows[0]
  foreach($w in @(670,850,1020)){
   $detail.Size=[Drawing.Size]::new($w,560)
   [Windows.Forms.Application]::DoEvents()
   if(!$title[0].Visible -or !$saleGrid.Visible -or
      !$saleGrid.ColumnHeadersVisible -or $saleGrid.ColumnHeadersHeight -lt 45){
    throw "Venda secundaria $w pixels: titulos de colunas ocultos."
   }
   $headerY=$detail.PointToClient($title[0].PointToScreen([Drawing.Point]::Empty)).Y
   $tableY=$detail.PointToClient($saleGrid.PointToScreen([Drawing.Point]::Empty)).Y
   $calcY=$detail.PointToClient($calc[0].PointToScreen([Drawing.Point]::Empty)).Y
   $calcBottom=$calcY+$calc[0].Height
   if($headerY -lt $calcBottom -or
      $tableY -lt $headerY+$title[0].Height){
    throw "Venda secundaria $w pixels: calculadora, cabecalho e tabela sobrepostos."
   }
   $names=@('Código','Produto','Quantidade','Preço (R$)','Valor (R$)')
   if($saleGrid.Rows.Count -ne 2 -or $saleGrid.Columns.Count -ne 5){
    throw 'Venda secundaria: produtos ou colunas ausentes.'
   }
   foreach($n in $names){
    $column=$saleGrid.Columns[$n]
    if($null -eq $column -or !$column.Visible -or $column.Width -lt 80 -or
       [string]::IsNullOrWhiteSpace($column.HeaderText)){
     throw "Venda secundaria $w pixels: coluna $n escondida/sem titulo."
    }
   }
   $last=$saleGrid.Columns['Valor (R$)']
   $rect=$saleGrid.GetColumnDisplayRectangle($last.Index,$true)
   if($rect.Width -lt 80 -or $rect.Right -gt $saleGrid.ClientSize.Width+2){
    throw "Venda secundaria $w pixels: coluna Valor cortada."
   }
   Write-Host "ITENS DA VENDA OK: cabecalho, cinco colunas e botoes em $w pixels."
  }
  $detail.Size=[Drawing.Size]::new(850,560)
  [Windows.Forms.Application]::DoEvents()
  $pic=[Drawing.Bitmap]::new($detail.ClientSize.Width,$detail.ClientSize.Height)
  try{
   $detail.DrawToBitmap($pic,[Drawing.Rectangle]::new(
    [Drawing.Point]::Empty,$detail.ClientSize))
   $imagePath=Join-Path $folder 'Previa-Vendas-Produtos-Da-Venda.png'
   $pic.Save($imagePath,[Drawing.Imaging.ImageFormat]::Png)
   if((Get-Item $imagePath).Length -lt 5000){throw 'Previa do dialogo de venda vazia.'}
  }finally{$pic.Dispose()}
 }finally{
  if($null -ne $detail){try{$detail.Close()}catch{};$detail.Dispose()}
 }
 $snapshot=[Drawing.Bitmap]::new($form.ClientSize.Width,$form.ClientSize.Height)
 try{
  $form.DrawToBitmap($snapshot,[Drawing.Rectangle]::new([Drawing.Point]::Empty,$form.ClientSize))
  $snapshot.Save($preview,[Drawing.Imaging.ImageFormat]::Png)
 }finally{$snapshot.Dispose()}
 if((Get-Item -LiteralPath $preview).Length -lt 15000){
  throw 'Captura do modulo de Vendas incompleta.'
 }
 Write-Host "VENDAS OK: painel duplo, cinco colunas, detalhes reais, retorno superior e filtros preservados."
 Write-Host "PREVIA MODULO VENDAS: $preview"
}finally{
 if($null -ne $form){try{$form.Close()}catch{};$form.Dispose()}
 if($null -ne $main){try{$main.Close()}catch{};$main.Dispose()}
}
