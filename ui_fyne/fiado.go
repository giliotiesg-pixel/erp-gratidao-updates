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

type creditRow struct { SaleID int64; Number, Customer, Date string; Total, Paid, Balance float64 }

func (s *Store) CreditSales(term string) []creditRow {
 out:=[]creditRow{}; if s==nil||s.DB==nil{return out}
 like:="%"+strings.TrimSpace(term)+"%"
 r,err:=s.DB.Query(`SELECT s.id,s.sale_number,COALESCE(s.customer_name,'Consumidor'),datetime(s.created_at,'localtime'),s.total,COALESCE((SELECT SUM(cp.amount) FROM credit_payments cp WHERE cp.sale_id=s.id),0),MAX(0,s.total-COALESCE((SELECT SUM(cp.amount) FROM credit_payments cp WHERE cp.sale_id=s.id),0)) FROM sales s WHERE s.payment_method='FIADO' AND s.deleted_at IS NULL AND (COALESCE(s.customer_name,'') LIKE ? OR s.sale_number LIKE ?) ORDER BY s.id DESC LIMIT 500`,like,like); if err!=nil{return out}; defer r.Close()
 for r.Next(){var x creditRow;if r.Scan(&x.SaleID,&x.Number,&x.Customer,&x.Date,&x.Total,&x.Paid,&x.Balance)==nil{out=append(out,x)}};return out
}
func (s *Store) AddCreditPayment(saleID int64, amount float64) error {
 if s==nil||s.DB==nil{return fmt.Errorf("banco de dados indisponível")};if amount<=0{return fmt.Errorf("informe um valor maior que zero")}
 tx,err:=s.DB.Begin();if err!=nil{return err};defer tx.Rollback()
 var balance float64;err=tx.QueryRow(`SELECT MAX(0,total-COALESCE((SELECT SUM(amount) FROM credit_payments WHERE sale_id=sales.id),0)) FROM sales WHERE id=? AND payment_method='FIADO' AND deleted_at IS NULL`,saleID).Scan(&balance);if err!=nil{return fmt.Errorf("venda fiado não encontrada")};if amount>balance+0.0001{return fmt.Errorf("valor maior que o saldo devedor")}
 if _,err=tx.Exec("INSERT INTO credit_payments(sale_id,amount,paid_at) VALUES(?,?,datetime('now','localtime'))",saleID,amount);err!=nil{return err};return tx.Commit()
}
func buildFiado(s *Store,w fyne.Window) fyne.CanvasObject {
 rows:=s.CreditSales(""); selected:=-1
 search:=widget.NewEntry();search.SetPlaceHolder("Pesquisar cliente ou número da venda...")
 summary:=widget.NewLabel(""); details:=widget.NewLabel("Selecione uma venda fiado.")
 table:=widget.NewTable(func()(int,int){return len(rows)+1,6},func()fyne.CanvasObject{return widget.NewLabel("")},func(id widget.TableCellID,o fyne.CanvasObject){l:=o.(*widget.Label);if id.Row==0{h:=[]string{"Venda","Cliente","Data","Total","Pago","Saldo"};l.SetText(h[id.Col]);l.TextStyle.Bold=true;return};l.TextStyle.Bold=false;r:=rows[id.Row-1];v:=[]string{r.Number,r.Customer,r.Date,fmt.Sprintf("R$ %.2f",r.Total),fmt.Sprintf("R$ %.2f",r.Paid),fmt.Sprintf("R$ %.2f",r.Balance)};l.SetText(v[id.Col])})
 widths:=[]float32{100,240,155,100,100,100};for i,v:=range widths{table.SetColumnWidth(i,v)}
 refresh:=func(){rows=s.CreditSales(search.Text);selected=-1;table.Refresh();details.SetText("Selecione uma venda fiado.");var total float64;for _,r:=range rows{total+=r.Balance};summary.SetText(fmt.Sprintf("%d venda(s) • Saldo em aberto: R$ %.2f",len(rows),total))}
 table.OnSelected=func(id widget.TableCellID){if id.Row==0{return};selected=id.Row-1;r:=rows[selected];details.SetText(fmt.Sprintf("Venda %s • %s\nTotal: R$ %.2f   Pago: R$ %.2f   Saldo: R$ %.2f",r.Number,r.Customer,r.Total,r.Paid,r.Balance))}
 search.OnChanged=func(string){refresh()}
 pay:=widget.NewButton("Registrar pagamento",func(){if selected<0||selected>=len(rows){dialog.ShowInformation("Fiado","Selecione uma venda.",w);return};r:=rows[selected];if r.Balance<=0{dialog.ShowInformation("Fiado","Esta venda já está paga.",w);return};e:=widget.NewEntry();e.SetPlaceHolder(fmt.Sprintf("Saldo: %.2f",r.Balance));dialog.ShowForm("Registrar pagamento • "+r.Number,"Salvar","Cancelar",[]*widget.FormItem{widget.NewFormItem("Valor recebido",e)},func(ok bool){if !ok{return};v,err:=strconv.ParseFloat(strings.ReplaceAll(strings.TrimSpace(e.Text),",","."),64);if err!=nil{dialog.ShowError(fmt.Errorf("valor inválido"),w);return};if err=s.AddCreditPayment(r.SaleID,v);err!=nil{dialog.ShowError(err,w);return};refresh();dialog.ShowInformation("Fiado","Pagamento registrado.",w)},w)})
 refreshBtn:=widget.NewButton("Atualizar",refresh);refresh()
 return container.NewBorder(container.NewVBox(widget.NewLabelWithStyle("Fiado",fyne.TextAlignLeading,fyne.TextStyle{Bold:true}),widget.NewLabel("Contas de clientes • pagamentos preservam a venda original"),container.NewBorder(nil,nil,nil,refreshBtn,search),summary,widget.NewSeparator()),container.NewVBox(widget.NewSeparator(),details,container.NewHBox(pay)),nil,nil,table)
}
