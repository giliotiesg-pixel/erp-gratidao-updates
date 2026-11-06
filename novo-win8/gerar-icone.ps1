# Icone azul/vermelho gerado localmente, sem recursos externos.
# Produz ICO multi-resolucao para executavel, instalador e atalhos.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$bitmap = New-Object System.Drawing.Bitmap(256,256)
$g = [System.Drawing.Graphics]::FromImage($bitmap)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$blue = [System.Drawing.Color]::FromArgb(11,51,147)
$red = [System.Drawing.Color]::FromArgb(215,20,36)
$white = [System.Drawing.Color]::White
$path = New-Object System.Drawing.Drawing2D.GraphicsPath
$path.AddArc(6,6,242,242,0,90)
$path.Reset()
$path.AddArc(6,6,55,55,180,90)
$path.AddArc(194,6,55,55,270,90)
$path.AddArc(194,194,55,55,0,90)
$path.AddArc(6,194,55,55,90,90)
$path.CloseFigure()
$background = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Rectangle(0,0,256,256)),
 [System.Drawing.Color]::FromArgb(15,111,231),[System.Drawing.Color]::FromArgb(8,24,92),
 [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal)
$g.FillPath($background,$path)
$edge = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(137,205,255),4)
$g.DrawPath($edge,$path)
$redPen = New-Object System.Drawing.Pen($red,17)
$redPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$redPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$g.DrawArc($redPen,25,19,207,161,205,120)
$basketPath = New-Object System.Drawing.Drawing2D.GraphicsPath
$basketPath.AddPolygon([System.Drawing.Point[]]@(
 (New-Object System.Drawing.Point(83,92)),
 (New-Object System.Drawing.Point(210,92)),
 (New-Object System.Drawing.Point(190,157)),
 (New-Object System.Drawing.Point(103,157))))
$g.FillPath((New-Object System.Drawing.SolidBrush($red)),$basketPath)
$whitePen = New-Object System.Drawing.Pen($white,12)
$whitePen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
$whitePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$whitePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$g.DrawLines($whitePen,[System.Drawing.Point[]]@(
 (New-Object System.Drawing.Point(47,73)),
 (New-Object System.Drawing.Point(72,73)),
 (New-Object System.Drawing.Point(98,167)),
 (New-Object System.Drawing.Point(189,167))))
$g.DrawLine($whitePen,81,92,210,92)
$g.DrawLine($whitePen,210,92,191,153)
$g.DrawLine($whitePen,191,153,98,153)
$gridPen = New-Object System.Drawing.Pen($white,5)
foreach($x in @(116,145,174)){$g.DrawLine($gridPen,$x,99,$x,150)}
$g.DrawLine($gridPen,100,125,200,125)
$wheels = New-Object System.Drawing.SolidBrush($white)
$g.FillEllipse($wheels,100,174,30,30)
$g.FillEllipse($wheels,168,174,30,30)
$g.FillEllipse((New-Object System.Drawing.SolidBrush($blue)),108,182,14,14)
$g.FillEllipse((New-Object System.Drawing.SolidBrush($blue)),176,182,14,14)
$badge = New-Object System.Drawing.SolidBrush($red)
$g.FillRectangle($badge,51,207,154,35)
$font = New-Object System.Drawing.Font('Segoe UI',18,[System.Drawing.FontStyle]::Bold,[System.Drawing.GraphicsUnit]::Pixel)
$fmt = New-Object System.Drawing.StringFormat
$fmt.Alignment = [System.Drawing.StringAlignment]::Center
$g.DrawString('AMZ.G',$font,(New-Object System.Drawing.SolidBrush($white)),(New-Object System.Drawing.RectangleF(51,209,154,32)),$fmt)
$g.Dispose()
$background.Dispose()
$path.Dispose()
$bitmap.Save((Join-Path $dir 'Armazem-Gratidao-Icone.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$images = New-Object System.Collections.Generic.List[object]
foreach($size in @(16,32,48,64,128,256)){
 $mini=New-Object System.Drawing.Bitmap($size,$size)
 $graphics=[System.Drawing.Graphics]::FromImage($mini)
 $graphics.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
 $graphics.SmoothingMode=[System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
 $graphics.DrawImage($bitmap,0,0,$size,$size)
 $graphics.Dispose()
 $stream=New-Object System.IO.MemoryStream
 $mini.Save($stream,[System.Drawing.Imaging.ImageFormat]::Png)
 $images.Add(@{Size=$size;Bytes=$stream.ToArray()})
 $stream.Dispose();$mini.Dispose()
}
$bitmap.Dispose()
$dest=Join-Path $dir 'Armazem-Gratidao.ico'
$file=[System.IO.File]::Create($dest)
$writer=New-Object System.IO.BinaryWriter($file)
try {
 $writer.Write([UInt16]0);$writer.Write([UInt16]1);$writer.Write([UInt16]$images.Count)
 $offset=6+16*$images.Count
 foreach($image in $images){
  $size=[int]$image.Size
  $writer.Write([byte]($size%256));$writer.Write([byte]($size%256))
  $writer.Write([byte]0);$writer.Write([byte]0)
  $writer.Write([UInt16]1);$writer.Write([UInt16]32)
  $writer.Write([UInt32]$image.Bytes.Length);$writer.Write([UInt32]$offset)
  $offset+=$image.Bytes.Length
 }
 foreach($image in $images){$writer.Write([byte[]]$image.Bytes)}
}finally{$writer.Dispose();$file.Dispose()}
Write-Host "Icone criado: $dest"
