package main

import (
 "fmt"
 "strconv"
 "strings"
 "time"
 "fyne.io/fyne/v2"
 "fyne.io/fyne/v2/container"
 "fyne.io/fyne/v2/widget"
)

type CartItem struct { Barcode, Description string; Qty, Price float64 }

func buildPDV(store *Store) fyne.CanvasObject {
 barcode:=widget.NewEntry(); barcode.SetPlaceHolder("Bipe ou digite o código de barras")
 qty:=widget.NewEntry(); qty.SetText("1"); qty.SetPlaceHolder("Qtd")
 status:=widget.NewLabel("Pronto para vender")
 total:=widget.NewLabelWithStyle("TOTAL  R$ 0,00",fyne.TextAlignTrailing,fyne.TextStyle{Bold:true})
 cart:=[]CartItem{}
 list:=widget.NewList(func()int{return len(cart)},func()fyne.CanvasObject{return widget.NewLabel("")},func(id widget.ListItemID,o fyne.CanvasObject){i:=cart[id];o.(*widget.Label).SetText(fmt.Sprintf("%s   %s   %.3f x R$ %.2f   = R$ %.2f",i.Barcode,i.Description,i.Qty,i.Price,i.Qty*i.Price))})
 recalc:=func(){var t float64;for _,i:=range cart{t+=i.Qty*i.Price};total.SetText(fmt.Sprintf("TOTAL  R$ %.2f",t))}
 add:=func(){
  code:=strings.TrimSpace(barcode.Text); if code==""{status.SetText("Informe ou bipe um código de barras");return}
  q,_:=strconv.ParseFloat(strings.ReplaceAll(qty.Text,",","."),64);if q<=0{q=1}
  rows:=store.SearchProducts(code); var found []string
  for _,r:=range rows{if len(r)>=4&&r[0]==code{found=r;break}}
  if found==nil{status.SetText("Produto não cadastrado");return}
  price,_:=strconv.ParseFloat(found[2],64);stock,_:=strconv.ParseFloat(found[3],64)
  if q>stock{status.SetText(fmt.Sprintf("Estoque insuficiente • atual: %.3f",stock));return}
  cart=append(cart,CartItem{Barcode:found[0],Description:found[1],Qty:q,Price:price});list.Refresh();recalc();barcode.SetText("");qty.SetText("1");status.SetText(fmt.Sprintf("%s • estoque atual %.3f",found[1],stock));barcode.FocusGained()
 }
 barcode.OnSubmitted=func(string){add()}
 addBtn:=widget.NewButton("Adicionar",add);addBtn.Importance=widget.HighImportance
 remove:=widget.NewButton("Excluir item",func(){if len(cart)>0{cart=cart[:len(cart)-1];list.Refresh();recalc()}})
 clear:=widget.NewButton("Cancelar venda",func(){cart=nil;list.Refresh();recalc();status.SetText("Venda cancelada")})
 payment:=widget.NewSelect([]string{"DINHEIRO","PIX","DÉBITO","CRÉDITO","ALELO","PLUXXE","TICKET","VR","FIADO"},nil);payment.SetSelected("DINHEIRO")
 finish:=widget.NewButton("Finalizar pagamento",func(){
  if len(cart)==0{status.SetText("Nenhum item na venda");return}
  tx,e:=store.DB.Begin();if e!=nil{status.SetText("Erro ao abrir venda: "+e.Error());return};defer tx.Rollback()
  var totalValue float64;for _,i:=range cart{totalValue+=i.Qty*i.Price}
  number:=time.Now().Format("20060102150405")
  res,e:=tx.Exec("INSERT INTO sales(sale_number,created_at,customer_name,payment_method,total,status) VALUES(?,datetime('now','localtime'),'Consumidor',?,?,'CONCLUIDA')",number,payment.Selected,totalValue)
  if e!=nil{status.SetText("Erro ao salvar venda: "+e.Error());return};saleID,e:=res.LastInsertId();if e!=nil{status.SetText(e.Error());return}
  for _,i:=range cart{
   var productID int64;var current float64
   if e=tx.QueryRow("SELECT id,stock FROM products WHERE barcode=? AND active=1",i.Barcode).Scan(&productID,&current);e!=nil{status.SetText("Produto não encontrado: "+i.Description);return}
   if current<i.Qty{status.SetText(fmt.Sprintf("Estoque insuficiente: %s • atual %.3f",i.Description,current));return}
   if _,e=tx.Exec("INSERT INTO sale_items(sale_id,product_id,description,quantity,unit_price) VALUES(?,?,?,?,?)",saleID,productID,i.Description,i.Qty,i.Price);e!=nil{status.SetText("Erro nos itens: "+e.Error());return}
   if _,e=tx.Exec("UPDATE products SET stock=stock-? WHERE id=?",i.Qty,productID);e!=nil{status.SetText("Erro no estoque: "+e.Error());return}
  }
  if e=tx.Commit();e!=nil{status.SetText("Erro ao concluir: "+e.Error());return}
  cart=nil;list.Refresh();recalc();status.SetText(fmt.Sprintf("Venda %s concluída • %s • R$ %.2f",number,payment.Selected,totalValue))
 });finish.Importance=widget.HighImportance
 entry:=container.NewBorder(nil,nil,nil,container.NewGridWithColumns(2,qty,addBtn),barcode)
 actions:=container.NewGridWithColumns(4,remove,clear,payment,finish)
 right:=container.NewVBox(widget.NewCard("Venda atual","Itens adicionados",container.NewPadded(list)),total,status,actions)
 return container.NewBorder(container.NewVBox(widget.NewLabelWithStyle("PDV",fyne.TextAlignLeading,fyne.TextStyle{Bold:true}),widget.NewLabel("Venda rápida • leitura por código de barras"),entry,widget.NewSeparator()),nil,nil,nil,right)
}
