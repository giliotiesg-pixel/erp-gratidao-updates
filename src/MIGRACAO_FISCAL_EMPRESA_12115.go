package main

import (
 "fmt"
 "strings"
)

func ensureFiscalEmpresa12115Schema() error {
 if e:=execSQL(`CREATE TABLE IF NOT EXISTS company_12115(id INTEGER PRIMARY KEY CHECK(id=1),legal_name TEXT,trade_name TEXT,cnpj TEXT,ie TEXT,address TEXT,city TEXT,state TEXT,zip TEXT,phone TEXT,email TEXT,updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP); INSERT OR IGNORE INTO company_12115(id,legal_name,trade_name) VALUES(1,'','Armazem Gratidao');`);e!=nil{return e}
 // Migração aditiva: não apaga nem recria products. Cada coluna é adicionada somente se ainda não existir.
 cols:=[][]string{{"ncm","TEXT"},{"cest","TEXT"},{"cfop","TEXT"},{"cst_csosn","TEXT"},{"origin","TEXT"},{"gtin_tributable","TEXT"},{"tributary_unit","TEXT"},{"tax_icms","REAL NOT NULL DEFAULT 0"},{"tax_pis","REAL NOT NULL DEFAULT 0"},{"tax_cofins","REAL NOT NULL DEFAULT 0"}}
 info,e:=queryRows("PRAGMA table_info(products)",6);if e!=nil{return e};have:=map[string]bool{};for _,r:=range info{if len(r)>1{have[strings.ToLower(r[1])]=true}}
 for _,c:=range cols{if !have[c[0]]{if e=execSQL("ALTER TABLE products ADD COLUMN "+c[0]+" "+c[1]);e!=nil{return e}}};return nil
}

func saveCompany12115(legal,trade,cnpj,ie,address,city,state,zip,phone,email string) error {
 if e:=ensureFiscalEmpresa12115Schema();e!=nil{return e};return execSQL(fmt.Sprintf("UPDATE company_12115 SET legal_name='%s',trade_name='%s',cnpj='%s',ie='%s',address='%s',city='%s',state='%s',zip='%s',phone='%s',email='%s',updated_at=CURRENT_TIMESTAMP WHERE id=1",esc(strings.TrimSpace(legal)),esc(strings.TrimSpace(trade)),esc(strings.TrimSpace(cnpj)),esc(strings.TrimSpace(ie)),esc(strings.TrimSpace(address)),esc(strings.TrimSpace(city)),esc(strings.TrimSpace(state)),esc(strings.TrimSpace(zip)),esc(strings.TrimSpace(phone)),esc(strings.TrimSpace(email))))
}
func fiscalProducts12115(search string)([][]string,error){if e:=ensureFiscalEmpresa12115Schema();e!=nil{return nil,e};where:="1=1";if strings.TrimSpace(search)!=""{s:=esc(strings.TrimSpace(search));where="(description LIKE '%"+s+"%' OR barcode LIKE '%"+s+"%')"};return queryRows("SELECT id,COALESCE(barcode,''),description,COALESCE(ncm,''),COALESCE(cest,''),COALESCE(cfop,''),COALESCE(cst_csosn,''),COALESCE(origin,''),printf('%.2f',COALESCE(tax_icms,0)),printf('%.2f',COALESCE(tax_pis,0)),printf('%.2f',COALESCE(tax_cofins,0)) FROM products WHERE "+where+" ORDER BY description",11)}
func saveProductFiscal12115(productID int64,ncm,cest,cfop,cst,origin,gtin,unit string,icms,pis,cofins float64) error {if productID<=0{return fmt.Errorf("produto invalido")};if e:=ensureFiscalEmpresa12115Schema();e!=nil{return e};return execSQL(fmt.Sprintf("UPDATE products SET ncm='%s',cest='%s',cfop='%s',cst_csosn='%s',origin='%s',gtin_tributable='%s',tributary_unit='%s',tax_icms=%.4f,tax_pis=%.4f,tax_cofins=%.4f,updated_at=CURRENT_TIMESTAMP WHERE id=%d",esc(ncm),esc(cest),esc(cfop),esc(cst),esc(origin),esc(gtin),esc(unit),icms,pis,cofins,productID))}
