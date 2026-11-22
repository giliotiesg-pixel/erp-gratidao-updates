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
 $asm=[Reflection.Assembly]::LoadFrom($exe)
 $ledger=$asm.GetType('ArmazemGratidao.FiadoAccountLedger',$true)
 $fiado=$asm.GetType('ArmazemGratidao.FiadoClientesForm',$true)
 $flags=[Reflection.BindingFlags]::Static -bor [Reflection.BindingFlags]::NonPublic -bor [Reflection.BindingFlags]::Public
 $conn=New-Object System.Data.SQLite.SQLiteConnection("Data Source=$db;Read Only=False")
 $conn.Open()
 try{
  function Calc([string]$name){
   $m=$ledger.GetMethod($name,$flags)
   if($null -eq $m){throw "Metodo do ledger ausente: $name"}
   return [decimal]$m.Invoke($null,[object[]]@($conn,$null,'Priscila Vera'))
  }
  function Verify([string]$scenario,[decimal]$sales,[decimal]$payments,[decimal]$due){
   $a=Calc 'SalesDue';$b=Calc 'Received';$c=Calc 'Due'
   if([Math]::Abs($a-$sales) -gt 0.005 -or [Math]::Abs($b-$payments) -gt 0.005 -or
      [Math]::Abs($c-$due) -gt 0.005){
    throw "$scenario : calculo diferente do esperado sales=$a paid=$b due=$c"
   }
  }
  $create=$ledger.GetMethod('CreateTables',$flags)
  if($null -eq $create){throw 'A tabela auditavel de recebimentos nao existe.'}
  [void]$create.Invoke($null,[object[]]@($conn,$null))
  Verify 'Divida original com R$50 pago numa venda' 400 0 400
  # O deposito antecipado de R$200 nao deve abater novamente a divida.
  Verify 'Deposito em carteira separado do Fiado' 400 0 400
  $auto=$fiado.GetMethod('ApplyCustomerCredit',$flags)
  if($null -eq $auto){throw 'Metodo de compatibilidade de credito ausente.'}
  $applied=[decimal]$auto.Invoke($null,[object[]]@($conn,$null,[long]2,'Priscila Vera',[decimal]100))
  if($applied -ne 0){throw 'Creditos nao podem ser usados automaticamente em novas vendas.'}
  Verify 'Credito automatico bloqueado' 400 0 400
  $cmd=$conn.CreateCommand()
  $cmd.CommandText="INSERT INTO fiado_account_receipts(receipt_key,customer_name,amount,received_at,method,description,balance_before,balance_after) VALUES('teste-1','Priscila Vera',100,'2026-11-21 09:00:00','PIX','Pagamento parcial do total',400,300)"
  [void]$cmd.ExecuteNonQuery()
  Verify 'Pagamento de R$100 diretamente no saldo total' 400 100 300
  # Abater carteira manualmente no valor de R$120 em uma das vendas.
  $cmd.CommandText="INSERT INTO credit_payments VALUES(2,1,120,'CREDITO DEPOSITADO','2026-11-22 11:00:00')"
  [void]$cmd.ExecuteNonQuery()
  $cmd.CommandText="INSERT INTO customer_credit_entries VALUES(2,'Priscila Vera',-120,'UTILIZACAO_FIADO','CREDITO_CLIENTE')"
  [void]$cmd.ExecuteNonQuery()
  Verify 'Uso manual do credito R$120 sem dinheiro novo' 280 100 180
  $wallet=$fiado.GetMethod('CustomerCreditBalance',$flags)
  $available=[decimal]$wallet.Invoke($null,[object[]]@($conn,$null,'Priscila Vera'))
  if($available -ne 80){throw "Carteira residual incorreta: $available"}
  # Correcao do pagamento geral R$100 -> R$80.
  $cmd.CommandText="UPDATE fiado_account_receipts SET amount=80 WHERE receipt_key='teste-1'"
  [void]$cmd.ExecuteNonQuery()
  Verify 'Correcao de pagamento parcial R$100 para R$80' 280 80 200
  $backup=$ledger.GetMethod('BackupVerified',$flags)
  $copy=[string]$backup.Invoke($null,[object[]]@($conn,$db,'antes-teste-fiado-'))
  if(!(Test-Path $copy) -or (Get-Item $copy).Length -le 0){
   throw 'Backup do saldo total do Fiado ausente ou vazio.'
  }
  Write-Host 'FIADO OK: saldo real, credito antecipado separado, aplicacao somente manual, correcao e backup.'
 }finally{$conn.Dispose()}
}finally{[Environment]::CurrentDirectory=$oldCurrent}
