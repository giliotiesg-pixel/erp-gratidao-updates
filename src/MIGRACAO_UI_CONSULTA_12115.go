package main

import (
 "fmt"
 "unsafe"
)

var consultaCode12115, consultaDesc12115, consultaPrice12115, consultaStock12115 uintptr
var consultaReturnModule12115="inicio"

func showConsultaGlobal12115(){
 if !ensureDB(){return}
 // Guarda o módulo de origem para o X fechar a consulta e voltar exatamente para onde o operador estava.
 if currentModule!="" && currentModule!="consulta" { consultaReturnModule12115=currentModule }
 currentModule="consulta"
 clearContent();header("Consulta de Produto","")
 add("STATIC","Código de barras / código interno",0,300,130,260,24,0)
 consultaCode12115=add("EDIT","",WS_BORDER|ES_AUTOHSCROLL|WS_TABSTOP,300,160,450,38,4301);subclassEdit(consultaCode12115)
 add("BUTTON","CONSULTAR",0,765,157,145,44,4302);add("BUTTON","X FECHAR",0,925,157,110,44,4303)
 consultaDesc12115=add("STATIC","",0x00000001,280,255,850,110,0);f64,_,_:=pCreateFontW.Call(64,0,0,0,700,0,0,0,0,0,0,0,0,uintptr(unsafe.Pointer(ws("Segoe UI"))));pSendMessageW.Call(consultaDesc12115,WM_SETFONT,f64,1)
 consultaPrice12115=add("STATIC","",0x00000001,280,385,850,150,0);f100,_,_:=pCreateFontW.Call(100,0,0,0,700,0,0,0,0,0,0,0,0,uintptr(unsafe.Pointer(ws("Segoe UI"))));pSendMessageW.Call(consultaPrice12115,WM_SETFONT,f100,1)
 consultaStock12115=add("STATIC","",0x00000001,280,560,850,50,0);pSendMessageW.Call(consultaStock12115,WM_SETFONT,fontBig,1);pSetFocus.Call(consultaCode12115)
}
func runConsultaGlobal12115(){p,e:=consultProduct12115(getText(consultaCode12115));if e!=nil{setText(consultaDesc12115,"PRODUTO NÃO CADASTRADO");setText(consultaPrice12115,"");setText(consultaStock12115,"");msgErr("produto não cadastrado");return};setText(consultaDesc12115,p.Description);setText(consultaPrice12115,"R$ "+money(p.Price));setText(consultaStock12115,fmt.Sprintf("Quantidade em estoque: %.3f",p.Stock))}
func closeConsultaGlobal12115(){
 switch consultaReturnModule12115 {
 case "produtos": showProdutosUI12115()
 case "estoque": showEstoqueUI12115()
 case "vendas": showVendasUI12115()
 case "caixa": showCaixaUI12115()
 case "fiado": showFiadoUI12115()
 case "financeiro": showFinanceiroUI12115()
 case "fiscal": showFiscalUI12115()
 case "relatorios": showRelatoriosUI12115()
 case "ofertas": showOfertasUI12115()
 case "pdv": showPDV()
 default: showInicio()
 }
}
func handleConsultaGlobal12115(id int)bool{switch id{case 4302:runConsultaGlobal12115();case 4303:closeConsultaGlobal12115();default:return false};return true}
