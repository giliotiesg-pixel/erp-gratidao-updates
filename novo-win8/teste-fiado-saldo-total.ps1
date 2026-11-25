param(
 [Parameter(Mandatory=$true)][string]$Executable,
 [Parameter(Mandatory=$true)][string]$OutputDir
)
$ErrorActionPreference='Stop'
$exe=[IO.Path]::GetFullPath($Executable)
$out=[IO.Path]::GetFullPath($OutputDir)
[IO.Directory]::CreateDirectory($out)|Out-Null
$db=Join-Path $out 'fiado-saldo-total-descartavel.db'
if(Test-Path $db){Remove-Item $db -Force}
$seed=@'
import sqlite3,sys
cn=sqlite3.connect(sys.argv[1])
cn.executescript("""
CREATE TABLE sales(id INTEGER PRIMARY KEY,customer_name TEXT,payment_method TEXT,status TEXT,gross_total NUMERIC,created_at TEXT);
CREATE TABLE sale_payments(sale_id INTEGER,payment_method TEXT);
CREATE TABLE credit_payments(id INTEGER PRIMARY KEY,sale_id INTEGER,amount NUMERIC,payment_method TEXT,paid_at TEXT);
CREATE TABLE customer_credit_entries(id INTEGER PRIMARY KEY,customer_name TEXT,amount NUMERIC,entry_type TEXT,method TEXT);
INSERT INTO sales VALUES(1,'Priscila Vera','FIADO','FIADO',200,'2026-11-18');
INSERT INTO sales VALUES(2,'Priscila Vera','FIADO','FIADO',150,'2026-11-19');
INSERT INTO sales VALUES(3,'Priscila Vera','FIADO','FIADO',100,'2026-11-20');
INSERT INTO credit_payments VALUES(1,1,50,'DINHEIRO','2026-11-20 10:00:00');
INSERT INTO customer_credit_entries VALUES(1,'Priscila Vera',200,'DEPOSITO','PIX');
""")
cn.commit();cn.close()
'@
$seedFile=Join-Path $out 'semear-fiado.py'
[IO.File]::WriteAllText($seedFile,$seed,(New-Object System.Text.UTF8Encoding($false)))
python $seedFile $db
if($LASTEXITCODE -ne 0){throw 'Falha ao criar fixture de Fiado.'}
$oldCurrent=[Environment]::CurrentDirectory
try{
 [Environment]::CurrentDirectory=[IO.Path]::GetDirectoryName($exe)
 $sqliteDll=Join-Path ([IO.Path]::GetDirectoryName($exe)) 'System.Data.SQLite.dll'
 if(!(Test-Path $sqliteDll)){throw "Dependencia SQLite nao encontrada: $sqliteDll"}
 [void][Reflection.Assembly]::LoadFrom($sqliteDll)
 $testClass=@'
using System;
using System.Data.SQLite;
using System.Data;
using System.IO;
using System.Reflection;
public static class AMZGFiadoTest {
 static BindingFlags Flags=BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
 static object Invoke(Type type,string name,params object[] args) {
  MethodInfo method=type.GetMethod(name,Flags);
  if(method==null)throw new InvalidOperationException("Metodo nao encontrado: "+name);
  try{return method.Invoke(null,args);}
  catch(TargetInvocationException e){throw e.InnerException;}
 }
 static void Verify(Type ledger,SQLiteConnection cn,string scenario,
  decimal sales,decimal general,decimal due) {
  decimal a=Convert.ToDecimal(Invoke(ledger,"SalesDue",cn,null,"Priscila Vera"));
  decimal b=Convert.ToDecimal(Invoke(ledger,"Received",cn,null,"Priscila Vera"));
  decimal c=Convert.ToDecimal(Invoke(ledger,"Due",cn,null,"Priscila Vera"));
  if(Math.Abs(a-sales)>0.005m||Math.Abs(b-general)>0.005m||Math.Abs(c-due)>0.005m)
   throw new Exception(scenario+" divergente: sales="+a+" paid="+b+" due="+c);
 }
 public static void Run(string exe,string database){
  var asm=Assembly.LoadFrom(exe);
  var ledger=asm.GetType("ArmazemGratidao.FiadoAccountLedger",true);
  var fiado=asm.GetType("ArmazemGratidao.FiadoClientesForm",true);
  using(var cn=new SQLiteConnection("Data Source="+database+";Read Only=False")){
   cn.Open();
   Invoke(ledger,"CreateTables",cn,null);
   Verify(ledger,cn,"Saldo antigo com R$ 50 pago",400,0,400);
   // Deposito antecipado nao e ainda pagamento de venda.
   Verify(ledger,cn,"Credito de R$ 200 no deposito",400,0,400);
   var automatic=Convert.ToDecimal(Invoke(fiado,"ApplyCustomerCredit",cn,null,2L,"Priscila Vera",100m));
   if(automatic!=0)throw new Exception("Abatimento automatico indevido.");
   Verify(ledger,cn,"Credito permanece sem abatimento automatico",400,0,400);
   using(var q=new SQLiteCommand(@"INSERT INTO fiado_account_receipts
    (receipt_key,customer_name,amount,received_at,method,description,balance_before,balance_after)
    VALUES('teste-1','Priscila Vera',100,'2026-11-21 09:00:00','PIX','Pagamento parcial no total',400,300)",cn))q.ExecuteNonQuery();
   Verify(ledger,cn,"Pagamento R$ 100 no saldo total",400,100,300);
   using(var q=new SQLiteCommand("INSERT INTO credit_payments VALUES(2,1,120,'CREDITO DEPOSITADO','2026-11-22 11:00:00')",cn))q.ExecuteNonQuery();
   using(var q=new SQLiteCommand("INSERT INTO customer_credit_entries VALUES(2,'Priscila Vera',-120,'UTILIZACAO_FIADO','CREDITO_CLIENTE')",cn))q.ExecuteNonQuery();
   Verify(ledger,cn,"Credito usado manualmente R$ 120 sem novo pagamento",280,100,180);
   decimal available=Convert.ToDecimal(Invoke(fiado,"CustomerCreditBalance",cn,null,"Priscila Vera"));
   if(available!=80)throw new Exception("Carteira residual esperada R$80, encontrada R$"+available);
   using(var q=new SQLiteCommand("UPDATE fiado_account_receipts SET amount=80 WHERE receipt_key='teste-1'",cn))q.ExecuteNonQuery();
   Verify(ledger,cn,"Corrigido pagamento R$100 para R$80",280,80,200);
   using(var q=new SQLiteCommand(@"INSERT INTO sales VALUES(2143,'Cliente teste','FIADO','FIADO',65.93,'2026-11-18 18:40');
    INSERT INTO sales VALUES(2118,'Cliente teste','FIADO','FIADO',15.99,'2026-11-20 20:48');
    INSERT INTO sales VALUES(2190,'Cliente teste','FIADO','FIADO',36.98,'2026-11-22 20:27');
    INSERT INTO sales VALUES(2206,'Cliente teste','FIADO','FIADO',20.28,'2026-11-23 21:39');
    INSERT INTO sales VALUES(2231,'Cliente teste','FIADO','FIADO',1.99,'2026-11-24 20:00');
    INSERT INTO sales VALUES(2242,'Cliente teste','FIADO','FIADO',54.25,'2026-11-25 20:42');
    INSERT INTO credit_payments VALUES(3,2143,34.29,'PIX','2026-11-19');
    INSERT INTO credit_payments VALUES(4,2118,5,'PIX','2026-11-21');
    INSERT INTO fiado_account_receipts(receipt_key,customer_name,amount,received_at,method,description,balance_before,balance_after)
    VALUES('foto','Cliente teste',101.88,'2026-11-24 21:00','PIX','Saldo total',101.88,0)",cn))q.ExecuteNonQuery();
   var projection=(DataTable)Invoke(ledger,"ReconciledSales",cn,null,"Cliente teste");
   if(projection.Rows.Count!=6)throw new Exception("Vendas ausentes na conciliacao");
   for(int i=0;i<5;i++)if(Convert.ToDecimal(projection.Rows[i]["saldo"])!=0m)throw new Exception("Venda antiga nao baixada");
   if(Convert.ToInt64(projection.Rows[5]["venda"])!=2242L||Convert.ToDecimal(projection.Rows[5]["saldo"])!=54.25m)throw new Exception("Venda nova incorreta");
   using(var q=new SQLiteCommand("UPDATE fiado_account_receipts SET amount=100 WHERE receipt_key='foto'",cn))q.ExecuteNonQuery();
   projection=(DataTable)Invoke(ledger,"ReconciledSales",cn,null,"Cliente teste");
   if(Convert.ToDecimal(projection.Rows[4]["saldo"])!=1.88m||Convert.ToDecimal(projection.Rows[5]["saldo"])!=54.25m)throw new Exception("Pagamento parcial ou correcao incorreta");
   using(var q=new SQLiteCommand("SELECT COUNT(*) FROM credit_payments WHERE sale_id>=2000",cn))if(Convert.ToInt32(q.ExecuteScalar())!=2)throw new Exception("Conciliacao duplicou pagamentos");
   var backup=Convert.ToString(Invoke(ledger,"BackupVerified",cn,database,"antes-teste-fiado-"));
   if(!File.Exists(backup)||new FileInfo(backup).Length<=0)
    throw new Exception("Backup verificado nao existe.");
  }
 }
}
'@
 Add-Type -TypeDefinition $testClass -ReferencedAssemblies @('System.dll','System.Data.dll',$sqliteDll) -ErrorAction Stop
 [AMZGFiadoTest]::Run($exe,$db)
 Write-Host 'FIADO OK: credito antecipado separado, saldo total, abatimento manual, correcao e backup.'
}finally{[Environment]::CurrentDirectory=$oldCurrent}
