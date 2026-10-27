using System;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
namespace ArmazemGratidao {
 public sealed class FiadoClientesForm:Form {
  readonly string db;
  readonly FlowLayoutPanel clients=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(8)};
  readonly DataGridView openSales=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
  readonly DataGridView paidSales=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
  readonly Label selected=new Label {Dock=DockStyle.Top,Height=42,Font=new Font("Segoe UI",14,FontStyle.Bold)};
  readonly NumericUpDown amount=new NumericUpDown {DecimalPlaces=2,Maximum=100000000,Width=130};
  readonly ComboBox method=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=160};
  string customer="";
  public FiadoClientesForm(string database) {
   db=database;Text="Armazem Gratidao | Fiado por Cliente";Width=1250;Height=790;
   var split=new SplitContainer {Dock=DockStyle.Fill,SplitterDistance=265};
   split.Panel1.Controls.Add(clients);
   var tabs=new TabControl {Dock=DockStyle.Fill};
   var pending=new TabPage("Vendas em aberto");pending.Controls.Add(openSales);
   var previous=new TabPage("Vendas anteriores / Pagas");previous.Controls.Add(paidSales);
   tabs.TabPages.Add(pending);tabs.TabPages.Add(previous);
   var bottom=new FlowLayoutPanel {Dock=DockStyle.Bottom,Height=66,Padding=new Padding(8),BackColor=Color.FromArgb(235,241,250)};
   bottom.Controls.Add(new Label {Text="Pagamento:",AutoSize=true,Padding=new Padding(0,9,0,0)});
   method.Items.AddRange(new object[]{"DINHEIRO","PIX","PIX QR CODE","DEBITO","CREDITO","ALIMENTACAO","ALELO","PLUXXE","TICKET","VR"});method.SelectedIndex=0;bottom.Controls.Add(method);
   bottom.Controls.Add(new Label {Text="Valor R$:",AutoSize=true,Padding=new Padding(0,9,0,0)});bottom.Controls.Add(amount);
   var pay=new Button {Text="Registrar baixa",AutoSize=true,Height=36,BackColor=Color.FromArgb(24,82,170),ForeColor=Color.White};pay.Click+=(s,e)=>Pay();bottom.Controls.Add(pay);
   var refresh=new Button {Text="Atualizar",AutoSize=true,Height=36};refresh.Click+=(s,e)=>LoadClients();bottom.Controls.Add(refresh);
   split.Panel2.Controls.Add(tabs);split.Panel2.Controls.Add(bottom);split.Panel2.Controls.Add(selected);
   Controls.Add(split);LoadClients();
  }
  SQLiteConnection Open(bool readOnly=false) {var c=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=db,ReadOnly=readOnly,FailIfMissing=true}.ToString());c.Open();return c;}
  DataTable Query(string sql,string p=null,object value=null) {
   using(var c=Open(true))using(var cmd=new SQLiteCommand(sql,c)) {
    if(p!=null)cmd.Parameters.AddWithValue(p,value);
    var dt=new DataTable();using(var a=new SQLiteDataAdapter(cmd))a.Fill(dt);return dt;
   }
  }
  const string CreditFilter="(UPPER(COALESCE(payment_method,'')) LIKE '%FIADO%' OR UPPER(COALESCE(status,'')) LIKE '%FIADO%')";
  const string Balance="COALESCE(s.gross_total,0)-COALESCE((SELECT SUM(p.amount) FROM credit_payments p WHERE p.sale_id=s.id),0)";
  void LoadClients(){
   try {
    var dt=Query("SELECT COALESCE(NULLIF(TRIM(s.customer_name),''),'SEM CLIENTE') AS cliente,COUNT(*) AS vendas,SUM(CASE WHEN "+Balance+">0.005 THEN "+Balance+" ELSE 0 END) AS saldo FROM sales s WHERE "+CreditFilter+" GROUP BY COALESCE(NULLIF(TRIM(s.customer_name),''),'SEM CLIENTE') ORDER BY cliente");
    clients.SuspendLayout();clients.Controls.Clear();
    foreach(DataRow r in dt.Rows) {
     var name=Convert.ToString(r["cliente"]);var balance=Convert.ToDecimal(r["saldo"]);
     var b=new Button {Text=name+"  |  R$ "+balance.ToString("N2"),Width=225,Height=52,TextAlign=ContentAlignment.MiddleLeft,Font=new Font("Segoe UI",9,FontStyle.Bold),BackColor=Color.White,ForeColor=Color.FromArgb(25,75,150),Tag=name};
     b.Click+=(s,e)=>SelectClient(Convert.ToString(((Button)s).Tag));clients.Controls.Add(b);
    }
    clients.ResumeLayout();
    if(customer.Length>0)SelectClient(customer);
   }catch(Exception ex){MessageBox.Show(ex.Message,"Erro ao listar clientes");}
  }
  void SelectClient(string name){
   customer=name;selected.Text="FIADO | "+customer;
   try {
    var sql="SELECT s.id AS venda,s.created_at AS data,s.gross_total AS valor,COALESCE((SELECT SUM(p.amount) FROM credit_payments p WHERE p.sale_id=s.id),0) AS pago,"+Balance+" AS saldo FROM sales s WHERE "+CreditFilter+" AND COALESCE(NULLIF(TRIM(s.customer_name),''),'SEM CLIENTE')=@name";
    openSales.DataSource=Query(sql+" AND "+Balance+">0.005 ORDER BY s.id DESC","@name",name);
    paidSales.DataSource=Query(sql+" AND "+Balance+"<=0.005 ORDER BY s.id DESC","@name",name);
    var dt=(DataTable)openSales.DataSource;decimal total=0;foreach(DataRow r in dt.Rows)total+=Convert.ToDecimal(r["saldo"]);
    selected.Text="FIADO | "+name+" | Saldo em aberto R$ "+total.ToString("N2");
   }catch(Exception ex){MessageBox.Show(ex.Message,"Erro ao abrir fiado");}
  }
  void Pay(){
   if(openSales.CurrentRow==null){MessageBox.Show("Selecione uma venda em aberto.");return;}
   if(amount.Value<=0){MessageBox.Show("Informe o valor recebido.");return;}
   long saleId=Convert.ToInt64(openSales.CurrentRow.Cells["venda"].Value);
   decimal balance=Convert.ToDecimal(openSales.CurrentRow.Cells["saldo"].Value);
   if(amount.Value>balance+0.005M){MessageBox.Show("O pagamento nao pode ultrapassar o saldo da venda.");return;}
   if(MessageBox.Show("Registrar recebimento de R$ "+amount.Value.ToString("N2")+" na venda #"+saleId+"?","Confirmar baixa",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
   try {
    using(var c=Open())using(var tx=c.BeginTransaction()) {
     decimal actual;
     using(var q=new SQLiteCommand("SELECT "+Balance+" FROM sales s WHERE s.id=@id AND "+CreditFilter+" AND COALESCE(NULLIF(TRIM(s.customer_name),''),'SEM CLIENTE')=@name",c,tx)) {
      q.Parameters.AddWithValue("@id",saleId);q.Parameters.AddWithValue("@name",customer);
      var obj=q.ExecuteScalar();if(obj==null||obj==DBNull.Value)throw new Exception("Venda ou cliente nao encontrado.");
      actual=Convert.ToDecimal(obj);
     }
     if(amount.Value>actual+0.005M||actual<=0)throw new Exception("Saldo alterado. Atualize a tela antes de pagar.");
     using(var q=new SQLiteCommand("INSERT INTO credit_payments(sale_id,amount,payment_method,paid_at,note) VALUES(@sale,@amount,@method,datetime('now','localtime'),'Recebimento registrado no novo Fiado')",c,tx)) {
      q.Parameters.AddWithValue("@sale",saleId);q.Parameters.AddWithValue("@amount",amount.Value);q.Parameters.AddWithValue("@method",Convert.ToString(method.SelectedItem));q.ExecuteNonQuery();
     }
     if(actual-amount.Value<=0.005M)using(var q=new SQLiteCommand("UPDATE sales SET settled_at=datetime('now','localtime'),settlement_method=@method WHERE id=@id",c,tx)){
      q.Parameters.AddWithValue("@method",Convert.ToString(method.SelectedItem));q.Parameters.AddWithValue("@id",saleId);q.ExecuteNonQuery();
     }
     tx.Commit();
    }
    amount.Value=0;SelectClient(customer);MessageBox.Show("Pagamento registrado. Saldo atualizado.");LoadClients();
   }catch(Exception ex){MessageBox.Show(ex.Message,"Baixa nao realizada",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  }
 }
}