package main

import (
 "fmt"
 "strconv"
 "strings"
)

// Venda 12.1.15: edição direta no módulo Vendas, sem reabrir no PDV.
type SaleEditItem12115 struct { ProductID int64; Qty, UnitPrice float64 }

func ensureVendas12115Schema() error {
 return execSQL(`CREATE TABLE IF NOT EXISTS sale_changes_12115(id INTEGER PRIMARY KEY AUTOINCREMENT,sale_id INTEGER NOT NULL,change_type TEXT NOT NULL,details TEXT,created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP); CREATE INDEX IF NOT EXISTS idx_sale_changes_12115_sale ON sale_changes_12115(sale_id,created_at);`)
}

func recalcSale12115(saleID int64) error {
 if saleID<=0{return fmt.Errorf("venda invalida")}
 subtotal:=parseF(scalar(fmt.Sprintf("SELECT COALESCE(SUM(line_total),0) FROM sale_items WHERE sale_id=%d",saleID)))
 cost:=parseF(scalar(fmt.Sprintf("SELECT COALESCE(SUM(line_cost),0) FROM sale_items WHERE sale_id=%d",saleID)))
 discount:=parseF(scalar(fmt.Sprintf("SELECT COALESCE(discount,0) FROM sales WHERE id=%d",saleID)))
 total:=subtotal-discount;if total<0{total=0};profit:=total-cost;tithe:=profit*0.10;if tithe<0{tithe=0}
 return execSQL(fmt.Sprintf("UPDATE sales SET subtotal=%.4f,total=%.4f,cost_total=%.4f,profit=%.4f,tithe_due=%.4f,updated_at=CURRENT_TIMESTAMP WHERE id=%d",subtotal,total,cost,profit,tithe,saleID))
}

// Edição transacional dos itens: calcula a diferença entre itens antigos e novos e ajusta o estoque.
// Assim trocar produto ou quantidade em Vendas não duplica nem faz desaparecer estoque.
func replaceSaleItems12115(saleID int64,items []SaleEditItem12115) error {
 if saleID<=0||len(items)==0{return fmt.Errorf("informe os itens da venda")};if ensureVendas12115Schema()!=nil{return fmt.Errorf("falha ao preparar vendas")}
 status:=scalar(fmt.Sprintf("SELECT status FROM sales WHERE id=%d",saleID));if status==""{return fmt.Errorf("venda nao encontrada")};if strings.HasPrefix(status,"EXCLUIDA"){return fmt.Errorf("venda excluida nao pode ser alterada")}
 oldRows,e:=queryRows(fmt.Sprintf("SELECT product_id,COALESCE(SUM(qty),0) FROM sale_items WHERE sale_id=%d GROUP BY product_id",saleID),2);if e!=nil{return e};oldQty:=map[int64]float64{};for _,r:=range oldRows{id,_:=strconv.ParseInt(r[0],10,64);oldQty[id]=parseF(r[1])}
 newQty:=map[int64]float64{};for _,it:=range items{if it.ProductID<=0||it.Qty<=0||it.UnitPrice<0{return fmt.Errorf("item invalido")};newQty[it.ProductID]+=it.Qty}
 if e=execSQL("BEGIN IMMEDIATE");e!=nil{return e};ok:=false;defer func(){if !ok{_=execSQL("ROLLBACK")}}()
 all:=map[int64]bool{};for id:=range oldQty{all[id]=true};for id:=range newQty{all[id]=true}
 for pid:=range all{
  delta:=oldQty[pid]-newQty[pid]
  if abs(delta)<=0.0001{continue}
  before:=parseF(scalar(fmt.Sprintf("SELECT stock FROM products WHERE id=%d",pid)));after:=before+delta
  if after < -0.0001{return fmt.Errorf("estoque insuficiente para concluir a alteracao")};if after<0{after=0}
  active:=0;if after>0.0001{active=1}
  if e=execSQL(fmt.Sprintf("UPDATE products SET stock=%.4f,active=%d,updated_at=CURRENT_TIMESTAMP WHERE id=%d",after,active,pid));e!=nil{return e}
  mov:="SAIDA";qtyMovement:=abs(delta);if delta>0{mov="ENTRADA"}
  if e=execSQL(fmt.Sprintf("INSERT INTO inventory_movements(product_id,movement_type,origin_type,origin_id,qty,stock_before,stock_after,user_id,reason) VALUES(%d,'%s','EDICAO_VENDA',%d,%.4f,%.4f,%.4f,1,'Ajuste por edição direta da venda')",pid,mov,saleID,qtyMovement,before,after));e!=nil{return e}
 }
 if e=execSQL(fmt.Sprintf("DELETE FROM sale_items WHERE sale_id=%d",saleID));e!=nil{return e}
 for _,it:=range items{rows,er:=queryRows(fmt.Sprintf("SELECT description,cost FROM products WHERE id=%d",it.ProductID),2);if er!=nil||len(rows)==0{return fmt.Errorf("produto nao encontrado")};cost:=parseF(rows[0][1]);lt:=it.Qty*it.UnitPrice;lc:=it.Qty*cost;if er=execSQL(fmt.Sprintf("INSERT INTO sale_items(sale_id,product_id,product_description_snapshot,qty,unit_price,unit_cost,line_total,line_cost,line_profit) VALUES(%d,%d,'%s',%.4f,%.4f,%.4f,%.4f,%.4f,%.4f)",saleID,it.ProductID,esc(rows[0][0]),it.Qty,it.UnitPrice,cost,lt,lc,lt-lc));er!=nil{return er}}
 if e=recalcSale12115(saleID);e!=nil{return e};_=execSQL(fmt.Sprintf("INSERT INTO sale_changes_12115(sale_id,change_type,details) VALUES(%d,'ITENS','Itens/produtos/quantidades/valores alterados com ajuste de estoque')",saleID));if e=execSQL("COMMIT");e!=nil{return e};ok=true;return nil
}

func updateSaleInfo12115(saleID int64,customer,payment,date string) error {
 if saleID<=0{return fmt.Errorf("venda invalida")};status:=scalar(fmt.Sprintf("SELECT status FROM sales WHERE id=%d",saleID));if status==""||strings.HasPrefix(status,"EXCLUIDA"){return fmt.Errorf("venda nao encontrada ou excluida")};sets:=[]string{}
 if strings.TrimSpace(customer)!=""{sets=append(sets,"customer_name='"+esc(strings.TrimSpace(customer))+"'")};if strings.TrimSpace(payment)!=""{sets=append(sets,"payment_method='"+esc(strings.TrimSpace(payment))+"'")};if strings.TrimSpace(date)!=""{sets=append(sets,"created_at='"+esc(strings.TrimSpace(date))+"'")};if len(sets)==0{return nil};sets=append(sets,"updated_at=CURRENT_TIMESTAMP");if e:=execSQL(fmt.Sprintf("UPDATE sales SET %s WHERE id=%d",strings.Join(sets,","),saleID));e!=nil{return e};_=ensureVendas12115Schema();_=execSQL(fmt.Sprintf("INSERT INTO sale_changes_12115(sale_id,change_type,details) VALUES(%d,'DADOS','Cliente/pagamento/data alterados')",saleID));return nil
}

// Exclusão transacional: devolve estoque e encerra a venda, evitando o antigo botão que não fazia nada.
func deleteSale12115(saleID int64,reason string) error {
 if saleID<=0{return fmt.Errorf("venda invalida")};status:=scalar(fmt.Sprintf("SELECT status FROM sales WHERE id=%d",saleID));if status==""||strings.HasPrefix(status,"EXCLUIDA"){return fmt.Errorf("venda nao encontrada ou ja excluida")}
 rows,e:=queryRows(fmt.Sprintf("SELECT product_id,qty FROM sale_items WHERE sale_id=%d",saleID),2);if e!=nil{return e};if e=execSQL("BEGIN IMMEDIATE");e!=nil{return e};ok:=false;defer func(){if !ok{_=execSQL("ROLLBACK")}}()
 for _,r:=range rows{pid,_:=strconv.ParseInt(r[0],10,64);qty:=parseF(r[1]);before:=parseF(scalar(fmt.Sprintf("SELECT stock FROM products WHERE id=%d",pid)));after:=before+qty;if e=execSQL(fmt.Sprintf("UPDATE products SET stock=%.4f,active=1,updated_at=CURRENT_TIMESTAMP WHERE id=%d",after,pid));e!=nil{return e};if e=execSQL(fmt.Sprintf("INSERT INTO inventory_movements(product_id,movement_type,origin_type,origin_id,qty,stock_before,stock_after,user_id,reason) VALUES(%d,'ENTRADA','EXCLUSAO_VENDA',%d,%.4f,%.4f,%.4f,1,'%s')",pid,saleID,qty,before,after,esc(reason)));e!=nil{return e}}
 if e=execSQL(fmt.Sprintf("UPDATE sales SET status='EXCLUIDA',deleted_at=CURRENT_TIMESTAMP,updated_at=CURRENT_TIMESTAMP WHERE id=%d",saleID));e!=nil{return e};_=ensureVendas12115Schema();_=execSQL(fmt.Sprintf("INSERT INTO sale_changes_12115(sale_id,change_type,details) VALUES(%d,'EXCLUSAO','%s')",saleID,esc(reason)));if e=execSQL("COMMIT");e!=nil{return e};ok=true;return nil
}
