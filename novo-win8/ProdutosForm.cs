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
  readonly Label info=new Label {Dock=DockStyle.Bottom,Height=32};
  public ProdutosForm(string database) {
   db=database;Text="Armazém Gratidão | Produtos";Width=1100;Height=720;
   var bar=new FlowLayoutPanel {Dock=DockStyle.Top,Height=105,Padding=new Padding(8),BackColor=Color.FromArgb(20,73,158)};
   bar.Controls.Add(search);
   var find=new Button {Text="Pesquisar",Width=100,Height=33};find.Click+=(s,e)=>LoadProducts();bar.Controls.Add(find);
   var add=new Button {Text="Inserir produto",Width=135,Height=33};add.Click+=(s,e)=>InsertProduct();bar.Controls.Add(add);
   var edit=new Button {Text="Editar selecionado",Width=155,Height=33};edit.Click+=(s,e)=>EditProduct();bar.Controls.Add(edit);
   var clear=new Button {Text="Limpar busca",Width=110,Height=33};clear.Click+=(s,e)=>{search.Clear();LoadProducts();search.Focus();};bar.Controls.Add(clear);
   var refresh=new Button {Text="Atualizar",Width=95,Height=33};refresh.Click+=(s,e)=>LoadProducts();bar.Controls.Add(refresh);
   var inactive=new Button {Text="Inativos",Width=105,Height=33};inactive.Click+=(s,e)=>{showInactive=!showInactive;inactive.Text=showInactive?"Todos os produtos":"Inativos";LoadProducts();};bar.Controls.Add(inactive);
   var reactivate=new Button {Text="Reativar selecionado",Width=165,Height=33};reactivate.Click+=(s,e)=>ReactivateProduct();bar.Controls.Add(reactivate);
   var import=new Button {Text="Importar CSV",Width=125,Height=33};import.Click+=(s,e)=>ImportProductsCsv();bar.Controls.Add(import);
   var export=new Button {Text="Exportar CSV",Width=125,Height=33};export.Click+=(s,e)=>ExportProducts();bar.Controls.Add(export);
   search.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;LoadProducts();}};
   grid.CellDoubleClick+=(s,e)=>{if(e.RowIndex>=0)EditProduct();};

   Controls.Add(grid);Controls.Add(info);Controls.Add(bar);LoadProducts();
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
     int barcodeIndex=-1,nameIndex=-1,priceIndex=-1,costIndex=-1,stockIndex=-1;
     for(int i=0;i<header.Count;i++){
      string h=header[i].Trim().Trim('\uFEFF').ToLowerInvariant().Replace(" ","").Replace("_","");
      if(h=="codigodebarras"||h=="barcode"||h=="ean")barcodeIndex=i;
      if(h=="descricao"||h=="name"||h=="produto")nameIndex=i;
      if(h=="preco"||h=="price"||h=="precovenda")priceIndex=i;
      if(h=="custo"||h=="cost")costIndex=i;
      if(h=="estoque"||h=="stock")stockIndex=i;
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
      rows.Add(new[]{code,name,price.ToString(System.Globalization.CultureInfo.InvariantCulture),cost.ToString(System.Globalization.CultureInfo.InvariantCulture),stock.ToString(System.Globalization.CultureInfo.InvariantCulture)});
     }
     if(rows.Count==0)throw new Exception("Nenhum produto válido.");
     using(var c=Open(false)){
      var cols=ProductColumns(c);
      foreach(var required in new[]{"barcode","name","price","cost","stock"})if(!cols.Contains(required))throw new Exception("Banco incompatível: falta products."+required);
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
       using(var insert=new SQLiteCommand("INSERT INTO products(barcode,name,price,cost,stock) VALUES(@barcode,@name,@price,@cost,@stock)",c,tx)){
        var cp=check.Parameters.Add("@barcode",DbType.String);
        var bp=insert.Parameters.Add("@barcode",DbType.String);
        var np=insert.Parameters.Add("@name",DbType.String);
        var pp=insert.Parameters.Add("@price",DbType.Decimal);
        var co=insert.Parameters.Add("@cost",DbType.Decimal);
        var st=insert.Parameters.Add("@stock",DbType.Decimal);
        foreach(var row in rows){
         cp.Value=row[0];if(Convert.ToInt64(check.ExecuteScalar())>0)continue;
         bp.Value=row[0];np.Value=row[1];
         pp.Value=decimal.Parse(row[2],System.Globalization.CultureInfo.InvariantCulture);
         co.Value=decimal.Parse(row[3],System.Globalization.CultureInfo.InvariantCulture);
         st.Value=decimal.Parse(row[4],System.Globalization.CultureInfo.InvariantCulture);
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
  void ExportProducts(){
   using(var dlg=new SaveFileDialog {Title="Exportar produtos para CSV",Filter="CSV separado por ponto e virgula (*.csv)|*.csv",FileName="produtos-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".csv"}){
    if(dlg.ShowDialog(this)!=DialogResult.OK)return;
    try{
     using(var c=Open())
     using(var cmd=new SQLiteCommand("SELECT id,barcode,name,price,cost,stock FROM products ORDER BY name",c))
     using(var r=cmd.ExecuteReader())
     using(var writer=new System.IO.StreamWriter(dlg.FileName,false,new System.Text.UTF8Encoding(true))){
      writer.WriteLine("ID;CodigoDeBarras;Descricao;Preco;Custo;Estoque");
      while(r.Read()){
       var cells=new string[6];
       for(int i=0;i<6;i++)cells[i]="\""+Convert.ToString(r.GetValue(i)).Replace("\"","\"\"")+"\"";
       writer.WriteLine(string.Join(";",cells));
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
    string filter=showInactive?" AND COALESCE(\""+activeColumn+"\",1)=0":"";
    using(var cmd=new SQLiteCommand("SELECT id AS codigo_interno,barcode AS codigo_barras,name AS descricao,price AS preco,cost AS custo,stock AS estoque FROM products WHERE (name LIKE @q OR barcode LIKE @q)"+filter+" ORDER BY name LIMIT 2000",c)){
     cmd.Parameters.AddWithValue("@q","%"+search.Text.Trim()+"%");
     var dt=new DataTable();using(var a=new SQLiteDataAdapter(cmd))a.Fill(dt);grid.DataSource=dt;
     info.Text="  "+dt.Rows.Count+" produtos exibidos. Cadastro preservado.";
    }
   }}catch(Exception ex){MessageBox.Show(ex.Message,"Consulta de produtos");}
  }
  void EditProduct(){
   if(grid.CurrentRow==null){MessageBox.Show("Selecione um produto.");return;}
   long id;
   if(!long.TryParse(Convert.ToString(grid.CurrentRow.Cells["codigo_interno"].Value),out id)){MessageBox.Show("Produto inválido.");return;}
   using(var c=Open()){
    using(var q=new SQLiteCommand("SELECT barcode,name,price,cost FROM products WHERE id=@id",c)){
     q.Parameters.AddWithValue("@id",id);
     using(var r=q.ExecuteReader()){
      if(!r.Read()){MessageBox.Show("Produto não encontrado.");return;}
      string oldCode=Convert.ToString(r["barcode"]);
      string oldName=Convert.ToString(r["name"]);
      decimal oldPrice=Convert.ToDecimal(r["price"]);
      decimal oldCost=Convert.ToDecimal(r["cost"]);
      using(var form=new Form {Text="Editar produto #"+id,Width=470,Height=330,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false}){
       var panel=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=5,Padding=new Padding(15)};
       panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,140));panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
       var code=new TextBox {Text=oldCode};var name=new TextBox {Text=oldName};
       var price=new NumericUpDown {DecimalPlaces=2,Maximum=1000000,Value=oldPrice};
       var cost=new NumericUpDown {DecimalPlaces=2,Maximum=1000000,Value=oldCost};
       Control[] fields={code,name,price,cost};string[] labels={"Código de barras","Descrição","Preço de venda","Custo"};
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
          using(var tx=dbconn.BeginTransaction())using(var update=new SQLiteCommand("UPDATE products SET barcode=@barcode,name=@name,price=@price,cost=@cost WHERE id=@id",dbconn,tx)){
           update.Parameters.AddWithValue("@barcode",code.Text.Trim());update.Parameters.AddWithValue("@name",name.Text.Trim());update.Parameters.AddWithValue("@price",price.Value);update.Parameters.AddWithValue("@cost",cost.Value);update.Parameters.AddWithValue("@id",id);
           if(update.ExecuteNonQuery()!=1)throw new Exception("Produto não atualizado.");
           tx.Commit();
          }
         }
         form.DialogResult=DialogResult.OK;form.Close();
        }catch(Exception ex){MessageBox.Show(ex.Message,"Alteração não salva",MessageBoxButtons.OK,MessageBoxIcon.Error);}
       };
       panel.Controls.Add(save,1,4);form.Controls.Add(panel);
       if(form.ShowDialog(this)==DialogResult.OK)LoadProducts();
      }
     }
    }
   }
  }
  void InsertProduct(){
   using(var form=new Form {Text="Inserir produto",Width=460,Height=360,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false}){
    var panel=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=6,Padding=new Padding(15)};
    panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,145));panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
    var barcode=new TextBox();var name=new TextBox();var price=new NumericUpDown {DecimalPlaces=2,Maximum=1000000,Width=170};var cost=new NumericUpDown {DecimalPlaces=2,Maximum=1000000,Width=170};var stock=new NumericUpDown {DecimalPlaces=3,Maximum=10000000,Width=170};
    Control[] fields={barcode,name,price,cost,stock};string[] labels={"Código de barras","Descrição","Preço de venda","Custo","Estoque inicial"};
    for(int i=0;i<fields.Length;i++){panel.Controls.Add(new Label {Text=labels[i],AutoSize=true},0,i);panel.Controls.Add(fields[i],1,i);}
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
       using(var tx=c.BeginTransaction())using(var cmd=new SQLiteCommand("INSERT INTO products(barcode,name,price,cost,stock) VALUES(@barcode,@name,@price,@cost,@stock)",c,tx)){
        cmd.Parameters.AddWithValue("@barcode",barcode.Text.Trim());cmd.Parameters.AddWithValue("@name",name.Text.Trim());cmd.Parameters.AddWithValue("@price",price.Value);cmd.Parameters.AddWithValue("@cost",cost.Value);cmd.Parameters.AddWithValue("@stock",stock.Value);
        cmd.ExecuteNonQuery();tx.Commit();
       }
      }
      form.DialogResult=DialogResult.OK;form.Close();
     }catch(Exception ex){MessageBox.Show(ex.Message,"Produto não salvo");}
    };
    panel.Controls.Add(save,1,5);form.Controls.Add(panel);
    if(form.ShowDialog(this)==DialogResult.OK)LoadProducts();
   }
  }
 }
}