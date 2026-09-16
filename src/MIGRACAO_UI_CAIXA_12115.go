package main

import (
 "fmt"
 "strconv"
)

var caixaOpenList12115, caixaPay12115, caixaSummary12115 uintptr
var caixaOpenIDs12115 []int64

func showCaixaUI12115(){
 if !ensureDB(){return};currentModule="caixa";clearContent();_=ensureCaixaPDV12115Schema();header("Caixa / Vendas em Aberto","")
 add("STATIC","Vendas deixadas em aberto no PDV",0,250,120,330,25,0);add("BUTTON","Atualizar",0,1030,115,110,38,4701)
 caixaOpenList12115=add("LISTBOX","",WS_BORDER|WS_VSCROLL|LBS_NOTIFY|LBS_NOINTEGRALHEIGHT,250,155,890,245,4710)
 add("STATIC","Forma de pagamento para finalizar",0,250,415,270,22,0);caixaPay12115=add("EDIT","DINHEIRO",WS_BORDER|ES_AUTOHSCROLL,250,440,220,32,4711);add("BUTTON","FINALIZAR VENDA",0,485,436,170,40,4712)
 add("STATIC","Resumo das vendas concluídas de hoje",0,250,500,330,25,0);caixaSummary12115=add("LISTBOX","",WS_BORDER|WS_VSCROLL|LBS_NOINTEGRALHEIGHT,250,535,600,150,4720);add("BUTTON","Atualizar resumo",0,865,535,150,40,4721)
 loadCaixaUI12115();loadCashSummaryUI12115()
}
func loadCaixaUI12115(){rows,e:=openSales12115();if e!=nil{msgErr(e.Error());return};listReset(caixaOpenList12115);caixaOpenIDs12115=nil;listAdd(caixaOpenList12115,"VENDA          CLIENTE                         DATA/HORA             TOTAL       PAGAMENTO");for _,r:=range rows{id,_:=strconv.ParseInt(r[0],10,64);caixaOpenIDs12115=append(caixaOpenIDs12115,id);listAdd(caixaOpenList12115,fmt.Sprintf("%-14s %-30s %-20s R$ %9s  %s",r[1],clip(r[2],30),r[3],r[4],r[5]))}}
func selectedCaixaOpen12115()int64{idx,_,_:=pSendMessageW.Call(caixaOpenList12115,LB_GETCURSEL,0,0);i:=int(idx)-1;if i<0||i>=len(caixaOpenIDs12115){return 0};return caixaOpenIDs12115[i]}
func finalizeCaixaOpenUI12115(){id:=selectedCaixaOpen12115();if id==0{msg("Selecione uma venda em aberto.");return};if e:=finalizeOpenSale12115(id,getText(caixaPay12115));e!=nil{msgErr(e.Error());return};msg("Venda finalizada. Retornando ao PDV.");showPDV()}
func loadCashSummaryUI12115(){rows,e:=cashSummary12115();if e!=nil{msgErr(e.Error());return};listReset(caixaSummary12115);listAdd(caixaSummary12115,"FORMA DE PAGAMENTO                 VENDAS       TOTAL");for _,r:=range rows{listAdd(caixaSummary12115,fmt.Sprintf("%-35s %6s   R$ %10s",r[0],r[1],r[2]))}}
func handleCaixaUI12115(id int)bool{switch id{case 4701:loadCaixaUI12115();case 4712:finalizeCaixaOpenUI12115();case 4721:loadCashSummaryUI12115();default:return false};return true}
