package main

import (
 "database/sql"
 "fmt"
 "strconv"
 "strings"
 "time"
 "fyne.io/fyne/v2"
 "fyne.io/fyne/v2/container"
 "fyne.io/fyne/v2/dialog"
 "fyne.io/fyne/v2/widget"
)

type purchaseItem struct{ Barcode, Description string; Qty, UnitCost float64 }

func ensurePurchaseSchema(db *sql.DB) error {
 _,err:=db.Exec(`CREATE TABLE IF NOT EXISTS purchases_fyne (id INTEGER PRIMARY KEY AUTOINCREMENT, supplier TEXT NOT NULL DEFAULT '', document TEXT NOT NULL DEFAULT '', purchased_at TEXT NOT NULL, total REAL NOT NULL DEFAULT 0, created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP); CREATE TABLE IF NOT EXISTS purchase_items_fyne (id INTEGER PRIMARY KEY AUTOINCREMENT, purchase_id INTEGER NOT NULL, barcode TEXT NOT NULL DEFAULT '', description TEXT NOT NULL, quantity REAL NOT NULL, unit_cost REAL NOT NULL, subtotal REAL NOT NULL, FOREIGN KEY(purchase_id) REFERENCES purchases_fyne(id) ON DELETE CASCADE);`)
 return err
}

func savePurchase(s *Store,supplier,document string,items []purchaseItem) error {
 if s==nil||s.DB==nil{return fmt.Errorf("banco de dados indisponível")}; if len(items)==0{return fmt.Errorf("adicione pelo menos um item")}; if err:=ensurePurchaseSchema(s.DB);err!=nil{return err}
 tx,err:=s.DB.Begin();if err!=nil{return err};defer tx.Rollback();total:=0.0;for _,i:=range items{total+=i.Qty*i.UnitCost}
 r,err:=tx.Exec("INSERT INTO purchases_fyne(supplier,document,purchased_at,total) VALUES(?,?,?,?)",strings.TrimSpace(supplier),strings.TrimSpace(document),time.Now().Format("2006-01-02 15:04:05"),total);if err!=nil{return err};pid,err:=r.LastInsertId();if err!=nil{return err}
 for _,i:=range items{if i.Qty<=0||i.UnitCost<0{return fmt.Errorf("quantidade e custo inválidos")};_,err=tx.Exec("INSERT INTO purchase_items_fyne(purchase_id,barcode,description,quantity,unit_cost,subtotal) VALUES(?,?,?,?,?,?)",pid,i.Barcode,i.Description,i.Qty,i.UnitCost,i.Qty*i.UnitCost);if err!=nil{return err};if strings.TrimSpace(i.Barcode)!=""{_,err=tx.Exec("UPDATE products SET stock=stock+?,active=1 WHERE barcode=?",i.Qty,i.Barcode)}else{_,err=tx.Exec("UPDATE products SET stock=stock+?,active=1 WHERE description=?",i.Qty,i.Description)};if err!=nil{return err}}
 return tx.Commit()
}

func buildPurchases(s *Store,w fyne.Window) fyne.CanvasObject {
 supplier:=widget.NewEntry();supplier.SetPlaceHolder("Fornecedor");document:=widget.NewEntry();document.SetPlaceHolder("Nota / documento")
 barcode:=widget.NewEntry();barcode.SetPlaceHolder("Código de barras");desc:=widget.NewEntry();desc.SetPlaceHolder("Descrição");qty:=widget.NewEntry();qty.SetPlaceHolder("Quantidade");cost:=widget.NewEntry();cost.SetPlaceHolder("Custo unitário")
 items:=[]purchaseItem{};total:=widget.NewLabelWithStyle("Total: R$ 0,00",fyne.TextAlignTrailing,fyne.TextStyle{Bold:true})
 list:=widget.NewList(func()int{return len(items)},func()fyne.CanvasObject{return widget.NewLabel("")},func(id widget.ListItemID,o fyne.CanvasObject){i:=items[id];o.(*widget.Label).SetText(fmt.Sprintf("%s  •  %.3f x R$ %.2f  •  R$ %.2f",i.Description,i.Qty,i.UnitCost,i.Qty*i.UnitCost))})
 refresh:=func(){sum:=0.0;for _,i:=range items{sum+=i.Qty*i.UnitCost};total.SetText(fmt.Sprintf("Total: R$ %.2f",sum));list.Refresh()}
 add:=widget.NewButton("Adicionar item",func(){q,e1:=strconv.ParseFloat(strings.ReplaceAll(strings.TrimSpace(qty.Text),",","."),64);c,e2:=strconv.ParseFloat(strings.ReplaceAll(strings.TrimSpace(cost.Text),",","."),64);if strings.TrimSpace(desc.Text)==""||e1!=nil||e2!=nil||q<=0||c<0{dialog.ShowError(fmt.Errorf("informe descrição, quantidade e custo válidos"),w);return};items=append(items,purchaseItem{strings.TrimSpace(barcode.Text),strings.TrimSpace(desc.Text),q,c});barcode.SetText("");desc.SetText("");qty.SetText("");cost.SetText("");refresh()})
 remove:=widget.NewButton("Remover último",func(){if len(items)>0{items=items[:len(items)-1];refresh()}})
 save:=widget.NewButton("Salvar compra e entrar estoque",func(){if err:=savePurchase(s,supplier.Text,document.Text,items);err!=nil{dialog.ShowError(err,w);return};dialog.ShowInformation("Compra","Compra salva e estoque atualizado.",w);supplier.SetText("");document.SetText("");items=nil;refresh()});save.Importance=widget.HighImportance
 form:=widget.NewCard("Dados da compra","Entrada de produtos",container.NewVBox(container.NewGridWithColumns(2,supplier,document),container.NewGridWithColumns(4,barcode,desc,qty,cost),container.NewHBox(add,remove)))
 return container.NewBorder(container.NewVBox(widget.NewLabelWithStyle("Compras",fyne.TextAlignLeading,fyne.TextStyle{Bold:true}),widget.NewLabel("Registre os itens e valores da compra; ao salvar, as quantidades entram no estoque."),form),container.NewVBox(widget.NewSeparator(),total,save),nil,nil,list)
}
