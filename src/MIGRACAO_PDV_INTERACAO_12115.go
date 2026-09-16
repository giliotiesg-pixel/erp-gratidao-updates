package main

import (
 "fmt"
 "strconv"
 "strings"
)

// addCart12115 substitui a inclusão antiga do PDV. Mostra o estoque real ao bipar/inserir
// e, quando insuficiente, deixa o produto selecionado para correção da quantidade física.
var pdvInsufficientProduct12115 int64
var pdvInsufficientDesc12115 string

func addCart12115(){
 if !ensureDB(){return}
 barcode:=strings.TrimSpace(getText(pdvBarcode));if barcode==""{return}
 qty:=parseF(strings.TrimSpace(getText(pdvQty)));if qty<=0{qty=1}
 rows,e:=queryRows("SELECT id,COALESCE(barcode,''),description,price,cost,stock,active FROM products WHERE (barcode='"+esc(barcode)+"' OR internal_code='"+esc(barcode)+"') LIMIT 1",7)
 if e!=nil||len(rows)==0{pdvInsufficientProduct12115=0;msgErr("produto não cadastrado");setText(pdvBarcode,"");pSetFocus.Call(pdvBarcode);return}
 r:=rows[0];id,_:=strconv.ParseInt(r[0],10,64);price:=parseF(r[3]);cost:=parseF(r[4]);stock:=parseF(r[5]);current:=0.0
 for _,it:=range cart{if it.ID==id{current+=it.Qty}}
 // Informação pedida no PDV: sempre informa a quantidade real atual do produto localizado.
 msg(fmt.Sprintf("Produto: %s\nQuantidade real em estoque: %.3f",r[2],stock))
 if current+qty>stock+0.0001{
  pdvInsufficientProduct12115=id;pdvInsufficientDesc12115=r[2]
  msgErr(fmt.Sprintf("Estoque insuficiente.\nProduto: %s\nEstoque atual: %.3f\n\nSelecione o produto na lista e use CORRIGIR ESTOQUE REAL.",r[2],stock));return
 }
 if r[6]!="1" && stock>0{_=execSQL(fmt.Sprintf("UPDATE products SET active=1,updated_at=CURRENT_TIMESTAMP WHERE id=%d",id))}
 found:=false;for i:=range cart{if cart[i].ID==id{cart[i].Qty+=qty;found=true;break}}
 if !found{cart=append(cart,CartItem{Product:Product{ID:id,Barcode:r[1],Desc:r[2],Price:price,Cost:cost,Stock:stock},Qty:qty})}
 pdvInsufficientProduct12115=0;pdvInsufficientDesc12115="";setText(pdvBarcode,"");setText(pdvQty,"1");refreshCart();pSetFocus.Call(pdvBarcode)
}

func correctSelectedPDVStock12115(){
 id:=pdvInsufficientProduct12115;desc:=pdvInsufficientDesc12115
 idx,_,_:=pSendMessageW.Call(pdvList,LB_GETCURSEL,0,0);i:=int(idx)-1
 if i>=0&&i<len(cart){id=cart[i].ID;desc=cart[i].Desc}
 if id<=0{msg("Bipe o produto com estoque insuficiente ou selecione um item da venda.");return}
 current:=parseF(scalar(fmt.Sprintf("SELECT stock FROM products WHERE id=%d",id)))
 // Usa o campo Qtd. como entrada rápida da quantidade física atual quando o operador escolhe corrigir.
 realQty:=parseF(strings.TrimSpace(getText(pdvQty)))
 if realQty<0{msgErr("Quantidade real inválida.");return}
 if e:=correctStockFromPDV12115(id,realQty);e!=nil{msgErr(e.Error());return}
 for j:=range cart{if cart[j].ID==id{cart[j].Stock=realQty}}
 pdvInsufficientProduct12115=0;pdvInsufficientDesc12115="";refreshCart()
 msg(fmt.Sprintf("Estoque atualizado.\nProduto: %s\nAnterior: %.3f\nQuantidade real: %.3f",desc,current,realQty));setText(pdvQty,"1");pSetFocus.Call(pdvBarcode)
}

func handlePDV12115(id int) bool{
 switch id{case 2103:addCart12115();case 2111:correctSelectedPDVStock12115();default:return false};return true
}
