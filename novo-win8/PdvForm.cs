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
   var remove=new Button {Text="Remover item",AutoSize=true,Height=40};remove.Click+=(s,e)=>RemoveSelected();footer.Controls.Add(remove);footer.Controls.Add(total);
   Controls.Add(grid);Controls.Add(footer);Controls.Add(header);Render();code.Focus();
  }
  SQLiteConnection Open() {var c=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=db,FailIfMissing=true}.ToString());c.Open();return c;}
  static decimal Dec(object o) {return Convert.ToDecimal(o,CultureInfo.InvariantCulture);}
  void AddProduct() {
   string search=code.Text.Trim();if(search.Length==0)return;
   try {
    using(var c=Open())using(var cmd=new SQLiteCommand("SELECT id,barcode,name,price,cost,stock FROM products WHERE barcode=@code OR internal_code=@code OR name LIKE @name ORDER BY CASE WHEN barcode=@code THEN 0 ELSE 1 END LIMIT 1",c)) {
     cmd.Parameters.AddWithValue("@code",search);cmd.Parameters.AddWithValue("@name","%"+search+"%");
     using(var r=cmd.ExecuteReader()) {
      if(!r.Read()) {MessageBox.Show("Produto nao cadastrado.");return;}
      var id=Convert.ToInt64(r["id"]);var stock=Dec(r["stock"]);var quantity=qty.Value;
      var existing=cart.FirstOrDefault(x=>x.Id==id);
      if(stock < quantity+(existing==null?0:existing.Qty)) {MessageBox.Show("Estoque insuficiente. Disponivel: "+stock);return;}
      if(existing!=null)existing.Qty+=quantity;
      else cart.Add(new Item {Id=id,Barcode=Convert.ToString(r["barcode"]),Name=Convert.ToString(r["name"]),Qty=quantity,Price=Dec(r["price"]),Cost=Dec(r["cost"])});
     }
    }
    code.Clear();qty.Value=1;Render();code.Focus();
   } catch(Exception ex) {MessageBox.Show(ex.Message,"Erro ao adicionar");}
  }
  void Render() {
   var dt=new DataTable();dt.Columns.Add("Codigo");dt.Columns.Add("Produto");dt.Columns.Add("Quantidade",typeof(decimal));dt.Columns.Add("Preco",typeof(decimal));dt.Columns.Add("Total",typeof(decimal));
   foreach(var i in cart)dt.Rows.Add(i.Barcode,i.Name,i.Qty,i.Price,i.Total);
   grid.DataSource=dt;total.Text="TOTAL: R$ "+cart.Sum(i=>i.Total).ToString("N2");
  }
  void RemoveSelected(){if(grid.CurrentRow==null)return;int n=grid.CurrentRow.Index;if(n>=0&&n<cart.Count){cart.RemoveAt(n);Render();}}
  void Finish(){
   if(cart.Count==0){MessageBox.Show("Carrinho vazio.");return;}
   var method=Convert.ToString(payment.SelectedItem);if(method=="FIADO"&&string.IsNullOrWhiteSpace(customer.Text)){MessageBox.Show("Informe o cliente para venda fiado.");return;}
   var sum=cart.Sum(i=>i.Total);if(method=="DINHEIRO"&&received.Value<sum){MessageBox.Show("Valor recebido menor que o total.");return;}
   if(MessageBox.Show("Finalizar venda de R$ "+sum.ToString("N2")+"?","Confirmar",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
   try {
    using(var c=Open())using(var tx=c.BeginTransaction()) {
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
   }catch(Exception ex){MessageBox.Show(ex.Message,"Venda nao finalizada",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  }
 }
}