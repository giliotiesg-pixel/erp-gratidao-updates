package main

import (
 "fmt"
 "strconv"
 "strings"
)

var prodList12115,prodSearch12115,prodBarcode12115,prodInternal12115,prodDesc12115,prodBrand12115,prodCategory12115,prodUnit12115,prodCost12115,prodPrice12115,prodStock12115,prodMin12115,prodExp12115 uintptr
var prodIDs12115 []int64
var prodSelected12115 int64

func showProdutosUI12115(){
 if !ensureDB(){return};currentModule="produtos";clearContent();_=ensureEstoque12115Schema();_=reactivateProductsWithStock12115();header("Produtos","")
 add("STATIC","Lista única de produtos — ativos e sem estoque ficam disponíveis para consulta.",0,250,112,800,24,0);prodSearch12115=add("EDIT","",WS_BORDER|ES_AUTOHSCROLL,250,140,330,30,4801);add("BUTTON","Buscar",0,595,137,95,36,4802);add("BUTTON","Todos",0,700,137,85,36,4803);add("BUTTON","Novo",0,795,137,85,36,4804)
 prodList12115=add("LISTBOX","",WS_BORDER|WS_VSCROLL|LBS_NOTIFY|LBS_NOINTEGRALHEIGHT,250,185,1000,220,4810);add("BUTTON","Carregar selecionado",0,250,415,170,36,4811);add("BUTTON","Desativar",0,430,415,110,36,4812)
 y:=465;add("STATIC","Código barras",0,250,y,100,20,0);prodBarcode12115=add("EDIT","",WS_BORDER|ES_AUTOHSCROLL,250,y+22,170,30,4820);add("STATIC","Código interno",0,435,y,110,20,0);prodInternal12115=add("EDIT","",WS_BORDER|ES_AUTOHSCROLL,435,y+22,140,30,4821);add("STATIC","Descrição",0,590,y,90,20,0);prodDesc12115=add("EDIT","",WS_BORDER|ES_AUTOHSCROLL,590,y+22,360,30,4822);add("STATIC","Unidade",0,965,y,70,20,0);prodUnit12115=add("EDIT","UN",WS_BORDER|ES_AUTOHSCROLL,965,y+22,80,30,4823)
 y=530;add("STATIC","Marca",0,250,y,70,20,0);prodBrand12115=add("EDIT","",WS_BORDER|ES_AUTOHSCROLL,250,y+22,180,30,4824);add("STATIC","Categoria",0,445,y,80,20,0);prodCategory12115=add("EDIT","",WS_BORDER|ES_AUTOHSCROLL,445,y+22,180,30,4825);add("STATIC","Custo",0,640,y,60,20,0);prodCost12115=add("EDIT","0,00",WS_BORDER|ES_AUTOHSCROLL,640,y+22,90,30,4826);add("STATIC","Preço",0,745,y,60,20,0);prodPrice12115=add("EDIT","0,00",WS_BORDER|ES_AUTOHSCROLL,745,y+22,90,30,4827);add("STATIC","Estoque",0,850,y,70,20,0);prodStock12115=add("EDIT","0",WS_BORDER|ES_AUTOHSCROLL,850,y+22,90,30,4828);add("STATIC","Mínimo",0,955,y,60,20,0);prodMin12115=add("EDIT","0",WS_BORDER|ES_AUTOHSCROLL,955,y+22,90,30,4829)
 y=595;add("STATIC","Validade (AAAA-MM-DD)",0,250,y,180,20,0);prodExp12115=add("EDIT","",WS_BORDER|ES_AUTOHSCROLL,250,y+22,180,30,4830);add("BUTTON","SALVAR PRODUTO",0,450,y+18,170,40,4831);add("STATIC","Sem lote. Ao salvar estoque maior que zero, o produto fica ativo para venda.",0,640,y+25,570,25,0);loadProdutosUI12115()
}
func loadProdutosUI12115(){rows,e:=productsUnified12115(getText(prodSearch12115));if e!=nil{msgErr(e.Error());return};listReset(prodList12115);prodIDs12115=nil;listAdd(prodList12115,"CÓDIGO           PRODUTO                                      PREÇO       ESTOQUE    ATIVO  VALIDADE");for _,r:=range rows{id,_:=strconv.ParseInt(r[0],10,64);prodIDs12115=append(prodIDs12115,id);ativo:="NÃO";if r[11]=="1"{ativo="SIM"};listAdd(prodList12115,fmt.Sprintf("%-16s %-44s R$ %8s  %9s  %-5s  %s",r[1],clip(r[3],44),r[8],r[9],ativo,r[12]))}}
func selectedProdutoUI12115()int64{idx,_,_:=pSendMessageW.Call(prodList12115,LB_GETCURSEL,0,0);i:=int(idx)-1;if i<0||i>=len(prodIDs12115){return 0};return prodIDs12115[i]}
func clearProdutoUI12115(){prodSelected12115=0;for _,h:=range []uintptr{prodBarcode12115,prodInternal12115,prodDesc12115,prodBrand12115,prodCategory12115,prodExp12115}{setText(h,"")};setText(prodUnit12115,"UN");setText(prodCost12115,"0,00");setText(prodPrice12115,"0,00");setText(prodStock12115,"0");setText(prodMin12115,"0")}
func loadSelectedProdutoUI12115(){id:=selectedProdutoUI12115();if id==0{msg("Selecione um produto.");return};rows,e:=queryRows(fmt.Sprintf("SELECT COALESCE(barcode,''),COALESCE(internal_code,''),description,COALESCE(brand,''),COALESCE(category,''),unit,cost,price,stock,min_stock,COALESCE(expiration_date,'') FROM products WHERE id=%d",id),11);if e!=nil||len(rows)==0{msgErr("Produto não encontrado.");return};r:=rows[0];prodSelected12115=id;vals:=[]struct{h uintptr;v string}{{prodBarcode12115,r[0]},{prodInternal12115,r[1]},{prodDesc12115,r[2]},{prodBrand12115,r[3]},{prodCategory12115,r[4]},{prodUnit12115,r[5]},{prodCost12115,r[6]},{prodPrice12115,r[7]},{prodStock12115,r[8]},{prodMin12115,r[9]},{prodExp12115,r[10]}};for _,x:=range vals{setText(x.h,x.v)}}
func saveProdutoUI12115(){desc:=strings.TrimSpace(getText(prodDesc12115));e:=saveProduct12115(prodSelected12115,getText(prodBarcode12115),getText(prodInternal12115),desc,getText(prodBrand12115),getText(prodCategory12115),getText(prodUnit12115),parseF(getText(prodCost12115)),parseF(getText(prodPrice12115)),parseF(getText(prodStock12115)),parseF(getText(prodMin12115)),getText(prodExp12115));if e!=nil{msgErr(e.Error());return};msg("Produto salvo.");clearProdutoUI12115();loadProdutosUI12115()}
func deactivateProdutoUI12115(){id:=selectedProdutoUI12115();if id==0{msg("Selecione um produto.");return};if e:=deactivateProduct12115(id);e!=nil{msgErr(e.Error());return};loadProdutosUI12115()}
func handleProdutosUI12115(id int)bool{switch id{case 4802:loadProdutosUI12115();case 4803:setText(prodSearch12115,"");loadProdutosUI12115();case 4804:clearProdutoUI12115();case 4811:loadSelectedProdutoUI12115();case 4812:deactivateProdutoUI12115();case 4831:saveProdutoUI12115();default:return false};return true}
