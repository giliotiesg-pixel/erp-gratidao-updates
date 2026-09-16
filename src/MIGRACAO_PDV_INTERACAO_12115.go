package main

import (
 "fmt"
 "strconv"
 "strings"
 "time"
)

var pdvInsufficientProduct12115 int64
var pdvInsufficientDesc12115 string

func addCart12115(){
 if !ensureDB(){return};barcode:=strings.TrimSpace(getText(pdvBarcode));if barcode==""{return};qty:=parseF(strings.TrimSpace(getText(pdvQty)));if qty<=0{qty=1}
 rows,e:=queryRows("SELECT id,COALESCE(barcode,''),description,price,cost,stock,active FROM products WHERE (barcode='"+esc(barcode)+"' OR internal_code='"+esc(barcode)+"') LIMIT 1",7)
 if e!=nil||len(rows)==0{pdvInsufficientProduct12115=0;msgErr("produto não cadastrado");setText(pdvBarcode,"");pSetFocus.Call(pdvBarcode);return}
 r:=rows[0];id,_:=strconv.ParseInt(r[0],10,64);price:=parseF(r[3]);cost:=parseF(r[4]);stock:=parseF(r[5]);current:=0.0;for _,it:=range cart{if it.ID==id{current+=it.Qty}}
 msg(fmt.Sprintf("Produto: %s\nQuantidade real em estoque: %.3f",r[2],stock))
 if current+qty>stock+0.0001{pdvInsufficientProduct12115=id;pdvInsufficientDesc12115=r[2];msgErr(fmt.Sprintf("Estoque insuficiente.\nProduto: %s\nEstoque atual: %.3f\n\nSelecione o produto na lista e use CORRIGIR ESTOQUE REAL.",r[2],stock));return}
 if r[6]!="1"&&stock>0{_=execSQL(fmt.Sprintf("UPDATE products SET active=1,updated_at=CURRENT_TIMESTAMP WHERE id=%d",id))};found:=false;for i:=range cart{if cart[i].ID==id{cart[i].Qty+=qty;found=true;break}};if !found{cart=append(cart,CartItem{Product:Product{ID:id,Barcode:r[1],Desc:r[2],Price:price,Cost:cost,Stock:stock},Qty:qty})};pdvInsufficientProduct12115=0;pdvInsufficientDesc12115="";setText(pdvBarcode,"");setText(pdvQty,"1");refreshCart();pSetFocus.Call(pdvBarcode)
}

func correctSelectedPDVStock12115(){
 id:=pdvInsufficientProduct12115;desc:=pdvInsufficientDesc12115;idx,_,_:=pSendMessageW.Call(pdvList,LB_GETCURSEL,0,0);i:=int(idx)-1;if i>=0&&i<len(cart){id=cart[i].ID;desc=cart[i].Desc};if id<=0{msg("Bipe o produto com estoque insuficiente ou selecione um item da venda.");return};current:=parseF(scalar(fmt.Sprintf("SELECT stock FROM products WHERE id=%d",id)));realQty:=parseF(strings.TrimSpace(getText(pdvQty)));if realQty<0{msgErr("Quantidade real inválida.");return};if e:=correctStockFromPDV12115(id,realQty);e!=nil{msgErr(e.Error());return};for j:=range cart{if cart[j].ID==id{cart[j].Stock=realQty}};pdvInsufficientProduct12115=0;pdvInsufficientDesc12115="";refreshCart();msg(fmt.Sprintf("Estoque atualizado.\nProduto: %s\nAnterior: %.3f\nQuantidade real: %.3f",desc,current,realQty));setText(pdvQty,"1");pSetFocus.Call(pdvBarcode)
}

// leaveCurrentPDVSaleOpen12115 grava a venda e reserva o estoque agora; o pagamento fica para o Caixa.
func leaveCurrentPDVSaleOpen12115(){
 if len(cart)==0{msgErr("Não há itens na venda.");return};total,cost:=0.0,0.0;for _,it:=range cart{total+=it.Qty*it.Price;cost+=it.Qty*it.Cost};profit:=total-cost;tithe:=0.0;if profit>0{tithe=profit*.10};cust:=strings.TrimSpace(getText(pdvCustomer));if cust==""{cust="Consumidor"};saleNo:=fmt.Sprintf("A%s-%06d",time.Now().Format("20060102"),time.Now().UnixNano()%1000000)
 if e:=execSQL("BEGIN IMMEDIATE");e!=nil{msgErr(e.Error());return};ok:=false;defer func(){if !ok{_=execSQL("ROLLBACK")}}();q:=fmt.Sprintf("INSERT INTO sales(sale_number,customer_name,payment_method,subtotal,total,cost_total,profit,tithe_due,status,created_by) VALUES('%s','%s','PENDENTE',%.2f,%.2f,%.2f,%.2f,%.2f,'ABERTA',1)",esc(saleNo),esc(cust),total,total,cost,profit,tithe);if e:=execSQL(q);e!=nil{msgErr(e.Error());return};sid:=scalar("SELECT last_insert_rowid()")
 for _,it:=range cart{before:=parseF(scalar(fmt.Sprintf("SELECT stock FROM products WHERE id=%d",it.ID)));after:=before-it.Qty;if after < -0.0001{msgErr("Estoque insuficiente para "+it.Desc);return};line:=it.Qty*it.Price;lc:=it.Qty*it.Cost;lp:=line-lc;qs:=fmt.Sprintf("INSERT INTO sale_items(sale_id,product_id,product_description_snapshot,qty,unit_price,unit_cost,line_total,line_cost,line_profit) VALUES(%s,%d,'%s',%.3f,%.2f,%.2f,%.2f,%.2f,%.2f); UPDATE products SET stock=%.3f,updated_at=CURRENT_TIMESTAMP WHERE id=%d; INSERT INTO inventory_movements(product_id,movement_type,origin_type,origin_id,qty,stock_before,stock_after,user_id,reason) VALUES(%d,'RESERVA_VENDA','VENDA_ABERTA',%s,%.3f,%.3f,%.3f,1,'Venda em aberto %s');",sid,it.ID,esc(it.Desc),it.Qty,it.Price,it.Cost,line,lc,lp,after,it.ID,it.ID,sid,-it.Qty,before,after,esc(saleNo));if e:=execSQL(qs);e!=nil{msgErr(e.Error());return}}
 _=ensureCaixaPDV12115Schema();if e:=execSQL(fmt.Sprintf("INSERT INTO open_sale_events_12115(sale_id,event_type,details) VALUES(%s,'ABERTA','Venda deixada em aberto diretamente no PDV')",sid));e!=nil{msgErr(e.Error());return};if e:=execSQL("COMMIT");e!=nil{msgErr(e.Error());return};ok=true;cart=nil;refreshCart();msg("Venda deixada em aberto.\nNúmero: "+saleNo+"\nFinalize o pagamento no módulo Caixa.");pSetFocus.Call(pdvBarcode)
}

func handlePDV12115(id int) bool{switch id{case 2103:addCart12115();case 2111:correctSelectedPDVStock12115();case 2112:leaveCurrentPDVSaleOpen12115();default:return false};return true}
