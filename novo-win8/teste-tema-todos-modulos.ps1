param(
 [Parameter(Mandatory=$true)][string]$Executable,
 [Parameter(Mandatory=$true)][string]$OutputDir
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$exe=[IO.Path]::GetFullPath($Executable)
$directory=[IO.Path]::GetFullPath($OutputDir)
[IO.Directory]::CreateDirectory($directory)|Out-Null
$database=Join-Path $directory 'teste-modulos-descartavel.db'
if(Test-Path -LiteralPath $database){Remove-Item -LiteralPath $database -Force}
$seed=@'
import sqlite3,sys,datetime
conn=sqlite3.connect(sys.argv[1])
conn.executescript("""
CREATE TABLE products(
 id INTEGER PRIMARY KEY,barcode TEXT,name TEXT,price REAL,cost REAL,
 quantity REAL DEFAULT 30,stock REAL DEFAULT 30,active INTEGER DEFAULT 1);
CREATE TABLE sales(
 id INTEGER PRIMARY KEY,created_at TEXT,customer_name TEXT,payment_method TEXT,
 gross_total REAL,subtotal REAL,discount_total REAL DEFAULT 0,machine_fee REAL DEFAULT 0,
 total_cost REAL DEFAULT 0,net_profit REAL DEFAULT 0,profit_reserve_10 REAL DEFAULT 0,status TEXT);
CREATE TABLE sale_items(
 id INTEGER PRIMARY KEY,sale_id INTEGER,product_id INTEGER,barcode TEXT,
 name TEXT,quantity REAL,unit_price REAL,unit_cost REAL,total REAL);
CREATE TABLE customers(id INTEGER PRIMARY KEY,name TEXT,cpf TEXT,phone TEXT);
CREATE TABLE purchases(id INTEGER PRIMARY KEY);
CREATE TABLE cash_sessions(id INTEGER PRIMARY KEY,opened_at TEXT,opening_amount REAL,closed_at TEXT,closing_amount REAL,expected_cash REAL,difference REAL,status TEXT);
CREATE TABLE sale_payments(id INTEGER PRIMARY KEY,sale_id INTEGER,payment_method TEXT,amount REAL,fee_percent REAL,fee_amount REAL,created_at TEXT);
CREATE TABLE credit_payments(id INTEGER PRIMARY KEY,sale_id INTEGER,amount REAL,payment_method TEXT,paid_at TEXT);
""")
p=[
 (1,"78900001","Refrigerante Cola 2L",11.99,6.80),
 (2,"78900002","Biscoito recheado 140g",4.75,2.90),
 (3,"78900003","Agua mineral 500ml",2.50,1.40),
 (4,"78900004","Leite integral 1L",5.49,3.58),
 (5,"78900005","Acucar refinado 1kg",4.89,3.20),
 (6,"78900006","Cafe torrado 500g",18.50,13.70),
 (7,"78900007","Arroz branco 5kg",25.90,19.00),
 (8,"78900008","Feijao carioca 1kg",7.90,5.60)
]
conn.executemany("INSERT INTO products(id,barcode,name,price,cost) VALUES(?,?,?,?,?)",p)
today=datetime.datetime.now().strftime("%Y-%m-%d")
conn.execute("INSERT INTO sales VALUES(101,?,?,?,?,?,?,?,?,?,?,?)",(
 today+" 13:35:00","Consumidor","DINHEIRO",35.48,35.48,0,0,22.0,13.48,1.35,"FINALIZADA"))
conn.execute("INSERT INTO sales VALUES(102,?,?,?,?,?,?,?,?,?,?,?)",(
 today+" 14:42:00","Ana Maria","PIX",67.37,67.37,0,0,42.0,25.37,2.54,"FINALIZADA"))
conn.executemany("INSERT INTO sale_items VALUES(?,?,?,?,?,?,?,?,?)",[
 (1,101,1,"78900001","Refrigerante Cola 2L",2,11.99,6.80,23.98),
 (2,101,3,"78900003","Agua mineral 500ml",2,2.50,1.40,5.00),
 (3,101,2,"78900002","Biscoito recheado 140g",1,4.75,2.90,4.75),
 (4,102,6,"78900006","Cafe torrado 500g",2,18.50,13.70,37.00),
 (5,102,4,"78900004","Leite integral 1L",3,5.49,3.58,16.47),
])
conn.execute("INSERT INTO customers VALUES(1,'Ana Maria','00000000000','11999999999')")
conn.execute("INSERT INTO sales VALUES(103,?,?,?,?,?,?,?,?,?,?,?)",(
 today+" 16:10:00","Jose Alves","FIADO",19.80,19.80,0,0,13.00,6.80,0.68,"FIADO"))
conn.commit()
conn.close()
'@
$seedPath=Join-Path $directory 'preparar-modulos.py'
[IO.File]::WriteAllText($seedPath,$seed,(New-Object System.Text.UTF8Encoding($false)))
python $seedPath $database
if($LASTEXITCODE -ne 0){throw 'Falha ao criar banco de dados visual descartavel.'}
[Environment]::CurrentDirectory=[IO.Path]::GetDirectoryName($exe)
[Windows.Forms.Application]::EnableVisualStyles()
$assembly=[Reflection.Assembly]::LoadFrom($exe)
$theme=$assembly.GetType('ArmazemGratidao.ModuleVisualTheme',$true)
$style=$theme.GetMethod('CheckOpenForms',[Reflection.BindingFlags]::Public -bor [Reflection.BindingFlags]::Static)
$gridTheme=$assembly.GetType('ArmazemGratidao.RoundedGridTheme',$true)
$styleGrid=$gridTheme.GetMethod('CheckOpenForms',[Reflection.BindingFlags]::Public -bor [Reflection.BindingFlags]::Static)
$items=@(
 @{Class='ConsultaPrecoForm';Name='Consulta-Preco';Args=@($database)},
 @{Class='ProdutosForm';Name='Produtos';Args=@($database)},
 @{Class='ModuloConsultaForm';Name='Estoque';Args=@($database,'Estoque')},
 @{Class='ModuloConsultaForm';Name='Clientes';Args=@($database,'Clientes')},
 @{Class='ModuloConsultaForm';Name='Caixa';Args=@($database,'Caixa')},
 @{Class='ModuloConsultaForm';Name='Sistema';Args=@($database,'Sistema')},
 @{Class='CascosForm';Name='Cascos-Retornaveis';Args=@($database)},
 @{Class='LucroForm';Name='Lucro';Args=@($database)},
 @{Class='FiadoClientesForm';Name='Fiado';Args=@($database)}
)
function Get-ThemeControls($parent){
 foreach($child in $parent.Controls){
  $child
  Get-ThemeControls $child
 }
}
$success=[Collections.Generic.List[string]]::new()
foreach($sample in $items){
 $form=$null
 try{
  $type=$assembly.GetType('ArmazemGratidao.'+$sample.Class,$true)
  $argTypes=if($sample.Args.Count -eq 1){[Type[]]@([string])}
    else{[Type[]]@([string],[string])}
  $ctor=$type.GetConstructor($argTypes)
  if($null -eq $ctor){throw "Construtor indisponivel: $($sample.Class)"}
  if($sample.Args.Count -eq 1){
   $argsList=[object[]]@([string]($sample.Args[0]))
  }else{
   $argsList=[object[]]@([string]($sample.Args[0]),[string]($sample.Args[1]))
  }
  $form=$ctor.Invoke($argsList)
  $form.StartPosition='Manual'
  $form.Location=[Drawing.Point]::new(40,40)
  $form.Size=[Drawing.Size]::new(
   [Math]::Min(1050,[Math]::Max(650,$form.Width)),
   [Math]::Min(690,[Math]::Max(520,$form.Height)))
  $form.Show()
  [Windows.Forms.Application]::DoEvents()
  $style.Invoke($null,@())|Out-Null
  $styleGrid.Invoke($null,@())|Out-Null
  [Windows.Forms.Application]::DoEvents()
  if($null -eq $form.Icon){throw "Marca Armazem Gratidao nao aplicada no modulo $($sample.Name)"}
  $buttons=@(Get-ThemeControls $form | Where-Object {$_.GetType().Name -eq 'Button'})
  $visibleButtons=@($buttons|Where-Object {$_.Visible -and $_.Width -ge 80})
  foreach($button in $visibleButtons){
   if($null -eq $button.Region){
    throw "Botao sem arredondamento em $($sample.Name): $($button.Text)"
   }
  }
  $out=Join-Path $directory ('Previa-Modulo-'+$sample.Name+'.png')
  $bitmap=[Drawing.Bitmap]::new($form.ClientSize.Width,$form.ClientSize.Height)
  try{
   $form.DrawToBitmap($bitmap,[Drawing.Rectangle]::new(
    [Drawing.Point]::Empty,$form.ClientSize))
   $bitmap.Save($out,[Drawing.Imaging.ImageFormat]::Png)
  }finally{$bitmap.Dispose()}
  if((Get-Item $out).Length -lt 5000){throw "Previa pequena: $out"}
  $success.Add($sample.Name)
  Write-Host ('TEMA OK: '+$sample.Name+' | '+$visibleButtons.Count+' botoes | '+$out)
 }catch{
  throw 'Falhou modulo '+$sample.Name+': '+$_.Exception.Message
 }finally{
  if($null -ne $form){try{$form.Close()}catch{};$form.Dispose()}
 }
}
if($success.Count -ne $items.Count){throw "Faltam modulos testados."}
Write-Host ('MODULOS COM TEMA PADRONIZADO: '+($success -join ', '))
