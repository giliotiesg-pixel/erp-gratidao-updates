package main

import (
 "fmt"
 "strconv"
 "strings"
 "unsafe"
)

var compraSupplier12115, compraDocument12115, compraDate12115 uintptr
var compraBarcode12115, compraQty12115, compraCost12115 uintptr
var compraItemsList12115, compraHistoryList12115 uintptr
var compraItems12115 []PurchaseItem12115
var compraHistoryIDs12115 []int64

func showComprasUI12115() {
 if !ensureDB(){return}
 currentModule="compras"
 clearContent()
 _=ensureCompras12115Schema()
 header("Compras","")
 add("STATIC","Fornecedor",0,250,115,100,22,0)
 compraSupplier12115=add("EDIT","",WS_BORDER|ES_AUTOHSCROLL|WS_TABSTOP,250,140,300,32,4101)
 add("STATIC","Documento / NF",0,565,115,130,22,0)
 compraDocument12115=add("EDIT","",WS_BORDER|ES_AUTOHSCROLL|WS_TABSTOP,565,140,190,32,4102)
 add("STATIC","Data",0,770,115,80,22,0)
 compraDate12115=add("EDIT","",WS_BORDER|ES_AUTOHSCROLL|WS_TABSTOP,770,140,150,32,4103)

 add("STATIC","Código de barras",0,250,190,140,22,0)
 compraBarcode12115=add("EDIT","",WS_BORDER|ES_AUTOHSCROLL|WS_TABSTOP,250,215,220,32,4110)
 add("STATIC","Quantidade",0,485,190,100,22,0)
 compraQty12115=add("EDIT","1",WS_BORDER|ES_AUTOHSCROLL|WS_TABSTOP,485,215,100,32,4111)
 add("STATIC","Custo unitário",0,600,190,120,22,0)
 compraCost12115=add("EDIT","0,00",WS_BORDER|ES_AUTOHSCROLL|WS_TABSTOP,600,215,120,32,4112)
 add("BUTTON","Adicionar item",0,735,211,135,40,4113)
 add("BUTTON","Remover item",0,885,211,125,40,4114)

 compraItemsList12115=add("LISTBOX","",WS_BORDER|WS_VSCROLL|LBS_NOTIFY|LBS_NOINTEGRALHEIGHT,250,270,1000,180,4120)
 add("BUTTON","SALVAR COMPRA E DAR ENTRADA NO ESTOQUE",0,250,465,355,45,4121)
 add("BUTTON","Limpar",0,620,465,100,45,4122)
 add("BUTTON","Atualizar histórico",0,735,465,160,45,4123)
 add("BUTTON","Excluir compra selecionada",0,910,465,210,45,4124)
 compraHistoryList12115=add("LISTBOX","",WS_BORDER|WS_VSCROLL|LBS_NOTIFY|LBS_NOINTEGRALHEIGHT,250,530,1000,165,4130)
 refreshCompraItems12115(); loadCompraHistoryUI12115(); pSetFocus.Call(compraBarcode12115)
}

func refreshCompraItems12115(){
 if compraItemsList12115==0{return};listReset(compraItemsList12115);listAdd(compraItemsList12115,"ITEM  PRODUTO                                      QTD       CUSTO       TOTAL")
 total:=0.0
 for i,it:=range compraItems12115{desc:=scalar(fmt.Sprintf("SELECT description FROM products WHERE id=%d",it.ProductID));v:=it.Qty*it.UnitCost;total+=v;listAdd(compraItemsList12115,fmt.Sprintf("%02d    %-42s %8.3f   R$ %8.2f  R$ %9.2f",i+1,clip(desc,42),it.Qty,it.UnitCost,v))}
 listAdd(compraItemsList12115,fmt.Sprintf("TOTAL DA COMPRA: R$ %s",money(total)))
}

func addCompraItemUI12115(){
 code:=strings.TrimSpace(getText(compraBarcode12115));if code==""{msgErr("Informe o código de barras.");return}
 rows,e:=queryRows("SELECT id,description,cost FROM products WHERE barcode='"+esc(code)+"' OR internal_code='"+esc(code)+"' LIMIT 1",3);if e!=nil||len(rows)==0{msgErr("Produto não cadastrado: "+code);return}
 id,_:=strconv.ParseInt(rows[0][0],10,64);qty:=parseF(getText(compraQty12115));cost:=parseF(getText(compraCost12115));if qty<=0{msgErr("Quantidade inválida.");return};if cost<=0{cost=parseF(rows[0][2])}
 found:=false;for i:=range compraItems12115{if compraItems12115[i].ProductID==id{compraItems12115[i].Qty+=qty;compraItems12115[i].UnitCost=cost;found=true;break}}
 if !found{compraItems12115=append(compraItems12115,PurchaseItem12115{ProductID:id,Qty:qty,UnitCost:cost})}
 setText(compraBarcode12115,"");setText(compraQty12115,"1");setText(compraCost12115,"0,00");refreshCompraItems12115();pSetFocus.Call(compraBarcode12115)
}

func removeCompraItemUI12115(){idx,_,_:=pSendMessageW.Call(compraItemsList12115,LB_GETCURSEL,0,0);i:=int(idx)-1;if i<0||i>=len(compraItems12115){msg("Selecione um item.");return};compraItems12115=append(compraItems12115[:i],compraItems12115[i+1:]...);refreshCompraItems12115()}
func clearCompraUI12115(){compraItems12115=nil;setText(compraSupplier12115,"");setText(compraDocument12115,"");setText(compraDate12115,"");refreshCompraItems12115()}
func saveCompraUI12115(){if len(compraItems12115)==0{msgErr("Adicione os itens da compra.");return};id,e:=savePurchase12115(getText(compraSupplier12115),getText(compraDocument12115),getText(compraDate12115),compraItems12115);if e!=nil{msgErr("Não foi possível salvar a compra:\n"+e.Error());return};msg(fmt.Sprintf("Compra %d salva. Estoque atualizado automaticamente.",id));clearCompraUI12115();loadCompraHistoryUI12115()}
func loadCompraHistoryUI12115(){if compraHistoryList12115==0{return};rows,e:=purchaseHistory12115();if e!=nil{msgErr(e.Error());return};listReset(compraHistoryList12115);compraHistoryIDs12115=nil;listAdd(compraHistoryList12115,"ID     FORNECEDOR                    DOCUMENTO          DATA                 TOTAL       STATUS");for _,r:=range rows{id,_:=strconv.ParseInt(r[0],10,64);compraHistoryIDs12115=append(compraHistoryIDs12115,id);listAdd(compraHistoryList12115,fmt.Sprintf("%-6s %-29s %-18s %-20s R$ %9s  %s",r[0],clip(r[1],29),clip(r[2],18),r[3],r[4],r[5]))}}
func deleteCompraUI12115(){idx,_,_:=pSendMessageW.Call(compraHistoryList12115,LB_GETCURSEL,0,0);i:=int(idx)-1;if i<0||i>=len(compraHistoryIDs12115){msg("Selecione uma compra.");return};if e:=deletePurchase12115(compraHistoryIDs12115[i]);e!=nil{msgErr(e.Error());return};msg("Compra excluída e estoque estornado.");loadCompraHistoryUI12115()}

// Referência explícita para manter unsafe disponível no mesmo padrão Win32 do projeto.
var _ = unsafe.Pointer(nil)
