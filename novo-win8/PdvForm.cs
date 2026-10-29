using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace ArmazemGratidao {
 public sealed class PdvForm : Form {
  sealed class Item {public long Id;public string Barcode,Name;public decimal Qty,Price,Cost;public decimal Total {get{return Math.Round(Qty*Price,2);}}}
  readonly string db;
  readonly List<Item> cart=new List<Item>();
  readonly TextBox code=new TextBox {Width=260,Font=new Font("Segoe UI",14,FontStyle.Bold)};
  readonly NumericUpDown qty=new NumericUpDown {Minimum=0.001M,Maximum=999999,DecimalPlaces=3,Value=1,Width=95};
  readonly DataGridView grid=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,SelectionMode=DataGridViewSelectionMode.FullRowSelect};
  readonly Label total=new Label {AutoSize=true,Font=new Font("Segoe UI",20,FontStyle.Bold),ForeColor=Color.FromArgb(175,25,45)};
  readonly ComboBox payment=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=160};
  readonly TextBox customer=new TextBox {Width=180};
  readonly NumericUpDown received=new NumericUpDown {DecimalPlaces=2,Maximum=99999999,Width=130};
  public PdvForm(string database) {
   db=database;Text="Armazem Gratidao - PDV";Width=1100;Height=730;
   var header=new FlowLayoutPanel {Dock=DockStyle.Top,Height=64,Padding=new Padding(8),BackColor=Color.FromArgb(22,75,160)};
   header.Controls.Add(new Label {Text="Codigo / Nome:",AutoSize=true,ForeColor=Color.White,Padding=new Padding(0,9,0,0)});
   header.Controls.Add(code);header.Controls.Add(new Label {Text="Qtd:",AutoSize=true,ForeColor=Color.White,Padding=new Padding(0,9,0,0)});header.Controls.Add(qty);
   var add=new Button {Text="Adicionar produto",AutoSize=true,Height=35};add.Click+=(s,e)=>AddProduct();header.Controls.Add(add);
   code.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;AddProduct();}};
   var footer=new FlowLayoutPanel {Dock=DockStyle.Bottom,Height=126,Padding=new Padding(10),AutoScroll=true};
   footer.Controls.Add(new Label {Text="Pagamento:",AutoSize=true,Padding=new Padding(0,9,0,0)});
   payment.Items.AddRange(new object[]{"DINHEIRO","PIX","DEBITO","CREDITO","ALIMENTACAO","FIADO"});payment.SelectedIndex=0;footer.Controls.Add(payment);
   footer.Controls.Add(new Label {Text="Cliente:",AutoSize=true,Padding=new Padding(0,9,0,0)});footer.Controls.Add(customer);
   footer.Controls.Add(new Label {Text="Recebido:",AutoSize=true,Padding=new Padding(0,9,0,0)});footer.Controls.Add(received);
   var finish=new Button {Text="FINALIZAR VENDA",Height=43,Width=180,BackColor=Color.FromArgb(30,100,175),ForeColor=Color.White};finish.Click+=(s,e)=>Finish();footer.Controls.Add(finish);
   var edit=new Button {Text="Editar quantidade / preço",AutoSize=true,Height=40};edit.Click+=(s,e)=>EditSelected();footer.Controls.Add(edit);
   var remove=new Button {Text="Remover item",AutoSize=true,Height=40};remove.Click+=(s,e)=>RemoveSelected();footer.Controls.Add(remove);footer.Controls.Add(total);
   grid.CellDoubleClick+=(s,e)=>{if(e.RowIndex>=0)EditSelected();};
   grid.Font=new Font("Segoe UI",11,FontStyle.Regular);grid.RowTemplate.Height=38;
   grid.CellFormatting+=(s,e)=>{if(e.RowIndex>=0&&e.ColumnIndex>=0&&e.Value!=null){string name=grid.Columns[e.ColumnIndex].Name.ToLowerInvariant();if(name.Contains("descri")||name.Contains("nome")||name.Contains("name")){e.CellStyle.Alignment=DataGridViewContentAlignment.MiddleLeft;e.CellStyle.Padding=new Padding(7,4,7,4);}}};
   grid.DataBindingComplete+=(s,e)=>{foreach(DataGridViewColumn column in grid.Columns){if(column.Name.ToLowerInvariant().Contains("descri")||column.Name.ToLowerInvariant().Contains("nome")||column.Name.ToLowerInvariant().Contains("name")){column.MinimumWidth=260;column.FillWeight=240;column.DefaultCellStyle.WrapMode=DataGridViewTriState.True;}}grid.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells;};
   Controls.Add(grid);Controls.Add(footer);Controls.Add(header);Render();code.Focus();
  }
  SQLiteConnection Open() {var c=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=db,FailIfMissing=true}.ToString());c.Open();return c;}
  static decimal Dec(object o) {return Convert.ToDecimal(o,CultureInfo.InvariantCulture);}
  void AddProduct() {
   string search=code.Text.Trim();if(search.Length==0)return;
   try {
    var matches=new List<Item>();
    var stocks=new Dictionary<long,decimal>();
    using(var connection=Open())
    using(var cmd=new SQLiteCommand("SELECT id,barcode,name,price,cost,stock FROM products WHERE barcode=@code OR internal_code=@code OR name LIKE @name ORDER BY CASE WHEN barcode=@code THEN 0 WHEN internal_code=@code THEN 1 ELSE 2 END,name LIMIT 100",connection)){
     cmd.Parameters.AddWithValue("@code",search);
     cmd.Parameters.AddWithValue("@name","%"+search+"%");
     using(var reader=cmd.ExecuteReader())while(reader.Read()){
      var item=new Item {Id=Convert.ToInt64(reader["id"]),Barcode=Convert.ToString(reader["barcode"]),Name=Convert.ToString(reader["name"]),Price=Dec(reader["price"]),Cost=Dec(reader["cost"])};
      matches.Add(item);stocks[item.Id]=Dec(reader["stock"]);
     }
    }
    if(matches.Count==0){MessageBox.Show("Produto não cadastrado.");return;}
    Item selected=matches[0];
    if(matches.Count>1){
     using(var dialog=new Form {Text="Selecione o produto",Width=780,Height=500,StartPosition=FormStartPosition.CenterParent,MinimumSize=new Size(580,350)}){
      var list=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
      var dt=new DataTable();dt.Columns.Add("Código");dt.Columns.Add("Descrição");dt.Columns.Add("Preço");dt.Columns.Add("Estoque");
      foreach(var item in matches)dt.Rows.Add(item.Barcode,item.Name,item.Price.ToString("N2"),stocks[item.Id].ToString("N3"));
      list.DataSource=dt;
      list.Columns[1].FillWeight=260;
      var choose=new Button {Text="Adicionar selecionado",Dock=DockStyle.Bottom,Height=45,BackColor=Color.FromArgb(22,75,160),ForeColor=Color.White};
      choose.Click+=(s,e)=>{if(list.CurrentRow!=null){dialog.Tag=list.CurrentRow.Index;dialog.DialogResult=DialogResult.OK;}};
      list.CellDoubleClick+=(s,e)=>{if(e.RowIndex>=0){dialog.Tag=e.RowIndex;dialog.DialogResult=DialogResult.OK;}};
      dialog.Controls.Add(list);dialog.Controls.Add(choose);
      if(dialog.ShowDialog(this)!=DialogResult.OK||!(dialog.Tag is int))return;
      selected=matches[(int)dialog.Tag];
     }
    }
    decimal quantity=qty.Value;
    var existing=cart.FirstOrDefault(x=>x.Id==selected.Id);
    if(stocks[selected.Id]<quantity+(existing==null?0:existing.Qty)){MessageBox.Show("Estoque insuficiente. Disponível: "+stocks[selected.Id].ToString("N3"));return;}
    if(existing!=null)existing.Qty+=quantity;
    else {selected.Qty=quantity;cart.Add(selected);}
    code.Clear();qty.Value=1;Render();code.Focus();
   }catch(Exception ex){MessageBox.Show(ex.Message,"Erro ao adicionar");}
  }
  void Render() {
   var dt=new DataTable();dt.Columns.Add("Codigo");dt.Columns.Add("Produto");dt.Columns.Add("Quantidade",typeof(decimal));dt.Columns.Add("Preco",typeof(decimal));dt.Columns.Add("Total",typeof(decimal));
   foreach(var i in cart)dt.Rows.Add(i.Barcode,i.Name,i.Qty,i.Price,i.Total);
   grid.DataSource=dt;total.Text="TOTAL: R$ "+cart.Sum(i=>i.Total).ToString("N2");
  }
  void EditSelected(){
   if(grid.CurrentRow==null||grid.CurrentRow.Index<0||grid.CurrentRow.Index>=cart.Count){MessageBox.Show("Selecione um produto do carrinho.");return;}
   var item=cart[grid.CurrentRow.Index];
   try{
    using(var dialog=new Form {Text="Editar item da venda",Width=450,Height=265,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false}){
     var nameLabel=new Label {Text=item.Name,Left=15,Top=15,Width=400,Height=35,Font=new Font("Segoe UI",11,FontStyle.Bold)};
     var quantityLabel=new Label {Text="Quantidade:",Left=15,Top=65,Width=125};
     var quantityInput=new NumericUpDown {Left=150,Top=60,Width=210,DecimalPlaces=3,Minimum=0.001M,Maximum=999999,Value=item.Qty};
     var priceLabel=new Label {Text="Preço unitário (R$):",Left=15,Top=105,Width=130};
     var priceInput=new NumericUpDown {Left=150,Top=100,Width=210,DecimalPlaces=2,Minimum=0,Maximum=99999999,Value=item.Price};
     var preview=new Label {Left=15,Top=145,Width=400,Height=26,Font=new Font("Segoe UI",11,FontStyle.Bold)};
     Action update=()=>preview.Text="Subtotal: R$ "+Math.Round(quantityInput.Value*priceInput.Value,2).ToString("N2");
     quantityInput.ValueChanged+=(s,e)=>update();priceInput.ValueChanged+=(s,e)=>update();update();
     var save=new Button {Text="Aplicar alterações",Left=205,Top=180,Width=155,Height=34,DialogResult=DialogResult.OK};
     dialog.Controls.AddRange(new Control[]{nameLabel,quantityLabel,quantityInput,priceLabel,priceInput,preview,save});
     dialog.AcceptButton=save;
     if(dialog.ShowDialog(this)!=DialogResult.OK)return;
     decimal available;
     using(var connection=Open())using(var cmd=new SQLiteCommand("SELECT stock FROM products WHERE id=@id",connection)){
      cmd.Parameters.AddWithValue("@id",item.Id);
      var value=cmd.ExecuteScalar();if(value==null||value==DBNull.Value)throw new Exception("Produto não encontrado no estoque.");
      available=Dec(value);
     }
     if(quantityInput.Value>available){MessageBox.Show("Estoque insuficiente. Disponível: "+available.ToString("N3"));return;}
     item.Qty=quantityInput.Value;item.Price=priceInput.Value;
     Render();
    }
   }catch(Exception ex){MessageBox.Show(ex.Message,"Não foi possível editar o item");}
  }
  void RemoveSelected(){if(grid.CurrentRow==null)return;int n=grid.CurrentRow.Index;if(n>=0&&n<cart.Count){cart.RemoveAt(n);Render();}}
  static void RequireColumns(SQLiteConnection conn,string table,params string[] required) {
   var existing=new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
   using(var cmd=new SQLiteCommand("PRAGMA table_info("+table+")",conn))
   using(var reader=cmd.ExecuteReader())while(reader.Read())existing.Add(Convert.ToString(reader["name"]));
   foreach(var column in required)if(!existing.Contains(column))
    throw new Exception("Banco incompatível para gravar venda: coluna "+table+"."+column+" ausente. Nenhum registro foi alterado.");
  }
  void ValidateSaleSchema(SQLiteConnection conn,string method) {
   RequireColumns(conn,"sales","id","customer_name","payment_method","gross_total","total_cost","net_profit","profit_reserve_10","created_at");
   RequireColumns(conn,"sale_items","sale_id","product_id","barcode","name","quantity","unit_price","unit_cost","total");
   RequireColumns(conn,"products","id","stock");
   RequireColumns(conn,"stock_audit_log","product_id","product_name","barcode","event_type","quantity_before","quantity_after","quantity_delta","reference_type","reference_id","reason");
   if(method!="FIADO")RequireColumns(conn,"sale_payments","sale_id","payment_method","amount");
  }
  void Finish(){
   if(cart.Count==0){MessageBox.Show("Carrinho vazio.");return;}
   var method=Convert.ToString(payment.SelectedItem);if(method=="FIADO"&&string.IsNullOrWhiteSpace(customer.Text)){MessageBox.Show("Informe o cliente para venda fiado.");return;}
   var sum=cart.Sum(i=>i.Total);if(method=="DINHEIRO"&&received.Value<sum){MessageBox.Show("Valor recebido menor que o total.");return;}
   if(MessageBox.Show("Finalizar venda de R$ "+sum.ToString("N2")+"?","Confirmar",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
   try {
    using(var c=Open()){
     ValidateSaleSchema(c,method);
     using(var tx=c.BeginTransaction()) {
     long saleId;
     using(var cmd=new SQLiteCommand("INSERT INTO sales(customer_name,payment_method,gross_total,total_cost,net_profit,profit_reserve_10,created_at) VALUES(@customer,@method,@total,@cost,@profit,@reserve,datetime('now','localtime')); SELECT last_insert_rowid();",c,tx)){
      var cost=cart.Sum(i=>i.Cost*i.Qty);var profit=sum-cost;
      cmd.Parameters.AddWithValue("@customer",customer.Text.Trim());cmd.Parameters.AddWithValue("@method",method);cmd.Parameters.AddWithValue("@total",sum);cmd.Parameters.AddWithValue("@cost",cost);cmd.Parameters.AddWithValue("@profit",profit);cmd.Parameters.AddWithValue("@reserve",Math.Round(profit*0.10M,2));cmd.Parameters.AddWithValue("@received",method=="DINHEIRO"?received.Value:sum);cmd.Parameters.AddWithValue("@change",method=="DINHEIRO"?received.Value-sum:0);cmd.Parameters.AddWithValue("@status",method=="FIADO"?"FIADO":"CONCLUIDA");saleId=Convert.ToInt64(cmd.ExecuteScalar());
     }
     foreach(var i in cart) {
      decimal before;
      using(var q=new SQLiteCommand("SELECT stock FROM products WHERE id=@id",c,tx)){q.Parameters.AddWithValue("@id",i.Id);before=Dec(q.ExecuteScalar());}
      if(before<i.Qty)throw new Exception("Estoque insuficiente: "+i.Name+". Venda cancelada sem alteracoes.");
      using(var q=new SQLiteCommand("UPDATE products SET stock=stock-@qty WHERE id=@id AND stock>=@qty",c,tx)){q.Parameters.AddWithValue("@qty",i.Qty);q.Parameters.AddWithValue("@id",i.Id);if(q.ExecuteNonQuery()!=1)throw new Exception("Falha na baixa de estoque: "+i.Name);}
      using(var q=new SQLiteCommand("INSERT INTO sale_items(sale_id,product_id,barcode,name,quantity,unit_price,unit_cost,total) VALUES(@sale,@id,@barcode,@name,@qty,@price,@cost,@total)",c,tx)){
       q.Parameters.AddWithValue("@sale",saleId);q.Parameters.AddWithValue("@id",i.Id);q.Parameters.AddWithValue("@barcode",i.Barcode);q.Parameters.AddWithValue("@name",i.Name);q.Parameters.AddWithValue("@qty",i.Qty);q.Parameters.AddWithValue("@price",i.Price);q.Parameters.AddWithValue("@cost",i.Cost);q.Parameters.AddWithValue("@total",i.Total);q.ExecuteNonQuery();
      }
      using(var q=new SQLiteCommand("INSERT INTO stock_audit_log(product_id,product_name,barcode,event_type,quantity_before,quantity_after,quantity_delta,reference_type,reference_id,reason) VALUES(@id,@name,@barcode,'VENDA',@before,@after,@delta,'SALE',@sale,'Baixa de estoque no PDV')",c,tx)){
       q.Parameters.AddWithValue("@id",i.Id);q.Parameters.AddWithValue("@name",i.Name);q.Parameters.AddWithValue("@barcode",i.Barcode);q.Parameters.AddWithValue("@before",before);q.Parameters.AddWithValue("@after",before-i.Qty);q.Parameters.AddWithValue("@delta",-i.Qty);q.Parameters.AddWithValue("@sale",saleId);q.ExecuteNonQuery();
      }
     }
     if(method!="FIADO")using(var q=new SQLiteCommand("INSERT INTO sale_payments(sale_id,payment_method,amount) VALUES(@sale,@method,@total)",c,tx)){q.Parameters.AddWithValue("@sale",saleId);q.Parameters.AddWithValue("@method",method);q.Parameters.AddWithValue("@total",sum);q.ExecuteNonQuery();}
     tx.Commit();
     MessageBox.Show("Venda #"+saleId+" registrada. Estoque atualizado.");cart.Clear();received.Value=0;customer.Clear();Render();code.Focus();
    }
    }
   }catch(Exception ex){MessageBox.Show(ex.Message,"Venda nao finalizada",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  }
 }
}