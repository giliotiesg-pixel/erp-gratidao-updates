# Icone azul/vermelho gerado localmente, sem recursos externos.
# Produz ICO multi-resolucao para executavel, instalador e atalhos.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$bitmap = [System.Drawing.Bitmap]::new(256,256)
$g = [System.Drawing.Graphics]::FromImage($bitmap)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$blue = [System.Drawing.Color]::FromArgb(11,51,147)
$red = [System.Drawing.Color]::FromArgb(215,20,36)
$white = [System.Drawing.Color]::White
$path = [System.Drawing.Drawing2D.GraphicsPath]::new()
$path.AddArc(6,6,242,242,0,90)
$path.Reset()
$path.AddArc(6,6,55,55,180,90)
$path.AddArc(194,6,55,55,270,90)
$path.AddArc(194,194,55,55,0,90)
$path.AddArc(6,194,55,55,90,90)
$path.CloseFigure()
$background = [System.Drawing.Drawing2D.LinearGradientBrush]::new([System.Drawing.Rectangle]::new(0,0,256,256),
 [System.Drawing.Color]::FromArgb(15,111,231),[System.Drawing.Color]::FromArgb(8,24,92),
 [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal)
$g.FillPath($background,$path)
$edge = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(137,205,255),4)
$g.DrawPath($edge,$path)
$redPen = [System.Drawing.Pen]::new($red,17)
$redPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$redPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$g.DrawArc($redPen,25,19,207,161,205,120)
$basketPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
$basketPath.AddPolygon([System.Drawing.Point[]]@(
 ([System.Drawing.Point]::new(83,92)),
 ([System.Drawing.Point]::new(210,92)),
 ([System.Drawing.Point]::new(190,157)),
 ([System.Drawing.Point]::new(103,157))))
$g.FillPath(([System.Drawing.SolidBrush]::new($red)),$basketPath)
$whitePen = [System.Drawing.Pen]::new($white,12)
$whitePen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
$whitePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$whitePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$g.DrawLines($whitePen,[System.Drawing.Point[]]@(
 ([System.Drawing.Point]::new(47,73)),
 ([System.Drawing.Point]::new(72,73)),
 ([System.Drawing.Point]::new(98,167)),
 ([System.Drawing.Point]::new(189,167))))
$g.DrawLine($whitePen,81,92,210,92)
$g.DrawLine($whitePen,210,92,191,153)
$g.DrawLine($whitePen,191,153,98,153)
$gridPen = [System.Drawing.Pen]::new($white,5)
foreach($x in @(116,145,174)){$g.DrawLine($gridPen,$x,99,$x,150)}
$g.DrawLine($gridPen,100,125,200,125)
$wheels = [System.Drawing.SolidBrush]::new($white)
$g.FillEllipse($wheels,100,174,30,30)
$g.FillEllipse($wheels,168,174,30,30)
$g.FillEllipse(([System.Drawing.SolidBrush]::new($blue)),108,182,14,14)
$g.FillEllipse(([System.Drawing.SolidBrush]::new($blue)),176,182,14,14)
$badge = [System.Drawing.SolidBrush]::new($red)
$g.FillRectangle($badge,51,207,154,35)
$font = [System.Drawing.Font]::new('Segoe UI',18,[System.Drawing.FontStyle]::Bold,[System.Drawing.GraphicsUnit]::Pixel)
$fmt = [System.Drawing.StringFormat]::new()
$fmt.Alignment = [System.Drawing.StringAlignment]::Center
$g.DrawString('AMZ.G',$font,([System.Drawing.SolidBrush]::new($white)),([System.Drawing.RectangleF]::new(51,209,154,32)),$fmt)
$g.Dispose()
$background.Dispose()
$path.Dispose()
$bitmap.Save((Join-Path $dir 'Armazem-Gratidao-Icone.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$images = New-Object System.Collections.Generic.List[object]
foreach($size in @(16,32,48,64,128,256)){
 $mini=[System.Drawing.Bitmap]::new($size,$size)
 $graphics=[System.Drawing.Graphics]::FromImage($mini)
 $graphics.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
 $graphics.SmoothingMode=[System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
 $graphics.DrawImage($bitmap,0,0,$size,$size)
 $graphics.Dispose()
 $stream=[System.IO.MemoryStream]::new()
 $mini.Save($stream,[System.Drawing.Imaging.ImageFormat]::Png)
 $images.Add(@{Size=$size;Bytes=$stream.ToArray()})
 $stream.Dispose();$mini.Dispose()
}
$bitmap.Dispose()
$dest=Join-Path $dir 'Armazem-Gratidao.ico'
$file=[System.IO.File]::Create($dest)
$writer=[System.IO.BinaryWriter]::new($file)
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
