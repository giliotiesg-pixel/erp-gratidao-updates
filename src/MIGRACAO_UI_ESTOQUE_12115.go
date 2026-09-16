package main

import (
 "fmt"
 "strconv"
 "strings"
)

var estoqueUIList12115, estoqueSearch12115, estoqueReal12115, estoqueValidity12115 uintptr
var estoqueUIIDs12115 []int64

func showEstoqueUI12115(){
 if !ensureDB(){return};currentModule="estoque";clearContent();_=ensureEstoque12115Schema();header("Estoque","")
 add("STATIC","Buscar produto",0,250,120,120,22,0);estoqueSearch12115=add("EDIT","",WS_BORDER|ES_AUTOHSCROLL|WS_TABSTOP,250,145,360,32,4401);subclassEdit(estoqueSearch12115);add("BUTTON","Buscar",0,625,142,100,38,4402);add("BUTTON","Estoque baixo",0,735,142,125,38,4403);add("BUTTON","Todos",0,870,142,90,38,4404)
 estoqueUIList12115=add("LISTBOX","",WS_BORDER|WS_VSCROLL|LBS_NOTIFY|LBS_NOINTEGRALHEIGHT,250,195,1000,315,4410)
 add("STATIC","Quantidade real atual",0,250,525,160,22,0);estoqueReal12115=add("EDIT","",WS_BORDER|ES_AUTOHSCROLL,250,550,140,32,4411);add("BUTTON","SALVAR ESTOQUE REAL",0,405,546,185,40,4412)
 add("STATIC","Validade (AAAA-MM-DD)",0,610,525,180,22,0);estoqueValidity12115=add("EDIT","",WS_BORDER|ES_AUTOHSCROLL,610,550,180,32,4413);add("BUTTON","SALVAR VALIDADE",0,805,546,160,40,4414)
 add("BUTTON","Extrato de movimentações",0,980,546,210,40,4415)
 add("STATIC","Sem controle de lote. Estoque positivo reativa automaticamente o produto para venda.",0,250,610,900,28,0)
 loadEstoqueUI12115(false)
}
func loadEstoqueUI12115(low bool){if estoqueUIList12115==0{return};term:=strings.TrimSpace(getText(estoqueSearch12115));q:="SELECT id,COALESCE(barcode,''),description,printf('%.3f',stock),printf('%.3f',min_stock),CASE WHEN active=1 THEN 'ATIVO' ELSE 'INATIVO' END,COALESCE(expiration_date,'') FROM products WHERE 1=1";if term!=""{q+=" AND (description LIKE '%"+esc(term)+"%' OR barcode LIKE '%"+esc(term)+"%' OR internal_code LIKE '%"+esc(term)+"%')"};if low{q+=" AND stock<=min_stock"};q+=" ORDER BY description LIMIT 2000";rows,e:=queryRows(q,7);if e!=nil{msgErr(e.Error());return};listReset(estoqueUIList12115);estoqueUIIDs12115=nil;listAdd(estoqueUIList12115,"CÓDIGO           PRODUTO                                      ESTOQUE     MÍNIMO      STATUS    VALIDADE");for _,r:=range rows{id,_:=strconv.ParseInt(r[0],10,64);estoqueUIIDs12115=append(estoqueUIIDs12115,id);listAdd(estoqueUIList12115,fmt.Sprintf("%-16s %-44s %9s  %9s  %-9s %s",r[1],clip(r[2],44),r[3],r[4],r[5],r[6]))}}
func selectedEstoqueUI12115()int64{idx,_,_:=pSendMessageW.Call(estoqueUIList12115,LB_GETCURSEL,0,0);i:=int(idx)-1;if i<0||i>=len(estoqueUIIDs12115){return 0};return estoqueUIIDs12115[i]}
func saveEstoqueRealUI12115(){id:=selectedEstoqueUI12115();if id==0{msg("Selecione um produto.");return};q:=parseF(getText(estoqueReal12115));if q<0{msgErr("Quantidade inválida.");return};if e:=setRealStock12115(id,q,"Quantidade real informada no módulo Estoque");e!=nil{msgErr(e.Error());return};msg("Estoque real atualizado.");loadEstoqueUI12115(false)}
func saveValidadeUI12115(){id:=selectedEstoqueUI12115();if id==0{msg("Selecione um produto.");return};if e:=setExpiration12115(id,getText(estoqueValidity12115));e!=nil{msgErr(e.Error());return};msg("Validade atualizada.");loadEstoqueUI12115(false)}
func showLedgerUI12115(){id:=selectedEstoqueUI12115();if id==0{msg("Selecione um produto.");return};rows,e:=inventoryLedger12115(id);if e!=nil{msgErr(e.Error());return};text:="MOVIMENTAÇÕES DO PRODUTO\n\n";for _,r:=range rows{text+=fmt.Sprintf("%s | %s | %s | antes %s | qtd %s | depois %s | %s\n",r[8],r[2],r[3],r[4],r[5],r[6],r[7])};if len(rows)==0{text+="Nenhuma movimentação registrada."};msg(text)}
func handleEstoqueUI12115(id int)bool{switch id{case 4402:loadEstoqueUI12115(false);case 4403:loadEstoqueUI12115(true);case 4404:setText(estoqueSearch12115,"");loadEstoqueUI12115(false);case 4412:saveEstoqueRealUI12115();case 4414:saveValidadeUI12115();case 4415:showLedgerUI12115();default:return false};return true}
