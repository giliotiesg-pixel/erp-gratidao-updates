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

type saleViewRow struct {
 ID int64
 Number, Date, Customer, Payment, Total, Status string
}

func (s *Store) salesView(term string) []saleViewRow {
 out:=[]saleViewRow{}
 if s==nil||s.DB==nil{return out}
 like:="%"+strings.TrimSpace(term)+"%"
 r,e:=s.DB.Query(`SELECT id,COALESCE(sale_number,''),datetime(created_at,'localtime'),COALESCE(customer_name,'Consumidor'),COALESCE(payment_method,''),printf('%.2f',total),COALESCE(status,'') FROM sales WHERE deleted_at IS NULL AND (COALESCE(sale_number,'') LIKE ? OR COALESCE(customer_name,'') LIKE ? OR COALESCE(payment_method,'') LIKE ?) ORDER BY id DESC LIMIT 1000`,like,like,like)
 if e!=nil{return out}; defer r.Close()
 for r.Next(){var v saleViewRow;if r.Scan(&v.ID,&v.Number,&v.Date,&v.Customer,&v.Payment,&v.Total,&v.Status)==nil{out=append(out,v)}}
 return out
}

func (s *Store) saleItems(id int64) [][]string {
 return s.rows(`SELECT COALESCE(p.description,si.description,''),printf('%.3f',si.quantity),printf('%.2f',si.unit_price),printf('%.2f',si.quantity*si.unit_price) FROM sale_items si LEFT JOIN products p ON p.id=si.product_id WHERE si.sale_id=? ORDER BY si.id`,id)
}

func buildSales(store *Store,w fyne.Window) fyne.CanvasObject {
 rows:=store.salesView("")
 selected:=-1
 search:=widget.NewEntry(); search.SetPlaceHolder("Pesquisar número, cliente ou pagamento...")
 detail:=widget.NewLabel("Selecione uma venda para consultar os itens."); detail.Wrapping=fyne.TextWrapWord
 items:=widget.NewList(func()int{return 0},func()fyne.CanvasObject{return widget.NewLabel("")},func(widget.ListItemID,fyne.CanvasObject){})
 var itemRows [][]string
 items.Length=func()int{return len(itemRows)}
 items.UpdateItem=func(id widget.ListItemID,o fyne.CanvasObject){o.(*widget.Label).SetText(strings.Join(itemRows[id],"   •   "))}
 table:=widget.NewTable(func()(int,int){return len(rows),6},func()fyne.CanvasObject{return widget.NewLabel("")},func(id widget.TableCellID,o fyne.CanvasObject){
  r:=rows[id.Row]; vals:=[]string{r.Number,r.Date,r.Customer,r.Payment,"R$ "+r.Total,r.Status}; o.(*widget.Label).SetText(vals[id.Col])
 })
 table.SetColumnWidth(0,110);table.SetColumnWidth(1,155);table.SetColumnWidth(2,220);table.SetColumnWidth(3,120);table.SetColumnWidth(4,110);table.SetColumnWidth(5,120)
 table.OnSelected=func(id widget.TableCellID){selected=id.Row;r:=rows[selected];itemRows=store.saleItems(r.ID);items.Refresh();detail.SetText(fmt.Sprintf("Venda %s • %s • %s • %s • R$ %s",r.Number,r.Date,r.Customer,r.Payment,r.Total))}
 refresh:=func(q string){rows=store.salesView(q);selected=-1;itemRows=nil;table.Refresh();items.Refresh();detail.SetText(fmt.Sprintf("%d venda(s) encontrada(s).",len(rows)))}
 search.OnChanged=refresh
 reload:=widget.NewButton("Atualizar",func(){refresh(search.Text)})
 closeSale:=widget.NewButton("Fechar detalhes",func(){table.UnselectAll();selected=-1;itemRows=nil;items.Refresh();detail.SetText("Selecione uma venda para consultar os itens.")})
 finalizeOpen:=widget.NewButton("Finalizar venda aberta",func(){
  if selected<0||selected>=len(rows){dialog.ShowInformation("Vendas","Selecione uma venda aberta primeiro.",w);return}
  r:=rows[selected];if strings.ToUpper(r.Status)!="ABERTA"{dialog.ShowInformation("Vendas","A venda selecionada não está em aberto.",w);return}
  pay:=widget.NewSelect([]string{"DINHEIRO","PIX","DÉBITO","CRÉDITO","ALELO","PLUXXE","TICKET","VR","FIADO"},nil);pay.SetSelected("DINHEIRO")
  form:=dialog.NewForm("Finalizar venda "+r.Number,"Finalizar","Cancelar",[]*widget.FormItem{widget.NewFormItem("Pagamento",pay)},func(ok bool){
   if !ok{return};if pay.Selected=="FIADO"&&(strings.TrimSpace(r.Customer)==""||r.Customer=="Consumidor"){dialog.ShowInformation("Vendas","Venda FIADO precisa ter um cliente identificado.",w);return}
   tx,e:=store.DB.Begin();if e!=nil{dialog.ShowError(e,w);return};defer tx.Rollback()
   q,e:=tx.Query("SELECT si.product_id,si.quantity,COALESCE(p.description,si.description,''),COALESCE(p.stock,0) FROM sale_items si LEFT JOIN products p ON p.id=si.product_id WHERE si.sale_id=?",r.ID);if e!=nil{dialog.ShowError(e,w);return}
   type stockLine struct{id int64;qty float64;name string;stock float64};lines:=[]stockLine{}
   for q.Next(){var x stockLine;if e=q.Scan(&x.id,&x.qty,&x.name,&x.stock);e!=nil{q.Close();dialog.ShowError(e,w);return};if x.stock<x.qty{q.Close();dialog.ShowError(fmt.Errorf("estoque insuficiente para %s: atual %.3f, necessário %.3f",x.name,x.stock,x.qty),w);return};lines=append(lines,x)};q.Close()
   for _,x:=range lines{if _,e=tx.Exec("UPDATE products SET stock=stock-? WHERE id=?",x.qty,x.id);e!=nil{dialog.ShowError(e,w);return}}
   if _,e=tx.Exec("UPDATE sales SET payment_method=?,status='CONCLUIDA' WHERE id=? AND status='ABERTA' AND deleted_at IS NULL",pay.Selected,r.ID);e!=nil{dialog.ShowError(e,w);return}
   if e=tx.Commit();e!=nil{dialog.ShowError(e,w);return};refresh(search.Text);dialog.ShowInformation("Vendas","Venda aberta finalizada e estoque atualizado.",w)
  },w);form.Resize(fyne.NewSize(480,220));form.Show()
 });finalizeOpen.Importance=widget.HighImportance
 editBtn:=widget.NewButton("Editar / Auditar venda",func(){
  if selected<0||selected>=len(rows){dialog.ShowInformation("Vendas","Selecione uma venda primeiro.",w);return}
  r:=rows[selected]; editable,er:=store.editableSaleItems(r.ID);if er!=nil{dialog.ShowError(er,w);return}
  customer:=widget.NewEntry();customer.SetText(r.Customer)
  payment:=widget.NewSelect([]string{"DINHEIRO","PIX","DÉBITO","CRÉDITO","ALELO","PLUXXE","TICKET","VR","FIADO"},nil);payment.SetSelected(r.Payment)
  dateEntry:=widget.NewEntry();dateEntry.SetText(r.Date)
  itemBox:=container.NewVBox()
  qtyEntries:=make([]*widget.Entry,len(editable));priceEntries:=make([]*widget.Entry,len(editable));descEntries:=make([]*widget.Entry,len(editable))
  totalLabel:=widget.NewLabelWithStyle("Total recalculado: R$ 0,00",fyne.TextAlignLeading,fyne.TextStyle{Bold:true})
  recalc:=func(){total:=0.0;for i:=range editable{total+=pf(qtyEntries[i].Text)*pf(priceEntries[i].Text)};totalLabel.SetText(fmt.Sprintf("Total recalculado: R$ %.2f",total))}
  for i,x:=range editable{i:=i;x:=x;descEntries[i]=widget.NewEntry();descEntries[i].SetText(x.Description);qtyEntries[i]=widget.NewEntry();qtyEntries[i].SetText(fmt.Sprintf("%.3f",x.Quantity));priceEntries[i]=widget.NewEntry();priceEntries[i].SetText(fmt.Sprintf("%.2f",x.UnitPrice));qtyEntries[i].OnChanged=func(string){recalc()};priceEntries[i].OnChanged=func(string){recalc()};itemBox.Add(widget.NewCard(fmt.Sprintf("Item %d",i+1),"Descrição, quantidade e valor unitário",widget.NewForm(widget.NewFormItem("Produto",descEntries[i]),widget.NewFormItem("Quantidade",qtyEntries[i]),widget.NewFormItem("Valor unitário",priceEntries[i]))))}
  recalc()
  formContent:=container.NewVBox(widget.NewForm(widget.NewFormItem("Cliente",customer),widget.NewFormItem("Pagamento",payment),widget.NewFormItem("Data",dateEntry)),widget.NewSeparator(),widget.NewLabelWithStyle("Itens vendidos",fyne.TextAlignLeading,fyne.TextStyle{Bold:true}),container.NewVScroll(itemBox),totalLabel)
  d:=dialog.NewCustomConfirm("Editar venda "+r.Number,"Salvar alterações","Cancelar",formContent,func(ok bool){if !ok{return};for i:=range editable{editable[i].Description=descEntries[i].Text;editable[i].Quantity=pf(qtyEntries[i].Text);editable[i].UnitPrice=pf(priceEntries[i].Text)};if er:=store.updateSaleAudited(r.ID,customer.Text,payment.Selected,dateEntry.Text,editable);er!=nil{dialog.ShowError(er,w);return};refresh(search.Text);dialog.ShowInformation("Vendas","Venda atualizada. Itens, valores, data e total foram registrados na auditoria.",w)},w);d.Resize(fyne.NewSize(760,700));d.Show()
 });editBtn.Importance=widget.HighImportance
 auditBtn:=widget.NewButton("Histórico de auditoria",func(){
  if selected<0||selected>=len(rows){dialog.ShowInformation("Vendas","Selecione uma venda primeiro.",w);return};r:=rows[selected];a:=store.salesAuditRows(r.ID);text:="Nenhuma alteração auditada.";if len(a)>0{parts:=[]string{};for _,x:=range a{parts=append(parts,strings.Join(x," • "))};text=strings.Join(parts,"\n")};lab:=widget.NewLabel(text);lab.Wrapping=fyne.TextWrapWord;sc:=container.NewVScroll(lab);sc.SetMinSize(fyne.NewSize(800,500));dialog.ShowCustom("Auditoria da venda "+r.Number,"Fechar",sc,w)
 })
 deleteBtn:=widget.NewButton("Excluir venda",func(){
  if selected<0||selected>=len(rows){dialog.ShowInformation("Vendas","Selecione uma venda primeiro.",w);return}
  r:=rows[selected]
  dialog.ShowConfirm("Excluir venda","Excluir a venda "+r.Number+"? O registro será preservado para auditoria e deixará de aparecer nas vendas ativas.",func(ok bool){
   if !ok{return}
   tx,e:=store.DB.Begin();if e!=nil{dialog.ShowError(e,w);return}
   // Exclusão lógica: preserva histórico e evita apagar itens/auditoria. Não devolve estoque automaticamente nesta etapa.
   res,e:=tx.Exec("UPDATE sales SET deleted_at=datetime('now','localtime') WHERE id=? AND deleted_at IS NULL",r.ID)
   if e!=nil{tx.Rollback();dialog.ShowError(e,w);return};n,_:=res.RowsAffected();if n!=1{tx.Rollback();dialog.ShowError(fmt.Errorf("venda não encontrada ou já excluída"),w);return}
   if _,e=tx.Exec("INSERT INTO audit_log(entity_type,entity_id,action,details,created_at) SELECT 'SALE',?,'DELETE','Exclusão pela interface Fyne',datetime('now','localtime') WHERE EXISTS(SELECT 1 FROM sqlite_master WHERE type='table' AND name='audit_log')",strconv.FormatInt(r.ID,10));e!=nil{
    // Bancos antigos podem não ter a tabela/estrutura de auditoria; a exclusão lógica continua segura.
   }
   if e=tx.Commit();e!=nil{dialog.ShowError(e,w);return};refresh(search.Text);dialog.ShowInformation("Vendas","Venda excluída da lista ativa.",w)
  },w)
 })
 deleteBtn.Importance=widget.DangerImportance
 header:=container.NewBorder(nil,nil,search,container.NewHBox(reload,closeSale,editBtn,auditBtn,finalizeOpen,deleteBtn))
 salesPane:=container.NewBorder(header,nil,nil,nil,table)
 detailPane:=container.NewBorder(container.NewVBox(widget.NewLabelWithStyle("Itens da venda",fyne.TextAlignLeading,fyne.TextStyle{Bold:true}),detail,widget.NewSeparator()),nil,nil,nil,items)
 split:=container.NewVSplit(salesPane,detailPane);split.Offset=.62
 return container.NewBorder(container.NewVBox(widget.NewLabelWithStyle("Vendas",fyne.TextAlignLeading,fyne.TextStyle{Bold:true}),widget.NewLabel("Histórico, edição completa e auditoria de itens, valores, data e totais"),widget.NewSeparator()),nil,nil,nil,split)
}
