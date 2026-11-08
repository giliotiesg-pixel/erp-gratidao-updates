param(
 [Parameter(Mandatory=$true)][string]$Executable,
 [Parameter(Mandatory=$true)][string]$PreviewFile
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$exe=[IO.Path]::GetFullPath($Executable)
$preview=[IO.Path]::GetFullPath($PreviewFile)
$folder=[IO.Path]::GetDirectoryName($preview)
[IO.Directory]::CreateDirectory($folder)|Out-Null
$db=Join-Path $folder 'teste-alteracao-vendas-descartavel.db'
if(Test-Path $db){Remove-Item $db -Force}
$seed=@'
import sqlite3,sys,datetime
db=sqlite3.connect(sys.argv[1])
db.executescript("""
CREATE TABLE sales(id INTEGER PRIMARY KEY,created_at TEXT,customer_name TEXT,payment_method TEXT,gross_total REAL,subtotal REAL,discount_total REAL DEFAULT 0,total_cost REAL DEFAULT 0,net_profit REAL DEFAULT 0,machine_fee REAL DEFAULT 0,profit_reserve_10 REAL DEFAULT 0);
CREATE TABLE sale_items(id INTEGER PRIMARY KEY,sale_id INTEGER,product_id INTEGER,barcode TEXT,name TEXT,quantity REAL,unit_price REAL,unit_cost REAL,total REAL);
CREATE TABLE products(id INTEGER PRIMARY KEY,barcode TEXT,name TEXT,price REAL,cost REAL);
""")
today=datetime.datetime.now().strftime("%Y-%m-%d")
db.execute("INSERT INTO sales VALUES(1924,?,?,?,?,?,?,?,?,?,?)",(today+" 20:55:02","Cliente teste","Dinheiro",56.24,56.24,0,13,43.24,0,4.32))
db.execute("INSERT INTO sales VALUES(1923,?,?,?,?,?,?,?,?,?,?)",(today+" 20:21:45","Consumidor","Pix",31.48,31.48,0,18,13.48,0,1.35))
db.executemany("INSERT INTO products VALUES(?,?,?,?,?)",[
 (1,"78901","Refrigerante 2L",6.50,3.20),
 (2,"78902","Biscoito teste",14.41,5.00),
 (3,"78903","Agua teste",7.87,2.20),
 (4,"78904","Produto adicional",10.00,4.00)])
db.executemany("INSERT INTO sale_items VALUES(?,?,?,?,?,?,?,?,?)",[
 (1,1924,1,"78901","Refrigerante 2L",2,6.50,3.20,13.00),
 (2,1924,2,"78902","Biscoito teste",3,14.413333,5,43.24),
 (3,1923,3,"78903","Agua teste",4,7.87,2.2,31.48)])
db.commit()
db.close()
'@
$seedPath=Join-Path $folder 'preparar-alteracao-vendas.py'
[IO.File]::WriteAllText($seedPath,$seed,(New-Object System.Text.UTF8Encoding($false)))
python $seedPath $db
if($LASTEXITCODE -ne 0){throw 'Falha na criacao de dados ficticios para teste'}
[Environment]::CurrentDirectory=[IO.Path]::GetDirectoryName($exe)
$source=@'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
public static class AmzgSalesUiTest {
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]
 static extern IntPtr FindWindow(string className,string windowName);
 [DllImport("user32.dll")]
 static extern bool PostMessage(IntPtr hwnd,uint msg,IntPtr wp,IntPtr lp);
 static Control Find(Form form,string name){
   Control[] found=form.Controls.Find(name,true);
   if(found.Length!=1)throw new Exception("Controle nao encontrado: "+name);
   return found[0];
 }
 static void One(string exe,string db,string screenshot,decimal qty,decimal price,decimal expected,bool capture){
  Assembly a=Assembly.LoadFrom(exe);
  Type t=a.GetType("ArmazemGratidao.ConsultaForm",true);
  ConstructorInfo ctor=t.GetConstructor(new Type[]{typeof(string),typeof(bool)});
  MethodInfo change=t.GetMethod("ChangeProduct",BindingFlags.NonPublic|BindingFlags.Instance);
  if(ctor==null||change==null)throw new Exception("Rotina de Vendas nao disponivel");
  Form host=(Form)ctor.Invoke(new object[]{db,false});
  var clock=Stopwatch.StartNew();
  var timer=new System.Windows.Forms.Timer();
  timer.Interval=75;
  bool clicked=false,seen=false,finished=false;
  string error=null;
  try{
   host.StartPosition=FormStartPosition.Manual;
   host.Size=new Size(1100,740);
   host.Location=new Point(35,35);
   host.Shown+=(sender,args)=>{
    host.BeginInvoke((Action)(()=>{
     try{change.Invoke(host,null);}
     catch(Exception ex){error="Erro ao abrir a edicao: "+ex.ToString();}
     finally{finished=true;host.Close();}
    }));
   };
   timer.Tick+=(sender,args)=>{
    try{
     if(clock.Elapsed.TotalSeconds>22){
      error="Tempo limite de 22s no modal. Encontrado="+seen+" clicado="+clicked;
      IntPtr msg=FindWindow("#32770",null);
      if(msg!=IntPtr.Zero)PostMessage(msg,0x0010,IntPtr.Zero,IntPtr.Zero);
      foreach(Form open in Application.OpenForms.Cast<Form>().ToArray())
       if(open!=host)open.Close();
      host.Close();return;
     }
     Form dialog=null;
     foreach(Form open in Application.OpenForms)
      if(open.Name=="amzgChangeProductDialog")dialog=open;
     if(dialog!=null&&!clicked){
      seen=true;
      var list=(DataGridView)Find(dialog,"amzgSaleEditSaleItems");
      var total=(Label)Find(dialog,"amzgSaleEditTotal");
      var quantity=(NumericUpDown)Find(dialog,"amzgSaleEditQty");
      var unitPrice=(NumericUpDown)Find(dialog,"amzgSaleEditPrice");
      var save=(Button)Find(dialog,"amzgSaleEditSave");
      if(list.Rows.Count!=2)
       throw new Exception("Mostrou catalogo no lugar dos 2 itens vendidos: "+list.Rows.Count);
      if(dialog.Controls.Find("amzgSaleEditFind",true).Length!=1||
         dialog.Controls.Find("amzgSaleEditRemove",true).Length!=1)
       throw new Exception("Faltam os comandos Buscar Substituto ou Retirar da venda");
      foreach(DataGridViewRow row in list.Rows){
       if(Convert.ToInt64(row.Cells["item_id"].Value)==1){
        list.CurrentCell=row.Cells["produto"];
        break;
       }
      }
      Application.DoEvents();
      if(capture){
       using(var bmp=new Bitmap(dialog.ClientSize.Width,dialog.ClientSize.Height)){
        dialog.DrawToBitmap(bmp,new Rectangle(Point.Empty,dialog.ClientSize));
        bmp.Save(screenshot,ImageFormat.Png);
       }
      }
      quantity.Value=qty;unitPrice.Value=price;
      if(!total.Text.Contains(expected.ToString("N2",new System.Globalization.CultureInfo("pt-BR"))))
       throw new Exception("Total individual esperado "+expected+"; exibido "+total.Text);
      clicked=true;
      save.PerformClick();
     }
     IntPtr popup=FindWindow("#32770",null);
     if(popup!=IntPtr.Zero && (clicked||clock.Elapsed.TotalSeconds>4))
      PostMessage(popup,0x0010,IntPtr.Zero,IntPtr.Zero);
    }catch(Exception ex){
     error=ex.Message;
     IntPtr popup=FindWindow("#32770",null);
     if(popup!=IntPtr.Zero)PostMessage(popup,0x0010,IntPtr.Zero,IntPtr.Zero);
     foreach(Form open in Application.OpenForms.Cast<Form>().ToArray())
      if(open!=host)open.Close();
     host.Close();
    }
   };
   timer.Start();host.Show();
   Application.Run(host);
   if(error!=null)throw new Exception(error);
   if(!finished||!seen||!clicked)
    throw new Exception("Fluxo nao completado: "+finished+"/"+seen+"/"+clicked);
  }finally{
   timer.Stop();timer.Dispose();
   if(!host.IsDisposed)host.Dispose();
  }
 }
 public static void Run(string exe,string db,string screenshot){
  Application.EnableVisualStyles();
  Application.SetCompatibleTextRenderingDefault(false);
  One(exe,db,screenshot,3m,7m,64.24m,true);
  Console.WriteLine("CENARIO 1: aumento da venda de 56,24 para 64,24 OK");
  One(exe,db,screenshot,1m,3m,46.24m,false);
  Console.WriteLine("CENARIO 2: diminuicao da venda para 46,24 OK");
 }
}
'@
Add-Type -TypeDefinition $source -ReferencedAssemblies @(
 'System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll') -ErrorAction Stop
[AmzgSalesUiTest]::Run($exe,$db,$preview)
$verify=@'
import sqlite3,sys
db=sqlite3.connect(sys.argv[1])
rows=db.execute("SELECT id,gross_total FROM sales ORDER BY id DESC").fetchall()
assert abs(rows[0][1]-46.24)<0.001,rows
assert abs(rows[1][1]-31.48)<0.001,rows
assert abs(sum(r[1] for r in rows)-77.72)<0.001,rows
line=db.execute("SELECT quantity,unit_price,total FROM sale_items WHERE id=1").fetchone()
assert abs(line[0]-1)<0.001 and abs(line[1]-3)<0.001 and abs(line[2]-3)<0.001,line
assert db.execute("SELECT count(*) FROM products").fetchone()[0]==4
assert db.execute("SELECT count(*) FROM sales_edit_audit WHERE action LIKE 'ALTERAR_ITEM_%'").fetchone()[0]==2
print("SUCESSO: totais de cada venda, total agregado e auditoria preservados.")
db.close()
'@
$verifyPath=Join-Path $folder 'validar-alteracao-vendas.py'
[IO.File]::WriteAllText($verifyPath,$verify,(New-Object System.Text.UTF8Encoding($false)))
python $verifyPath $db
if($LASTEXITCODE -ne 0){throw 'Totais ou auditoria incorretos apos os testes'}
if(!(Test-Path -LiteralPath $preview) -or (Get-Item $preview).Length -lt 9000){
 throw 'Nao foi gerada a captura real da janela de alteracao'
}
Write-Host "ALTERACAO DE VENDAS OK: $preview"
