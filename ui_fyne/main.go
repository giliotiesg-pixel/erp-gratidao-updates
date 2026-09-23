package main

import (
 "strings"
 "time"
 "fyne.io/fyne/v2"
 "fyne.io/fyne/v2/app"
 "fyne.io/fyne/v2/container"
 "fyne.io/fyne/v2/canvas"
 "image/color"
 "fyne.io/fyne/v2/theme"
 "fyne.io/fyne/v2/widget"
)
func metricCard(title,value,note string)fyne.CanvasObject{return widget.NewCard(title,note,container.NewPadded(widget.NewLabelWithStyle(value,fyne.TextAlignLeading,fyne.TextStyle{Bold:true})))}
type gratitudeTheme struct{ fyne.Theme }
func (gratitudeTheme) Color(n fyne.ThemeColorName,v fyne.ThemeVariant) color.Color {
 switch n {
 case theme.ColorNamePrimary: return color.NRGBA{R:18,G:76,B:170,A:255}
 case theme.ColorNameError: return color.NRGBA{R:210,G:35,B:42,A:255}
 case theme.ColorNameSelection: return color.NRGBA{R:18,G:76,B:170,A:70}
 case theme.ColorNameForeground: return color.NRGBA{R:0,G:0,B:0,A:255}
 }
 return theme.DefaultTheme().Color(n,v)
}
func brandHeader() fyne.CanvasObject {
 cart:=canvas.NewText("🛒",color.NRGBA{R:18,G:76,B:170,A:255});cart.TextSize=30;cart.TextStyle=fyne.TextStyle{Bold:true}
 armazem:=canvas.NewText("ARMAZÉM",color.NRGBA{R:18,G:76,B:170,A:255});armazem.TextSize=18;armazem.TextStyle=fyne.TextStyle{Bold:true}
 gratidao:=canvas.NewText("GRATIDÃO",color.NRGBA{R:210,G:35,B:42,A:255});gratidao.TextSize=18;gratidao.TextStyle=fyne.TextStyle{Bold:true}
 pro:=canvas.NewText("PRO",color.Black);pro.TextSize=18;pro.TextStyle=fyne.TextStyle{Bold:true}
 return container.NewVBox(container.NewCenter(cart),container.NewCenter(container.NewHBox(armazem,gratidao,pro)),widget.NewSeparator())
}
