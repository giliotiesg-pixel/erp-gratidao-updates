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
  readonly Label info=new Label {Dock=DockStyle.Bottom,Height=32};
  public ProdutosForm(string database) {
   db=database;Text="Armazém Gratidão | Produtos";Width=1100;Height=720;
   var bar=new FlowLayoutPanel {Dock=DockStyle.Top,Height=56,Padding=new Padding(8),BackColor=Color.FromArgb(20,73,158)};
   bar.Controls.Add(search);
   var find=new Button {Text="Pesquisar",Width=100,Height=33};find.Click+=(s,e)=>LoadProducts();bar.Controls.Add(find);
   var add=new Button {Text="Inserir produto",Width=135,Height=33};add.Click+=(s,e)=>InsertProduct();bar.Controls.Add(add);
   var edit=new Button {Text="Editar selecionado",Width=155,Height=33};edit.Click+=(s,e)=>EditProduct();bar.Controls.Add(edit);
   var clear=new Button {Text="Limpar busca",Width=110,Height=33};clear.Click+=(s,e)=>{search.Clear();LoadProducts();search.Focus();};bar.Controls.Add(clear);
   var refresh=new Button {Text="Atualizar",Width=95,Height=33};refresh.Click+=(s,e)=>LoadProducts();bar.Controls.Add(refresh);
   search.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;LoadProducts();}};
   grid.CellDoubleClick+=(s,e)=>{if(e.RowIndex>=0)EditProduct();};

   Controls.Add(grid);Controls.Add(info);Controls.Add(bar);LoadProducts();
  }
  SQLiteConnection Open(bool readOnly=true){
   var c=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=db,ReadOnly=readOnly,FailIfMissing=true}.ToString());c.Open();return c;
  }
  void LoadProducts(){
   try{
    using(var c=Open())using(var cmd=new SQLiteCommand("SELECT id AS codigo_interno,barcode AS codigo_barras,name AS descricao,price AS preco,cost AS custo,stock AS estoque FROM products WHERE name LIKE @q OR barcode LIKE @q ORDER BY name LIMIT 2000",c)){
     cmd.Parameters.AddWithValue("@q","%"+search.Text.Trim()+"%");
     var dt=new DataTable();using(var a=new SQLiteDataAdapter(cmd))a.Fill(dt);grid.DataSource=dt;
     info.Text="  "+dt.Rows.Count+" produtos exibidos. Cadastro preservado.";
    }
   }catch(Exception ex){MessageBox.Show(ex.Message,"Consulta de produtos");}
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