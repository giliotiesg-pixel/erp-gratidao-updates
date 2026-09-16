package main

import (
 "fmt"
 "strings"
)

// Produtos 12.1.15: lista unica; produto com estoque positivo permanece ativo para venda.
func saveProduct12115(id int64,barcode,internalCode,description,brand,category,unit string,cost,price,stock,minStock float64,expiration string) error {
 description=strings.TrimSpace(description);if description==""{return fmt.Errorf("informe a descricao")};if unit==""{unit="UN"};if cost<0||price<0||stock<0||minStock<0{return fmt.Errorf("valores do produto invalidos")};_=ensureEstoque12115Schema();active:=0;if stock>0{active=1}
 if id>0{return execSQL(fmt.Sprintf("UPDATE products SET barcode='%s',internal_code='%s',description='%s',brand='%s',category='%s',unit='%s',cost=%.4f,price=%.4f,stock=%.4f,min_stock=%.4f,active=%d,expiration_date='%s',updated_at=CURRENT_TIMESTAMP WHERE id=%d",esc(strings.TrimSpace(barcode)),esc(strings.TrimSpace(internalCode)),esc(description),esc(brand),esc(category),esc(unit),cost,price,stock,minStock,active,esc(expiration),id))}
 return execSQL(fmt.Sprintf("INSERT INTO products(barcode,internal_code,description,brand,category,unit,cost,price,stock,min_stock,active,expiration_date) VALUES('%s','%s','%s','%s','%s','%s',%.4f,%.4f,%.4f,%.4f,%d,'%s')",esc(strings.TrimSpace(barcode)),esc(strings.TrimSpace(internalCode)),esc(description),esc(brand),esc(category),esc(unit),cost,price,stock,minStock,active,esc(expiration)))
}

func productsUnified12115(search string) ([][]string,error) {where:="1=1";if strings.TrimSpace(search)!=""{s:=esc(strings.TrimSpace(search));where="(description LIKE '%"+s+"%' OR barcode LIKE '%"+s+"%' OR internal_code LIKE '%"+s+"%')"};return queryRows("SELECT id,COALESCE(barcode,''),COALESCE(internal_code,''),description,COALESCE(brand,''),COALESCE(category,''),unit,printf('%.2f',cost),printf('%.2f',price),printf('%.3f',stock),printf('%.3f',min_stock),active,COALESCE(expiration_date,'') FROM products WHERE "+where+" ORDER BY description",13)}

func reactivateProductsWithStock12115() error {return execSQL("UPDATE products SET active=1,updated_at=CURRENT_TIMESTAMP WHERE stock>0 AND active=0")}
func deactivateProduct12115(id int64) error {if id<=0{return fmt.Errorf("produto invalido")};return execSQL(fmt.Sprintf("UPDATE products SET active=0,updated_at=CURRENT_TIMESTAMP WHERE id=%d",id))}
