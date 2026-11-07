param(
 [Parameter(Mandatory=$true)][string]$Executable,
 [Parameter(Mandatory=$true)][string]$PreviewFile
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$exe=[IO.Path]::GetFullPath($Executable)
$previewAbsolute=[IO.Path]::GetFullPath($PreviewFile)
if(!(Test-Path -LiteralPath $exe)){throw "PDV nao encontrado: $exe"}
[Environment]::CurrentDirectory=[IO.Path]::GetDirectoryName($exe)
[System.Windows.Forms.Application]::EnableVisualStyles()
$assembly=[System.Reflection.Assembly]::LoadFrom($exe)
$type=$assembly.GetType('ArmazemGratidao.MainForm',$true)
$form=$null
try {
 $form=[Activator]::CreateInstance($type)
 $form.StartPosition='Manual'
 $form.Location=[System.Drawing.Point]::new(24,24)
 $form.Size=[System.Drawing.Size]::new(1230,800)
 $form.Show()
 [System.Windows.Forms.Application]::DoEvents()
 $start=@($form.Controls.Find('amzgStartPanel',$true))
 $quick=@($form.Controls.Find('amzgQuickActions',$true))
 $search=@($form.Controls.Find('amzgStartSearch',$true))
 $sections=@($form.Controls.Find('amzgStartSections',$true))
 $workspace=@($form.Controls.Find('amzgStartWorkspace',$true))
 if($start.Count -ne 1 -or $quick.Count -ne 1 -or $search.Count -ne 1 -or
    $sections.Count -ne 1 -or $workspace.Count -ne 1){
    throw 'Menu Iniciar incompleto: painel, pesquisa, atalhos ou secoes ausentes'
 }
 if(!$start[0].Visible){throw 'Menu Iniciar deveria abrir inicialmente'}
 if($quick[0].Controls.Count -lt 8){throw 'Faltam os atalhos mais usados'}
 if($sections[0].Controls.Count -ne 5){throw 'O menu deveria conter cinco secoes ocultaveis'}
 $pageStart=@($form.Controls.Find('amzgStartButton',$true))
 if($pageStart.Count -ne 1){throw 'Botao Iniciar nao esta visivel na barra do menu'}
 if($form.Text -notmatch ('Build '+$env:AMZG_BUILD)){throw 'Barra de titulo nao mostra a build correta'}
 $total=0
 $originalLabels=@{}
 foreach($block in $sections[0].Controls){
    $row=@($block.Controls | Where-Object {$_ -is [System.Windows.Forms.FlowLayoutPanel]})
    $bar=@($block.Controls | Where-Object {$_ -is [System.Windows.Forms.Panel]})
    if($row.Count -ne 1 -or $bar.Count -lt 1){throw 'Secao sem barra ou fileira horizontal'}
    if(@($bar[0].Controls | Where-Object {$_ -is [System.Windows.Forms.Button]}).Count -lt 1){
       throw 'Secao sem botao Mostrar / Ocultar'
    }
    $total+=$row[0].Controls.Count
    foreach($button in $row[0].Controls){
      $originalLabels[$button.AccessibleDescription]=$button.Text
    }
 }
 if($total -lt 20){throw 'Atalhos de modulos nao foram preservados'}
 $search[0].Text='produto'
 [System.Windows.Forms.Application]::DoEvents()
 $search[0].Text=''
 [System.Windows.Forms.Application]::DoEvents()
 foreach($block in $sections[0].Controls){
   foreach($row in @($block.Controls | Where-Object {$_ -is [System.Windows.Forms.FlowLayoutPanel]})){
     foreach($button in $row.Controls){
       if($button.Text -ne $originalLabels[$button.AccessibleDescription]){
         throw "Pesquisa alterou texto do modulo $($button.AccessibleDescription): $($button.Text)"
       }
     }
   }
 }
 $toggle=$type.GetMethod('ToggleStartMenu',
   [System.Reflection.BindingFlags]::Instance -bor
   [System.Reflection.BindingFlags]::NonPublic)
 if($null -eq $toggle){throw 'Botao Iniciar nao tem acao de abrir/recolher'}
 $toggle.Invoke($form,@()) | Out-Null
 [System.Windows.Forms.Application]::DoEvents()
 if($start[0].Visible){throw 'Botao Iniciar nao recolheu o painel'}
 $toggle.Invoke($form,@()) | Out-Null
 [System.Windows.Forms.Application]::DoEvents()
 if(!$start[0].Visible){throw 'Botao Iniciar nao reabriu o painel'}
 $directory=[IO.Path]::GetDirectoryName($previewAbsolute)
 [IO.Directory]::CreateDirectory($directory) | Out-Null
 $picture=[System.Drawing.Bitmap]::new($form.ClientSize.Width,$form.ClientSize.Height)
 try {
   $rect=[System.Drawing.Rectangle]::new([System.Drawing.Point]::Empty,$form.ClientSize)
   $form.DrawToBitmap($picture,$rect)
   $picture.Save($previewAbsolute,[System.Drawing.Imaging.ImageFormat]::Png)
 }finally{$picture.Dispose()}
 if((Get-Item -LiteralPath $previewAbsolute).Length -lt 5000){
   throw 'Previa do menu gerada incompleta'
 }
 Write-Host "TESTE MENU INICIAR OK: $total modulos, $($quick[0].Controls.Count) atalhos, 5 secoes, pesquisa e abrir/recolher"
 Write-Host "PREVIA VISUAL: $previewAbsolute"
}finally{
 if($null -ne $form){try{$form.Close()}catch{};$form.Dispose()}
}
