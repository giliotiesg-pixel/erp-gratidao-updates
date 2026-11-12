param(
 [Parameter(Mandatory=$true)][string]$Executable,
 [Parameter(Mandatory=$true)][string]$OutputDir
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$exe=[IO.Path]::GetFullPath($Executable)
$out=[IO.Path]::GetFullPath($OutputDir)
[IO.Directory]::CreateDirectory($out)|Out-Null
$db=Join-Path $out 'teste-caixa-descartavel.db'
if(Test-Path $db){Remove-Item $db -Force}
$seed=@'
import sqlite3,sys,datetime
db=sys.argv[1]
today=datetime.datetime.now().strftime("%Y-%m-%d")
yesterday=(datetime.datetime.now()-datetime.timedelta(days=1)).strftime("%Y-%m-%d")
c=sqlite3.connect(db)
c.executescript("""
CREATE TABLE cash_sessions(id INTEGER PRIMARY KEY AUTOINCREMENT,
 opened_at TEXT NOT NULL,opening_amount NUMERIC NOT NULL,
 closed_at TEXT,closing_amount NUMERIC,expected_cash NUMERIC,difference NUMERIC,
 status TEXT NOT NULL);
CREATE TABLE sales(id INTEGER PRIMARY KEY,created_at TEXT,status TEXT,payment_method TEXT,gross_total NUMERIC);
CREATE TABLE sale_payments(id INTEGER PRIMARY KEY,sale_id INTEGER,payment_method TEXT,amount NUMERIC);
CREATE TABLE credit_payments(id INTEGER PRIMARY KEY,paid_at TEXT,payment_method TEXT,amount NUMERIC,note TEXT);
""")
c.execute("INSERT INTO cash_sessions(id,opened_at,opening_amount,closed_at,closing_amount,expected_cash,difference,status) VALUES (9,?,60,?,90,90,0,'FECHADO')",(yesterday+" 08:00:00",yesterday+" 20:00:00"))
c.execute("INSERT INTO sales VALUES (101,?,'CONCLUIDA','DINHEIRO',100)",(today+" 10:00:00",))
c.execute("INSERT INTO sales VALUES (102,?,'CONCLUIDA','PIX',25)",(today+" 10:30:00",))
c.execute("INSERT INTO sale_payments VALUES (1,101,'DINHEIRO',100)")
c.execute("INSERT INTO sale_payments VALUES (2,102,'PIX',25)")
c.execute("INSERT INTO credit_payments VALUES (1,?,'DINHEIRO',20,'Recebimento parcial antigo')",(today+" 11:00:00",))
c.commit()
c.close()
'@
$seedFile=Join-Path $out 'semente-caixa.py'
[IO.File]::WriteAllText($seedFile,$seed,(New-Object System.Text.UTF8Encoding($false)))
python $seedFile $db
if($LASTEXITCODE -ne 0){throw "Banco de testes do Caixa nao foi criado."}
$oldCurrent=[Environment]::CurrentDirectory
[Environment]::CurrentDirectory=[IO.Path]::GetDirectoryName($exe)
$settings=Join-Path $env:LOCALAPPDATA 'ArmazemGratidaoNovo'
New-Item -ItemType Directory -Path $settings -Force|Out-Null
$setting=Join-Path $settings 'backup-location.txt'
$oldSetting=if(Test-Path $setting){[IO.File]::ReadAllText($setting)}else{$null}
$backupFolder=Join-Path $out 'backups-caixa'
New-Item -ItemType Directory -Path $backupFolder -Force|Out-Null
[IO.File]::WriteAllText($setting,$backupFolder)
try{
 [Windows.Forms.Application]::EnableVisualStyles()
 $assembly=[Reflection.Assembly]::LoadFrom($exe)
 $type=$assembly.GetType('ArmazemGratidao.CashControlForm',$true)
 $form=[Activator]::CreateInstance($type,[object[]]@($db))
 try{
  $form.Show()
  [Windows.Forms.Application]::DoEvents()
  $flags=[Reflection.BindingFlags]::Instance -bor [Reflection.BindingFlags]::NonPublic -bor [Reflection.BindingFlags]::Public
  function Invoke-Cash([string]$name,[object[]]$arguments){
   $method=$type.GetMethod($name,$flags)
   if($null -eq $method){throw "Metodo do Caixa ausente: $name"}
   try{return $method.Invoke($form,$arguments)}
   catch [Reflection.TargetInvocationException]{
    throw "Operacao $name falhou: $($_.Exception.InnerException.Message)"
   }
  }
  function Query([string]$sql){
   $conn=New-Object System.Data.SQLite.SQLiteConnection("Data Source=$db;Read Only=True")
   try{
    $conn.Open();$cmd=$conn.CreateCommand()
    try{$cmd.CommandText=$sql;return $cmd.ExecuteScalar()}
    finally{$cmd.Dispose()}
   }finally{$conn.Dispose()}
  }
  $today=$type.GetField('today',$flags).GetValue($form)
  $history=$type.GetField('history',$flags).GetValue($form)
  $movements=$type.GetField('movements',$flags).GetValue($form)
  $pages=$type.GetField('pages',$flags).GetValue($form)
  if($today.Rows.Count -ne 0 -or $history.Rows.Count -ne 1){
   throw 'Caixas antigos estao aparecendo no dia ou desapareceram do historico.'
  }
  if(@($form.Controls.Find('amzgCashTodayButton',$true)).Count -ne 1 -or
     @($form.Controls.Find('amzgCashArchiveButton',$true)).Count -ne 1){
   throw 'Botoes Caixa do Dia e Caixas Anteriores ausentes.'
  }
  $date=[DateTime]::Today.AddHours(8)
  $sid=[long](Invoke-Cash 'OpenSession' ([object[]]@($date,[decimal]30,'Troco de abertura')))
  if($today.Rows.Count -ne 1 -or $history.Rows.Count -ne 1){
   throw 'Abertura nao apareceu em Caixa do Dia ou historico mudou.'
  }
  $id1=[long](Invoke-Cash 'AddMovement' ([object[]]@($sid,$date.AddHours(4),'ENTRADA',[decimal]12,'Troco extra')))
  $id2=[long](Invoke-Cash 'AddMovement' ([object[]]@($sid,$date.AddHours(5),'SANGRIA',[decimal]7,'Retirada conferida')))
  Invoke-Cash 'CloseSession' ([object[]]@($sid,$date.AddHours(9),[decimal]150,'Fechamento contado'))|Out-Null
  $expected=[decimal](Query "SELECT expected_cash FROM cash_sessions WHERE id=$sid")
  $diff=[decimal](Query "SELECT difference FROM cash_sessions WHERE id=$sid")
  if($expected -ne 155 -or $diff -ne -5){
   throw "Caixa fechou incorretamente. Esperado=$expected, diferenca=$diff (correto 155,-5)."
  }
  # Edicao manual do troco, valor contado, previsto e data/hora com registro de motivo.
  Invoke-Cash 'EditSession' ([object[]]@($sid,$date,[decimal]35,$true,$date.AddHours(9),[decimal]150,[decimal]160,'Correcao da abertura e valor esperado'))|Out-Null
  if([decimal](Query "SELECT expected_cash FROM cash_sessions WHERE id=$sid") -ne 160){
   throw 'Correcao manual do valor esperado nao foi gravada.'
  }
  Invoke-Cash 'EditMovement' ([object[]]@($id1,$date.AddHours(4),'ENTRADA',[decimal]17,'Troco extra revisado','Digitacao do valor'))|Out-Null
  # Ao modificar um movimento antigo, a conferencia de um caixa fechado recalcula.
  if([decimal](Query "SELECT expected_cash FROM cash_sessions WHERE id=$sid") -ne 165){
   throw 'Correcao de movimento nao recalculou o saldo fechado.'
  }
  Invoke-Cash 'DeleteMovement' ([object[]]@($id2,'Sangria duplicada'))|Out-Null
  if([decimal](Query "SELECT expected_cash FROM cash_sessions WHERE id=$sid") -ne 172){
   throw 'Exclusao auditada nao atualizou o valor do caixa.'
  }
  $auditCount=[long](Query 'SELECT count(*) FROM cash_change_audit')
  if($auditCount -lt 6){throw "Auditoria incompleta: apenas $auditCount eventos."}
  if($movements.Rows.Count -ne 1){throw 'Lançamento auditado nao aparece na tela.'}
  $capture=Join-Path $out 'Previa-Caixa-do-Dia.png'
  $img=[Drawing.Bitmap]::new($form.ClientSize.Width,$form.ClientSize.Height)
  try{
   $form.DrawToBitmap($img,[Drawing.Rectangle]::new([Drawing.Point]::Empty,$form.ClientSize))
   $img.Save($capture,[Drawing.Imaging.ImageFormat]::Png)
  }finally{$img.Dispose()}
  # Apos corrigir a data de abertura para ontem, o caixa sai do Dia e aparece no arquivo.
  Invoke-Cash 'EditSession' ([object[]]@($sid,$date.AddDays(-1),[decimal]35,$true,
   $date.AddHours(9),[decimal]150,[decimal]172,'Corrigir data de abertura retroativa'))|Out-Null
  if($today.Rows.Count -ne 0 -or $history.Rows.Count -ne 2){
   throw 'Caixa retroativo permanece no dia ou sumiu de Caixas Anteriores.'
  }
  $pages.SelectedIndex=1
  [Windows.Forms.Application]::DoEvents()
  $oldGrid=$type.GetField('previousMovements',$flags).GetValue($form)
  if($oldGrid.Rows.Count -lt 0){throw 'Falta lista de movimentos anteriores.'}
  $screens=Join-Path $out 'Previa-Caixas-Anteriores.png'
  $img=[Drawing.Bitmap]::new($form.ClientSize.Width,$form.ClientSize.Height)
  try{$form.DrawToBitmap($img,[Drawing.Rectangle]::new([Drawing.Point]::Empty,$form.ClientSize))
   $img.Save($screens,[Drawing.Imaging.ImageFormat]::Png)
  }finally{$img.Dispose()}
  $backupCount=@(Get-ChildItem -LiteralPath $backupFolder -File -Filter 'caixa-antes-alteracao-*.db').Count
  if($backupCount -lt 7){throw "Faltam backups obrigatorios do Caixa: $backupCount"}
  Write-Host "CAIXA OK: abertura troco, entrada, sangria, fechamento, correcao retroativa, historico, $auditCount auditorias e $backupCount backups."
 }finally{$form.Dispose()}
}finally{
 if($null -eq $oldSetting){if(Test-Path $setting){Remove-Item $setting -Force}}
 else{[IO.File]::WriteAllText($setting,$oldSetting)}
 [Environment]::CurrentDirectory=$oldCurrent
}
