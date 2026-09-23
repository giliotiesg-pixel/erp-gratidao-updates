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

type productFull struct {
 ID int64
 InternalCode,Barcode,Description,Brand,Category,Unit,NCM,CEST,CFOP,CST,Origin,GTIN,TribUnit,CNAE,Expiration,PhotoURL,Status string
 Cost,Price,Stock,MinStock,PromoDiscount float64
 Promo,SoldByWeight bool
}

func (s *Store) ensureProduct211Fields() error {
 defs:=[]string{
  "internal_code TEXT","brand TEXT","category TEXT","cost REAL NOT NULL DEFAULT 0","min_stock REAL NOT NULL DEFAULT 0","expiration_date TEXT","status TEXT NOT NULL DEFAULT 'ATIVO'",
  "promotion_active INTEGER NOT NULL DEFAULT 0","promotion_discount_percent REAL NOT NULL DEFAULT 0","photo_url TEXT","ncm TEXT","cest TEXT","cfop TEXT","cst_csosn TEXT","origin TEXT",
  "tax_icms REAL NOT NULL DEFAULT 0","tax_pis REAL NOT NULL DEFAULT 0","tax_cofins REAL NOT NULL DEFAULT 0","gtin_tributable TEXT","tributary_unit TEXT","cnae TEXT","sold_by_weight INTEGER NOT NULL DEFAULT 0",
 }
 cols:=map[string]bool{};r,e:=s.DB.Query("PRAGMA table_info(products)");if e!=nil{return e};defer r.Close()
 for r.Next(){var cid int;var name,typ string;var notnull,pk int;var d any;if r.Scan(&cid,&name,&typ,&notnull,&d,&pk)==nil{cols[name]=true}}
 for _,d:=range defs{name:=strings.Fields(d)[0];if !cols[name]{if _,e=s.DB.Exec("ALTER TABLE products ADD COLUMN "+d);e!=nil{return e}}}
 return nil
}
func pf(v string)float64{x,_:=strconv.ParseFloat(strings.ReplaceAll(strings.TrimSpace(v),",","."),64);return x}
func (s *Store) product211Rows(term,filter string)[][]string{
 _=s.ensureProduct211Fields();like:="%"+strings.TrimSpace(term)+"%";where:="(description LIKE ? OR COALESCE(barcode,'') LIKE ? OR COALESCE(internal_code,'') LIKE ? OR COALESCE(brand,'') LIKE ? OR COALESCE(category,'') LIKE ?)"
 switch filter{case"ATIVOS":where+=" AND active=1";case"INATIVOS":where+=" AND active=0";case"SEM ESTOQUE":where+=" AND stock<=0";case"ESTOQUE BAIXO":where+=" AND stock<=min_stock"}
 return s.rows("SELECT COALESCE(internal_code,''),COALESCE(barcode,''),description,COALESCE(brand,''),COALESCE(category,''),printf('%.2f',cost),printf('%.2f',price),printf('%.3f',stock),printf('%.3f',min_stock),COALESCE(expiration_date,''),CASE WHEN active=1 THEN 'ATIVO' ELSE 'INATIVO' END FROM products WHERE "+where+" ORDER BY description LIMIT 500",like,like,like,like,like)
}
func (s *Store) loadProduct211(barcode,internal,description string)(productFull,error){
 _=s.ensureProduct211Fields();var p productFull;var promo,weight int
 e:=s.DB.QueryRow(`SELECT id,COALESCE(internal_code,''),COALESCE(barcode,''),description,COALESCE(brand,''),COALESCE(category,''),COALESCE(unit,'UN'),COALESCE(ncm,''),COALESCE(cest,''),COALESCE(cfop,''),COALESCE(cst_csosn,''),COALESCE(origin,''),COALESCE(gtin_tributable,''),COALESCE(tributary_unit,''),COALESCE(cnae,''),COALESCE(expiration_date,''),COALESCE(photo_url,''),CASE WHEN active=1 THEN 'ATIVO' ELSE 'INATIVO' END,COALESCE(cost,0),price,stock,COALESCE(min_stock,0),COALESCE(promotion_discount_percent,0),COALESCE(promotion_active,0),COALESCE(sold_by_weight,0) FROM products WHERE (COALESCE(barcode,'')<>'' AND barcode=?) OR (COALESCE(internal_code,'')<>'' AND internal_code=?) OR description=? ORDER BY id LIMIT 1`,barcode,internal,description).Scan(&p.ID,&p.InternalCode,&p.Barcode,&p.Description,&p.Brand,&p.Category,&p.Unit,&p.NCM,&p.CEST,&p.CFOP,&p.CST,&p.Origin,&p.GTIN,&p.TribUnit,&p.CNAE,&p.Expiration,&p.PhotoURL,&p.Status,&p.Cost,&p.Price,&p.Stock,&p.MinStock,&p.PromoDiscount,&promo,&weight)
 p.Promo=promo==1;p.SoldByWeight=weight==1;return p,e
}
func (s *Store) saveProduct211(p productFull,icms,pis,cofins float64)error{
 if strings.TrimSpace(p.Description)==""{return fmt.Errorf("informe o nome/descrição do produto")};if p.Cost<0||p.Price<0||p.Stock<0||p.MinStock<0{return fmt.Errorf("valores não podem ser negativos")}
 if e:=s.ensureProduct211Fields();e!=nil{return e};active:=1;if p.Status=="INATIVO"&&p.Stock<=0{active=0};if p.Stock>0{active=1};promo,weight:=0,0;if p.Promo{promo=1};if p.SoldByWeight{weight=1}
 tx,e:=s.DB.Begin();if e!=nil{return e};defer tx.Rollback()
 if p.ID>0{_,e=tx.Exec(`UPDATE products SET internal_code=?,barcode=?,description=?,brand=?,category=?,unit=?,cost=?,price=?,stock=?,min_stock=?,expiration_date=?,active=?,status=?,promotion_active=?,promotion_discount_percent=?,photo_url=?,ncm=?,cest=?,cfop=?,cst_csosn=?,origin=?,tax_icms=?,tax_pis=?,tax_cofins=?,gtin_tributable=?,tributary_unit=?,cnae=?,sold_by_weight=? WHERE id=?`,p.InternalCode,p.Barcode,p.Description,p.Brand,p.Category,p.Unit,p.Cost,p.Price,p.Stock,p.MinStock,p.Expiration,active,p.Status,promo,p.PromoDiscount,p.PhotoURL,p.NCM,p.CEST,p.CFOP,p.CST,p.Origin,icms,pis,cofins,p.GTIN,p.TribUnit,p.CNAE,weight,p.ID)}else{_,e=tx.Exec(`INSERT INTO products(internal_code,barcode,description,brand,category,unit,cost,price,stock,min_stock,expiration_date,active,status,promotion_active,promotion_discount_percent,photo_url,ncm,cest,cfop,cst_csosn,origin,tax_icms,tax_pis,tax_cofins,gtin_tributable,tributary_unit,cnae,sold_by_weight) VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)`,p.InternalCode,p.Barcode,p.Description,p.Brand,p.Category,p.Unit,p.Cost,p.Price,p.Stock,p.MinStock,p.Expiration,active,p.Status,promo,p.PromoDiscount,p.PhotoURL,p.NCM,p.CEST,p.CFOP,p.CST,p.Origin,icms,pis,cofins,p.GTIN,p.TribUnit,p.CNAE,weight)}
 if e!=nil{return e};return tx.Commit()
}

func buildProducts(s *Store,w fyne.Window)fyne.CanvasObject{
 _=s.ensureProduct211Fields()
 e:=func(ph string)*widget.Entry{x:=widget.NewEntry();x.SetPlaceHolder(ph);return x}
 search:=e("Digite produto, código, marca ou categoria")
 filter:=widget.NewSelect([]string{"TODOS","ATIVOS","INATIVOS","SEM ESTOQUE","ESTOQUE BAIXO"},nil);filter.SetSelected("TODOS")
 rows:=s.product211Rows("","TODOS");selectedRow:=-1
 table:=widget.NewTable(func()(int,int){return len(rows)+1,11},func()fyne.CanvasObject{return widget.NewLabel("")},func(id widget.TableCellID,o fyne.CanvasObject){l:=o.(*widget.Label);heads:=[]string{"Código","Código de barras","Produto","Marca","Categoria","Custo","Venda","Estoque","Estoque mínimo","Validade","Situação"};if id.Row==0{l.SetText(heads[id.Col]);l.TextStyle=fyne.TextStyle{Bold:true};return};l.TextStyle=fyne.TextStyle{};l.SetText(rows[id.Row-1][id.Col])})
 widths:=[]float32{80,135,280,120,120,85,85,85,100,105,90};for i,v:=range widths{table.SetColumnWidth(i,v)}
 refresh:=func(){rows=s.product211Rows(search.Text,filter.Selected);table.Refresh()};search.OnChanged=func(string){refresh()};filter.OnChanged=func(string){refresh()}

 openForm:=func(existing *productFull){
  var editID int64
  internal,barcode,name,brand,category:=e("Código interno"),e("Código de barras"),e("Nome / descrição do produto"),e("Marca"),e("Categoria")
  cost,price,stock,minStock,expiration:=e("0,00"),e("0,00"),e("0"),e("0"),e("AAAA-MM-DD")
  promoDiscount,photo,ncm,cest,cfop,cst:=e("0,00"),e("URL da foto"),e("NCM"),e("CEST"),e("CFOP"),e("CST / CSOSN")
  icms,pis,cofins,gtin,tribUnit,cnae:=e("0,00"),e("0,00"),e("0,00"),e("GTIN tributável"),e("Unidade tributável"),e("CNAE")
  unit:=widget.NewSelect([]string{"UN","KG","G","L","ML","CX","FD","PCT","LT","DZ"},nil);unit.SetSelected("UN")
  origin:=widget.NewSelect([]string{"","0 - Nacional","1 - Estrangeira, importação direta","2 - Estrangeira, adquirida no mercado interno","3 - Nacional, conteúdo de importação superior a 40%","4 - Nacional, processos produtivos básicos","5 - Nacional, conteúdo de importação até 40%","6 - Estrangeira, importação direta sem similar nacional","7 - Estrangeira, mercado interno sem similar nacional","8 - Nacional, conteúdo de importação superior a 70%"},nil)
  statusSel:=widget.NewSelect([]string{"ATIVO","INATIVO"},nil);statusSel.SetSelected("ATIVO")
  promo:=widget.NewCheck("Produto em promoção",nil);weight:=widget.NewCheck("Produto vendido por peso",nil)
  margin:=widget.NewLabel("Margem entre custo e venda: 0,00%");promoPrice:=widget.NewLabel("Valor atualizado da promoção: R$ 0,00")
  calc:=func(){c,p,d:=pf(cost.Text),pf(price.Text),pf(promoDiscount.Text);m:=0.0;if c>0{m=(p-c)/c*100};pp:=p;if promo.Checked{pp=p*(1-d/100)};margin.SetText(fmt.Sprintf("Margem entre custo e venda: %.2f%%",m));promoPrice.SetText(fmt.Sprintf("Valor atualizado da promoção: R$ %.2f",pp))}
  cost.OnChanged=func(string){calc()};price.OnChanged=func(string){calc()};promoDiscount.OnChanged=func(string){calc()};promo.OnChanged=func(bool){calc()}
  if existing!=nil{p:=*existing;editID=p.ID;internal.SetText(p.InternalCode);barcode.SetText(p.Barcode);name.SetText(p.Description);brand.SetText(p.Brand);category.SetText(p.Category);unit.SetSelected(p.Unit);cost.SetText(fmt.Sprintf("%.2f",p.Cost));price.SetText(fmt.Sprintf("%.2f",p.Price));stock.SetText(fmt.Sprintf("%.3f",p.Stock));minStock.SetText(fmt.Sprintf("%.3f",p.MinStock));expiration.SetText(p.Expiration);statusSel.SetSelected(p.Status);promo.SetChecked(p.Promo);promoDiscount.SetText(fmt.Sprintf("%.2f",p.PromoDiscount));photo.SetText(p.PhotoURL);ncm.SetText(p.NCM);cest.SetText(p.CEST);cfop.SetText(p.CFOP);cst.SetText(p.CST);origin.SetSelected(p.Origin);gtin.SetText(p.GTIN);tribUnit.SetText(p.TribUnit);cnae.SetText(p.CNAE);weight.SetChecked(p.SoldByWeight)}
  mainForm:=widget.NewForm(widget.NewFormItem("Versão do layout",widget.NewLabel("AG-PRODUTOS-1.0")),widget.NewFormItem("Código interno",internal),widget.NewFormItem("Código de barras",barcode),widget.NewFormItem("Nome / descrição do produto",name),widget.NewFormItem("Marca",brand),widget.NewFormItem("Categoria",category),widget.NewFormItem("Preço de custo (R$)",cost),widget.NewFormItem("Preço de venda normal (R$)",price),widget.NewFormItem("Quantidade em estoque",stock),widget.NewFormItem("Estoque mínimo",minStock),widget.NewFormItem("Status do produto",statusSel),widget.NewFormItem("Data de validade",expiration),widget.NewFormItem("Unidade de venda",unit))
  fiscal:=widget.NewForm(widget.NewFormItem("Foto do produto (URL)",photo),widget.NewFormItem("NCM",ncm),widget.NewFormItem("CEST",cest),widget.NewFormItem("CFOP",cfop),widget.NewFormItem("CST / CSOSN",cst),widget.NewFormItem("Origem da mercadoria",origin),widget.NewFormItem("ICMS (%)",icms),widget.NewFormItem("PIS (%)",pis),widget.NewFormItem("COFINS (%)",cofins),widget.NewFormItem("GTIN tributável",gtin),widget.NewFormItem("Unidade tributável",tribUnit),widget.NewFormItem("CNAE",cnae))
  promoBox:=container.NewVBox(promo,widget.NewForm(widget.NewFormItem("Desconto da promoção (%)",promoDiscount)),promoPrice,margin,weight)
  body:=container.NewAppTabs(container.NewTabItem("Cadastro",container.NewVScroll(container.NewVBox(mainForm,promoBox))),container.NewTabItem("Dados fiscais e complementares",container.NewVScroll(fiscal)))
  var d dialog.Dialog
  save:=widget.NewButton("Salvar",func(){p:=productFull{ID:editID,InternalCode:internal.Text,Barcode:barcode.Text,Description:name.Text,Brand:brand.Text,Category:category.Text,Unit:unit.Selected,Cost:pf(cost.Text),Price:pf(price.Text),Stock:pf(stock.Text),MinStock:pf(minStock.Text),Expiration:expiration.Text,Status:statusSel.Selected,Promo:promo.Checked,PromoDiscount:pf(promoDiscount.Text),PhotoURL:photo.Text,NCM:ncm.Text,CEST:cest.Text,CFOP:cfop.Text,CST:cst.Text,Origin:origin.Selected,GTIN:gtin.Text,TribUnit:tribUnit.Text,CNAE:cnae.Text,SoldByWeight:weight.Checked};if er:=s.saveProduct211(p,pf(icms.Text),pf(pis.Text),pf(cofins.Text));er!=nil{dialog.ShowError(er,w);return};refresh();d.Hide();dialog.ShowInformation("Produtos","Produto salvo.",w)});save.Importance=widget.HighImportance
  actions:=container.NewGridWithColumns(2,save,widget.NewButton("Cancelar",func(){d.Hide()}))
  title:="Cadastro de produto";if existing!=nil{title="Cadastro / edição de produto"}
  d=dialog.NewCustom(title,"Fechar",container.NewBorder(nil,actions,nil,nil,body),w);d.Resize(fyne.NewSize(920,720));d.Show()
 }
 newBtn:=widget.NewButton("Cadastro / Edição",func(){openForm(nil)});newBtn.Importance=widget.HighImportance
 editBtn:=widget.NewButton("Editar produto selecionado",func(){if selectedRow<0||selectedRow>=len(rows){dialog.ShowInformation("Produtos","Selecione um produto na lista.",w);return};r:=rows[selectedRow];p,er:=s.loadProduct211(r[1],r[0],r[2]);if er!=nil{dialog.ShowError(er,w);return};openForm(&p)})
 table.OnSelected=func(id widget.TableCellID){if id.Row<=0||id.Row>len(rows){selectedRow=-1;return};selectedRow=id.Row-1}
 clearSearch:=widget.NewButton("Limpar",func(){search.SetText("");filter.SetSelected("TODOS");refresh()})
 header:=container.NewVBox(widget.NewLabelWithStyle("Produtos cadastrados",fyne.TextAlignLeading,fyne.TextStyle{Bold:true}),widget.NewLabel("Consulta de produtos • cadastro e edição abrem em formulário separado, conforme Armazém Gratidão 2.1.1"),container.NewBorder(nil,nil,search,container.NewHBox(filter,clearSearch,newBtn,editBtn)))
 return container.NewBorder(header,nil,nil,nil,table)
}

