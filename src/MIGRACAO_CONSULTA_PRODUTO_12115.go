package main

import (
 "fmt"
 "strings"
)

type ProductConsult12115 struct { ID int64; Barcode,Description string; Price,Stock float64; Active bool; Expiration string }

// Consulta global: mesma regra em qualquer modulo do PRO.
func consultProduct12115(code string) (ProductConsult12115,error) {
 code=strings.TrimSpace(code);if code==""{return ProductConsult12115{},fmt.Errorf("produto nao cadastrado")}
 rows,e:=queryRows(fmt.Sprintf("SELECT id,COALESCE(barcode,''),description,price,stock,active,COALESCE(expiration_date,'') FROM products WHERE barcode='%s' OR internal_code='%s' LIMIT 1",esc(code),esc(code)),7)
 if e!=nil||len(rows)==0{return ProductConsult12115{},fmt.Errorf("produto nao cadastrado")}
 var id int64;fmt.Sscan(rows[0][0],&id);return ProductConsult12115{ID:id,Barcode:rows[0][1],Description:rows[0][2],Price:effectiveProductPrice12115(id),Stock:parseF(rows[0][4]),Active:rows[0][5]=="1",Expiration:rows[0][6]},nil
}

func consultProductByID12115(id int64) (ProductConsult12115,error) {if id<=0{return ProductConsult12115{},fmt.Errorf("produto nao cadastrado")};code:=scalar(fmt.Sprintf("SELECT COALESCE(NULLIF(barcode,''),internal_code) FROM products WHERE id=%d",id));return consultProduct12115(code)}

// Usado quando o PDV encontra estoque insuficiente e o operador informa a quantidade fisica atual.
func correctStockFromPDV12115(productID int64,realQty float64) error {return setRealStock12115(productID,realQty,"Quantidade real informada no PDV")}
