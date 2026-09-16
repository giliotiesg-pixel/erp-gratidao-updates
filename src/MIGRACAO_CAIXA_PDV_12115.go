package main

import (
 "fmt"
 "strings"
)

// Caixa/PDV 12.1.15: venda pode ficar em aberto e ser finalizada depois sem perder itens.
func ensureCaixaPDV12115Schema() error {
 return execSQL(`
CREATE TABLE IF NOT EXISTS open_sale_events_12115(id INTEGER PRIMARY KEY AUTOINCREMENT,sale_id INTEGER NOT NULL,event_type TEXT NOT NULL,details TEXT,created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP);
CREATE INDEX IF NOT EXISTS idx_open_sale_events_sale ON open_sale_events_12115(sale_id,created_at);
`)
}

func leaveSaleOpen12115(saleID int64) error {
 if saleID<=0{return fmt.Errorf("venda invalida")};if ensureCaixaPDV12115Schema()!=nil{return fmt.Errorf("falha ao preparar venda em aberto")}
 if scalar(fmt.Sprintf("SELECT id FROM sales WHERE id=%d",saleID))==""{return fmt.Errorf("venda nao encontrada")}
 if e:=execSQL(fmt.Sprintf("UPDATE sales SET status='ABERTA',updated_at=CURRENT_TIMESTAMP WHERE id=%d",saleID));e!=nil{return e}
 return execSQL(fmt.Sprintf("INSERT INTO open_sale_events_12115(sale_id,event_type,details) VALUES(%d,'ABERTA','Venda deixada em aberto no PDV')",saleID))
}

func finalizeOpenSale12115(saleID int64,payment string) error {
 if saleID<=0{return fmt.Errorf("venda invalida")};payment=strings.ToUpper(strings.TrimSpace(payment));if payment==""{return fmt.Errorf("informe a forma de pagamento")}
 allowed:=map[string]bool{"DINHEIRO":true,"PIX_QR":true,"PIX_SEM_QR":true,"PIX":true,"DEBITO":true,"CREDITO":true,"ALELO":true,"PLUXXE":true,"TICKET":true,"VR":true,"FIADO":true}
 if !allowed[payment]{return fmt.Errorf("forma de pagamento invalida")}
 if scalar(fmt.Sprintf("SELECT status FROM sales WHERE id=%d",saleID))!="ABERTA"{return fmt.Errorf("venda nao esta em aberto")}
 total:=parseF(scalar(fmt.Sprintf("SELECT total FROM sales WHERE id=%d",saleID)));if total<0{return fmt.Errorf("total da venda invalido")}
 if e:=execSQL("BEGIN IMMEDIATE");e!=nil{return e};ok:=false;defer func(){if !ok{_=execSQL("ROLLBACK")}}()
 if e:=execSQL(fmt.Sprintf("UPDATE sales SET status='CONCLUIDA',payment_method='%s',updated_at=CURRENT_TIMESTAMP WHERE id=%d",esc(payment),saleID));e!=nil{return e}
 // Registra o pagamento somente na finalização. Assim uma venda ABERTA não entra no caixa antes de ser paga.
 fee,feeType,feeValue:=paymentFeeFor(payment,total);net:=total-fee;feePercent:=0.0;if feeType=="PERCENT"{feePercent=feeValue}
 if e:=execSQL(fmt.Sprintf("INSERT INTO sale_payments(sale_id,payment_type,gross_amount,discount_percent,discount_amount,net_amount,reference) VALUES(%d,'%s',%.2f,%.4f,%.2f,%.2f,'FINALIZACAO_ABERTA_TAXA_%s_%.4f')",saleID,esc(payment),total,feePercent,fee,net,esc(feeType),feeValue));e!=nil{return e}
 if payment=="FIADO"{
  customer:=strings.TrimSpace(scalar(fmt.Sprintf("SELECT customer_name FROM sales WHERE id=%d",saleID)))
  if customer==""||strings.EqualFold(customer,"Consumidor"){return fmt.Errorf("para finalizar no FIADO, informe o cliente da venda")}
 }
 if e:=ensureCaixaPDV12115Schema();e!=nil{return e}
 if e:=execSQL(fmt.Sprintf("INSERT INTO open_sale_events_12115(sale_id,event_type,details) VALUES(%d,'FINALIZADA','Pagamento: %s')",saleID,esc(payment)));e!=nil{return e}
 if e:=execSQL("COMMIT");e!=nil{return e};ok=true;return nil
}

func openSales12115() ([][]string,error) {
 return queryRows("SELECT id,sale_number,customer_name,datetime(created_at,'localtime'),printf('%.2f',total),payment_method FROM sales WHERE status='ABERTA' ORDER BY id DESC",6)
}

func cashSummary12115() ([][]string,error) {
 q:="SELECT payment_method,COUNT(*),printf('%.2f',COALESCE(SUM(total),0)) FROM sales WHERE status='CONCLUIDA' AND date(created_at,'localtime')=date('now','localtime') GROUP BY payment_method ORDER BY payment_method"
 return queryRows(q,3)
}

func currentCashSession12115() ([][]string,error) {
 return queryRows("SELECT id,opened_at,printf('%.2f',opening_amount),COALESCE(closed_at,''),printf('%.2f',COALESCE(closing_amount,0)),status FROM cash_sessions ORDER BY id DESC LIMIT 1",6)
}
