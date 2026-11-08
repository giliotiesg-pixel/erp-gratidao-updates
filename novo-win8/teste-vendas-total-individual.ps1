param(
 [Parameter(Mandatory=$true)][string]$Executable,
 [Parameter(Mandatory=$true)][string]$PreviewFile
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class AMZGWin32 {
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]
 public static extern IntPtr FindWindow(string cls,string title);
 [DllImport("user32.dll")]
 public static extern bool PostMessage(IntPtr hwnd,uint msg,IntPtr w,IntPtr l);
}
'@
$exe=[IO.Path]::GetFullPath($Executable)
$preview=[IO.Path]::GetFullPath($PreviewFile)
$folder=[IO.Path]::GetDirectoryName($preview)
[IO.Directory]::CreateDirectory($folder) | Out-Null
if(!(Test-Path $exe)){throw "Programa nao encontrado: $exe"}
$db=Join-Path $folder 'teste-vendas-alteracao-descartavel.db'
if(Test-Path $db){Remove-Item $db -Force}
$seed=@'
import sqlite3,sys,datetime
db=sqlite3.connect(sys.argv[1])
db.executescript("""
CREATE TABLE sales(id INTEGER PRIMARY KEY,created_at TEXT,customer_name TEXT,payment_method TEXT,gross_total REAL,subtotal REAL,discount_total REAL DEFAULT 0,total_cost REAL DEFAULT 0,net_profit REAL DEFAULT 0,machine_fee REAL DEFAULT 0,profit_reserve_10 REAL DEFAULT 0);
CREATE TABLE sale_items(id INTEGER PRIMARY KEY,sale_id INTEGER,product_id INTEGER,barcode TEXT,name TEXT,quantity REAL,unit_price REAL,unit_cost REAL,total REAL);
CREATE TABLE products(id INTEGER PRIMARY KEY,barcode TEXT,name TEXT,price REAL,cost REAL);
""")
date=datetime.datetime.now().strftime("%Y-%m-%d")
db.execute("INSERT INTO sales VALUES(1924,?,?,?,?,?,?,?,?,?,?)",(date+" 20:55:02","Cliente teste","Dinheiro",56.24,56.24,0,13,43.24,0,4.32))
db.execute("INSERT INTO sales VALUES(1923,?,?,?,?,?,?,?,?,?,?)",(date+" 20:21:45","Consumidor","Pix",31.48,31.48,0,18,13.48,0,1.35))
products=[(1,"78901","Refrigerante 2L",6.50,3.20),(2,"78902","Biscoito teste",14.41,5.00),(3,"78903","Agua teste",7.87,2.20),(4,"78904","Produto adicional",10.00,4.00)]
db.executemany("INSERT INTO products VALUES(?,?,?,?,?)",products)
items=[(1,1924,1,"78901","Refrigerante 2L",2,6.50,3.20,13.00),(2,1924,2,"78902","Biscoito teste",3,14.413333,5,43.24),(3,1923,3,"78903","Agua teste",4,7.87,2.2,31.48)]
db.executemany("INSERT INTO sale_items VALUES(?,?,?,?,?,?,?,?,?)",items)
db.commit()
db.close()
'@
$seedFile=Join-Path $folder 'criar-vendas-para-teste.py'
[IO.File]::WriteAllText($seedFile,$seed,[Text.UTF8Encoding]::new($false))
python $seedFile $db
if($LASTEXITCODE -ne 0){throw 'Falha ao preparar banco de testes.'}
[Environment]::CurrentDirectory=[IO.Path]::GetDirectoryName($exe)
[Windows.Forms.Application]::EnableVisualStyles()
$assembly=[Reflection.Assembly]::LoadFrom($exe)
$salesType=$assembly.GetType('ArmazemGratidao.ConsultaForm',$true)
$ctor=$salesType.GetConstructor([Type[]]@([string],[bool]))
$method=$salesType.GetMethod('ChangeProduct',
 [Reflection.BindingFlags]::NonPublic -bor [Reflection.BindingFlags]::Instance)
if($null -eq $ctor -or $null -eq $method){throw 'Metodo de edicao nao encontrado.'}
$normalize=$salesType.GetMethod('NormalizeSaleProductSearch',
 [Reflection.BindingFlags]::NonPublic -bor [Reflection.BindingFlags]::Static)
if($null -eq $normalize){throw 'Busca inteligente nao encontrada.'}
$expected=([string]([char]0x00C1))+'GUA DE 20l'
$actual=$normalize.Invoke($null,[object[]]@($expected))
if($actual -ne 'agua de 20l'){throw "Busca nao ignora acentos: $actual"}
$test2=$normalize.Invoke($null,[object[]]@('CoCA   COLA'))
if($test2 -ne 'coca cola'){throw "Busca nao ignora maiusculas: $test2"}
$form=$null
try{
 $form=$ctor.Invoke([object[]]@([string]$db,[bool]$false))
 $form.StartPosition='Manual'
 $form.Location=[Drawing.Point]::new(50,50)
 $form.Size=[Drawing.Size]::new(1100,740)
 $form.Show()
 [Windows.Forms.Application]::DoEvents()
 foreach($scenario in @(
  @{qty=[decimal]3;price=[decimal]7;expected='64';first=$true},
  @{qty=[decimal]1;price=[decimal]3;expected='46';first=$false}
 )){
  $script:targetQty=$scenario.qty
  $script:targetPrice=$scenario.price
  $script:expectedValue=$scenario.expected
  $script:makeCapture=$scenario.first
  $script:mode=0
  $script:failure=""
  $script:started=[Diagnostics.Stopwatch]::StartNew()
  $timer=[Windows.Forms.Timer]::new()
  $timer.Interval=80
  $timer.Add_Tick({
   try{
    if($script:started.Elapsed.TotalSeconds -gt 18){
     $script:failure="Timeout ao testar janela de alteracao"
     foreach($win in [Windows.Forms.Application]::OpenForms){
      if($win.Name -eq 'amzgChangeProductDialog'){$win.Close()}
     }
    }
    if($script:mode -eq 0){
     $modal=@([Windows.Forms.Application]::OpenForms |
      Where-Object {$_.Name -eq 'amzgChangeProductDialog'} | Select-Object -First 1)
     if($modal.Count -eq 0){return}
     $popup=$modal[0]
     $list=@($popup.Controls.Find('amzgSaleEditSaleItems',$true))[0]
     $total=@($popup.Controls.Find('amzgSaleEditTotal',$true))[0]
     $qty=@($popup.Controls.Find('amzgSaleEditQty',$true))[0]
     $price=@($popup.Controls.Find('amzgSaleEditPrice',$true))[0]
     $save=@($popup.Controls.Find('amzgSaleEditSave',$true))[0]
     if($null -eq $list -or $null -eq $total -or $null -eq $qty -or $null -eq $price -or $null -eq $save){
      throw "Campo da edicao ausente."
     }
     if($list.Rows.Count -ne 2){throw "Lista deve mostrar apenas os 2 produtos desta venda: $($list.Rows.Count)"}
     if($list.Columns.Contains('name') -or !$list.Columns.Contains('produto')){
      throw 'Janela principal exibiu cadastro em vez dos itens vendidos.'
     }
     if(@($popup.Controls.Find('amzgSaleEditFind',$true)).Count -ne 1 -or
        @($popup.Controls.Find('amzgSaleEditRemove',$true)).Count -ne 1){
      throw 'Alterar produto precisa de Busca de substituto e Retirar da venda.'
     }
     if($popup.Controls.Find('amzgSalesTotalsBar',$true).Count -gt 0){
      throw 'Totais do dia/mes nao devem aparecer na janela de edicao.'
     }
     if($script:makeCapture){
      $image=[Drawing.Bitmap]::new($popup.ClientSize.Width,$popup.ClientSize.Height)
      try{
       $popup.DrawToBitmap($image,[Drawing.Rectangle]::new([Drawing.Point]::Empty,$popup.ClientSize))
       $image.Save($preview,[Drawing.Imaging.ImageFormat]::Png)
      }finally{$image.Dispose()}
     }
     $qty.Value=$script:targetQty
     $price.Value=$script:targetPrice
     if($total.Text -notmatch ($script:expectedValue+'[,.]24')){
      throw "Total da venda projetado incorreto: $($total.Text)"
     }
     $script:mode=1
     $save.PerformClick()
    }elseif($script:mode -eq 1){
     $message=[AMZGWin32]::FindWindow('#32770','Vendas')
     if($message -ne [IntPtr]::Zero){
      [AMZGWin32]::PostMessage($message,0x0010,[IntPtr]::Zero,[IntPtr]::Zero) | Out-Null
      $script:mode=2
     }
    }
   }catch{
    $script:failure=$_.Exception.Message
    $script:mode=99
    foreach($win in [Windows.Forms.Application]::OpenForms){
     if($win.Name -eq 'amzgChangeProductDialog'){$win.Close()}
    }
    $message=[AMZGWin32]::FindWindow('#32770','Vendas')
    if($message -ne [IntPtr]::Zero){
     [AMZGWin32]::PostMessage($message,0x0010,[IntPtr]::Zero,[IntPtr]::Zero) | Out-Null
    }
   }
  })
  try{
   $timer.Start()
   $method.Invoke($form,$null) | Out-Null
  }finally{$timer.Stop();$timer.Dispose()}
  if($script:failure){throw $script:failure}
  [Windows.Forms.Application]::DoEvents()
 }
}finally{
 if($null -ne $form){$form.Close();$form.Dispose()}
}
$verify=@'
import sqlite3,sys
db=sqlite3.connect(sys.argv[1])
sales=db.execute("SELECT id,gross_total FROM sales ORDER BY id DESC").fetchall()
assert abs(sales[0][1]-46.24)<0.001,sales
assert abs(sales[1][1]-31.48)<0.001,sales
total=db.execute("SELECT SUM(gross_total) FROM sales").fetchone()[0]
assert abs(total-77.72)<0.001,total
line=db.execute("SELECT quantity,unit_price,total FROM sale_items WHERE id=1").fetchone()
assert abs(line[0]-1)<0.001 and abs(line[1]-3)<0.001 and abs(line[2]-3)<0.001,line
audit=db.execute("SELECT COUNT(*) FROM sales_edit_audit WHERE action LIKE 'ALTERAR_ITEM_%'").fetchone()[0]
assert audit==2,audit
assert db.execute("SELECT COUNT(*) FROM products").fetchone()[0]==4
print("EDICAO DE VENDAS OK: aumento, reducao, total individual, totais agregados e auditoria.")
db.close()
'@
$verifyFile=Join-Path $folder 'validar-alteracao-de-venda.py'
[IO.File]::WriteAllText($verifyFile,$verify,[Text.UTF8Encoding]::new($false))
python $verifyFile $db
if($LASTEXITCODE -ne 0){throw 'Totais da venda e do dia/mes incoerentes apos salvar.'}
if(!(Test-Path $preview) -or (Get-Item $preview).Length -lt 9000){
 throw 'Previa real da janela de edicao nao foi gerada.'
}
Write-Host "PREVIA ALTERAR PRODUTO: $preview"
