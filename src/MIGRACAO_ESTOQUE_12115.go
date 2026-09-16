package main

import (
    "fmt"
    "strconv"
    "strings"
)

// Estoque 12.1.15: sem lotes. A referencia operacional usa somente estoque atual e validade.
func ensureEstoque12115Schema() error {
    if err := execSQL(`
CREATE TABLE IF NOT EXISTS inventory_adjustments_12115(
 id INTEGER PRIMARY KEY AUTOINCREMENT,
 product_id INTEGER NOT NULL,
 stock_before REAL NOT NULL,
 stock_after REAL NOT NULL,
 reason TEXT,
 created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX IF NOT EXISTS idx_inv_adj_12115_product ON inventory_adjustments_12115(product_id,created_at);
`); err != nil { return err }
    _ = execSQL("ALTER TABLE products ADD COLUMN expiration_date TEXT")
    return nil
}

// setRealStock12115 permite informar a quantidade real encontrada fisicamente.
// Ao informar estoque positivo, o produto volta automaticamente a ficar ativo para venda.
func setRealStock12115(productID int64, realQty float64, reason string) error {
    if productID <= 0 { return fmt.Errorf("produto invalido") }
    if realQty < 0 { return fmt.Errorf("quantidade atual nao pode ser negativa") }
    if err := ensureEstoque12115Schema(); err != nil { return err }
    rows, err := queryRows(fmt.Sprintf("SELECT stock FROM products WHERE id=%d", productID), 1)
    if err != nil || len(rows) == 0 { return fmt.Errorf("produto nao encontrado") }
    before := parseF(rows[0][0])
    active := 0
    if realQty > 0 { active = 1 }
    if err := execSQL("BEGIN IMMEDIATE"); err != nil { return err }
    ok := false
    defer func(){ if !ok { _ = execSQL("ROLLBACK") } }()
    if err := execSQL(fmt.Sprintf("UPDATE products SET stock=%.4f,active=%d,updated_at=CURRENT_TIMESTAMP WHERE id=%d", realQty, active, productID)); err != nil { return err }
    delta := realQty-before
    movement := "AJUSTE"
    if delta > 0 { movement = "ENTRADA_AJUSTE" } else if delta < 0 { movement = "SAIDA_AJUSTE" }
    if err := execSQL(fmt.Sprintf("INSERT INTO inventory_adjustments_12115(product_id,stock_before,stock_after,reason) VALUES(%d,%.4f,%.4f,'%s')", productID,before,realQty,esc(strings.TrimSpace(reason)))); err != nil { return err }
    if err := execSQL(fmt.Sprintf("INSERT INTO inventory_movements(product_id,movement_type,origin_type,qty,stock_before,stock_after,user_id,reason) VALUES(%d,'%s','AJUSTE_REAL',%.4f,%.4f,%.4f,1,'%s')", productID,movement,delta,before,realQty,esc(strings.TrimSpace(reason)))); err != nil { return err }
    if err := execSQL("COMMIT"); err != nil { return err }
    ok = true
    return nil
}

func setExpiration12115(productID int64, expiration string) error {
    if productID <= 0 { return fmt.Errorf("produto invalido") }
    if err := ensureEstoque12115Schema(); err != nil { return err }
    return execSQL(fmt.Sprintf("UPDATE products SET expiration_date='%s',updated_at=CURRENT_TIMESTAMP WHERE id=%d",esc(strings.TrimSpace(expiration)),productID))
}

func productByBarcode12115(barcode string) (Product,error) {
    barcode = strings.TrimSpace(barcode)
    if barcode == "" { return Product{},fmt.Errorf("produto nao cadastrado") }
    rows,err:=queryRows(fmt.Sprintf("SELECT id,COALESCE(barcode,''),description,price,cost,stock FROM products WHERE barcode='%s' LIMIT 1",esc(barcode)),6)
    if err!=nil || len(rows)==0 { return Product{},fmt.Errorf("produto nao cadastrado") }
    id,_:=strconv.ParseInt(rows[0][0],10,64)
    return Product{ID:id,Barcode:rows[0][1],Desc:rows[0][2],Price:parseF(rows[0][3]),Cost:parseF(rows[0][4]),Stock:parseF(rows[0][5])},nil
}

func inventoryLedger12115(productID int64) ([][]string,error) {
    q := "SELECT im.id,p.description,im.movement_type,im.origin_type,printf('%.3f',im.stock_before),printf('%.3f',im.qty),printf('%.3f',im.stock_after),COALESCE(im.reason,''),im.created_at FROM inventory_movements im JOIN products p ON p.id=im.product_id"
    if productID > 0 { q += fmt.Sprintf(" WHERE im.product_id=%d",productID) }
    q += " ORDER BY im.id DESC LIMIT 1000"
    return queryRows(q,9)
}
