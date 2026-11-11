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
$seed='import sqlite3,sys; c=sqlite3.connect(sys.argv[1]); c.execute("CREATE TABLE products(id INTEGER PRIMARY KEY,barcode TEXT,name TEXT,price REAL,cost REAL,stock REAL,brand TEXT,category TEXT)"); c.commit(); c.close()'
python -c $seed $db
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
  $dialogs=@([Windows.Forms.Application]::OpenForms | Where-Object {$_.Text -eq 'Inserir produto'})
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
   $null=$cost.Focus()
   [Windows.Forms.Application]::DoEvents()
   $null=$dlg.SelectNextControl($cost,$true,$true,$true,$true)
   [Windows.Forms.Application]::DoEvents()
   if(!$price.ContainsFocus){throw 'Ao sair do Custo, o foco pulou o Preco de venda.'}
   $script:done=$true
  }catch{$script:errorMessage=$_.Exception.Message}
  finally{
   $timer.Stop()
   $dlg.DialogResult=[Windows.Forms.DialogResult]::Cancel
   $dlg.Close()
  }
 })
 $timer.Start()
 $products.OpenNewProduct('')
 if($script:errorMessage){throw $script:errorMessage}
 if(!$script:done){throw 'Janela Inserir Produto nao foi testada.'}
 Write-Host 'CADASTRO OK: foco do Custo passa ao Preco e a calculadora nao ocupa a lateral.'
}finally{
 if($null -ne $timer){$timer.Stop();$timer.Dispose()}
 if($null -ne $products){$products.Close();$products.Dispose()}
 if(Test-Path $db){Remove-Item -LiteralPath $db -Force}
}
