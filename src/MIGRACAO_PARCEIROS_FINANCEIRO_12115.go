package main

import (
 "fmt"
 "strings"
)

func ensureParceirosFinanceiro12115() error {
 return execSQL(`
CREATE TABLE IF NOT EXISTS partners_12115(id INTEGER PRIMARY KEY AUTOINCREMENT,type TEXT NOT NULL,name TEXT NOT NULL,document TEXT,phone TEXT,email TEXT,address TEXT,active INTEGER NOT NULL DEFAULT 1,created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP);
CREATE TABLE IF NOT EXISTS payable_accounts_12115(id INTEGER PRIMARY KEY AUTOINCREMENT,type TEXT NOT NULL,partner_name TEXT NOT NULL,description TEXT,value REAL NOT NULL DEFAULT 0,paid_value REAL NOT NULL DEFAULT 0,due_date TEXT,status TEXT NOT NULL DEFAULT 'ABERTA',created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP);
CREATE TABLE IF NOT EXISTS payable_payments_12115(id INTEGER PRIMARY KEY AUTOINCREMENT,account_id INTEGER NOT NULL,value REAL NOT NULL,source TEXT NOT NULL DEFAULT 'CAIXA',reference TEXT,created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP);
CREATE INDEX IF NOT EXISTS idx_partners_12115_name ON partners_12115(name); CREATE INDEX IF NOT EXISTS idx_payable_12115_status ON payable_accounts_12115(status,due_date);`)
}

func savePartner12115(id int64, kind,name,document,phone,email,address string)(int64,error){
 if e:=ensureParceirosFinanceiro12115();e!=nil{return 0,e};name=strings.TrimSpace(name);kind=strings.ToUpper(strings.TrimSpace(kind));if name==""{return 0,fmt.Errorf("informe o nome")};if kind==""{kind="CLIENTE"}
 if id>0{e:=execSQL(fmt.Sprintf("UPDATE partners_12115 SET type='%s',name='%s',document='%s',phone='%s',email='%s',address='%s',updated_at=CURRENT_TIMESTAMP WHERE id=%d",esc(kind),esc(name),esc(document),esc(phone),esc(email),esc(address),id));return id,e}
 e:=execSQL(fmt.Sprintf("INSERT INTO partners_12115(type,name,document,phone,email,address) VALUES('%s','%s','%s','%s','%s','%s')",esc(kind),esc(name),esc(document),esc(phone),esc(email),esc(address)));if e!=nil{return 0,e};return int64(parseF(scalar("SELECT last_insert_rowid()"))),nil
}
func listPartners12115(search string)([][]string,error){_=ensureParceirosFinanceiro12115();w:="active=1";if strings.TrimSpace(search)!=""{w+=" AND (name LIKE '%"+esc(search)+"%' OR document LIKE '%"+esc(search)+"%')"};return queryRows("SELECT id,type,name,COALESCE(document,''),COALESCE(phone,''),COALESCE(email,'') FROM partners_12115 WHERE "+w+" ORDER BY name",6)}
func deletePartner12115(id int64)error{if id<=0{return fmt.Errorf("cadastro invalido")};_=ensureParceirosFinanceiro12115();return execSQL(fmt.Sprintf("UPDATE partners_12115 SET active=0,updated_at=CURRENT_TIMESTAMP WHERE id=%d",id))}

func savePayable12115(kind,partner,description string,value float64,due string)(int64,error){
 if e:=ensureParceirosFinanceiro12115();e!=nil{return 0,e};partner=strings.TrimSpace(partner);if partner==""||value<=0{return 0,fmt.Errorf("informe fornecedor/cliente e valor")};if strings.TrimSpace(kind)==""{kind="FORNECEDOR"}
 e:=execSQL(fmt.Sprintf("INSERT INTO payable_accounts_12115(type,partner_name,description,value,due_date) VALUES('%s','%s','%s',%.4f,'%s')",esc(kind),esc(partner),esc(description),value,esc(due)));if e!=nil{return 0,e};return int64(parseF(scalar("SELECT last_insert_rowid()"))),nil
}
func payPayable12115(id int64,value float64,source,reference string)error{
 if id<=0||value<=0{return fmt.Errorf("pagamento invalido")};_=ensureParceirosFinanceiro12115();rows,e:=queryRows(fmt.Sprintf("SELECT value,paid_value,status FROM payable_accounts_12115 WHERE id=%d",id),3);if e!=nil||len(rows)==0{return fmt.Errorf("conta nao encontrada")};balance:=parseF(rows[0][0])-parseF(rows[0][1]);if value>balance+0.0001{return fmt.Errorf("valor maior que o saldo")};if source==""{source="CAIXA"};if e=execSQL("BEGIN IMMEDIATE");e!=nil{return e};ok:=false;defer func(){if !ok{_=execSQL("ROLLBACK")}}();if e=execSQL(fmt.Sprintf("INSERT INTO payable_payments_12115(account_id,value,source,reference) VALUES(%d,%.4f,'%s','%s')",id,value,esc(source),esc(reference)));e!=nil{return e};newPaid:=parseF(rows[0][1])+value;status:="PARCIAL";if newPaid>=parseF(rows[0][0])-0.0001{status="PAGA"};if e=execSQL(fmt.Sprintf("UPDATE payable_accounts_12115 SET paid_value=%.4f,status='%s',updated_at=CURRENT_TIMESTAMP WHERE id=%d",newPaid,status,id));e!=nil{return e};if e=execSQL("COMMIT");e!=nil{return e};ok=true;return nil
}
func payableStatement12115()([][]string,error){_=ensureParceirosFinanceiro12115();return queryRows("SELECT id,type,partner_name,COALESCE(description,''),printf('%.2f',value),printf('%.2f',paid_value),printf('%.2f',value-paid_value),COALESCE(due_date,''),status FROM payable_accounts_12115 ORDER BY id DESC",9)}
