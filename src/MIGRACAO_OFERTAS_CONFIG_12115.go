package main

import (
 "fmt"
 "strings"
)

func ensureOfertasConfig12115Schema() error {
 return execSQL(`
CREATE TABLE IF NOT EXISTS offers_12115(id INTEGER PRIMARY KEY AUTOINCREMENT,product_id INTEGER NOT NULL,title TEXT,offer_price REAL NOT NULL DEFAULT 0,start_date TEXT,end_date TEXT,active INTEGER NOT NULL DEFAULT 1,created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP);
CREATE TABLE IF NOT EXISTS settings_12115(setting_key TEXT PRIMARY KEY,setting_value TEXT,updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP);
CREATE INDEX IF NOT EXISTS idx_offers_12115_product ON offers_12115(product_id,active);
`)
}

func saveOffer12115(id,productID int64,title string,price float64,start,end string,active bool) error {
 if productID<=0{return fmt.Errorf("produto invalido")};if price<0{return fmt.Errorf("preco invalido")};if e:=ensureOfertasConfig12115Schema();e!=nil{return e};a:=0;if active{a=1}
 if id>0{return execSQL(fmt.Sprintf("UPDATE offers_12115 SET product_id=%d,title='%s',offer_price=%.4f,start_date='%s',end_date='%s',active=%d,updated_at=CURRENT_TIMESTAMP WHERE id=%d",productID,esc(strings.TrimSpace(title)),price,esc(start),esc(end),a,id))}
 return execSQL(fmt.Sprintf("INSERT INTO offers_12115(product_id,title,offer_price,start_date,end_date,active) VALUES(%d,'%s',%.4f,'%s','%s',%d)",productID,esc(strings.TrimSpace(title)),price,esc(start),esc(end),a))
}

func activeOffers12115() ([][]string,error) {
 return queryRows("SELECT o.id,p.description,printf('%.2f',p.price),printf('%.2f',o.offer_price),COALESCE(o.start_date,''),COALESCE(o.end_date,'') FROM offers_12115 o JOIN products p ON p.id=o.product_id WHERE o.active=1 AND (o.start_date IS NULL OR o.start_date='' OR date(o.start_date)<=date('now','localtime')) AND (o.end_date IS NULL OR o.end_date='' OR date(o.end_date)>=date('now','localtime')) ORDER BY p.description",6)
}

func effectiveProductPrice12115(productID int64) float64 {
 offer:=scalar(fmt.Sprintf("SELECT offer_price FROM offers_12115 WHERE product_id=%d AND active=1 AND (start_date IS NULL OR start_date='' OR date(start_date)<=date('now','localtime')) AND (end_date IS NULL OR end_date='' OR date(end_date)>=date('now','localtime')) ORDER BY id DESC LIMIT 1",productID));if offer!=""{return parseF(offer)};return parseF(scalar(fmt.Sprintf("SELECT price FROM products WHERE id=%d",productID)))
}

func setSetting12115(key,value string) error {if strings.TrimSpace(key)==""{return fmt.Errorf("chave invalida")};if e:=ensureOfertasConfig12115Schema();e!=nil{return e};return execSQL(fmt.Sprintf("INSERT INTO settings_12115(setting_key,setting_value,updated_at) VALUES('%s','%s',CURRENT_TIMESTAMP) ON CONFLICT(setting_key) DO UPDATE SET setting_value=excluded.setting_value,updated_at=CURRENT_TIMESTAMP",esc(strings.TrimSpace(key)),esc(value)))}
func getSetting12115(key string) string {_=ensureOfertasConfig12115Schema();return scalar("SELECT setting_value FROM settings_12115 WHERE setting_key='"+esc(strings.TrimSpace(key))+"'")}
