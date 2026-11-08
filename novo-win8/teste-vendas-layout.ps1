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
conn.execute("INSERT INTO sale_items VALUES (1,1924,?,?,?,?,?)",("78901","Refrigerante de teste 2 L",2,6.50,13.00))
conn.execute("INSERT INTO sale_items VALUES (2,1924,?,?,?,?,?)",("78902","Biscoito de teste",3,14.413333,43.24))
conn.execute("INSERT INTO sale_items VALUES (3,1923,?,?,?,?,?)",("78903","Agua de teste",4,7.87,31.48))
conn.commit()
conn.close()
'@
python -c $python $db
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
 $form=[Activator]::CreateInstance($salesType,[object[]]@($db,$false))
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
