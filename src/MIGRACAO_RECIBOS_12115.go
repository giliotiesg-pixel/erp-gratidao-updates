package main

import (
 "fmt"
 "strings"
)

type Receipt12115 struct { SaleNumber,Customer,Payment,CreatedAt string; Subtotal,Discount,Total float64; Items [][]string }

func receiptData12115(saleID int64) (Receipt12115,error) {
 if saleID<=0{return Receipt12115{},fmt.Errorf("venda invalida")};rows,e:=queryRows(fmt.Sprintf("SELECT sale_number,customer_name,payment_method,created_at,subtotal,discount,total FROM sales WHERE id=%d",saleID),7);if e!=nil||len(rows)==0{return Receipt12115{},fmt.Errorf("venda nao encontrada")}
 items,e:=queryRows(fmt.Sprintf("SELECT product_description_snapshot,printf('%%.3f',qty),printf('%%.2f',unit_price),printf('%%.2f',line_total) FROM sale_items WHERE sale_id=%d ORDER BY id",saleID),4);if e!=nil{return Receipt12115{},e}
 return Receipt12115{SaleNumber:rows[0][0],Customer:rows[0][1],Payment:rows[0][2],CreatedAt:rows[0][3],Subtotal:parseF(rows[0][4]),Discount:parseF(rows[0][5]),Total:parseF(rows[0][6]),Items:items},nil
}

func receiptText80mm12115(saleID int64) (string,error) {r,e:=receiptData12115(saleID);if e!=nil{return "",e};var b strings.Builder;b.WriteString("ARMAZEM GRATIDAO\n");b.WriteString("Venda: "+r.SaleNumber+"\nCliente: "+r.Customer+"\n");b.WriteString("--------------------------------\n");for _,it:=range r.Items{b.WriteString(it[0]+"\n"+it[1]+" x R$ "+it[2]+" = R$ "+it[3]+"\n")};b.WriteString("--------------------------------\n");b.WriteString(fmt.Sprintf("TOTAL: R$ %.2f\nPagamento: %s\n%s\n",r.Total,r.Payment,r.CreatedAt));b.WriteString("COMPROVANTE NAO FISCAL\n");return b.String(),nil}

func whatsappReceipt12115(saleID int64) (string,error) {r,e:=receiptData12115(saleID);if e!=nil{return "",e};var b strings.Builder;b.WriteString("Armazem Gratidao - Venda "+r.SaleNumber+"\n");for _,it:=range r.Items{b.WriteString(it[0]+" - "+it[1]+" x R$ "+it[2]+" = R$ "+it[3]+"\n")};b.WriteString(fmt.Sprintf("Total: R$ %.2f\nPagamento: %s",r.Total,r.Payment));return b.String(),nil}
