package main

import (
 "fmt"
 "strings"

 "fyne.io/fyne/v2"
 "fyne.io/fyne/v2/container"
 "fyne.io/fyne/v2/dialog"
 "fyne.io/fyne/v2/widget"
)

type partyRow struct { ID int64; Kind, Name, Document, Phone, Email string }

func (s *Store) ensurePartiesTable() error {
 _,err:=s.DB.Exec(`CREATE TABLE IF NOT EXISTS parties (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  kind TEXT NOT NULL CHECK(kind IN ('CLIENTE','FORNECEDOR')),
  name TEXT NOT NULL,
  document TEXT NOT NULL DEFAULT '',
  phone TEXT NOT NULL DEFAULT '',
  email TEXT NOT NULL DEFAULT '',
  active INTEGER NOT NULL DEFAULT 1,
  created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
 ); CREATE INDEX IF NOT EXISTS idx_parties_name ON parties(name);`)
 return err
}

func (s *Store) searchParties(term string) ([]partyRow,error) {
 if err:=s.ensurePartiesTable();err!=nil{return nil,err}
 like:="%"+strings.TrimSpace(term)+"%"
 r,err:=s.DB.Query(`SELECT id,kind,name,document,phone,email FROM parties WHERE active=1 AND (name LIKE ? OR document LIKE ? OR phone LIKE ?) ORDER BY name LIMIT 1000`,like,like,like)
 if err!=nil{return nil,err}; defer r.Close()
 out:=[]partyRow{}; for r.Next(){var p partyRow;if err:=r.Scan(&p.ID,&p.Kind,&p.Name,&p.Document,&p.Phone,&p.Email);err!=nil{return nil,err};out=append(out,p)};return out,r.Err()
}

func (s *Store) saveParty(id int64,kind,name,document,phone,email string) error {
 if s==nil||s.DB==nil{return fmt.Errorf("banco de dados indisponível")}
 if err:=s.ensurePartiesTable();err!=nil{return err}
 kind=strings.ToUpper(strings.TrimSpace(kind)); name=strings.TrimSpace(name)
 if kind!="CLIENTE"&&kind!="FORNECEDOR"{return fmt.Errorf("tipo inválido")};if name==""{return fmt.Errorf("informe o nome")}
 tx,err:=s.DB.Begin();if err!=nil{return err};defer tx.Rollback()
 if id==0 {_,err=tx.Exec(`INSERT INTO parties(kind,name,document,phone,email) VALUES(?,?,?,?,?)`,kind,name,strings.TrimSpace(document),strings.TrimSpace(phone),strings.TrimSpace(email))} else {res,e:=tx.Exec(`UPDATE parties SET kind=?,name=?,document=?,phone=?,email=?,updated_at=CURRENT_TIMESTAMP WHERE id=? AND active=1`,kind,name,strings.TrimSpace(document),strings.TrimSpace(phone),strings.TrimSpace(email),id);err=e;if err==nil{n,_:=res.RowsAffected();if n!=1{err=fmt.Errorf("cadastro não encontrado")}}}
 if err!=nil{return err};return tx.Commit()
}

func buildParties(s *Store,w fyne.Window) fyne.CanvasObject {
 var rows []partyRow; var selected int64
 search:=widget.NewEntry();search.SetPlaceHolder("Pesquisar nome, documento ou telefone...")
 kind:=widget.NewSelect([]string{"CLIENTE","FORNECEDOR"},nil);kind.SetSelected("CLIENTE")
 name:=widget.NewEntry();document:=widget.NewEntry();phone:=widget.NewEntry();email:=widget.NewEntry()
 name.SetPlaceHolder("Nome / razão social");document.SetPlaceHolder("CPF / CNPJ");phone.SetPlaceHolder("Telefone");email.SetPlaceHolder("E-mail")
 table:=widget.NewTable(func()(int,int){return len(rows),5},func()fyne.CanvasObject{return widget.NewLabel("")},func(id widget.TableCellID,o fyne.CanvasObject){p:=rows[id.Row];v:=[]string{p.Kind,p.Name,p.Document,p.Phone,p.Email};o.(*widget.Label).SetText(v[id.Col])})
 table.SetColumnWidth(0,110);table.SetColumnWidth(1,300);table.SetColumnWidth(2,160);table.SetColumnWidth(3,150);table.SetColumnWidth(4,260)
 clear:=func(){selected=0;kind.SetSelected("CLIENTE");name.SetText("");document.SetText("");phone.SetText("");email.SetText("");table.UnselectAll()}
 load:=func(){r,err:=s.searchParties(search.Text);if err!=nil{dialog.ShowError(err,w);return};rows=r;table.Refresh()}
 table.OnSelected=func(id widget.TableCellID){if id.Row<0||id.Row>=len(rows){return};p:=rows[id.Row];selected=p.ID;kind.SetSelected(p.Kind);name.SetText(p.Name);document.SetText(p.Document);phone.SetText(p.Phone);email.SetText(p.Email)}
 search.OnChanged=func(string){load()}
 save:=widget.NewButton("Salvar cadastro",func(){if err:=s.saveParty(selected,kind.Selected,name.Text,document.Text,phone.Text,email.Text);err!=nil{dialog.ShowError(err,w);return};dialog.ShowInformation("Cadastro","Cadastro salvo com sucesso.",w);clear();load()})
 clearBtn:=widget.NewButton("Limpar",clear)
 form:=widget.NewCard("Cadastro","Clientes e fornecedores",container.NewVBox(container.NewGridWithColumns(2,kind,name),container.NewGridWithColumns(2,document,phone),email,container.NewHBox(save,clearBtn)))
 load()
 return container.NewBorder(container.NewVBox(widget.NewLabelWithStyle("Clientes / Fornecedores",fyne.TextAlignLeading,fyne.TextStyle{Bold:true}),widget.NewLabel("Cadastro isolado da interface experimental; não altera cadastros do ERP oficial."),search,form,widget.NewSeparator()),nil,nil,nil,table)
}
