package main

import (
 "fmt"
 "strconv"
 "strings"
)

var fiadoSearchUI12115, fiadoListUI12115, fiadoCustomerUI12115, fiadoAmountUI12115, fiadoMethodUI12115 uintptr
var fiadoIDsUI12115 []int64

func showFiadoUI12115(){
 if !ensureDB(){return};currentModule="fiado";clearContent();_=ensureFiado12115Schema();header("Fiado","")
 add("STATIC","Cliente",0,250,120,80,22,0);fiadoSearchUI12115=add("EDIT","",WS_BORDER|ES_AUTOHSCROLL|WS_TABSTOP,250,145,330,32,4501);subclassEdit(fiadoSearchUI12115);add("BUTTON","Buscar",0,595,142,100,38,4502);add("BUTTON","Todos",0,705,142,90,38,4503);add("BUTTON","Compras pagas",0,805,142,140,38,4504);add("BUTTON","Voltar",0,955,142,100,38,4505)
 fiadoListUI12115=add("LISTBOX","",WS_BORDER|WS_VSCROLL|LBS_NOTIFY|LBS_NOINTEGRALHEIGHT,250,195,1000,300,4510)
 add("STATIC","Inserir / alterar cliente da venda selecionada",0,250,510,320,22,0);fiadoCustomerUI12115=add("EDIT","",WS_BORDER|ES_AUTOHSCROLL,250,535,300,32,4511);add("BUTTON","SALVAR CLIENTE",0,565,531,150,40,4512)
 add("STATIC","Valor pago",0,250,585,100,22,0);fiadoAmountUI12115=add("EDIT","",WS_BORDER|ES_AUTOHSCROLL,250,610,120,32,4513);add("STATIC","Forma",0,385,585,70,22,0);fiadoMethodUI12115=add("EDIT","DINHEIRO",WS_BORDER|ES_AUTOHSCROLL,385,610,150,32,4514);add("BUTTON","REGISTRAR PAGAMENTO",0,550,606,190,40,4515)
 add("STATIC","O cliente pode ser informado mesmo depois da venda Fiado concluída; o nome também é atualizado em Vendas.",0,250,665,950,25,0)
 loadFiadoUI12115(false)
}
func loadFiadoUI12115(paid bool){if fiadoListUI12115==0{return};customer:=strings.TrimSpace(getText(fiadoSearchUI12115));var rows [][]string;var e error;if paid{rows,e=paidFiadoStatement12115(customer)}else{rows,e=fiadoOpenSales12115(customer)};if e!=nil{msgErr(e.Error());return};listReset(fiadoListUI12115);fiadoIDsUI12115=nil;if paid{listAdd(fiadoListUI12115,"VENDA          CLIENTE                         DATA                 TOTAL       PAGA EM");for _,r:=range rows{id,_:=strconv.ParseInt(r[0],10,64);fiadoIDsUI12115=append(fiadoIDsUI12115,id);listAdd(fiadoListUI12115,fmt.Sprintf("%-14s %-30s %-20s R$ %9s  %s",r[1],clip(r[2],30),r[3],r[4],r[5]))};return};listAdd(fiadoListUI12115,"VENDA          CLIENTE                         DATA                 TOTAL       PAGO        SALDO");for _,r:=range rows{id,_:=strconv.ParseInt(r[0],10,64);fiadoIDsUI12115=append(fiadoIDsUI12115,id);listAdd(fiadoListUI12115,fmt.Sprintf("%-14s %-30s %-20s R$ %9s  R$ %9s  R$ %9s",r[1],clip(r[2],30),r[3],r[4],r[5],r[6]))}}
func selectedFiadoUI12115()int64{idx,_,_:=pSendMessageW.Call(fiadoListUI12115,LB_GETCURSEL,0,0);i:=int(idx)-1;if i<0||i>=len(fiadoIDsUI12115){return 0};return fiadoIDsUI12115[i]}
func saveFiadoCustomerUI12115(){id:=selectedFiadoUI12115();if id==0{msg("Selecione uma venda Fiado.");return};name:=strings.TrimSpace(getText(fiadoCustomerUI12115));if e:=assignFiadoCustomer12115(id,name);e!=nil{msgErr(e.Error());return};msg("Cliente atualizado no Fiado e em Vendas.");loadFiadoUI12115(false)}
func payFiadoUI12115(){id:=selectedFiadoUI12115();if id==0{msg("Selecione uma venda Fiado.");return};v:=parseF(getText(fiadoAmountUI12115));if e:=payFiado12115(id,v,getText(fiadoMethodUI12115));e!=nil{msgErr(e.Error());return};msg("Pagamento registrado.");setText(fiadoAmountUI12115,"");loadFiadoUI12115(false)}
func handleFiadoUI12115(id int)bool{switch id{case 4502:loadFiadoUI12115(false);case 4503:setText(fiadoSearchUI12115,"");loadFiadoUI12115(false);case 4504:loadFiadoUI12115(true);case 4505:showInicio();case 4512:saveFiadoCustomerUI12115();case 4515:payFiadoUI12115();default:return false};return true}
