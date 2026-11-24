param([Parameter(Mandatory=$true)][string]$Executable)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
# Resolver caminho contra a raiz do repositorio, nao contra CurrentDirectory do teste anterior.
$repoRoot=Split-Path -Parent $PSScriptRoot
$exe=if([IO.Path]::IsPathRooted($Executable)){[IO.Path]::GetFullPath($Executable)}
     else{[IO.Path]::GetFullPath((Join-Path $repoRoot $Executable))}
if(!(Test-Path -LiteralPath $exe)){throw "Executavel de Produtos nao encontrado: $exe"}
[Environment]::CurrentDirectory=[IO.Path]::GetDirectoryName($exe)
$db=Join-Path $env:RUNNER_TEMP ('amzg-produtos-tabulacao-'+[Guid]::NewGuid().ToString('N')+'.db')
$seed=@'
import sqlite3
import sys
connection=sqlite3.connect(sys.argv[1])
connection.execute("CREATE TABLE products(id INTEGER PRIMARY KEY,barcode TEXT,name TEXT,price REAL,cost REAL,stock REAL,brand TEXT,category TEXT,sold_by_weight INTEGER DEFAULT 0)")
connection.execute("CREATE TABLE runtime_operation_context(id INTEGER PRIMARY KEY, authorized INTEGER NOT NULL, operation TEXT NOT NULL)")
connection.execute("INSERT INTO runtime_operation_context VALUES(1,0,'')")
connection.execute("CREATE TABLE stock_audit_log(product_id INTEGER, product_name TEXT, barcode TEXT, event_type TEXT, quantity_before NUMERIC, quantity_after NUMERIC, quantity_delta NUMERIC, reference_type TEXT, reason TEXT)")
connection.commit()
connection.close()
'@
$seedPath=Join-Path $env:RUNNER_TEMP ('amzg-seed-produtos-'+[Guid]::NewGuid().ToString('N')+'.py')
[IO.File]::WriteAllText($seedPath,$seed,(New-Object System.Text.UTF8Encoding($false)))
python $seedPath $db
if($LASTEXITCODE -ne 0){throw 'Nao criou SQLite de teste.'}
$asm=[Reflection.Assembly]::LoadFrom($exe)
$type=$asm.GetType('ArmazemGratidao.ProdutosForm',$true)
$products=$null;$timer=$null
$script:done=$false
$script:errorMessage=''
try{
 $products=[Activator]::CreateInstance($type,[object[]]@([string]$db))
 $products.StartPosition='Manual'
 $products.Location=[Drawing.Point]::new(20,20)
 $products.Show()
 [Windows.Forms.Application]::DoEvents()
 $timer=New-Object System.Windows.Forms.Timer
 $timer.Interval=150
 $timer.Add_Tick({
  $dialogs=@([Windows.Forms.Application]::OpenForms | Where-Object {$_.Text -eq 'Cadastrar Produto'})
  if($dialogs.Count -eq 0){return}
  $dlg=$dialogs[0]
  try{
   $fields=@{}
   foreach($key in @('amzgInsertBarcode','amzgInsertName','amzgInsertStock','amzgInsertCost','amzgInsertPrice')){
    $controls=@($dlg.Controls.Find($key,$true))
    if($controls.Count -ne 1){throw "Campo faltando: $key"}
    $fields[$key]=$controls[0]
   }
   $code=$fields['amzgInsertBarcode'];$name=$fields['amzgInsertName']
   $stock=$fields['amzgInsertStock'];$cost=$fields['amzgInsertCost']
   $price=$fields['amzgInsertPrice']
   if($code.TabIndex -ne 0 -or $name.TabIndex -ne 1 -or
      $stock.TabIndex -ne 2 -or $cost.TabIndex -ne 3 -or
      $price.Parent.TabIndex -ne 4 -or $price.TabIndex -ne 0){
    throw 'TAB nao segue Codigo, Descricao, QTD, Custo e Preco.'
   }
   if(@($dlg.Controls.Find('amzgOpenCalculatorModule',$true)).Count -ne 1){
    throw 'Botao Calculadora ausente no cadastro de produtos.'
   }
   if($cost.Text -ne '' -or $price.Text -ne '' -or $stock.Text -ne '0'){throw "Campos iniciais invalidos: custo=[$($cost.Text)] preco=[$($price.Text)] qtd=[$($stock.Text)]"}
   $weighted=@($dlg.Controls.Find('amzgInsertByWeight',$true))[0]
   $weighted.Checked=$true
   $stock.Text='1,250'
   if($stock.Value -ne [decimal]1.25){throw 'Quantidade por peso nao aceita tres casas.'}
   $stock.Text='0'
   $weighted.Checked=$false
   $stock.Text='2,5'
   if($stock.Value -ne [decimal]0){throw 'Quantidade unitaria aceitou fracao.'}
   $stock.Text='0'
   $null=$cost.Focus()
   [Windows.Forms.Application]::DoEvents()
   $null=$dlg.SelectNextControl($cost,$true,$true,$true,$true)
   [Windows.Forms.Application]::DoEvents()
   if(!$price.ContainsFocus){throw 'Ao sair do Custo, o foco pulou o Preco de venda.'}
   $work=[Windows.Forms.Screen]::FromControl($dlg).WorkingArea
   if($dlg.Text -ne 'Cadastrar Produto' -or $dlg.Width -gt $work.Width -or $dlg.Height -gt $work.Height){
    throw 'Cadastro deve manter o titulo e caber no monitor, sem limite compacto antigo.'
   }
   $layout=$price.Parent.Parent.Parent
   $buttons=New-Object 'System.Collections.Generic.List[System.Windows.Forms.Button]'
   $walk=$null
   $walk={
    param($node)
    if($node -is [Windows.Forms.Button]){$buttons.Add($node)}
    foreach($child in $node.Controls){& $walk $child}
   }
   & $walk $dlg
   foreach($caption in @('Procurar','Verificar','Atualizar dados','Cadastrar Produto','Cancelar')){
    if(@($buttons | Where-Object {$_.Text -eq $caption}).Count -ne 1){
     throw "Botao ausente no novo formulario: $caption"
    }
   }
   $fixed=@($dlg.Controls.Find('amzgInsertFixedFooter',$true))
   if($fixed.Count -ne 1 -or $fixed[0].Parent -ne $dlg -or $fixed[0].Dock -ne [Windows.Forms.DockStyle]::Bottom){
    throw 'Rodape Salvar/Cancelar deve ficar fixo na janela, fora da area rolavel.'
   }
   foreach($buttonName in @('amzgInsertSave','amzgInsertCancel')){
    if(@($fixed[0].Controls.Find($buttonName,$true)).Count -ne 1){
     throw "Botao obrigatorio ausente do rodape fixo: $buttonName"
    }
   }
   $barcode=@($dlg.Controls.Find('amzgInsertBarcode',$true))
   $name=@($dlg.Controls.Find('amzgInsertName',$true))
   if($barcode.Count -ne 1 -or $name.Count -ne 1){throw 'Campos de codigo ou descricao ausentes.'}
   if($barcode[0].Width -gt 260){throw 'Campo codigo de barras longo demais.'}
   if($name[0].Width -lt $barcode[0].Width){throw 'Descricao deve ser mais larga que codigo.'}
   $generator=@($dlg.Controls.Find('amzgGenerateBarcode',$true))
   if($generator.Count -ne 1){throw 'Gerador de codigo de barras ausente.'}
   $generator[0].PerformClick()
   $generated=$code.Text
   if($generated -notmatch '^20[0-9]{11}$'){throw "Codigo proprio invalido: $generated"}
   $sum=0
   for($i=0;$i -lt 12;$i++){$sum += [int]::Parse($generated.Substring($i,1)) * $(if($i%2 -eq 0){1}else{3})}
   if(((10-($sum%10))%10) -ne [int]::Parse($generated.Substring(12,1))){throw 'Digito verificador invalido.'}
   $name[0].Text='Teste cadastro preco'
   $stock.Text='10'
   $cost.Text='11,75'
   $price.Text='16,99'
   if($cost.Value -ne [decimal]11.75 -or $price.Value -ne [decimal]16.99){throw 'Conversao de moeda brasileira incorreta.'}
   $gain=@($dlg.Controls.Find('amzgInsertGainPercent',$true))[0]
   $apply=@($dlg.Controls.Find('amzgApplyGain',$true))[0]
   $gain.Value=[decimal]50
   if($price.Value -ne [decimal]16.99){throw 'Preco manual foi alterado sem aplicar percentual.'}
   $apply.PerformClick()
   if($price.Value -ne [decimal]17.63){throw 'Percentual manual nao foi aplicado corretamente.'}
   $price.Value=[decimal]16.99
   $null=$cost.Focus()
   [Windows.Forms.Application]::DoEvents()
   if($price.Value -ne [decimal]16.99){throw 'Preco manual foi alterado pelo markup.'}
   $timer.Stop()
   $save=@($fixed[0].Controls.Find('amzgInsertSave',$true))[0]
   $save.PerformClick()
   if($dlg.DialogResult -ne [Windows.Forms.DialogResult]::OK){throw 'Cadastro nao foi salvo.'}
   $script:done=$true
  }catch{$script:errorMessage=$_.Exception.Message}
  finally{
   $timer.Stop()
   if(!$dlg.IsDisposed -and $dlg.DialogResult -ne [Windows.Forms.DialogResult]::OK){$dlg.DialogResult=[Windows.Forms.DialogResult]::Cancel;$dlg.Close()}
  }
 })
 $timer.Start()
 $products.OpenNewProduct('')
 if($script:errorMessage){throw $script:errorMessage}
 if(!$script:done){throw 'Janela Cadastrar Produto nao foi testada.'}
 $verify=@'
import sqlite3,sys
c=sqlite3.connect(sys.argv[1])
p=c.execute("SELECT id,price,cost,stock FROM products WHERE name='Teste cadastro preco'").fetchone()
assert p is not None and tuple(p[1:])==(16.99,11.75,10),p
assert c.execute("SELECT quantity_before,quantity_added,quantity_after FROM product_quantity_movements WHERE product_id=?",(p[0],)).fetchone()==(0,10,10)
assert c.execute("SELECT quantity_delta FROM stock_audit_log WHERE product_id=?",(p[0],)).fetchone()==(10,)
assert c.execute("SELECT authorized FROM runtime_operation_context WHERE id=1").fetchone()==(0,)
'@
 [IO.File]::WriteAllText($seedPath,$verify,(New-Object System.Text.UTF8Encoding($false)))
 python $seedPath $db
 if($LASTEXITCODE -ne 0){throw 'Valores persistidos ou auditoria do cadastro incorretos.'}
 Write-Host 'CADASTRO OK: codigo proprio, preco manual e estoque inicial persistidos.'
}finally{
 if($null -ne $timer){$timer.Stop();$timer.Dispose()}
 if($null -ne $products){$products.Close();$products.Dispose()}
 if(Test-Path $db){Remove-Item -LiteralPath $db -Force}
 if(Test-Path $seedPath){Remove-Item -LiteralPath $seedPath -Force}
}
