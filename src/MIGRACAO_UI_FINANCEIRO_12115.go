package main

import (
 "fmt"
 "strconv"
 "strings"
)

var finList12115, finKind12115, finPartner12115, finDesc12115, finValue12115, finDue12115, finPay12115, finSource12115 uintptr
var finIDs12115 []int64

func showFinanceiroUI12115(){
 if !ensureDB(){return};currentModule="financeiro";clearContent();_=ensureParceirosFinanceiro12115();header("Saldo Devedor / Financeiro","")
 add("STATIC","Tipo",0,250,120,70,20,0);finKind12115=add("EDIT","FORNECEDOR",WS_BORDER|ES_AUTOHSCROLL,250,143,130,30,4601)
 add("STATIC","Cliente / Fornecedor",0,395,120,170,20,0);finPartner12115=add("EDIT","",WS_BORDER|ES_AUTOHSCROLL,395,143,260,30,4602)
 add("STATIC","Descrição",0,670,120,100,20,0);finDesc12115=add("EDIT","",WS_BORDER|ES_AUTOHSCROLL,670,143,260,30,4603)
 add("STATIC","Valor",0,945,120,70,20,0);finValue12115=add("EDIT","",WS_BORDER|ES_AUTOHSCROLL,945,143,100,30,4604)
 add("STATIC","Vencimento",0,1060,120,100,20,0);finDue12115=add("EDIT","",WS_BORDER|ES_AUTOHSCROLL,1060,143,130,30,4605)
 add("BUTTON","SALVAR CONTA",0,250,188,150,40,4606);add("BUTTON","ATUALIZAR",0,415,188,120,40,4607);add("BUTTON","CLIENTES / FORNECEDORES",0,550,188,230,40,4608)
 finList12115=add("LISTBOX","",WS_BORDER|WS_VSCROLL|LBS_NOTIFY|LBS_NOINTEGRALHEIGHT,250,245,1000,310,4610)
 add("STATIC","Pagar valor",0,250,570,100,20,0);finPay12115=add("EDIT","",WS_BORDER|ES_AUTOHSCROLL,250,593,120,30,4611);add("STATIC","Origem",0,385,570,80,20,0);finSource12115=add("EDIT","CAIXA",WS_BORDER|ES_AUTOHSCROLL,385,593,150,30,4612);add("BUTTON","REGISTRAR PAGAMENTO",0,550,589,200,40,4613)
 add("STATIC","A origem pode identificar CAIXA, FIADO ou outra compensação usada no pagamento.",0,250,650,900,25,0)
 loadFinanceiroUI12115()
}
func loadFinanceiroUI12115(){rows,e:=payableStatement12115();if e!=nil{msgErr(e.Error());return};listReset(finList12115);finIDs12115=nil;listAdd(finList12115,"TIPO          CLIENTE/FORNECEDOR             DESCRIÇÃO                 VALOR       PAGO        SALDO       VENC.       STATUS");for _,r:=range rows{id,_:=strconv.ParseInt(r[0],10,64);finIDs12115=append(finIDs12115,id);listAdd(finList12115,fmt.Sprintf("%-13s %-30s %-25s %9s  %9s  %9s  %-10s %s",r[1],clip(r[2],30),clip(r[3],25),r[4],r[5],r[6],r[7],r[8]))}}
func selectedFinanceiroUI12115()int64{idx,_,_:=pSendMessageW.Call(finList12115,LB_GETCURSEL,0,0);i:=int(idx)-1;if i<0||i>=len(finIDs12115){return 0};return finIDs12115[i]}
func saveFinanceiroUI12115(){_,e:=savePayable12115(getText(finKind12115),getText(finPartner12115),getText(finDesc12115),parseF(getText(finValue12115)),getText(finDue12115));if e!=nil{msgErr(e.Error());return};msg("Conta registrada.");setText(finDesc12115,"");setText(finValue12115,"");loadFinanceiroUI12115()}
func payFinanceiroUI12115(){id:=selectedFinanceiroUI12115();if id==0{msg("Selecione uma conta.");return};source:=strings.TrimSpace(getText(finSource12115));if e:=payPayable12115(id,parseF(getText(finPay12115)),source,"");e!=nil{msgErr(e.Error());return};msg("Pagamento registrado e saldo atualizado.");setText(finPay12115,"");loadFinanceiroUI12115()}
func showPartnersUI12115(){rows,e:=listPartners12115("");if e!=nil{msgErr(e.Error());return};text:="CLIENTES / FORNECEDORES\n\n";for _,r:=range rows{text+=fmt.Sprintf("%s | %s | %s | %s\n",r[1],r[2],r[3],r[4])};if len(rows)==0{text+="Nenhum cadastro."};msg(text)}
func handleFinanceiroUI12115(id int)bool{switch id{case 4606:saveFinanceiroUI12115();case 4607:loadFinanceiroUI12115();case 4608:showPartnersUI12115();case 4613:payFinanceiroUI12115();default:return false};return true}
