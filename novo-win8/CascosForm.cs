using System;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ArmazemGratidao {
 public sealed class CascosForm : Form {
  const decimal Deposit=4m;
  readonly string db;
  readonly DataGridView grid=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,RowHeadersVisible=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
  readonly TextBox customer=new TextBox {Width=230};
  readonly TextBox filter=new TextBox {Width=190};
  readonly NumericUpDown quantity=new NumericUpDown {Minimum=1,Maximum=9999,Value=1,Width=75};
  readonly Label summary=new Label {Dock=DockStyle.Bottom,Height=48,Font=new Font("Segoe UI",10,FontStyle.Bold),Padding=new Padding(12,10,0,0)};
  public CascosForm(string database){
   db=database;Text="Cascos retornáveis — controle separado de vendas";Width=1050;Height=650;MinimumSize=new Size(760,480);
   var bar=new FlowLayoutPanel {Dock=DockStyle.Top,Height=95,Padding=new Padding(10),BackColor=Color.FromArgb(229,242,255)};
   bar.Controls.Add(new Label {Text="Pessoa:",AutoSize=true,Padding=new Padding(0,8,0,0)});bar.Controls.Add(customer);
   bar.Controls.Add(new Label {Text="Qtd.:",AutoSize=true,Padding=new Padding(0,8,0,0)});bar.Controls.Add(quantity);
   var deliver=new Button {Text="Entregar casco (+ R$ 4)",Width=190,Height=35};deliver.Click+=(s,e)=>Deliver();bar.Controls.Add(deliver);
   var returned=new Button {Text="Receber e devolver R$ 4",Width=210,Height=35};returned.Click+=(s,e)=>Return();bar.Controls.Add(returned);
   var history=new Button {Text="Histórico de devoluções",Width=175,Height=35};history.Click+=(s,e)=>ShowReturns();bar.Controls.Add(history);
   bar.Controls.Add(new Label {Text="Buscar pessoa:",AutoSize=true,Padding=new Padding(0,8,0,0)});bar.Controls.Add(filter);
   filter.TextChanged+=(s,e)=>RefreshRows();
   var refresh=new Button {Text="Atualizar",Width=90,Height=32};refresh.Click+=(s,e)=>RefreshRows();bar.Controls.Add(refresh);
   grid.BackgroundColor=Color.White;grid.CellDoubleClick+=(s,e)=>{if(e.RowIndex>=0)customer.Text=Convert.ToString(grid.Rows[e.RowIndex].Cells["Pessoa"].Value);};
   Controls.Add(grid);Controls.Add(summary);Controls.Add(bar);
   try{EnsureSchema();RefreshRows();}catch(Exception ex){bar.Enabled=false;MessageBox.Show("Controle de cascos indisponível: "+ex.Message,"Cascos",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  }
  SQLiteConnection Open(){var c=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=db,FailIfMissing=true}.ToString());c.Open();return c;}
  void Backup(){
   string path=db+".antes-cascos-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+".db";
   using(var source=Open())using(var dest=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=path}.ToString())){
    dest.Open();source.BackupDatabase(dest,"main","main",-1,null,0);
    using(var check=new SQLiteCommand("PRAGMA integrity_check",dest))if(Convert.ToString(check.ExecuteScalar())!="ok")throw new Exception("Backup não passou na verificação de integridade.");
   }
   if(new FileInfo(path).Length==0)throw new Exception("Backup vazio; operação cancelada.");
  }
  void EnsureSchema(){
   using(var c=Open())using(var check=new SQLiteCommand("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='bottle_loans'",c)){
    if(Convert.ToInt32(check.ExecuteScalar())!=0)return;
   }
   Backup();
   using(var c=Open())using(var tx=c.BeginTransaction()){
    using(var cmd=new SQLiteCommand("CREATE TABLE IF NOT EXISTS bottle_loans(id INTEGER PRIMARY KEY AUTOINCREMENT, customer_name TEXT NOT NULL, quantity INTEGER NOT NULL CHECK(quantity>0), unit_deposit NUMERIC NOT NULL, delivered_at TEXT NOT NULL); CREATE TABLE IF NOT EXISTS bottle_returns(id INTEGER PRIMARY KEY AUTOINCREMENT,loan_id INTEGER NOT NULL,quantity INTEGER NOT NULL CHECK(quantity>0),amount NUMERIC NOT NULL,returned_at TEXT NOT NULL,refund_method TEXT NOT NULL,FOREIGN KEY(loan_id) REFERENCES bottle_loans(id));",c,tx))cmd.ExecuteNonQuery();
    tx.Commit();
   }
  }
  void Deliver(){
   string name=customer.Text.Trim();int count=(int)quantity.Value;
   if(name.Length<2){MessageBox.Show("Informe quem retirou o casco.");customer.Focus();return;}
   if(MessageBox.Show(name+" retirou "+count+" casco(s). Receber R$ "+(count*Deposit).ToString("N2")+" como caução?\nNão será registrado como venda.","Confirmar retirada",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
   try{Backup();using(var c=Open())using(var tx=c.BeginTransaction())using(var cmd=new SQLiteCommand("INSERT INTO bottle_loans(customer_name,quantity,unit_deposit,delivered_at) VALUES(@name,@qty,@unit,@date)",c,tx)){
    cmd.Parameters.AddWithValue("@name",name);cmd.Parameters.AddWithValue("@qty",count);cmd.Parameters.AddWithValue("@unit",Deposit);cmd.Parameters.AddWithValue("@date",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));cmd.ExecuteNonQuery();tx.Commit();
   }RefreshRows();}catch(Exception ex){MessageBox.Show("Retirada não registrada: "+ex.Message,"Cascos");}
  }
  void Return(){
   if(grid.CurrentRow==null){MessageBox.Show("Selecione a retirada da pessoa que está devolvendo.");return;}
   long id=Convert.ToInt64(grid.CurrentRow.Cells["ID"].Value);
   string name=Convert.ToString(grid.CurrentRow.Cells["Pessoa"].Value);
   int count=(int)quantity.Value;
   try{
    using(var c=Open())using(var cmd=new SQLiteCommand("SELECT l.quantity-COALESCE((SELECT SUM(r.quantity) FROM bottle_returns r WHERE r.loan_id=l.id),0) FROM bottle_loans l WHERE l.id=@id",c)){
     cmd.Parameters.AddWithValue("@id",id);object available=cmd.ExecuteScalar();if(available==null||count>Convert.ToInt32(available))throw new Exception("Quantidade maior que os cascos pendentes desta pessoa.");
    }
    if(MessageBox.Show("Receber "+count+" casco(s) de "+name+" e devolver R$ "+(count*Deposit).ToString("N2")+" em dinheiro?","Confirmar devolução",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
    Backup();
    using(var c=Open())using(var tx=c.BeginTransaction()){
     using(var cmd=new SQLiteCommand("INSERT INTO bottle_returns(loan_id,quantity,amount,returned_at,refund_method) SELECT id,@qty,@amount,@date,'DINHEIRO' FROM bottle_loans WHERE id=@id AND quantity-COALESCE((SELECT SUM(quantity) FROM bottle_returns WHERE loan_id=@id),0)>=@qty",c,tx)){
      cmd.Parameters.AddWithValue("@id",id);cmd.Parameters.AddWithValue("@qty",count);cmd.Parameters.AddWithValue("@amount",count*Deposit);cmd.Parameters.AddWithValue("@date",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
      if(cmd.ExecuteNonQuery()!=1)throw new Exception("Saldo mudou; devolução não registrada.");
     }tx.Commit();
    }RefreshRows();
   }catch(Exception ex){MessageBox.Show("Devolução não registrada: "+ex.Message,"Cascos");}
  }
  void ShowReturns(){
   if(grid.CurrentRow==null){MessageBox.Show("Selecione uma retirada.");return;}
   long id=Convert.ToInt64(grid.CurrentRow.Cells["ID"].Value);
   using(var dialog=new Form{Text="Devoluções da retirada #"+id,Width=660,Height=380,StartPosition=FormStartPosition.CenterParent}){
    var list=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
    using(var c=Open())using(var cmd=new SQLiteCommand("SELECT id AS ID,quantity AS Quantidade,amount AS Reembolso,refund_method AS Forma,returned_at AS Data_hora FROM bottle_returns WHERE loan_id=@id ORDER BY id",c)){
     cmd.Parameters.AddWithValue("@id",id);using(var adapter=new SQLiteDataAdapter(cmd)){var data=new DataTable();adapter.Fill(data);list.DataSource=data;}
    }
    dialog.Controls.Add(list);dialog.ShowDialog(this);
   }
  }
  void RefreshRows(){
   try{using(var c=Open())using(var cmd=new SQLiteCommand("SELECT l.id AS ID,l.customer_name AS Pessoa,l.quantity AS Retirados,COALESCE(SUM(r.quantity),0) AS Devolvidos,l.quantity-COALESCE(SUM(r.quantity),0) AS Pendentes,l.delivered_at AS Retirada,MAX(r.returned_at) AS Ultima_devolucao,l.unit_deposit AS Caucao_unitaria FROM bottle_loans l LEFT JOIN bottle_returns r ON r.loan_id=l.id WHERE l.customer_name LIKE @name GROUP BY l.id ORDER BY l.id DESC LIMIT 2000",c)){
    cmd.Parameters.AddWithValue("@name","%"+filter.Text.Trim()+"%");using(var adapter=new SQLiteDataAdapter(cmd)){var data=new DataTable();adapter.Fill(data);grid.DataSource=data;}
    using(var totals=new SQLiteCommand("SELECT COALESCE((SELECT SUM(quantity*unit_deposit) FROM bottle_loans),0),COALESCE((SELECT SUM(amount) FROM bottle_returns),0),COALESCE((SELECT SUM(quantity) FROM bottle_loans),0)-COALESCE((SELECT SUM(quantity) FROM bottle_returns),0)",c))using(var reader=totals.ExecuteReader())if(reader.Read())summary.Text="Cauções recebidas: R$ "+Convert.ToDecimal(reader[0]).ToString("N2")+"  |  Devolvidas: R$ "+Convert.ToDecimal(reader[1]).ToString("N2")+"  |  Cascos pendentes: "+reader[2]+"  (fora das vendas)";
   }}catch(Exception ex){summary.Text="Falha ao consultar cascos: "+ex.Message;}
  }
 }
}
