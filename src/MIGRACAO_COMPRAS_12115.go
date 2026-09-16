package main

import (
    "fmt"
    "strconv"
    "strings"
)

// PurchaseItem12115 representa o comportamento de itens de compra da referencia 12.1.15.
type PurchaseItem12115 struct {
    ProductID int64
    Qty       float64
    UnitCost  float64
}

// ensureCompras12115Schema adiciona somente estruturas novas e preserva os dados existentes.
func ensureCompras12115Schema() error {
    return execSQL(`
CREATE TABLE IF NOT EXISTS purchases_12115(
 id INTEGER PRIMARY KEY AUTOINCREMENT,
 supplier_name TEXT,
 document_number TEXT,
 purchase_date TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
 total REAL NOT NULL DEFAULT 0,
 status TEXT NOT NULL DEFAULT 'ATIVA',
 created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
 deleted_at TEXT
);
CREATE TABLE IF NOT EXISTS purchase_items_12115(
 id INTEGER PRIMARY KEY AUTOINCREMENT,
 purchase_id INTEGER NOT NULL,
 product_id INTEGER NOT NULL,
 product_description_snapshot TEXT NOT NULL,
 qty REAL NOT NULL,
 unit_cost REAL NOT NULL,
 total REAL NOT NULL,
 stock_before REAL NOT NULL,
 stock_after REAL NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_purchase_items_12115_purchase ON purchase_items_12115(purchase_id);
CREATE INDEX IF NOT EXISTS idx_purchases_12115_date ON purchases_12115(purchase_date);
`)
}

// savePurchase12115 cadastra a compra, calcula os totais, dá entrada no estoque e registra movimentacao.
func savePurchase12115(supplier, document, date string, items []PurchaseItem12115) (int64, error) {
    if len(items) == 0 { return 0, fmt.Errorf("informe pelo menos um item") }
    if err := ensureCompras12115Schema(); err != nil { return 0, err }
    if strings.TrimSpace(date) == "" { date = "datetime('now','localtime')" } else { date = "'" + esc(date) + "'" }
    total := 0.0
    for _, it := range items {
        if it.ProductID <= 0 || it.Qty <= 0 || it.UnitCost < 0 { return 0, fmt.Errorf("item de compra invalido") }
        total += it.Qty * it.UnitCost
    }
    if err := execSQL("BEGIN IMMEDIATE"); err != nil { return 0, err }
    ok := false
    defer func(){ if !ok { _ = execSQL("ROLLBACK") } }()
    q := fmt.Sprintf("INSERT INTO purchases_12115(supplier_name,document_number,purchase_date,total,status) VALUES('%s','%s',%s,%.4f,'ATIVA')", esc(strings.TrimSpace(supplier)), esc(strings.TrimSpace(document)), date, total)
    if err := execSQL(q); err != nil { return 0, err }
    id, _ := strconv.ParseInt(scalar("SELECT last_insert_rowid()"),10,64)
    for _, it := range items {
        rows, err := queryRows(fmt.Sprintf("SELECT description,stock FROM products WHERE id=%d AND active=1", it.ProductID),2)
        if err != nil || len(rows)==0 { return 0, fmt.Errorf("produto %d nao encontrado", it.ProductID) }
        desc := rows[0][0]
        before := parseF(rows[0][1])
        after := before + it.Qty
        itemTotal := it.Qty * it.UnitCost
        if err := execSQL(fmt.Sprintf("INSERT INTO purchase_items_12115(purchase_id,product_id,product_description_snapshot,qty,unit_cost,total,stock_before,stock_after) VALUES(%d,%d,'%s',%.4f,%.4f,%.4f,%.4f,%.4f)", id,it.ProductID,esc(desc),it.Qty,it.UnitCost,itemTotal,before,after)); err != nil { return 0, err }
        if err := execSQL(fmt.Sprintf("UPDATE products SET stock=%.4f,cost=%.4f,active=1,updated_at=CURRENT_TIMESTAMP WHERE id=%d",after,it.UnitCost,it.ProductID)); err != nil { return 0, err }
        _ = execSQL(fmt.Sprintf("INSERT INTO inventory_movements(product_id,movement_type,origin_type,origin_id,qty,stock_before,stock_after,user_id,reason) VALUES(%d,'ENTRADA','COMPRA',%d,%.4f,%.4f,%.4f,1,'Compra 12.1.15')",it.ProductID,id,it.Qty,before,after))
    }
    if err := execSQL("COMMIT"); err != nil { return 0, err }
    ok = true
    return id,nil
}

// deletePurchase12115 estorna exatamente a quantidade que entrou no estoque e preserva historico.
func deletePurchase12115(id int64) error {
    if id <= 0 { return fmt.Errorf("compra invalida") }
    if err := ensureCompras12115Schema(); err != nil { return err }
    if scalar(fmt.Sprintf("SELECT status FROM purchases_12115 WHERE id=%d",id)) != "ATIVA" { return fmt.Errorf("compra nao encontrada ou ja excluida") }
    rows, err := queryRows(fmt.Sprintf("SELECT product_id,qty FROM purchase_items_12115 WHERE purchase_id=%d",id),2)
    if err != nil { return err }
    if err := execSQL("BEGIN IMMEDIATE"); err != nil { return err }
    ok := false
    defer func(){ if !ok { _ = execSQL("ROLLBACK") } }()
    for _, r := range rows {
        pid,_ := strconv.ParseInt(r[0],10,64); qty:=parseF(r[1])
        before:=parseF(scalar(fmt.Sprintf("SELECT stock FROM products WHERE id=%d",pid)))
        after:=before-qty
        if after < 0 { return fmt.Errorf("nao e possivel estornar a compra: estoque atual insuficiente para o produto %d",pid) }
        if err:=execSQL(fmt.Sprintf("UPDATE products SET stock=%.4f,updated_at=CURRENT_TIMESTAMP WHERE id=%d",after,pid)); err!=nil{return err}
        _=execSQL(fmt.Sprintf("INSERT INTO inventory_movements(product_id,movement_type,origin_type,origin_id,qty,stock_before,stock_after,user_id,reason) VALUES(%d,'SAIDA','ESTORNO_COMPRA',%d,%.4f,%.4f,%.4f,1,'Exclusao de compra 12.1.15')",pid,id,qty,before,after))
    }
    if err:=execSQL(fmt.Sprintf("UPDATE purchases_12115 SET status='EXCLUIDA',deleted_at=CURRENT_TIMESTAMP WHERE id=%d",id));err!=nil{return err}
    if err:=execSQL("COMMIT");err!=nil{return err}; ok=true; return nil
}

func purchaseHistory12115() ([][]string,error) {
    if err:=ensureCompras12115Schema();err!=nil{return nil,err}
    return queryRows("SELECT id,COALESCE(supplier_name,''),COALESCE(document_number,''),purchase_date,printf('%.2f',total),status FROM purchases_12115 ORDER BY id DESC",6)
}
