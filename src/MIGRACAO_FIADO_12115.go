package main

import (
 "fmt"
 "strings"
)

// Fiado 12.1.15: preserva vendas, permite identificar cliente após a conclusão e mantém histórico de pagamentos.
func ensureFiado12115Schema() error {
 return execSQL(`
CREATE TABLE IF NOT EXISTS fiado_customer_changes_12115(
 id INTEGER PRIMARY KEY AUTOINCREMENT,
 sale_id INTEGER NOT NULL,
 old_customer TEXT,
 new_customer TEXT NOT NULL,
 created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX IF NOT EXISTS idx_fiado_customer_changes_sale ON fiado_customer_changes_12115(sale_id,created_at);
`)
}

func assignFiadoCustomer12115(saleID int64, customer string) error {
 customer=strings.TrimSpace(customer)
 if saleID<=0{return fmt.Errorf("venda invalida")};if customer==""{return fmt.Errorf("informe o cliente")}
 if ensureFiado12115Schema()!=nil{return fmt.Errorf("falha ao preparar fiado")}
 rows,e:=queryRows(fmt.Sprintf("SELECT customer_name,payment_method,status FROM sales WHERE id=%d",saleID),3);if e!=nil||len(rows)==0{return fmt.Errorf("venda nao encontrada")}
 old:=rows[0][0];pay:=strings.ToUpper(rows[0][1]);if !strings.Contains(pay,"FIADO"){return fmt.Errorf("a venda nao e fiado")};if strings.HasPrefix(rows[0][2],"EXCLUIDA"){return fmt.Errorf("venda excluida")}
 if e=execSQL("BEGIN IMMEDIATE");e!=nil{return e};ok:=false;defer func(){if !ok{_=execSQL("ROLLBACK")}}()
 if e=execSQL(fmt.Sprintf("UPDATE sales SET customer_name='%s',updated_at=CURRENT_TIMESTAMP WHERE id=%d",esc(customer),saleID));e!=nil{return e}
 _=execSQL(fmt.Sprintf("UPDATE credit_payments SET customer_name='%s' WHERE sale_id=%d",esc(customer),saleID))
 if e=execSQL(fmt.Sprintf("INSERT INTO fiado_customer_changes_12115(sale_id,old_customer,new_customer) VALUES(%d,'%s','%s')",saleID,esc(old),esc(customer)));e!=nil{return e}
 if e=execSQL("COMMIT");e!=nil{return e};ok=true;return nil
}

func fiadoOpenSales12115(customer string) ([][]string,error) {
 where:="s.status='CONCLUIDA' AND UPPER(s.payment_method) LIKE '%FIADO%'"
 if strings.TrimSpace(customer)!=""{where+=" AND s.customer_name LIKE '%"+esc(strings.TrimSpace(customer))+"%'"}
 q:="SELECT s.id,s.sale_number,s.customer_name,s.created_at,printf('%.2f',s.total),printf('%.2f',COALESCE((SELECT SUM(cp.amount) FROM credit_payments cp WHERE cp.sale_id=s.id),0)),printf('%.2f',MAX(0,s.total-COALESCE((SELECT SUM(cp.amount) FROM credit_payments cp WHERE cp.sale_id=s.id),0))) FROM sales s WHERE "+where+" ORDER BY s.id DESC"
 return queryRows(q,7)
}

func payFiado12115(saleID int64, amount float64, method string) error {
 if saleID<=0||amount<=0{return fmt.Errorf("pagamento invalido")};method=strings.TrimSpace(method);if method==""{method="DINHEIRO"}
 rows,e:=queryRows(fmt.Sprintf("SELECT customer_name,total,payment_method,status FROM sales WHERE id=%d",saleID),4);if e!=nil||len(rows)==0{return fmt.Errorf("venda nao encontrada")};if !strings.Contains(strings.ToUpper(rows[0][2]),"FIADO"){return fmt.Errorf("venda nao e fiado")};if strings.HasPrefix(rows[0][3],"EXCLUIDA"){return fmt.Errorf("venda excluida")}
 total:=parseF(rows[0][1]);paid:=parseF(scalar(fmt.Sprintf("SELECT COALESCE(SUM(amount),0) FROM credit_payments WHERE sale_id=%d",saleID)));balance:=total-paid;if amount>balance+0.0001{return fmt.Errorf("valor maior que o saldo devedor")}
 return execSQL(fmt.Sprintf("INSERT INTO credit_payments(sale_id,amount,payment_method,customer_name) VALUES(%d,%.4f,'%s','%s')",saleID,amount,esc(method),esc(rows[0][0])))
}

func paidFiadoStatement12115(customer string) ([][]string,error) {
 where:="UPPER(s.payment_method) LIKE '%FIADO%' AND COALESCE((SELECT SUM(cp.amount) FROM credit_payments cp WHERE cp.sale_id=s.id),0)>=s.total"
 if strings.TrimSpace(customer)!=""{where+=" AND s.customer_name LIKE '%"+esc(strings.TrimSpace(customer))+"%'"}
 return queryRows("SELECT s.id,s.sale_number,s.customer_name,s.created_at,printf('%.2f',s.total),COALESCE((SELECT MAX(cp.created_at) FROM credit_payments cp WHERE cp.sale_id=s.id),'') FROM sales s WHERE "+where+" ORDER BY s.id DESC",6)
}
