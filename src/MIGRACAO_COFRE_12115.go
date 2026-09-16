package main

import (
 "fmt"
 "strings"
)

// Estrutura nativa do Cofre Digital. Segredos permanecem armazenados apenas no banco local.
func ensureCofre12115Schema() error {
 return execSQL(`CREATE TABLE IF NOT EXISTS vault_12115(id INTEGER PRIMARY KEY AUTOINCREMENT,category TEXT NOT NULL,title TEXT NOT NULL,bank TEXT,agency TEXT,account TEXT,operation TEXT,pix TEXT,email TEXT,login TEXT,password_value TEXT,pin TEXT,site TEXT,phone TEXT,notes TEXT,favorite INTEGER NOT NULL DEFAULT 0,created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP); CREATE INDEX IF NOT EXISTS idx_vault_12115_category ON vault_12115(category,favorite,title);`)
}

func saveVaultEntry12115(id int64,category,title,bank,agency,account,operation,pix,email,login,password,pin,site,phone,notes string,favorite bool) error {
 if strings.TrimSpace(title)==""{return fmt.Errorf("informe o titulo")};if e:=ensureCofre12115Schema();e!=nil{return e};fav:=0;if favorite{fav=1}
 vals:=[]string{category,title,bank,agency,account,operation,pix,email,login,password,pin,site,phone,notes};for i:=range vals{vals[i]=esc(strings.TrimSpace(vals[i]))}
 if id>0{return execSQL(fmt.Sprintf("UPDATE vault_12115 SET category='%s',title='%s',bank='%s',agency='%s',account='%s',operation='%s',pix='%s',email='%s',login='%s',password_value='%s',pin='%s',site='%s',phone='%s',notes='%s',favorite=%d,updated_at=CURRENT_TIMESTAMP WHERE id=%d",vals[0],vals[1],vals[2],vals[3],vals[4],vals[5],vals[6],vals[7],vals[8],vals[9],vals[10],vals[11],vals[12],vals[13],fav,id))}
 return execSQL(fmt.Sprintf("INSERT INTO vault_12115(category,title,bank,agency,account,operation,pix,email,login,password_value,pin,site,phone,notes,favorite) VALUES('%s','%s','%s','%s','%s','%s','%s','%s','%s','%s','%s','%s','%s','%s',%d)",vals[0],vals[1],vals[2],vals[3],vals[4],vals[5],vals[6],vals[7],vals[8],vals[9],vals[10],vals[11],vals[12],vals[13],fav))
}

func vaultList12115(category,search string) ([][]string,error) {_=ensureCofre12115Schema();where:="1=1";if strings.TrimSpace(category)!=""{where+=" AND category='"+esc(category)+"'"};if strings.TrimSpace(search)!=""{s:=esc(search);where+=" AND (title LIKE '%"+s+"%' OR login LIKE '%"+s+"%' OR email LIKE '%"+s+"%')"};return queryRows("SELECT id,category,title,COALESCE(login,''),COALESCE(email,''),favorite,updated_at FROM vault_12115 WHERE "+where+" ORDER BY favorite DESC,title",7)}
