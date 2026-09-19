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

func (s *Store) OpenCash(amount float64) error {
 if s==nil||s.DB==nil{return fmt.Errorf("banco de dados indisponível")}
 if amount<0{return fmt.Errorf("valor inicial não pode ser negativo")}
 var n int
 if err:=s.DB.QueryRow("SELECT COUNT(*) FROM cash_sessions WHERE status='ABERTO'").Scan(&n);err!=nil{return err}
 if n>0{return fmt.Errorf("já existe um caixa aberto")}
 tx,err:=s.DB.Begin();if err!=nil{return err};defer tx.Rollback()
 if _,err=tx.Exec("INSERT INTO cash_sessions(opened_at,opening_amount,status) VALUES(datetime('now','localtime'),?,'ABERTO')",amount);err!=nil{return err}
 return tx.Commit()
}

func (s *Store) CloseCash(id int64, amount float64) error {
 if s==nil||s.DB==nil{return fmt.Errorf("banco de dados indisponível")}
 if amount<0{return fmt.Errorf("valor final não pode ser negativo")}
 tx,err:=s.DB.Begin();if err!=nil{return err};defer tx.Rollback()
 res,err:=tx.Exec("UPDATE cash_sessions SET closed_at=datetime('now','localtime'),closing_amount=?,status='FECHADO' WHERE id=? AND status='ABERTO'",amount,id)
 if err!=nil{return err};n,err:=res.RowsAffected();if err!=nil{return err};if n!=1{return fmt.Errorf("caixa não encontrado ou já fechado")}
 return tx.Commit()
}

func buildCash(s *Store,w fyne.Window) fyne.CanvasObject {
 title:=widget.NewLabelWithStyle("Caixa",fyne.TextAlignLeading,fyne.TextStyle{Bold:true})
 status:=widget.NewLabel("Selecione uma sessão para ver os detalhes.")
 opening:=widget.NewEntry();opening.SetPlaceHolder("Valor inicial")
 closing:=widget.NewEntry();closing.SetPlaceHolder("Valor contado no fechamento")
 rows:=s.Cash()
 selected:=-1
 list:=widget.NewList(func()int{return len(rows)},func()fyne.CanvasObject{return widget.NewLabel("")},func(id widget.ListItemID,o fyne.CanvasObject){
  r:=rows[id];text:=strings.Join(r,"   •   ");o.(*widget.Label).SetText(text)
 })
 refresh:=func(){rows=s.Cash();selected=-1;status.SetText("Selecione uma sessão para ver os detalhes.");list.UnselectAll();list.Refresh()}
 list.OnSelected=func(id widget.ListItemID){
  if id<0||id>=len(rows){return};selected=id;r:=rows[id]
  if len(r)>=6{status.SetText(fmt.Sprintf("Caixa #%s | Aberto: %s | Inicial: R$ %s | Fechado: %s | Final: R$ %s | %s",r[0],r[1],r[2],r[3],r[4],r[5]))}
 }
 openBtn:=widget.NewButton("Abrir caixa",func(){
  v,err:=strconv.ParseFloat(strings.ReplaceAll(strings.TrimSpace(opening.Text),",","."),64);if err!=nil{dialog.ShowError(fmt.Errorf("informe um valor inicial válido"),w);return}
  if err=s.OpenCash(v);err!=nil{dialog.ShowError(err,w);return};opening.SetText("");refresh();dialog.ShowInformation("Caixa","Caixa aberto com sucesso.",w)
 })
 closeBtn:=widget.NewButton("Fechar caixa",func(){
  if selected<0||selected>=len(rows){dialog.ShowInformation("Caixa","Selecione um caixa aberto.",w);return};r:=rows[selected];if len(r)<6||r[5]!="ABERTO"{dialog.ShowInformation("Caixa","A sessão selecionada não está aberta.",w);return}
  id,err:=strconv.ParseInt(r[0],10,64);if err!=nil{dialog.ShowError(err,w);return};v,err:=strconv.ParseFloat(strings.ReplaceAll(strings.TrimSpace(closing.Text),",","."),64);if err!=nil{dialog.ShowError(fmt.Errorf("informe o valor contado no fechamento"),w);return}
  dialog.ShowConfirm("Fechar caixa",fmt.Sprintf("Confirmar fechamento do caixa #%d com R$ %.2f?",id,v),func(ok bool){if !ok{return};if err:=s.CloseCash(id,v);err!=nil{dialog.ShowError(err,w);return};closing.SetText("");refresh();dialog.ShowInformation("Caixa","Caixa fechado com sucesso.",w)},w)
 })
 refreshBtn:=widget.NewButton("Atualizar",refresh)
 form:=container.NewGridWithColumns(2,container.NewVBox(widget.NewLabel("Abertura"),opening,openBtn),container.NewVBox(widget.NewLabel("Fechamento"),closing,closeBtn))
 return container.NewBorder(container.NewVBox(title,widget.NewLabel("Abertura, acompanhamento e fechamento das sessões de caixa."),form,status,widget.NewSeparator()),container.NewHBox(refreshBtn),nil,nil,list)
}
