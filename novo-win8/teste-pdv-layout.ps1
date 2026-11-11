param(
 [Parameter(Mandatory=$true)][string]$Executable,
 [Parameter(Mandatory=$true)][string]$PreviewFile
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$exe=[IO.Path]::GetFullPath($Executable)
$preview=[IO.Path]::GetFullPath($PreviewFile)
[Environment]::CurrentDirectory=[IO.Path]::GetDirectoryName($exe)
[System.Windows.Forms.Application]::EnableVisualStyles()
$asm=[System.Reflection.Assembly]::LoadFrom($exe)
$pdvType=$asm.GetType('ArmazemGratidao.PdvForm',$true)
$mainType=$asm.GetType('ArmazemGratidao.MainForm',$true)
$main=$null;$pdv=$null
$database=Join-Path $env:RUNNER_TEMP 'pdv-layout-dados-descartaveis.db'
try{
 $main=[Activator]::CreateInstance($mainType)
 # Usar o construtor publico de um argumento elimina ambiguidades do binder PowerShell 5.1.
 $ctor=$pdvType.GetConstructor([type[]]@([string]))
 if($null -eq $ctor){throw 'Construtor publico PdvForm(string) ausente.'}
 $invokePdv=New-Object 'System.Object[]' 1
 $invokePdv[0]=[string]$database
 $pdv=$ctor.Invoke($invokePdv)
 # O clique em FIADO deve usar uma selecao de cliente obrigatoria distinta.
 $private=[Reflection.BindingFlags]::Instance -bor [Reflection.BindingFlags]::NonPublic
 $fiadoPicker=$pdvType.GetMethod('ChooseCustomerInWindow',$private,$null,[type[]]@([bool]),$null)
 if($null -eq $fiadoPicker -or $fiadoPicker.ReturnType -ne [bool]){
  throw 'Fiado sem lista de clientes obrigatoria ao finalizar.'
 }
 # Reproduz a mesma injecao de navegacao feita ao abrir PDV via menu Iniciar.
 $flags=[System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::NonPublic
 $backMethod=$mainType.GetMethod('AddBackToMenu',$flags)
 if($null -eq $backMethod){throw 'MainForm sem botao Voltar.'}
 $invokeBack=New-Object 'System.Object[]' 1
 $invokeBack[0]=[System.Windows.Forms.Form]$pdv
 $backMethod.Invoke([object]$main,$invokeBack) | Out-Null
 $pdv.StartPosition='Manual'
 $pdv.Location=[System.Drawing.Point]::new(20,20)
 $pdv.Size=[System.Drawing.Size]::new(1180,740)
 $pdv.Show()
 [System.Windows.Forms.Application]::DoEvents()
 if(@($pdv.Controls.Find('amzgOpenCalculatorModule',$true)).Count -ne 1){throw 'Calculadora ausente no PDV'}
 $binding=[Reflection.BindingFlags]::Static -bor [Reflection.BindingFlags]::NonPublic
 $detect=$pdvType.GetMethod('IsCocaReturnableBottle',$binding)
 if($null -eq $detect){throw 'Detector de cascos ausente.'}
 foreach($example in @(
  @{p='Coca Cola 1 L retornavel vidro';b=$true},
  @{p='Coca-Cola Retornável 2 Litros';b=$true},
  @{p='Coca Cola 1 litro vidro';b=$true},
  @{p='Coca Cola 2L RET';b=$true},
  @{p='Coca Cola 1000 ml retornavel';b=$true},
  @{p='Coca Cola 2000ml retornavel';b=$true},
  @{p='Coca Cola 200 ml';b=$false},
  @{p='Coca Cola 600 ml';b=$false},
  @{p='Coca Cola 350 ml lata';b=$false},
  @{p='Coca Cola 2 L PET';b=$false},
  @{p='Coca Cola 2 L descartavel';b=$false},
  @{p='Coca Cola 2.5 L retornavel';b=$false},
  @{p='Guarana retornavel 2 L';b=$false},
  @{p='Coca Cola 1 L nao retornavel';b=$false}
 )){
  $actual=[bool]$detect.Invoke($null,[object[]]@([string]$example.p))
  if($actual -ne $example.b){throw "Casco incorreto: $($example.p) = $actual"}
 }
 $find={param([string]$name)
  $controls=@($pdv.Controls.Find($name,$true))
  if($controls.Count -ne 1){throw "Controle obrigatorio ausente ou duplicado: $name ($($controls.Count))"}
  return $controls[0]
 }
 $brand=& $find 'amzgPdvBrand'
 $back=& $find 'amzgBackBar'
 $search=& $find 'amzgPdvSearchBar'
 $code=& $find 'amzgPdvCodeInput'
 $list=& $find 'amzgPdvItemBox'
 $actions=& $find 'amzgPdvActions'
 $finish=& $find 'amzgPdvFinish'
 $summary=& $find 'amzgPdvSummary'
 $total=& $find 'amzgPdvTotal'
 $other=& $find 'amzgPdvMoreOptions'
 $grid=@($pdv.Controls.Find('amzgPdvItemBox',$true)[0].Controls |
    Where-Object {$_ -is [System.Windows.Forms.DataGridView] -and $_.Visible}) |
    Select-Object -First 1
 if($null -eq $grid){throw 'Tabela de itens ausente no novo PDV.'}
 $screenY={param($control)
  return $control.PointToScreen([System.Drawing.Point]::Empty).Y
 }
 if((& $screenY $brand) -ge (& $screenY $back) -or
    (& $screenY $back) -ge (& $screenY $search) -or
    (& $screenY $search) -ge (& $screenY $list)){
  throw 'Ordem incorreta: marca, Voltar, Codigo/Nome e lista.'
 }
 if($actions.PointToScreen([System.Drawing.Point]::Empty).X -le
    $list.PointToScreen([System.Drawing.Point]::Empty).X+$list.Width){
  throw 'Botoes principais nao foram colocados na lateral direita.'
 }
 if((& $screenY $summary) -le (& $screenY $list) -or
    $total.Parent -eq $null){throw 'Resumo e total nao ficaram na base.'}
 if($list.ClientSize.Width -lt 390 -or $actions.ClientSize.Width -lt 220){
  throw "Layout estreito ou nao distribuido: lista=$($list.Width), acoes=$($actions.Width)"
 }
 foreach($expected in @('amzgPdvAddProduct','amzgPdvMoreOptions',
    'amzgPdvCustomer','amzgPdvReceived','amzgPdvChange','amzgPdvTotal')){
  $null=& $find $expected
 }
 [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($preview)) | Out-Null
 $bitmap=[System.Drawing.Bitmap]::new($pdv.ClientSize.Width,$pdv.ClientSize.Height)
 try{
  $pdv.DrawToBitmap($bitmap,[System.Drawing.Rectangle]::new(
   [System.Drawing.Point]::Empty,$pdv.ClientSize))
  $bitmap.Save($preview,[System.Drawing.Imaging.ImageFormat]::Png)
 }finally{$bitmap.Dispose()}
 if((Get-Item -LiteralPath $preview).Length -lt 7000){
  throw 'Preview da tela do PDV incompleta.'
 }
 Write-Host 'PDV OK: Voltar no topo, produto abaixo, lista central, botoes a direita e total na base.'
 Write-Host "PREVIA REAL PDV: $preview"
}finally{
 if($null -ne $pdv){try{$pdv.Close()}catch{};$pdv.Dispose()}
 if($null -ne $main){try{$main.Close()}catch{};$main.Dispose()}
}
