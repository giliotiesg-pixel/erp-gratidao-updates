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
 if saleID<=0{return fmt.Errorf("venda invalida")};payment=strings.TrimSpace(payment);if payment==""{return fmt.Errorf("informe a forma de pagamento")}
 if scalar(fmt.Sprintf("SELECT status FROM sales WHERE id=%d",saleID))!="ABERTA"{return fmt.Errorf("venda nao esta em aberto")}
 if e:=execSQL(fmt.Sprintf("UPDATE sales SET status='CONCLUIDA',payment_method='%s',updated_at=CURRENT_TIMESTAMP WHERE id=%d",esc(payment),saleID));e!=nil{return e}
 _=ensureCaixaPDV12115Schema();return execSQL(fmt.Sprintf("INSERT INTO open_sale_events_12115(sale_id,event_type,details) VALUES(%d,'FINALIZADA','Pagamento: %s')",saleID,esc(payment)))
}

func openSales12115() ([][]string,error) {
 return queryRows("SELECT id,sale_number,customer_name,created_at,printf('%.2f',total),payment_method FROM sales WHERE status='ABERTA' ORDER BY id DESC",6)
}

func cashSummary12115() ([][]string,error) {
 q:="SELECT payment_method,COUNT(*),printf('%.2f',COALESCE(SUM(total),0)) FROM sales WHERE status='CONCLUIDA' AND date(created_at,'localtime')=date('now','localtime') GROUP BY payment_method ORDER BY payment_method"
 return queryRows(q,3)
}

func currentCashSession12115() ([][]string,error) {
 return queryRows("SELECT id,opened_at,printf('%.2f',opening_amount),COALESCE(closed_at,''),printf('%.2f',COALESCE(closing_amount,0)),status FROM cash_sessions ORDER BY id DESC LIMIT 1",6)
}
