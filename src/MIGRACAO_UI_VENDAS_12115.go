package main

import (
 "fmt"
 "strconv"
 "strings"
 "unsafe"
)

var vendasEditList12115, vendasItems12115 uintptr
var vendasCustomer12115, vendasPayment12115, vendasDate12115 uintptr
var vendasBarcode12115, vendasQty12115, vendasPrice12115 uintptr
var vendasEditIDs12115 []int64
var vendasSelected12115 int64
var vendasEditItems12115 []SaleEditItem12115

func showVendasUI12115(){
 if !ensureDB(){return};currentModule="vendas";clearContent();_=ensureVendas12115Schema();header("Vendas","")
 add("STATIC","Todas as vendas em um único lugar. Edite diretamente aqui; não reabre no PDV.",0,250,112,850,25,0)
 vendasEditList12115=add("LISTBOX","",WS_BORDER|WS_VSCROLL|LBS_NOTIFY|LBS_NOINTEGRALHEIGHT,250,145,1000,205,4201)
 add("BUTTON","Carregar venda selecionada",0,250,360,205,38,4202);add("BUTTON","Atualizar lista",0,465,360,125,38,4203);add("BUTTON","EXCLUIR VENDA",0,600,360,150,38,4204);add("BUTTON","Fechar",0,760,360,100,38,4205)
 add("STATIC","Cliente",0,250,412,80,20,0);vendasCustomer12115=add("EDIT","",WS_BORDER|ES_AUTOHSCROLL,250,435,260,30,4210)
 add("STATIC","Pagamento",0,525,412,100,20,0);vendasPayment12115=add("EDIT","",WS_BORDER|ES_AUTOHSCROLL,525,435,160,30,4211)
 add("STATIC","Data/Hora",0,700,412,100,20,0);vendasDate12115=add("EDIT","",WS_BORDER|ES_AUTOHSCROLL,700,435,210,30,4212)
 add("BUTTON","SALVAR DADOS",0,925,431,150,38,4213)
 vendasItems12115=add("LISTBOX","",WS_BORDER|WS_VSCROLL|LBS_NOTIFY|LBS_NOINTEGRALHEIGHT,250,480,1000,130,4220)
 add("STATIC","Código",0,250,620,70,20,0);vendasBarcode12115=add("EDIT","",WS_BORDER|ES_AUTOHSCROLL,250,642,170,30,4221)
 add("STATIC","Qtd",0,435,620,50,20,0);vendasQty12115=add("EDIT","1",WS_BORDER|ES_AUTOHSCROLL,435,642,80,30,4222)
 add("STATIC","Preço",0,530,620,60,20,0);vendasPrice12115=add("EDIT","0,00",WS_BORDER|ES_AUTOHSCROLL,530,642,100,30,4223)
 add("BUTTON","Adicionar/alterar item",0,645,638,180,38,4224);add("BUTTON","Remover item",0,835,638,120,38,4225);add("BUTTON","SALVAR ITENS E TOTAL",0,965,638,190,38,4226)
 loadVendasUI12115()
}

func loadVendasUI12115(){if vendasEditList12115==0{return};rows,e:=queryRows("SELECT id,sale_number,datetime(created_at,'localtime'),customer_name,payment_method,printf('%.2f',total),status FROM sales ORDER BY id DESC LIMIT 1500",7);if e!=nil{msgErr(e.Error());return};listReset(vendasEditList12115);vendasEditIDs12115=nil;listAdd(vendasEditList12115,"VENDA          DATA/HORA            CLIENTE                    PAGAMENTO       TOTAL       STATUS");for _,r:=range rows{id,_:=strconv.ParseInt(r[0],10,64);vendasEditIDs12115=append(vendasEditIDs12115,id);listAdd(vendasEditList12115,fmt.Sprintf("%-14s %-19s %-26s %-15s R$ %9s  %s",r[1],r[2],clip(r[3],26),r[4],r[5],r[6]))}}
func selectedVendaUI12115() int64{idx,_,_:=pSendMessageW.Call(vendasEditList12115,LB_GETCURSEL,0,0);i:=int(idx)-1;if i<0||i>=len(vendasEditIDs12115){return 0};return vendasEditIDs12115[i]}
func loadSelectedVendaUI12115(){sid:=selectedVendaUI12115();if sid==0{msg("Selecione uma venda.");return};vendasSelected12115=sid;rows,e:=queryRows(fmt.Sprintf("SELECT customer_name,payment_method,created_at FROM sales WHERE id=%d",sid),3);if e!=nil||len(rows)==0{msgErr("Venda não encontrada.");return};setText(vendasCustomer12115,rows[0][0]);setText(vendasPayment12115,rows[0][1]);setText(vendasDate12115,rows[0][2]);ir,e:=queryRows(fmt.Sprintf("SELECT product_id,product_description_snapshot,qty,unit_price FROM sale_items WHERE sale_id=%d ORDER BY id",sid),4);if e!=nil{msgErr(e.Error());return};vendasEditItems12115=nil;listReset(vendasItems12115);listAdd(vendasItems12115,"PRODUTO                                      QTD        PREÇO       TOTAL");for _,r:=range ir{pid,_:=strconv.ParseInt(r[0],10,64);q:=parseF(r[2]);p:=parseF(r[3]);vendasEditItems12115=append(vendasEditItems12115,SaleEditItem12115{ProductID:pid,Qty:q,UnitPrice:p});listAdd(vendasItems12115,fmt.Sprintf("%-44s %8.3f   R$ %8.2f  R$ %9.2f",clip(r[1],44),q,p,q*p))}}
func saveVendaInfoUI12115(){if vendasSelected12115==0{msg("Carregue uma venda.");return};if e:=updateSaleInfo12115(vendasSelected12115,getText(vendasCustomer12115),getText(vendasPayment12115),getText(vendasDate12115));e!=nil{msgErr(e.Error());return};msg("Dados da venda salvos.");loadVendasUI12115()}
func addVendaItemUI12115(){if vendasSelected12115==0{msg("Carregue uma venda.");return};code:=strings.TrimSpace(getText(vendasBarcode12115));rows,e:=queryRows("SELECT id,description,price FROM products WHERE barcode='"+esc(code)+"' OR internal_code='"+esc(code)+"' LIMIT 1",3);if e!=nil||len(rows)==0{msgErr("Produto não cadastrado.");return};pid,_:=strconv.ParseInt(rows[0][0],10,64);q:=parseF(getText(vendasQty12115));if q<=0{q=1};p:=parseF(getText(vendasPrice12115));if p<=0{p=parseF(rows[0][2])};found:=false;for i:=range vendasEditItems12115{if vendasEditItems12115[i].ProductID==pid{vendasEditItems12115[i].Qty=q;vendasEditItems12115[i].UnitPrice=p;found=true;break}};if !found{vendasEditItems12115=append(vendasEditItems12115,SaleEditItem12115{ProductID:pid,Qty:q,UnitPrice:p})};refreshVendaItemsUI12115()}
func refreshVendaItemsUI12115(){listReset(vendasItems12115);listAdd(vendasItems12115,"PRODUTO                                      QTD        PREÇO       TOTAL");for _,it:=range vendasEditItems12115{d:=scalar(fmt.Sprintf("SELECT description FROM products WHERE id=%d",it.ProductID));listAdd(vendasItems12115,fmt.Sprintf("%-44s %8.3f   R$ %8.2f  R$ %9.2f",clip(d,44),it.Qty,it.UnitPrice,it.Qty*it.UnitPrice))}}
func removeVendaItemUI12115(){idx,_,_:=pSendMessageW.Call(vendasItems12115,LB_GETCURSEL,0,0);i:=int(idx)-1;if i<0||i>=len(vendasEditItems12115){msg("Selecione um item.");return};vendasEditItems12115=append(vendasEditItems12115[:i],vendasEditItems12115[i+1:]...);refreshVendaItemsUI12115()}
func saveVendaItemsUI12115(){if vendasSelected12115==0{msg("Carregue uma venda.");return};if len(vendasEditItems12115)==0{msgErr("A venda precisa ter pelo menos um item.");return};if e:=replaceSaleItems12115(vendasSelected12115,vendasEditItems12115);e!=nil{msgErr(e.Error());return};msg("Itens alterados e total recalculado automaticamente.");loadVendasUI12115();loadSelectedVendaUI12115()}
func deleteVendaUI12115(){
 sid:=selectedVendaUI12115();if sid==0{sid=vendasSelected12115};if sid==0{msg("Selecione uma venda.");return}
 if e:=deleteSale12115(sid,"Exclusão solicitada no módulo Vendas");e!=nil{msgErr(e.Error());return}
 // Requisito 12.1.15: excluir de verdade, devolver o estoque e sair da tela de Vendas após a exclusão.
 vendasSelected12115=0;vendasEditIDs12115=nil;vendasEditItems12115=nil
 msg("Venda excluída. Estoque devolvido.")
 showInicio()
}
func handleVendasUI12115(id int) bool{switch id{case 4202:loadSelectedVendaUI12115();case 4203:loadVendasUI12115();case 4204:deleteVendaUI12115();case 4205:showInicio();case 4213:saveVendaInfoUI12115();case 4224:addVendaItemUI12115();case 4225:removeVendaItemUI12115();case 4226:saveVendaItemsUI12115();default:return false};return true}
var _=unsafe.Pointer(nil)
