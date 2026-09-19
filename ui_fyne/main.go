package main

import (
 "fmt"
 "time"
 "fyne.io/fyne/v2"
 "fyne.io/fyne/v2/app"
 "fyne.io/fyne/v2/container"
 "fyne.io/fyne/v2/theme"
 "fyne.io/fyne/v2/widget"
)

func metricCard(title, value, note string) fyne.CanvasObject {
 v:=widget.NewLabelWithStyle(value,fyne.TextAlignLeading,fyne.TextStyle{Bold:true})
 v.TextStyle=fyne.TextStyle{Bold:true}
 return widget.NewCard(title,note,container.NewPadded(v))
}
func actionButton(label string, action func()) *widget.Button {
 b:=widget.NewButton(label,action); b.Importance=widget.MediumImportance; return b
}
func main() {
 a:=app.NewWithID("br.com.armazemgratidao.erp")
 a.Settings().SetTheme(theme.LightTheme())
 w:=a.NewWindow("ERP Gratidão • Interface Moderna")
 w.Resize(fyne.NewSize(1400,850))
 w.SetMaster()

 content:=container.NewStack()
 current:=widget.NewLabel("Início")
 clock:=widget.NewLabel("")
 go func(){for range time.Tick(time.Second){clock.SetText(time.Now().Format("02/01/2006 15:04"))}}()

 showPlaceholder:=func(name,sub string){
  current.SetText(name)
  title:=widget.NewLabelWithStyle(name,fyne.TextAlignLeading,fyne.TextStyle{Bold:true})
  desc:=widget.NewLabel(sub)
  content.Objects=[]fyne.CanvasObject{container.NewPadded(container.NewVBox(title,desc,widget.NewSeparator(),widget.NewCard("Módulo","Estrutura visual pronta para receber a função existente do ERP.",widget.NewLabel("Nenhuma regra operacional foi alterada."))))}
  content.Refresh()
 }

 var showHome func()
 showHome=func(){
  current.SetText("Início")
  greeting:=widget.NewLabelWithStyle("Visão geral",fyne.TextAlignLeading,fyne.TextStyle{Bold:true})
  subtitle:=widget.NewLabel("Acompanhamento rápido do Armazém Gratidão")
  cards:=container.NewGridWithColumns(4,
   metricCard("Vendas de hoje","—","carrega do ERP"),
   metricCard("Faturamento","R$ —","carrega do ERP"),
   metricCard("Produtos ativos","—","carrega do ERP"),
   metricCard("Estoque baixo","—","carrega do ERP"),
  )
  quick:=widget.NewCard("Acesso rápido","Operações mais usadas",container.NewGridWithColumns(4,
   actionButton("Nova venda • PDV",func(){showPlaceholder("PDV","Venda rápida")}),
   actionButton("Consultar produto",func(){showPlaceholder("Consulta de Produto","Consulta global")}),
   actionButton("Vendas",func(){showPlaceholder("Vendas","Histórico e manutenção")}),
   actionButton("Caixa",func(){showPlaceholder("Caixa","Abertura e fechamento")}),
  ))
  alerts:=widget.NewCard("Atenção","Indicadores que precisam de acompanhamento",container.NewVBox(
   widget.NewLabel("• Produtos com estoque baixo: —"),
   widget.NewLabel("• Produtos próximos da validade: —"),
   widget.NewLabel("• Fiado em aberto: R$ —"),
  ))
  footer:=widget.NewLabel("Interface nova isolada • banco e operações atuais preservados")
  content.Objects=[]fyne.CanvasObject{container.NewPadded(container.NewVBox(greeting,subtitle,cards,quick,alerts,footer))}
  content.Refresh()
 }

 menu:=container.NewVBox()
 addMenu:=func(label,sub string){
  b:=widget.NewButton(label,func(){if label=="Início"{showHome()}else{showPlaceholder(label,sub)}})
  b.Alignment=widget.ButtonAlignLeading; b.Importance=widget.LowImportance; menu.Add(b)
 }
 menu.Add(widget.NewLabelWithStyle("OPERAÇÃO",fyne.TextAlignLeading,fyne.TextStyle{Bold:true}))
 addMenu("Início","Visão geral e atalhos"); addMenu("PDV","Venda rápida"); addMenu("Vendas","Histórico e manutenção"); addMenu("Caixa","Abertura e fechamento")
 menu.Add(widget.NewSeparator()); menu.Add(widget.NewLabelWithStyle("CADASTROS / ESTOQUE",fyne.TextAlignLeading,fyne.TextStyle{Bold:true}))
 addMenu("Produtos","Cadastro e consulta"); addMenu("Estoque","Quantidade e movimentações"); addMenu("Validade","Controle de validade"); addMenu("Compras","Entrada de produtos"); addMenu("Clientes / Fornecedores","Cadastros")
 menu.Add(widget.NewSeparator()); menu.Add(widget.NewLabelWithStyle("FINANCEIRO",fyne.TextAlignLeading,fyne.TextStyle{Bold:true}))
 addMenu("Fiado","Contas de clientes"); addMenu("Lucro","Resultados"); addMenu("Relatórios","Consultas gerenciais")
 menu.Add(widget.NewSeparator()); menu.Add(widget.NewLabelWithStyle("FERRAMENTAS",fyne.TextAlignLeading,fyne.TextStyle{Bold:true}))
 addMenu("Consulta de Produto","Consulta global"); addMenu("Configurações","Sistema e atualizações")

 sideTitle:=widget.NewLabelWithStyle("ERP GRATIDÃO",fyne.TextAlignLeading,fyne.TextStyle{Bold:true})
 side:=container.NewBorder(container.NewPadded(sideTitle),nil,nil,nil,container.NewVScroll(container.NewPadded(menu)))
 side.Resize(fyne.NewSize(235,700))

 search:=widget.NewEntry(); search.SetPlaceHolder("Ir para módulo...")
 search.OnSubmitted=func(q string){showPlaceholder(q,"Pesquisa de módulo")}
 top:=container.NewBorder(nil,nil,current,container.NewHBox(widget.NewIcon(theme.ComputerIcon()),widget.NewLabel("Sistema local"),clock),search)
 body:=container.NewBorder(nil,nil,side,nil,content)
 root:=container.NewBorder(container.NewPadded(top),nil,nil,nil,body)
 w.SetContent(root)
 showHome()
 fmt.Print("")
 w.ShowAndRun()
}
