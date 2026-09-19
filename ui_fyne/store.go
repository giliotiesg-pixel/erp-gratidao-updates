package main

import (
 "database/sql"
 "fmt"
 "os"
 "path/filepath"
 "strings"
 _ "modernc.org/sqlite"
)

type Store struct{ DB *sql.DB }

func OpenStore() (*Store,error) {
 exe,err:=os.Executable(); if err!=nil{return nil,err}
 root:=filepath.Dir(exe)
 candidates:=[]string{filepath.Join(root,"data","erp.sqlite"),filepath.Join(filepath.Dir(root),"data","erp.sqlite")}
 var dbPath string
 for _,p:=range candidates { if _,e:=os.Stat(p); e==nil {dbPath=p;break} }
 if dbPath=="" { dbPath=candidates[0]; _=os.MkdirAll(filepath.Dir(dbPath),0755) }
 db,err:=sql.Open("sqlite",dbPath); if err!=nil{return nil,err}
 db.SetMaxOpenConns(4)
 db.SetMaxIdleConns(2)
 if _,err=db.Exec("PRAGMA foreign_keys=ON; PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA temp_store=MEMORY; PRAGMA cache_size=-20000; PRAGMA busy_timeout=3000;");err!=nil{db.Close();return nil,err}
 return &Store{DB:db},nil
}
func (s *Store) scalar(q string,args ...any) string { var v any; if s==nil||s.DB==nil{return "—"}; if err:=s.DB.QueryRow(q,args...).Scan(&v);err!=nil{return "—"}; return fmt.Sprint(v) }
func (s *Store) rows(q string,args ...any) [][]string {
 out:=[][]string{}; if s==nil||s.DB==nil{return out}
 r,e:=s.DB.Query(q,args...); if e!=nil{return out}; defer r.Close()
 cols,_:=r.Columns()
 for r.Next(){ vals:=make([]any,len(cols)); ptr:=make([]any,len(cols)); for i:=range vals{ptr[i]=&vals[i]}; if r.Scan(ptr...)!=nil{continue}; row:=make([]string,len(cols)); for i,v:=range vals{if v!=nil{row[i]=fmt.Sprint(v)}}; out=append(out,row)}
 return out
}
func (s *Store) SearchProducts(term string) [][]string {
 like:="%"+strings.TrimSpace(term)+"%"
 return s.rows("SELECT COALESCE(barcode,''),description,printf('%.2f',price),printf('%.3f',stock),COALESCE(unit,'UN') FROM products WHERE active=1 AND (description LIKE ? OR barcode LIKE ?) ORDER BY description LIMIT 200",like,like)
}
func (s *Store) Sales() [][]string { return s.rows("SELECT sale_number,datetime(created_at,'localtime'),customer_name,payment_method,printf('%.2f',total),status FROM sales WHERE deleted_at IS NULL ORDER BY id DESC LIMIT 500") }
func (s *Store) Stock() [][]string { return s.StockSearch("") }
func (s *Store) StockSearch(term string) [][]string { like:="%"+strings.TrimSpace(term)+"%"; return s.rows("SELECT COALESCE(barcode,''),description,printf('%.3f',stock),printf('%.3f',min_stock) FROM products WHERE active=1 AND (description LIKE ? OR COALESCE(barcode,'') LIKE ?) ORDER BY description LIMIT 300",like,like) }
func (s *Store) AdjustStock(barcode,description string,qty,min float64) error {
 if s==nil||s.DB==nil{return fmt.Errorf("banco de dados indisponível")}; if qty<0||min<0{return fmt.Errorf("quantidades não podem ser negativas")}
 tx,err:=s.DB.Begin(); if err!=nil{return err}; defer tx.Rollback()
 var res sql.Result
 if strings.TrimSpace(barcode)!="" {res,err=tx.Exec("UPDATE products SET stock=?,min_stock=?,active=CASE WHEN ?>0 THEN 1 ELSE active END WHERE barcode=?",qty,min,qty,barcode)} else {res,err=tx.Exec("UPDATE products SET stock=?,min_stock=?,active=CASE WHEN ?>0 THEN 1 ELSE active END WHERE description=?",qty,min,qty,description)}
 if err!=nil{return err}; n,err:=res.RowsAffected();if err!=nil{return err};if n!=1{return fmt.Errorf("produto não encontrado ou seleção ambígua")};return tx.Commit()
}
func (s *Store) Fiado() [][]string { return s.rows("SELECT sale_number,customer_name,printf('%.2f',total),printf('%.2f',MAX(0,total-COALESCE((SELECT SUM(amount) FROM credit_payments cp WHERE cp.sale_id=sales.id),0))) FROM sales WHERE payment_method='FIADO' AND deleted_at IS NULL ORDER BY customer_name,id DESC LIMIT 500") }
func (s *Store) Validity() [][]string { return s.rows("SELECT description,COALESCE(expiration_date,''),printf('%.3f',stock) FROM products WHERE active=1 ORDER BY CASE WHEN expiration_date IS NULL OR expiration_date='' THEN 1 ELSE 0 END,expiration_date LIMIT 1000") }
func (s *Store) Cash() [][]string { return s.rows("SELECT id,datetime(opened_at,'localtime'),printf('%.2f',opening_amount),COALESCE(datetime(closed_at,'localtime'),''),COALESCE(printf('%.2f',closing_amount),''),status FROM cash_sessions ORDER BY id DESC LIMIT 200") }
func (s *Store) Dashboard() (string,string,string,string) {
 return s.scalar("SELECT COUNT(*) FROM sales WHERE status='CONCLUIDA' AND date(created_at,'localtime')=date('now','localtime')"),
 "R$ "+s.scalar("SELECT printf('%.2f',COALESCE(SUM(total),0)) FROM sales WHERE status='CONCLUIDA' AND date(created_at,'localtime')=date('now','localtime')"),
 s.scalar("SELECT COUNT(*) FROM products WHERE active=1"),
 s.scalar("SELECT COUNT(*) FROM products WHERE active=1 AND stock<=min_stock")
}
