package main

import (
 "fmt"
 "strconv"
 "strings"

 "fyne.io/fyne/v2"
 "fyne.io/fyne/v2/container"
 "fyne.io/fyne/v2/dialog"
 "fyne.io/fyne/v2/widget"
)

type productRow struct{ Barcode, Description, Price, Stock, Unit string }

func loadProductRows(s *Store, term string) []productRow {
 raw:=s.SearchProducts(term); out:=make([]productRow,0,len(raw))
 for _,r:=range raw { if len(r)>=5 { out=append(out,productRow{r[0],r[1],r[2],r[3],r[4]}) } }
 return out
}

func (s *Store) SaveProduct(barcode,description string,price,stock float64,unit string) error {
 description=strings.TrimSpace(description); barcode=strings.TrimSpace(barcode); unit=strings.TrimSpace(unit)
 if description=="" { return fmt.Errorf("informe a descrição") }
 if price<0||stock<0 { return fmt.Errorf("preço e estoque não podem ser negativos") }
 if unit=="" { unit="UN" }
 tx,err:=s.DB.Begin(); if err!=nil{return err}; defer tx.Rollback()
 if barcode!="" {
  var id int64
  e:=tx.QueryRow("SELECT id FROM products WHERE barcode=? LIMIT 1",barcode).Scan(&id)
  if e==nil { _,err=tx.Exec("UPDATE products SET description=?,price=?,stock=?,unit=?,active=1 WHERE id=?",description,price,stock,unit,id) } else if e==sql.ErrNoRows { _,err=tx.Exec("INSERT INTO products(barcode,description,price,stock,unit,active) VALUES(?,?,?,?,?,1)",barcode,description,price,stock,unit) } else { return e }
 } else { _,err=tx.Exec("INSERT INTO products(description,price,stock,unit,active) VALUES(?,?,?,?,1)",description,price,stock,unit) }
 if err!=nil{return err}; return tx.Commit()
}

func buildProducts(s *Store,w fyne.Window) fyne.CanvasObject {
 search:=widget.NewEntry(); search.SetPlaceHolder("Pesquisar por descrição ou código de barras")
 barcode:=widget.NewEntry(); barcode.SetPlaceHolder("Código de barras")
 description:=widget.NewEntry(); description.SetPlaceHolder("Descrição do produto")
 price:=widget.NewEntry(); price.SetPlaceHolder("0,00")
 stock:=widget.NewEntry(); stock.SetPlaceHolder("0")
 unit:=widget.NewSelect([]string{"UN","KG","G","L","ML","CX","PCT"},nil); unit.SetSelected("UN")
 status:=widget.NewLabel("Selecione um produto para editar ou cadastre um novo.")
 rows:=loadProductRows(s,"")
 selected:=-1
 table:=widget.NewTable(func()(int,int){return len(rows)+1,5},func()fyne.CanvasObject{return widget.NewLabel("")},func(id widget.TableCellID,o fyne.CanvasObject){
  l:=o.(*widget.Label); if id.Row==0 {h:=[]string{"Código","Descrição","Preço","Estoque","Un."};l.SetText(h[id.Col]);l.TextStyle=fyne.TextStyle{Bold:true};return}; l.TextStyle=fyne.TextStyle{};r:=rows[id.Row-1];v:=[]string{r.Barcode,r.Description,"R$ "+r.Price,r.Stock,r.Unit};l.SetText(v[id.Col])
 })
 table.SetColumnWidth(0,150);table.SetColumnWidth(1,380);table.SetColumnWidth(2,110);table.SetColumnWidth(3,110);table.SetColumnWidth(4,70)
 refresh:=func(){rows=loadProductRows(s,search.Text);selected=-1;table.Refresh()}
 table.OnSelected=func(id widget.TableCellID){if id.Row==0{return};selected=id.Row-1;r:=rows[selected];barcode.SetText(r.Barcode);description.SetText(r.Description);price.SetText(r.Price);stock.SetText(r.Stock);unit.SetSelected(r.Unit);status.SetText("Produto selecionado: "+r.Description)}
 search.OnChanged=func(string){refresh()}
 clear:=func(){selected=-1;barcode.SetText("");description.SetText("");price.SetText("");stock.SetText("");unit.SetSelected("UN");table.UnselectAll();status.SetText("Novo cadastro.")}
 save:=widget.NewButton("Salvar produto",func(){
  p,e:=strconv.ParseFloat(strings.ReplaceAll(strings.TrimSpace(price.Text),",","."),64);if e!=nil{dialog.ShowError(fmt.Errorf("preço inválido"),w);return}
  q,e:=strconv.ParseFloat(strings.ReplaceAll(strings.TrimSpace(stock.Text),",","."),64);if e!=nil{dialog.ShowError(fmt.Errorf("estoque inválido"),w);return}
  if e=s.SaveProduct(barcode.Text,description.Text,p,q,unit.Selected);e!=nil{dialog.ShowError(e,w);return};dialog.ShowInformation("Produtos","Produto salvo com sucesso.",w);clear();refresh()
 });save.Importance=widget.HighImportance
 form:=widget.NewCard("Cadastro / edição","Produto com estoque salvo volta a ficar ativo",container.NewVBox(widget.NewForm(widget.NewFormItem("Código",barcode),widget.NewFormItem("Descrição",description),widget.NewFormItem("Preço",price),widget.NewFormItem("Estoque",stock),widget.NewFormItem("Unidade",unit)),container.NewGridWithColumns(2,save,widget.NewButton("Limpar",clear)),status))
 return container.NewBorder(container.NewVBox(widget.NewLabelWithStyle("Produtos",fyne.TextAlignLeading,fyne.TextStyle{Bold:true}),search),nil,nil,form,table)
}
