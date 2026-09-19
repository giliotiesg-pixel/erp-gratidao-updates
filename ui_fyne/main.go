package main

import (
	"fyne.io/fyne/v2"
	"fyne.io/fyne/v2/app"
	"fyne.io/fyne/v2/container"
	"fyne.io/fyne/v2/theme"
	"fyne.io/fyne/v2/widget"
)

func main() {
	a := app.NewWithID("br.com.armazemgratidao.erp")
	a.Settings().SetTheme(theme.LightTheme())
	w := a.NewWindow("ERP Gratidão • Interface Moderna")
	w.Resize(fyne.NewSize(1360, 820))

	title := widget.NewLabelWithStyle("ERP Gratidão", fyne.TextAlignLeading, fyne.TextStyle{Bold:true})
	version := widget.NewLabel("Nova interface • dados preservados")
	search := widget.NewEntry()
	search.SetPlaceHolder("Ir para módulo...")
	top := container.NewBorder(nil,nil,title,container.NewHBox(version),search)

	content := container.NewStack()
	show := func(name, sub string) {
		h := widget.NewLabelWithStyle(name, fyne.TextAlignLeading, fyne.TextStyle{Bold:true})
		d := widget.NewLabel(sub)
		card := widget.NewCard("", "", container.NewVBox(h,d,widget.NewSeparator(),
			widget.NewLabel("Interface moderna em construção isolada do ERP oficial.")))
		content.Objects=[]fyne.CanvasObject{container.NewPadded(card)}
		content.Refresh()
	}

	menu := container.NewVBox()
	items := []struct{name,sub string}{
		{"Início","Visão geral e atalhos principais"},
		{"PDV","Venda rápida"},
		{"Vendas","Histórico e manutenção de vendas"},
		{"Caixa","Abertura e fechamento"},
		{"Produtos","Cadastro e consulta"},
		{"Estoque","Quantidade e movimentações"},
		{"Validade","Controle de validade"},
		{"Compras","Entrada de produtos"},
		{"Clientes / Fornecedores","Cadastros"},
		{"Fiado","Contas de clientes"},
		{"Lucro","Resultados"},
		{"Relatórios","Consultas gerenciais"},
		{"Consulta de Produto","Consulta global"},
		{"Configurações","Sistema e atualizações"},
	}
	for _, item := range items {
		it:=item
		b:=widget.NewButton(it.name,func(){show(it.name,it.sub)})
		b.Importance=widget.LowImportance
		menu.Add(b)
	}
	side:=container.NewVScroll(container.NewPadded(menu))
	side.SetMinSize(fyne.NewSize(225,650))
	body:=container.NewBorder(nil,nil,side,nil,content)
	w.SetContent(container.NewBorder(container.NewPadded(top),nil,nil,nil,body))
	show("Início","Visão geral e atalhos principais")
	w.ShowAndRun()
}
