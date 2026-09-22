package main

import (
 "fmt"
 "strings"
 "time"
)

type editableSaleItem struct {
 ID, ProductID int64
 Description string
 Quantity, UnitPrice float64
}

func (s *Store) ensureSalesAudit() error {
 _,e:=s.DB.Exec(`CREATE TABLE IF NOT EXISTS sales_audit (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  sale_id INTEGER NOT NULL,
  field_name TEXT NOT NULL,
  old_value TEXT,
  new_value TEXT,
  details TEXT,
  changed_at TEXT NOT NULL DEFAULT (datetime('now','localtime'))
 )`)
 return e
}
func (s *Store) editableSaleItems(saleID int64)([]editableSaleItem,error){
 r,e:=s.DB.Query(`SELECT si.id,COALESCE(si.product_id,0),COALESCE(p.description,si.description,''),si.quantity,si.unit_price FROM sale_items si LEFT JOIN products p ON p.id=si.product_id WHERE si.sale_id=? ORDER BY si.id`,saleID);if e!=nil{return nil,e};defer r.Close()
 out:=[]editableSaleItem{};for r.Next(){var x editableSaleItem;if e=r.Scan(&x.ID,&x.ProductID,&x.Description,&x.Quantity,&x.UnitPrice);e!=nil{return nil,e};out=append(out,x)};return out,r.Err()
}
func auditTx(tx interface{Exec(string,...any)(interface{},error)}, saleID int64, field,old,new,details string){}
// updateSaleAudited atualiza cabecalho e itens em uma unica transacao, recalcula total e ajusta estoque pela diferenca.
func (s *Store) updateSaleAudited(saleID int64, customer,payment,dateText string, items []editableSaleItem) error {
 if e:=s.ensureSalesAudit();e!=nil{return e}
 tx,e:=s.DB.Begin();if e!=nil{return e};defer tx.Rollback()
 var oldCustomer,oldPayment,oldDate,status string;var oldTotal float64
 if e=tx.QueryRow(`SELECT COALESCE(customer_name,''),COALESCE(payment_method,''),created_at,total,COALESCE(status,'') FROM sales WHERE id=? AND deleted_at IS NULL`,saleID).Scan(&oldCustomer,&oldPayment,&oldDate,&oldTotal,&status);e!=nil{return e}
 if strings.TrimSpace(customer)==""{customer="Consumidor"};if strings.TrimSpace(payment)==""{return fmt.Errorf("informe a forma de pagamento")}
 parsed,e:=time.Parse("2006-01-02 15:04:05",strings.TrimSpace(dateText));if e!=nil{return fmt.Errorf("data deve estar em AAAA-MM-DD HH:MM:SS")}
 _=parsed
 oldItems:=map[int64]editableSaleItem{};rr,e:=tx.Query(`SELECT id,COALESCE(product_id,0),COALESCE(description,''),quantity,unit_price FROM sale_items WHERE sale_id=?`,saleID);if e!=nil{return e}
 for rr.Next(){var x editableSaleItem;if e=rr.Scan(&x.ID,&x.ProductID,&x.Description,&x.Quantity,&x.UnitPrice);e!=nil{rr.Close();return e};oldItems[x.ID]=x};rr.Close()
 total:=0.0
 for _,x:=range items {
  if x.ID<=0||x.Quantity<=0||x.UnitPrice<0{return fmt.Errorf("item inválido: %s",x.Description)}
  old,ok:=oldItems[x.ID];if !ok{return fmt.Errorf("item %d não pertence à venda",x.ID)}
  if status=="CONCLUIDA"&&old.ProductID>0 {
   delta:=x.Quantity-old.Quantity
   if delta>0 {var stock float64;if e=tx.QueryRow("SELECT stock FROM products WHERE id=?",old.ProductID).Scan(&stock);e!=nil{return e};if stock<delta{return fmt.Errorf("estoque insuficiente para %s",x.Description)}}
   if delta!=0 {if _,e=tx.Exec("UPDATE products SET stock=stock-? WHERE id=?",delta,old.ProductID);e!=nil{return e}}
  }
  if _,e=tx.Exec("UPDATE sale_items SET quantity=?,unit_price=?,description=? WHERE id=? AND sale_id=?",x.Quantity,x.UnitPrice,x.Description,x.ID,saleID);e!=nil{return e}
  if old.Quantity!=x.Quantity||old.UnitPrice!=x.UnitPrice||old.Description!=x.Description {_,e=tx.Exec(`INSERT INTO sales_audit(sale_id,field_name,old_value,new_value,details) VALUES(?,?,?,?,?)`,saleID,"ITEM",fmt.Sprintf("%s | %.3f x %.2f",old.Description,old.Quantity,old.UnitPrice),fmt.Sprintf("%s | %.3f x %.2f",x.Description,x.Quantity,x.UnitPrice),"Item vendido alterado");if e!=nil{return e}}
  total+=x.Quantity*x.UnitPrice
 }
 changes:=[][4]string{{"CLIENTE",oldCustomer,customer,"Cliente alterado"},{"PAGAMENTO",oldPayment,payment,"Pagamento alterado"},{"DATA",oldDate,dateText,"Data da venda alterada"},{"TOTAL",fmt.Sprintf("%.2f",oldTotal),fmt.Sprintf("%.2f",total),"Total recalculado automaticamente"}}
 if _,e=tx.Exec("UPDATE sales SET customer_name=?,payment_method=?,created_at=?,total=? WHERE id=?",customer,payment,dateText,total,saleID);e!=nil{return e}
 for _,c:=range changes{if c[1]!=c[2]{if _,e=tx.Exec("INSERT INTO sales_audit(sale_id,field_name,old_value,new_value,details) VALUES(?,?,?,?,?)",saleID,c[0],c[1],c[2],c[3]);e!=nil{return e}}}
 return tx.Commit()
}
func (s *Store) salesAuditRows(saleID int64)[][]string{
 _=s.ensureSalesAudit()
 return s.rows("SELECT changed_at,field_name,COALESCE(old_value,''),COALESCE(new_value,''),COALESCE(details,'') FROM sales_audit WHERE sale_id=? ORDER BY id DESC LIMIT 500",saleID)
}
