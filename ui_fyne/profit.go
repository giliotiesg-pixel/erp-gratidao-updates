package main

import (
 "fmt"
 "time"
 "fyne.io/fyne/v2"
 "fyne.io/fyne/v2/container"
 "fyne.io/fyne/v2/widget"
)

// buildProfit is read-only: it never changes sales, products or the official ERP database.
func buildProfit(s *Store, w fyne.Window) fyne.CanvasObject {
 title:=widget.NewLabelWithStyle("Lucro",fyne.TextAlignLeading,fyne.TextStyle{Bold:true})
 period:=widget.NewSelect([]string{"Hoje","Últimos 7 dias","Mês atual"},nil); period.SetSelected("Hoje")
 summary:=widget.NewLabel("")
 note:=widget.NewLabel("Lucro estimado = faturamento - custo dos itens vendidos. Nenhum dado é alterado por esta tela.")
 rows:=[][]string{}
 table:=widget.NewTable(func()(int,int){return len(rows)+1,4},func()fyne.CanvasObject{return widget.NewLabel("")},func(id widget.TableCellID,o fyne.CanvasObject){l:=o.(*widget.Label);if id.Row==0{h:=[]string{"Venda","Faturamento","Custo","Lucro"};l.SetText(h[id.Col]);l.TextStyle=fyne.TextStyle{Bold:true};return};l.TextStyle=fyne.TextStyle{};if id.Row-1<len(rows)&&id.Col<len(rows[id.Row-1]){l.SetText(rows[id.Row-1][id.Col])}})
 table.SetColumnWidth(0,180);table.SetColumnWidth(1,140);table.SetColumnWidth(2,140);table.SetColumnWidth(3,140)
 refresh:=func(){
  if s==nil||s.DB==nil{summary.SetText("Banco de dados indisponível");rows=nil;table.Refresh();return}
  where:="date(s.created_at,'localtime')=date('now','localtime')"
  switch period.Selected {case "Últimos 7 dias":where="date(s.created_at,'localtime')>=date('now','localtime','-6 days')";case "Mês atual":where="strftime('%Y-%m',s.created_at,'localtime')=strftime('%Y-%m','now','localtime')"}
  q:=`SELECT s.sale_number,printf('%.2f',s.total),printf('%.2f',COALESCE(SUM(si.quantity*COALESCE(p.cost,0)),0)),printf('%.2f',s.total-COALESCE(SUM(si.quantity*COALESCE(p.cost,0)),0)) FROM sales s LEFT JOIN sale_items si ON si.sale_id=s.id LEFT JOIN products p ON p.id=si.product_id WHERE s.deleted_at IS NULL AND s.status='CONCLUIDA' AND `+where+` GROUP BY s.id ORDER BY s.id DESC LIMIT 500`
  rows=s.rows(q);table.Refresh()
  var revenue,cost float64
  q2:=`SELECT COALESCE(SUM(s.total),0),COALESCE(SUM(x.cost),0) FROM sales s LEFT JOIN (SELECT si.sale_id,SUM(si.quantity*COALESCE(p.cost,0)) cost FROM sale_items si LEFT JOIN products p ON p.id=si.product_id GROUP BY si.sale_id) x ON x.sale_id=s.id WHERE s.deleted_at IS NULL AND s.status='CONCLUIDA' AND `+where
  if err:=s.DB.QueryRow(q2).Scan(&revenue,&cost);err!=nil{summary.SetText("Não foi possível calcular: "+err.Error());return}
  summary.SetText(fmt.Sprintf("Faturamento: R$ %.2f    •    Custo: R$ %.2f    •    Lucro estimado: R$ %.2f",revenue,cost,revenue-cost))
 }
 period.OnChanged=func(string){refresh()};refresh()
 footer:=widget.NewLabel("Atualizado em "+time.Now().Format("02/01/2006 15:04"))
 return container.NewBorder(container.NewVBox(title,note,container.NewHBox(widget.NewLabel("Período:"),period),summary,widget.NewSeparator()),footer,nil,nil,table)
}
