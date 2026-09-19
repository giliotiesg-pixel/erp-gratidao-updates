package main

import (
 "fmt"
 "strconv"
 "strings"

 "fyne.io/fyne/v2"
 "fyne.io/fyne/v2/container"
 "fyne.io/fyne/v2/dialog"
 "fyne.io/fyne/v2/widget"
)

func buildStock(s *Store, w fyne.Window) fyne.CanvasObject {
 title:=widget.NewLabelWithStyle("Estoque",fyne.TextAlignLeading,fyne.TextStyle{Bold:true})
 info:=widget.NewLabel("Consulta e ajuste da quantidade real dos produtos")
 search:=widget.NewEntry(); search.SetPlaceHolder("Pesquisar por produto ou código de barras...")
 rows:=s.StockSearch("")
 selected:=-1
 code:=widget.NewEntry(); code.Disable()
 description:=widget.NewEntry(); description.Disable()
 current:=widget.NewEntry(); current.Disable()
 minimum:=widget.NewEntry(); minimum.SetPlaceHolder("Estoque mínimo")
 realQty:=widget.NewEntry(); realQty.SetPlaceHolder("Quantidade real atual")
 status:=widget.NewLabel("Selecione um produto para ajustar o estoque.")

 list:=widget.NewTable(
  func()(int,int){return len(rows)+1,4},
  func()fyne.CanvasObject{return widget.NewLabel("")},
  func(id widget.TableCellID,o fyne.CanvasObject){
   l:=o.(*widget.Label)
   if id.Row==0 { headers:=[]string{"Código","Produto","Estoque","Mínimo"}; l.SetText(headers[id.Col]); l.TextStyle=fyne.TextStyle{Bold:true}; return }
   l.TextStyle=fyne.TextStyle{}
   r:=rows[id.Row-1]; if id.Col<len(r){l.SetText(r[id.Col])}else{l.SetText("")}
  })
 list.SetColumnWidth(0,150); list.SetColumnWidth(1,430); list.SetColumnWidth(2,130); list.SetColumnWidth(3,130)
 list.OnSelected=func(id widget.TableCellID){
  if id.Row==0{return}; selected=id.Row-1; if selected<0||selected>=len(rows){return}; r:=rows[selected]
  code.SetText(r[0]); description.SetText(r[1]); current.SetText(r[2]); realQty.SetText(r[2]); minimum.SetText(r[3]); status.SetText("Informe a quantidade contada fisicamente e clique em Salvar ajuste.")
 }
 refresh:=func(term string){rows=s.StockSearch(term);selected=-1;list.Refresh();code.SetText("");description.SetText("");current.SetText("");realQty.SetText("");minimum.SetText("")}
 search.OnChanged=func(q string){refresh(q)}
 save:=widget.NewButton("Salvar ajuste",func(){
  if selected<0||selected>=len(rows){dialog.ShowInformation("Estoque","Selecione um produto.",w);return}
  qty,err:=strconv.ParseFloat(strings.ReplaceAll(strings.TrimSpace(realQty.Text),",","."),64); if err!=nil||qty<0{dialog.ShowError(fmt.Errorf("quantidade inválida"),w);return}
  min,err:=strconv.ParseFloat(strings.ReplaceAll(strings.TrimSpace(minimum.Text),",","."),64); if err!=nil||min<0{dialog.ShowError(fmt.Errorf("estoque mínimo inválido"),w);return}
  if err=s.AdjustStock(rows[selected][0],rows[selected][1],qty,min);err!=nil{dialog.ShowError(err,w);return}
  status.SetText("Estoque atualizado com segurança."); refresh(search.Text); dialog.ShowInformation("Estoque","Quantidade atualizada.",w)
 }); save.Importance=widget.HighImportance
 reload:=widget.NewButton("Atualizar lista",func(){refresh(search.Text)})
 form:=widget.NewForm(widget.NewFormItem("Código",code),widget.NewFormItem("Produto",description),widget.NewFormItem("Estoque atual",current),widget.NewFormItem("Quantidade real",realQty),widget.NewFormItem("Estoque mínimo",minimum))
 editor:=widget.NewCard("Ajuste de estoque","Use a contagem física real. O produto é reativado quando houver quantidade.",container.NewVBox(form,container.NewHBox(save,reload),status))
 return container.NewBorder(container.NewVBox(title,info,search,widget.NewSeparator()),editor,nil,nil,list)
}
