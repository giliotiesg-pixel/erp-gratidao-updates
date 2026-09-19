package main

import (
 "fmt"
 "strings"
 "time"

 "fyne.io/fyne/v2"
 "fyne.io/fyne/v2/container"
 "fyne.io/fyne/v2/dialog"
 "fyne.io/fyne/v2/widget"
)

func (s *Store) ValiditySearch(term string) [][]string {
 like := "%" + strings.TrimSpace(term) + "%"
 return s.rows("SELECT COALESCE(barcode,''),description,COALESCE(expiration_date,''),printf('%.3f',stock) FROM products WHERE active=1 AND (description LIKE ? OR COALESCE(barcode,'') LIKE ?) ORDER BY CASE WHEN expiration_date IS NULL OR expiration_date='' THEN 1 ELSE 0 END, expiration_date, description LIMIT 1000", like, like)
}

func (s *Store) UpdateValidity(barcode, description, value string) error {
 if s == nil || s.DB == nil { return fmt.Errorf("banco de dados indisponível") }
 value = strings.TrimSpace(value)
 if value != "" {
  if _, err := time.Parse("2006-01-02", value); err != nil { return fmt.Errorf("data inválida; use DD/MM/AAAA") }
 }
 tx, err := s.DB.Begin(); if err != nil { return err }; defer tx.Rollback()
 var res interface{ RowsAffected() (int64,error) }
 if strings.TrimSpace(barcode) != "" {
  res, err = tx.Exec("UPDATE products SET expiration_date=NULLIF(?,'') WHERE barcode=?", value, barcode)
 } else {
  res, err = tx.Exec("UPDATE products SET expiration_date=NULLIF(?,'') WHERE description=?", value, description)
 }
 if err != nil { return err }
 n, err := res.RowsAffected(); if err != nil { return err }; if n != 1 { return fmt.Errorf("produto não encontrado ou seleção ambígua") }
 return tx.Commit()
}

func validityDisplay(v string) string {
 if strings.TrimSpace(v)=="" { return "Sem validade" }
 t,err:=time.Parse("2006-01-02",v); if err!=nil{return v}; return t.Format("02/01/2006")
}
func validityDB(v string)(string,error){
 v=strings.TrimSpace(v); if v=="" {return "",nil}
 if t,e:=time.Parse("02/01/2006",v);e==nil{return t.Format("2006-01-02"),nil}
 if t,e:=time.Parse("2006-01-02",v);e==nil{return t.Format("2006-01-02"),nil}
 return "",fmt.Errorf("data inválida; use DD/MM/AAAA")
}

func buildValidity(store *Store, win fyne.Window) fyne.CanvasObject {
 rows:=store.ValiditySearch("")
 search:=widget.NewEntry(); search.SetPlaceHolder("Pesquisar produto ou código de barras...")
 selected:=widget.NewLabel("Selecione um produto para alterar a validade")
 date:=widget.NewEntry(); date.SetPlaceHolder("DD/MM/AAAA")
 barcode,description:="",""
 table:=widget.NewTable(func()(int,int){return len(rows)+1,4},func()fyne.CanvasObject{return widget.NewLabel("")},func(id widget.TableCellID,o fyne.CanvasObject){
  l:=o.(*widget.Label); if id.Row==0 { h:=[]string{"Código","Produto","Validade","Estoque"};l.SetText(h[id.Col]);l.TextStyle=fyne.TextStyle{Bold:true};return }; l.TextStyle=fyne.TextStyle{}; r:=rows[id.Row-1]; if id.Col==2 {l.SetText(validityDisplay(r[id.Col]))}else{l.SetText(r[id.Col])}
 })
 table.SetColumnWidth(0,150);table.SetColumnWidth(1,420);table.SetColumnWidth(2,150);table.SetColumnWidth(3,110)
 table.OnSelected=func(id widget.TableCellID){if id.Row==0{return};r:=rows[id.Row-1];barcode,description=r[0],r[1];selected.SetText(description+"  •  estoque: "+r[3]);date.SetText(validityDisplay(r[2]));if r[2]==""{date.SetText("")}}
 refresh:=func(){rows=store.ValiditySearch(search.Text);table.Refresh()}
 search.OnChanged=func(string){refresh()}
 save:=widget.NewButton("Salvar validade",func(){if description==""{dialog.ShowInformation("Validade","Selecione um produto.",win);return};v,err:=validityDB(date.Text);if err!=nil{dialog.ShowError(err,win);return};if err=store.UpdateValidity(barcode,description,v);err!=nil{dialog.ShowError(err,win);return};refresh();dialog.ShowInformation("Validade","Validade atualizada com sucesso.",win)})
 clear:=widget.NewButton("Remover validade",func(){if description==""{dialog.ShowInformation("Validade","Selecione um produto.",win);return};dialog.ShowConfirm("Remover validade","Remover a validade cadastrada de "+description+"?",func(ok bool){if !ok{return};if err:=store.UpdateValidity(barcode,description,"");err!=nil{dialog.ShowError(err,win);return};date.SetText("");refresh()},win)})
 form:=widget.NewCard("Validade do produto","Use somente a data de validade; não há controle de lote.",container.NewVBox(selected,container.NewGridWithColumns(2,widget.NewLabel("Data de validade"),date),container.NewHBox(save,clear)))
 return container.NewBorder(container.NewVBox(widget.NewLabelWithStyle("Controle de Validade",fyne.TextAlignLeading,fyne.TextStyle{Bold:true}),widget.NewLabel("Produtos ativos ordenados pela validade mais próxima."),search,form),nil,nil,nil,table)
}
