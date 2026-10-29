using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Windows.Forms;
namespace ArmazemGratidao {
 public sealed class ProdutosForm:Form {
  readonly string db;
  readonly DataGridView grid=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells};
  readonly TextBox search=new TextBox {Width=240};
  bool showInactive=false;
  readonly ComboBox statusFilter=new ComboBox {Width=155,DropDownStyle=ComboBoxStyle.DropDownList};
  readonly Label info=new Label {Dock=DockStyle.Bottom,Height=32};
  readonly Label metrics=new Label {Dock=DockStyle.Top,Height=45,TextAlign=ContentAlignment.MiddleLeft};
  public ProdutosForm(string database) {
   db=database;Text="Armazém Gratidão | Produtos";Width=1100;Height=720;
   var bar=new FlowLayoutPanel {Dock=DockStyle.Top,Height=105,Padding=new Padding(8),BackColor=Color.FromArgb(20,73,158)};
   bar.Controls.Add(search);
   statusFilter.Items.AddRange(new object[]{"Todos","Ativos","Inativos","Sem estoque","Estoque baixo"});statusFilter.SelectedIndex=0;statusFilter.SelectedIndexChanged+=(s,e)=>LoadProducts();bar.Controls.Add(statusFilter);
   var find=new Button {Text="Pesquisar",Width=100,Height=33};find.Click+=(s,e)=>LoadProducts();bar.Controls.Add(find);
   var add=new Button {Text="Inserir produto",Width=135,Height=33};add.Click+=(s,e)=>InsertProduct();bar.Controls.Add(add);
   var edit=new Button {Text="Editar selecionado",Width=155,Height=33};edit.Click+=(s,e)=>EditProduct();bar.Controls.Add(edit);
   var clear=new Button {Text="Limpar busca",Width=110,Height=33};clear.Click+=(s,e)=>{search.Clear();LoadProducts();search.Focus();};bar.Controls.Add(clear);
   var refresh=new Button {Text="Atualizar",Width=95,Height=33};refresh.Click+=(s,e)=>LoadProducts();bar.Controls.Add(refresh);

   var reactivate=new Button {Text="Reativar selecionado",Width=165,Height=33};reactivate.Click+=(s,e)=>ReactivateProduct();bar.Controls.Add(reactivate);
   var import=new Button {Text="Importar CSV",Width=125,Height=33};import.Click+=(s,e)=>ImportProductsCsv();bar.Controls.Add(import);
   var export=new Button {Text="Exportar CSV",Width=125,Height=33};export.Click+=(s,e)=>ExportProducts();bar.Controls.Add(export);
   search.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;LoadProducts();}};
   grid.CellDoubleClick+=(s,e)=>{if(e.RowIndex>=0)EditProduct();};

   StyleProductsScreen(bar);
   metrics.BackColor=Color.FromArgb(235,242,250);metrics.ForeColor=Color.FromArgb(20,40,75);metrics.Font=new Font("Segoe UI",11,FontStyle.Bold);
   Controls.Add(grid);Controls.Add(info);Controls.Add(metrics);Controls.Add(bar);LoadProducts();
  }
  static void StyleButton(Button b,Color background){
   b.UseVisualStyleBackColor=false;b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderSize=1;
   b.FlatAppearance.BorderColor=Color.FromArgb(22,35,55);b.BackColor=background;
   b.ForeColor=Color.White;b.Font=new Font("Segoe UI",10,FontStyle.Bold);
   b.Padding=new Padding(5,2,5,2);b.Height=Math.Max(b.Height,39);
  }
  static void StyleDialog(Form form,TableLayoutPanel panel){
   form.BackColor=Color.FromArgb(245,248,252);form.ForeColor=Color.FromArgb(18,31,50);
   form.Font=new Font("Segoe UI",10,FontStyle.Bold);
   panel.BackColor=form.BackColor;
   foreach(Control control in panel.Controls){
    if(control is Label){control.ForeColor=Color.FromArgb(18,31,50);control.Font=new Font("Segoe UI",10,FontStyle.Bold);}
    if(control is TextBox || control is NumericUpDown){control.BackColor=Color.White;control.ForeColor=Color.Black;control.Font=new Font("Segoe UI",11,FontStyle.Regular);}
    if(control is Button)StyleButton((Button)control,Color.FromArgb(19,92,169));
   }
  }
  void StyleProductsScreen(FlowLayoutPanel bar){
   search.BackColor=Color.White;search.ForeColor=Color.Black;search.Font=new Font("Segoe UI",11,FontStyle.Regular);
   Color[] colors={Color.FromArgb(20,103,180),Color.FromArgb(12,120,82),Color.FromArgb(105,65,170),Color.FromArgb(92,100,112),Color.FromArgb(15,110,125),Color.FromArgb(147,67,35),Color.FromArgb(23,111,75),Color.FromArgb(72,86,153),Color.FromArgb(130,55,105)};
   int index=0;foreach(Control control in bar.Controls)if(control is Button)StyleButton((Button)control,colors[(index++)%colors.Length]);
   grid.BackgroundColor=Color.White;grid.DefaultCellStyle.BackColor=Color.White;grid.DefaultCellStyle.ForeColor=Color.Black;
   grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(20,73,158);grid.DefaultCellStyle.SelectionForeColor=Color.White;
   grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(20,73,158);grid.ColumnHeadersDefaultCellStyle.ForeColor=Color.White;
   grid.EnableHeadersVisualStyles=false;grid.Font=new Font("Segoe UI",10,FontStyle.Regular);
   grid.RowTemplate.Height=35;grid.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(238,244,251);
   grid.BorderStyle=BorderStyle.None;grid.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;
   grid.RowHeadersVisible=false;grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;grid.MultiSelect=false;
   grid.ColumnHeadersHeight=42;grid.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
   grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
   info.BackColor=Color.FromArgb(235,242,250);info.ForeColor=Color.FromArgb(20,35,55);info.Font=new Font("Segoe UI",10,FontStyle.Bold);
  }
  SQLiteConnection Open(bool readOnly=true){
   var c=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=db,ReadOnly=readOnly,FailIfMissing=true}.ToString());c.Open();return c;
  }
  HashSet<string> ProductColumns(SQLiteConnection c){
   var cols=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   using(var cmd=new SQLiteCommand("PRAGMA table_info(products)",c))
   using(var r=cmd.ExecuteReader())while(r.Read())cols.Add(Convert.ToString(r["name"]));
   return cols;
  }
  void ReactivateProduct(){
   if(grid.CurrentRow==null){MessageBox.Show("Selecione um produto inativo.");return;}
   long id;
   if(!long.TryParse(Convert.ToString(grid.CurrentRow.Cells["codigo_interno"].Value),out id)){MessageBox.Show("Produto inválido.");return;}
   try{
    using(var c=Open(false)){
     var cols=ProductColumns(c);
     string column=cols.Contains("active")?"active":cols.Contains("is_active")?"is_active":cols.Contains("ativo")?"ativo":null;
     if(column==null){MessageBox.Show("Este banco não possui campo de situação compatível. Nada foi alterado.");return;}
     using(var check=new SQLiteCommand("SELECT COALESCE(\""+column+"\",1) FROM products WHERE id=@id",c)){
      check.Parameters.AddWithValue("@id",id);
      object value=check.ExecuteScalar();
      if(value==null){MessageBox.Show("Produto não encontrado.");return;}
      if(Convert.ToInt32(value)!=0){MessageBox.Show("Este produto já está ativo.");return;}
     }
     if(MessageBox.Show("Reativar o produto selecionado? O estoque não será alterado.","Confirmar reativação",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
     string backup=db+".antes-reativar-produto-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+".db";
     using(var copy=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=backup}.ToString())){
      copy.Open();c.BackupDatabase(copy,"main","main",-1,null,0);
     }
     using(var tx=c.BeginTransaction())using(var cmd=new SQLiteCommand("UPDATE products SET \""+column+"\"=1 WHERE id=@id AND COALESCE(\""+column+"\",1)=0",c,tx)){
      cmd.Parameters.AddWithValue("@id",id);
      if(cmd.ExecuteNonQuery()!=1)throw new Exception("Nenhum produto foi reativado.");
      tx.Commit();
     }
    }
    LoadProducts();MessageBox.Show("Produto reativado. Estoque preservado.");
   }catch(Exception ex){MessageBox.Show(ex.Message,"Falha na reativação");}
  }
  void ImportProductsCsv(){
   using(var dlg=new OpenFileDialog {Title="Importar produtos (CSV)",Filter="CSV (*.csv)|*.csv"}){
    if(dlg.ShowDialog(this)!=DialogResult.OK)return;
    try{
     var rows=new List<string[]>();
     var lines=System.IO.File.ReadAllLines(dlg.FileName,System.Text.Encoding.UTF8);
     if(lines.Length<2)throw new Exception("Arquivo sem produtos.");
     var header=ParseCsvLine(lines[0]);
     int barcodeIndex=-1,nameIndex=-1,priceIndex=-1,costIndex=-1,stockIndex=-1,brandIndex=-1,categoryIndex=-1;
     for(int i=0;i<header.Count;i++){
      string h=header[i].Trim().Trim('\uFEFF').ToLowerInvariant().Replace(" ","").Replace("_","");
      if(h=="codigodebarras"||h=="barcode"||h=="ean")barcodeIndex=i;
      if(h=="descricao"||h=="name"||h=="produto")nameIndex=i;
      if(h=="preco"||h=="price"||h=="precovenda")priceIndex=i;
      if(h=="custo"||h=="cost")costIndex=i;
      if(h=="estoque"||h=="stock")stockIndex=i;
      if(h=="marca"||h=="brand")brandIndex=i;
      if(h=="categoria"||h=="category")categoryIndex=i;
     }
     if(barcodeIndex<0||nameIndex<0||priceIndex<0)throw new Exception("CSV precisa ter CodigoDeBarras, Descricao e Preco.");
     var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
     for(int n=1;n<lines.Length;n++){
      if(string.IsNullOrWhiteSpace(lines[n]))continue;
      var fields=ParseCsvLine(lines[n]);
      int max=Math.Max(barcodeIndex,Math.Max(nameIndex,priceIndex));
      if(fields.Count<=max)throw new Exception("Linha "+(n+1)+": colunas insuficientes.");
      string code=fields[barcodeIndex].Trim(),name=fields[nameIndex].Trim();
      if(code.Length==0||name.Length==0)throw new Exception("Linha "+(n+1)+": código ou descrição vazios.");
      if(!seen.Add(code))throw new Exception("Código duplicado no CSV: "+code);
      decimal price=ParseMoney(fields[priceIndex],n+1);
      decimal cost=costIndex>=0&&costIndex<fields.Count?ParseMoney(fields[costIndex],n+1):0;
      decimal stock=stockIndex>=0&&stockIndex<fields.Count?ParseMoney(fields[stockIndex],n+1):0;
      if(price<0||cost<0||stock<0)throw new Exception("Linha "+(n+1)+": valores negativos.");
      string brand=brandIndex>=0&&brandIndex<fields.Count?fields[brandIndex].Trim():"";
      string category=categoryIndex>=0&&categoryIndex<fields.Count?fields[categoryIndex].Trim():"";
      rows.Add(new[]{code,name,price.ToString(System.Globalization.CultureInfo.InvariantCulture),cost.ToString(System.Globalization.CultureInfo.InvariantCulture),stock.ToString(System.Globalization.CultureInfo.InvariantCulture),brand,category});
     }
     if(rows.Count==0)throw new Exception("Nenhum produto válido.");
     using(var c=Open(false)){
      var cols=ProductColumns(c);
      foreach(var required in new[]{"barcode","name","price","cost","stock"})if(!cols.Contains(required))throw new Exception("Banco incompatível: falta products."+required);
      if((brandIndex>=0&&!cols.Contains("brand"))||(categoryIndex>=0&&!cols.Contains("category")))
       throw new Exception("O arquivo possui Marca ou Categoria, mas o banco não contém esses campos. Importação cancelada para evitar perda de dados.");
      int existing=0;
      using(var check=new SQLiteCommand("SELECT COUNT(*) FROM products WHERE barcode=@barcode",c)){
       var p=check.Parameters.Add("@barcode",DbType.String);
       foreach(var row in rows){p.Value=row[0];if(Convert.ToInt64(check.ExecuteScalar())>0)existing++;}
      }
      if(MessageBox.Show("Produtos no arquivo: "+rows.Count+"\nJá cadastrados (serão ignorados): "+existing+"\nNovos: "+(rows.Count-existing)+"\n\nNenhum cadastro ou estoque existente será modificado. Importar?","Conferir importação",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
      if(existing==rows.Count){MessageBox.Show("Todos os produtos já estão cadastrados. Nada foi alterado.");return;}
      string backup=db+".antes-importar-produtos-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+".db";
      using(var copy=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=backup}.ToString())){
       copy.Open();c.BackupDatabase(copy,"main","main",-1,null,0);
      }
      int inserted=0;
      using(var tx=c.BeginTransaction()){
       using(var check=new SQLiteCommand("SELECT COUNT(*) FROM products WHERE barcode=@barcode",c,tx))
       using(var insert=new SQLiteCommand("INSERT INTO products(barcode,name,price,cost,stock"+(brandIndex>=0?",brand":"")+(categoryIndex>=0?",category":"")+") VALUES(@barcode,@name,@price,@cost,@stock"+(brandIndex>=0?",@brand":"")+(categoryIndex>=0?",@category":"")+")",c,tx)){
        var cp=check.Parameters.Add("@barcode",DbType.String);
        var bp=insert.Parameters.Add("@barcode",DbType.String);
        var np=insert.Parameters.Add("@name",DbType.String);
        var pp=insert.Parameters.Add("@price",DbType.Decimal);
        var co=insert.Parameters.Add("@cost",DbType.Decimal);
        var st=insert.Parameters.Add("@stock",DbType.Decimal);
        SQLiteParameter br=brandIndex>=0?insert.Parameters.Add("@brand",DbType.String):null;
        SQLiteParameter ca=categoryIndex>=0?insert.Parameters.Add("@category",DbType.String):null;
        foreach(var row in rows){
         cp.Value=row[0];if(Convert.ToInt64(check.ExecuteScalar())>0)continue;
         bp.Value=row[0];np.Value=row[1];
         pp.Value=decimal.Parse(row[2],System.Globalization.CultureInfo.InvariantCulture);
         co.Value=decimal.Parse(row[3],System.Globalization.CultureInfo.InvariantCulture);
         st.Value=decimal.Parse(row[4],System.Globalization.CultureInfo.InvariantCulture);
         if(br!=null)br.Value=row[5];
         if(ca!=null)ca.Value=row[6];
         inserted+=insert.ExecuteNonQuery();
        }
       }
       tx.Commit();
      }
      LoadProducts();MessageBox.Show(inserted+" produtos novos importados. Existentes preservados.\nBackup: "+backup);
     }
    }catch(Exception ex){MessageBox.Show(ex.Message,"Importação cancelada",MessageBoxButtons.OK,MessageBoxIcon.Error);}
   }
  }
  static decimal ParseMoney(string raw,int line){
   decimal result;
   if(!decimal.TryParse(raw.Trim(),System.Globalization.NumberStyles.Number,System.Globalization.CultureInfo.GetCultureInfo("pt-BR"),out result)
    &&!decimal.TryParse(raw.Trim(),System.Globalization.NumberStyles.Number,System.Globalization.CultureInfo.InvariantCulture,out result))
    throw new Exception("Linha "+line+": número inválido: "+raw);
   return result;
  }
  static List<string> ParseCsvLine(string line){
   var values=new List<string>();var cell=new System.Text.StringBuilder();bool quoted=false;
   for(int i=0;i<line.Length;i++){
    char ch=line[i];
    if(ch=='"'){if(quoted&&i+1<line.Length&&line[i+1]=='"'){cell.Append('"');i++;}else quoted=!quoted;}
    else if(ch==';'&&!quoted){values.Add(cell.ToString());cell.Clear();}
    else cell.Append(ch);
   }
   if(quoted)throw new Exception("CSV com aspas não fechadas.");
   values.Add(cell.ToString());return values;
  }
  static string BuildProductsExportSql(HashSet<string> cols){
   string expiry=cols.Contains("expiry_date")?"expiry_date":cols.Contains("expiration_date")?"expiration_date":cols.Contains("validade")?"validade":null;
   string weight=cols.Contains("sold_by_weight")?"sold_by_weight":cols.Contains("is_weighted")?"is_weighted":cols.Contains("por_peso")?"por_peso":null;
   return "SELECT id,barcode,name,price,cost,stock"+(cols.Contains("brand")?",brand":"")+(cols.Contains("category")?",category":"")+(expiry!=null?",\""+expiry+"\"":"")+(weight!=null?",\""+weight+"\"":"")+" FROM products ORDER BY name";
  }
  void ExportProducts(){
   using(var dlg=new SaveFileDialog {Title="Exportar produtos para CSV",Filter="CSV separado por ponto e virgula (*.csv)|*.csv",FileName="produtos-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".csv"}){
    if(dlg.ShowDialog(this)!=DialogResult.OK)return;
    try{
     using(var c=Open()){
      var cols=ProductColumns(c);
      var names=new List<string> {"ID","CodigoDeBarras","Descricao","Preco","Custo","Estoque"};
      if(cols.Contains("brand"))names.Add("Marca");
      if(cols.Contains("category"))names.Add("Categoria");
      if(cols.Contains("expiry_date")||cols.Contains("expiration_date")||cols.Contains("validade"))names.Add("Validade");
      if(cols.Contains("sold_by_weight")||cols.Contains("is_weighted")||cols.Contains("por_peso"))names.Add("VendaPorPeso");
      using(var cmd=new SQLiteCommand(BuildProductsExportSql(cols),c))
      using(var r=cmd.ExecuteReader())
      using(var writer=new System.IO.StreamWriter(dlg.FileName,false,new System.Text.UTF8Encoding(true))){
       writer.WriteLine(string.Join(";",names));
      while(r.Read()){
       var cells=new string[r.FieldCount];
       for(int i=0;i<r.FieldCount;i++)cells[i]="\""+Convert.ToString(r.GetValue(i)).Replace("\"","\"\"")+"\"";
       writer.WriteLine(string.Join(";",cells));
      }
     }
     }
     MessageBox.Show("Produtos exportados. O banco não foi modificado.");
    }catch(Exception ex){MessageBox.Show(ex.Message,"Falha na exportação");}
   }
  }
  void LoadProducts(){
   try{
    using(var c=Open()){
    var cols=ProductColumns(c);
    string activeColumn=cols.Contains("active")?"active":cols.Contains("is_active")?"is_active":cols.Contains("ativo")?"ativo":null;
    if(showInactive && activeColumn==null){showInactive=false;MessageBox.Show("Este banco não possui campo de situação ativo/inativo. Nenhum produto foi alterado.");}
    string filter="";
    int mode=statusFilter.SelectedIndex;
    if(mode==1&&activeColumn!=null)filter=" AND COALESCE(\""+activeColumn+"\",1)=1";
    if(mode==2&&activeColumn!=null)filter=" AND COALESCE(\""+activeColumn+"\",1)=0";
    if(mode==3)filter=" AND COALESCE(stock,0)<=0";
    if(mode==4)filter=" AND COALESCE(stock,0)>0 AND COALESCE(stock,0)<=5";
    if((mode==1||mode==2)&&activeColumn==null){info.Text="  Este banco não possui campo de status; filtro indisponível.";return;}
    string searchColumns="name LIKE @q OR barcode LIKE @q";
    if(cols.Contains("brand"))searchColumns+=" OR brand LIKE @q";
    if(cols.Contains("category"))searchColumns+=" OR category LIKE @q";
    string extraSelect=(cols.Contains("brand")?",brand AS marca":"")+(cols.Contains("category")?",category AS categoria":"");
    string expiryColumn=cols.Contains("expiry_date")?"expiry_date":cols.Contains("expiration_date")?"expiration_date":cols.Contains("validade")?"validade":null;
    if(expiryColumn!=null)extraSelect+=",\""+expiryColumn+"\" AS validade";
    string weightColumn=cols.Contains("sold_by_weight")?"sold_by_weight":cols.Contains("is_weighted")?"is_weighted":cols.Contains("por_peso")?"por_peso":null;
    if(weightColumn!=null)extraSelect+=",\""+weightColumn+"\" AS venda_por_peso";
    using(var cmd=new SQLiteCommand("SELECT id AS codigo_interno,barcode AS codigo_barras,name AS descricao"+extraSelect+",price AS preco,cost AS custo,ROUND(COALESCE(price,0)-COALESCE(cost,0),2) AS lucro_unitario,CASE WHEN COALESCE(cost,0)>0 THEN ROUND(((COALESCE(price,0)-cost)/cost)*100,2) ELSE NULL END AS margem_percentual,stock AS estoque FROM products WHERE ("+searchColumns+")"+filter+" ORDER BY name LIMIT 2000",c)){
     cmd.Parameters.AddWithValue("@q","%"+search.Text.Trim()+"%");
     var dt=new DataTable();using(var a=new SQLiteDataAdapter(cmd))a.Fill(dt);grid.DataSource=dt;
     info.Text="  "+dt.Rows.Count+" produtos exibidos. Cadastro preservado.";
     decimal stockTotal=0,stockValue=0;int zeroStock=0;
     foreach(DataRow row in dt.Rows){decimal qty=0,cost=0;decimal.TryParse(Convert.ToString(row["estoque"]),out qty);decimal.TryParse(Convert.ToString(row["custo"]),out cost);stockTotal+=qty;stockValue+=qty*cost;if(qty<=0)zeroStock++;}
     metrics.Text="    PRODUTOS: "+dt.Rows.Count+"     |     SEM ESTOQUE: "+zeroStock+"     |     UNIDADES: "+stockTotal.ToString("N3")+"     |     CUSTO ESTIMADO: R$ "+stockValue.ToString("N2");
    }
   }}catch(Exception ex){MessageBox.Show(ex.Message,"Consulta de produtos");}
  }
  void EditProduct(){
   if(grid.CurrentRow==null){MessageBox.Show("Selecione um produto.");return;}
   long id;
   if(!long.TryParse(Convert.ToString(grid.CurrentRow.Cells["codigo_interno"].Value),out id)){MessageBox.Show("Produto inválido.");return;}
   using(var c=Open()){
    var columns=ProductColumns(c);
    string expiryCol=columns.Contains("expiry_date")?"expiry_date":columns.Contains("expiration_date")?"expiration_date":columns.Contains("validade")?"validade":null;
    string weightCol=columns.Contains("sold_by_weight")?"sold_by_weight":columns.Contains("is_weighted")?"is_weighted":columns.Contains("por_peso")?"por_peso":null;
    string extra=(columns.Contains("brand")?",brand":"")+(columns.Contains("category")?",category":"")+(expiryCol!=null?",\""+expiryCol+"\" AS saved_expiry":"")+(weightCol!=null?",\""+weightCol+"\" AS saved_weight":"");
    using(var q=new SQLiteCommand("SELECT barcode,name,price,cost"+extra+" FROM products WHERE id=@id",c)){
     q.Parameters.AddWithValue("@id",id);
     using(var r=q.ExecuteReader()){
      if(!r.Read()){MessageBox.Show("Produto não encontrado.");return;}
      string oldCode=Convert.ToString(r["barcode"]);
      string oldName=Convert.ToString(r["name"]);
      decimal oldPrice=Convert.ToDecimal(r["price"]);
      decimal oldCost=Convert.ToDecimal(r["cost"]);
      string oldBrand=columns.Contains("brand")?Convert.ToString(r["brand"]):"";
      string oldCategory=columns.Contains("category")?Convert.ToString(r["category"]):"";
      DateTime oldExpiry=DateTime.Today;bool hasExpiry=expiryCol!=null&&DateTime.TryParse(Convert.ToString(r["saved_expiry"]),out oldExpiry);
      bool oldWeight=weightCol!=null&&r["saved_weight"]!=DBNull.Value&&Convert.ToString(r["saved_weight"])!="0";
      using(var form=new Form {Text="Editar produto #"+id,Width=560,Height=495,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false}){
       var panel=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=9,Padding=new Padding(15)};
       panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,140));panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
       var code=new TextBox {Text=oldCode};var name=new TextBox {Text=oldName};
       var brand=new TextBox {Text=oldBrand,Enabled=columns.Contains("brand")};
       var category=new TextBox {Text=oldCategory,Enabled=columns.Contains("category")};
       var expiry=new DateTimePicker {Format=DateTimePickerFormat.Short,ShowCheckBox=true,Checked=hasExpiry,Enabled=expiryCol!=null};
       if(hasExpiry)expiry.Value=oldExpiry;
       var byWeight=new CheckBox {Text="Vendido por peso (kg)",AutoSize=true,Checked=oldWeight,Enabled=weightCol!=null};
       var price=new NumericUpDown {DecimalPlaces=2,Maximum=1000000,Value=oldPrice};
       var cost=new NumericUpDown {DecimalPlaces=2,Maximum=1000000,Value=oldCost};
       Control[] fields={code,name,brand,category,price,cost,expiry,byWeight};string[] labels={"Código de barras","Descrição","Marca","Categoria","Preço de venda","Custo","Validade","Tipo de venda"};
       for(int i=0;i<fields.Length;i++){panel.Controls.Add(new Label {Text=labels[i],AutoSize=true},0,i);panel.Controls.Add(fields[i],1,i);}
       var save=new Button {Text="Salvar alterações",Width=165,Height=38,BackColor=Color.FromArgb(20,69,147),ForeColor=Color.White};
       save.Click+=(s,e)=>{
        if(string.IsNullOrWhiteSpace(name.Text)){MessageBox.Show("Informe a descrição.");return;}
        try{
         using(var dbconn=Open(false)){
          using(var duplicate=new SQLiteCommand("SELECT COUNT(*) FROM products WHERE barcode=@barcode AND id<>@id AND @barcode<>''",dbconn)){
           duplicate.Parameters.AddWithValue("@barcode",code.Text.Trim());duplicate.Parameters.AddWithValue("@id",id);
           if(Convert.ToInt64(duplicate.ExecuteScalar())>0)throw new Exception("Código de barras já utilizado por outro produto.");
          }
          string backup=db+".antes-editar-produto-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+".db";
          using(var copy=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=backup}.ToString())){
           copy.Open();dbconn.BackupDatabase(copy,"main","main",-1,null,0);
          }
          string optional=(columns.Contains("brand")?",brand=@brand":"")+(columns.Contains("category")?",category=@category":"")+(expiryCol!=null?",\""+expiryCol+"\"=@expiry":"")+(weightCol!=null?",\""+weightCol+"\"=@weight":"");
          using(var tx=dbconn.BeginTransaction())using(var update=new SQLiteCommand("UPDATE products SET barcode=@barcode,name=@name,price=@price,cost=@cost"+optional+" WHERE id=@id",dbconn,tx)){
           update.Parameters.AddWithValue("@barcode",code.Text.Trim());update.Parameters.AddWithValue("@name",name.Text.Trim());update.Parameters.AddWithValue("@price",price.Value);update.Parameters.AddWithValue("@cost",cost.Value);update.Parameters.AddWithValue("@id",id);
           if(columns.Contains("brand"))update.Parameters.AddWithValue("@brand",brand.Text.Trim());
           if(columns.Contains("category"))update.Parameters.AddWithValue("@category",category.Text.Trim());
           if(expiryCol!=null)update.Parameters.AddWithValue("@expiry",expiry.Checked?(object)expiry.Value.ToString("yyyy-MM-dd"):DBNull.Value);
           if(weightCol!=null)update.Parameters.AddWithValue("@weight",byWeight.Checked?1:0);
           if(update.ExecuteNonQuery()!=1)throw new Exception("Produto não atualizado.");
           tx.Commit();
          }
         }
         form.DialogResult=DialogResult.OK;form.Close();
        }catch(Exception ex){MessageBox.Show(ex.Message,"Alteração não salva",MessageBoxButtons.OK,MessageBoxIcon.Error);}
       };
       panel.Controls.Add(save,1,8);StyleDialog(form,panel);form.Controls.Add(panel);
       if(form.ShowDialog(this)==DialogResult.OK)LoadProducts();
      }
     }
    }
   }
  }
  static string LookupBarcodeName(string barcode){
   System.Net.ServicePointManager.SecurityProtocol|=System.Net.SecurityProtocolType.Tls12;
   string url="https://world.openfoodfacts.org/api/v2/product/"+barcode+".json?fields=product_name_pt,product_name";
   using(var client=new System.Net.WebClient()){
    client.Encoding=System.Text.Encoding.UTF8;
    client.Headers[System.Net.HttpRequestHeader.UserAgent]="ArmazemGratidao/1.0 (product lookup)";
    string json=client.DownloadString(url);
    foreach(string key in new[]{"product_name_pt","product_name"}){
     var match=System.Text.RegularExpressions.Regex.Match(json, "\\\"" + key + "\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"");
     if(match.Success){
      string name=System.Text.RegularExpressions.Regex.Unescape(match.Groups[1].Value).Trim();
      if(name.Length>0)return name;
     }
    }
   }
   return null;
  }
  void InsertProduct(){
   using(var form=new Form {Text="Inserir produto",Width=560,Height=530,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false}){
    var panel=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=11,Padding=new Padding(15)};
    panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,145));panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
    var barcode=new TextBox();var name=new TextBox();var brand=new TextBox();var category=new TextBox();
    HashSet<string> availableColumns;
    using(var checkConnection=Open())availableColumns=ProductColumns(checkConnection);
    brand.Enabled=availableColumns.Contains("brand");
    category.Enabled=availableColumns.Contains("category");
    var expiry=new DateTimePicker {Format=DateTimePickerFormat.Short,ShowCheckBox=true,Checked=false};
    var byWeight=new CheckBox {Text="Vendido por peso (kg)",AutoSize=true};
    expiry.Enabled=availableColumns.Contains("expiry_date")||availableColumns.Contains("expiration_date")||availableColumns.Contains("validade");
    byWeight.Enabled=availableColumns.Contains("sold_by_weight")||availableColumns.Contains("is_weighted")||availableColumns.Contains("por_peso");
    var price=new NumericUpDown {DecimalPlaces=2,Maximum=1000000,Width=170};var cost=new NumericUpDown {DecimalPlaces=2,Maximum=1000000,Width=170};var stock=new NumericUpDown {DecimalPlaces=3,Maximum=10000000,Width=170};
    Control[] fields={barcode,name,brand,category,price,cost,stock,expiry,byWeight};string[] labels={"Código de barras","Descrição","Marca","Categoria","Preço de venda","Custo","Estoque inicial","Validade","Tipo de venda"};
    for(int i=0;i<fields.Length;i++){panel.Controls.Add(new Label {Text=labels[i],AutoSize=true},0,i);panel.Controls.Add(fields[i],1,i);}
    var lookup=new Button {Text="Buscar descrição na internet",Width=225,Height=32};
    lookup.Click+=async (s,e)=>{
     string code=barcode.Text.Trim();
     if(!System.Text.RegularExpressions.Regex.IsMatch(code,@"^\d{8,14}$")){
      MessageBox.Show("Informe um código de barras numérico de 8 a 14 dígitos.");return;
     }
     lookup.Enabled=false;lookup.Text="Consultando...";
     try{
      string description=await System.Threading.Tasks.Task.Run(()=>LookupBarcodeName(code));
      if(string.IsNullOrWhiteSpace(description)){MessageBox.Show("Produto não encontrado na base pública. Preencha manualmente.");return;}
      if(!string.IsNullOrWhiteSpace(name.Text)&&MessageBox.Show("Substituir a descrição digitada por:\n"+description+"?","Confirmar",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
      name.Text=description;
     }catch(Exception ex){MessageBox.Show("Consulta indisponível: "+ex.Message+"\nVocê pode continuar o cadastro manualmente.","Busca de produto");}
     finally{lookup.Enabled=true;lookup.Text="Buscar descrição na internet";}
    };
    panel.Controls.Add(lookup,1,9);
    var save=new Button {Text="Salvar produto",Width=145,Height=36,BackColor=Color.FromArgb(24,84,170),ForeColor=Color.White};
    save.Click+=(s,e)=>{
     if(string.IsNullOrWhiteSpace(name.Text)){MessageBox.Show("Informe a descrição.");return;}
     try{
      using(var c=Open(false)){
       var cols=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
       using(var cmd=new SQLiteCommand("PRAGMA table_info(products)",c))using(var reader=cmd.ExecuteReader())while(reader.Read())cols.Add(Convert.ToString(reader["name"]));
       foreach(var field in new[]{"barcode","name","price","cost","stock"})if(!cols.Contains(field))throw new Exception("Campo necessário ausente: products."+field);
       using(var check=new SQLiteCommand("SELECT COUNT(*) FROM products WHERE barcode=@code AND @code<>''",c)){
        check.Parameters.AddWithValue("@code",barcode.Text.Trim());
        if(Convert.ToInt64(check.ExecuteScalar())>0)throw new Exception("Código de barras já cadastrado.");
       }
       // Uma cópia SQLite é criada antes de qualquer alteração.
       string backup=db+".antes-produto-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+".db";
       using(var copy=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=backup}.ToString())){
        copy.Open();c.BackupDatabase(copy,"main","main",-1,null,0);
       }
       string expiryCol=cols.Contains("expiry_date")?"expiry_date":cols.Contains("expiration_date")?"expiration_date":cols.Contains("validade")?"validade":null;
       string weightCol=cols.Contains("sold_by_weight")?"sold_by_weight":cols.Contains("is_weighted")?"is_weighted":cols.Contains("por_peso")?"por_peso":null;
       string extraCols=(cols.Contains("brand")?",brand":"")+(cols.Contains("category")?",category":"")+(expiryCol!=null?",\""+expiryCol+"\"":"")+(weightCol!=null?",\""+weightCol+"\"":"");
       string extraParams=(cols.Contains("brand")?",@brand":"")+(cols.Contains("category")?",@category":"")+(expiryCol!=null?",@expiry":"")+(weightCol!=null?",@weight":"");
       using(var tx=c.BeginTransaction())using(var cmd=new SQLiteCommand("INSERT INTO products(barcode,name,price,cost,stock"+extraCols+") VALUES(@barcode,@name,@price,@cost,@stock"+extraParams+")",c,tx)){
        cmd.Parameters.AddWithValue("@barcode",barcode.Text.Trim());cmd.Parameters.AddWithValue("@name",name.Text.Trim());cmd.Parameters.AddWithValue("@price",price.Value);cmd.Parameters.AddWithValue("@cost",cost.Value);cmd.Parameters.AddWithValue("@stock",stock.Value);
        if(cols.Contains("brand"))cmd.Parameters.AddWithValue("@brand",brand.Text.Trim());
        if(cols.Contains("category"))cmd.Parameters.AddWithValue("@category",category.Text.Trim());
        if(expiryCol!=null)cmd.Parameters.AddWithValue("@expiry",expiry.Checked?(object)expiry.Value.ToString("yyyy-MM-dd"):DBNull.Value);
        if(weightCol!=null)cmd.Parameters.AddWithValue("@weight",byWeight.Checked?1:0);
        cmd.ExecuteNonQuery();tx.Commit();
       }
      }
      form.DialogResult=DialogResult.OK;form.Close();
     }catch(Exception ex){MessageBox.Show(ex.Message,"Produto não salvo");}
    };
    panel.Controls.Add(save,1,10);StyleDialog(form,panel);form.Controls.Add(panel);
    if(form.ShowDialog(this)==DialogResult.OK)LoadProducts();
   }
  }
 }
}