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
   var clients=new Button{Text="Clientes",Width=115,Height=35,BackColor=Color.FromArgb(22,75,160),ForeColor=Color.White};
   clients.Click+=(sender,args)=>SelectCustomer();
   bar.Controls.Add(clients);
   bar.Controls.Add(new Label {Text="Qtd.:",AutoSize=true,Padding=new Padding(0,8,0,0)});bar.Controls.Add(quantity);
   var deliver=new Button {Text="Entregar casco (+ R$ 4)",Width=190,Height=35};deliver.Click+=(s,e)=>Deliver();bar.Controls.Add(deliver);
   var returned=new Button {Text="Receber e devolver R$ 4",Width=210,Height=35};returned.Click+=(s,e)=>Return();bar.Controls.Add(returned);
   var history=new Button {Text="Histórico de devoluções",Width=175,Height=35};history.Click+=(s,e)=>ShowReturns();bar.Controls.Add(history);
   bar.Controls.Add(new Label {Text="Buscar pessoa:",AutoSize=true,Padding=new Padding(0,8,0,0)});bar.Controls.Add(filter);
   filter.TextChanged+=(s,e)=>RefreshRows();
   var refresh=new Button {Text="Atualizar",Width=90,Height=32};refresh.Click+=(s,e)=>RefreshRows();bar.Controls.Add(refresh);
   grid.BackgroundColor=Color.White;grid.CellDoubleClick+=(s,e)=>{if(e.RowIndex>=0)customer.Text=Convert.ToString(grid.Rows[e.RowIndex].Cells["Pessoa"].Value);};
   // Estrutura unica no estilo de Vendas: marca no topo, barra de acoes,
   // tabela e rodape. A altura da barra nao cobre os totais.
   var layout=new TableLayoutPanel{Name="amzgCascosLayout",Dock=DockStyle.Fill,
    ColumnCount=1,RowCount=4,Padding=new Padding(7),Margin=Padding.Empty,
    BackColor=Color.FromArgb(245,249,254)};
   layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   layout.RowStyles.Add(new RowStyle(SizeType.Absolute,54));
   layout.RowStyles.Add(new RowStyle(SizeType.Absolute,109));
   layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   layout.RowStyles.Add(new RowStyle(SizeType.Absolute,53));
   var head=new Panel{Name="amzgCascosHeader",Dock=DockStyle.Fill,
    BackColor=Color.FromArgb(14,56,128),Margin=Padding.Empty};
   var emblem=new PictureBox{Name="amzgCascosLogo",Left=8,Top=4,
    Width=44,Height=44,SizeMode=PictureBoxSizeMode.Zoom,
    BackColor=Color.Transparent};
   try{
    using(var resource=typeof(CascosForm).Assembly.GetManifestResourceStream(
     "ArmazemGratidao.LogotipoMarcaDagua.jpg")){
     if(resource!=null)using(var image=Image.FromStream(resource))
      emblem.Image=new Bitmap(image);
    }
   }catch(Exception){}
   emblem.Disposed+=(sender,args)=>{if(emblem.Image!=null)emblem.Image.Dispose();};
   var brand=new Label{Name="amzgCascosTitle",
    Text="ARMAZÉM GRATIDÃO   •   CASCOS RETORNÁVEIS",
    Left=63,Top=7,Width=880,Height=41,
    Font=new Font("Segoe UI",15f,FontStyle.Bold),
    ForeColor=Color.White,TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true};
   head.Resize+=(sender,args)=>brand.Width=Math.Max(80,head.ClientSize.Width-74);
   head.Controls.Add(emblem);head.Controls.Add(brand);
   bar.Dock=DockStyle.Fill;bar.Margin=new Padding(0,5,0,4);
   bar.AutoScroll=true;bar.WrapContents=true;bar.BackColor=Color.FromArgb(231,240,252);
   grid.Dock=DockStyle.Fill;grid.Margin=Padding.Empty;
   summary.Dock=DockStyle.Fill;summary.Margin=new Padding(0,3,0,0);
   summary.BackColor=Color.FromArgb(223,235,253);
   summary.ForeColor=Color.FromArgb(17,63,134);
   layout.Controls.Add(head,0,0);
   layout.Controls.Add(bar,0,1);
   layout.Controls.Add(grid,0,2);
   layout.Controls.Add(summary,0,3);
   Controls.Add(layout);
   try{EnsureSchema();RefreshRows();}catch(Exception ex){bar.Enabled=false;MessageBox.Show("Controle de cascos indisponível: "+ex.Message,"Cascos",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  }
  // Busca clientes cadastrados sem modificar cadastros, fiado ou vendas.
  // Inclui nomes do controle de cascos para preservar o acesso aos registros antigos.
  void SelectCustomer(){
   try{
    using(var dialog=new Form{Text="Clientes | Controle de Cascos",Width=750,Height=550,MinimumSize=new Size(540,380),StartPosition=FormStartPosition.CenterParent,BackColor=Color.FromArgb(229,242,255)}){
     var search=new TextBox{Dock=DockStyle.Top,Font=new Font("Segoe UI",12),Height=38};
     var heading=new Label{Text="Pesquisar cliente cadastrado por nome ou documento:",Dock=DockStyle.Top,Height=36,Padding=new Padding(9,8,0,0),Font=new Font("Segoe UI",10,FontStyle.Bold)};
     var listing=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,RowHeadersVisible=false,MultiSelect=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,BackgroundColor=Color.White};
     var actionBar=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=55,Padding=new Padding(8),BackColor=Color.White};
     var choose=new Button{Text="Usar cliente selecionado",Width=220,Height=36,BackColor=Color.FromArgb(22,75,160),ForeColor=Color.White};
     var cancel=new Button{Text="Cancelar",Width=120,Height=36,DialogResult=DialogResult.Cancel};
     actionBar.Controls.Add(choose);actionBar.Controls.Add(cancel);
     var table=new DataTable();
     table.Columns.Add("Nome");table.Columns.Add("Telefone");table.Columns.Add("CPF / CNPJ");table.Columns.Add("Origem");
     using(var cn=Open()){
      var hasCustomers=new SQLiteCommand("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='customers'",cn);
      if(Convert.ToInt32(hasCustomers.ExecuteScalar())>0){
       using(var cmd=new SQLiteCommand("SELECT name,COALESCE(phone,''),COALESCE(document,'') FROM customers WHERE TRIM(COALESCE(name,''))<>'' ORDER BY name COLLATE NOCASE",cn))
       using(var rd=cmd.ExecuteReader())while(rd.Read())table.Rows.Add(Convert.ToString(rd[0]),Convert.ToString(rd[1]),Convert.ToString(rd[2]),"Cadastro");
      }
      using(var cmd=new SQLiteCommand("SELECT DISTINCT customer_name FROM bottle_loans WHERE TRIM(COALESCE(customer_name,''))<>'' ORDER BY customer_name COLLATE NOCASE",cn))
      using(var rd=cmd.ExecuteReader())while(rd.Read()){
       string name=Convert.ToString(rd[0]);bool found=false;
       foreach(DataRow row in table.Rows)if(string.Equals(Convert.ToString(row["Nome"]),name,StringComparison.OrdinalIgnoreCase)){found=true;break;}
       if(!found)table.Rows.Add(name,"","","Cascos anteriores");
      }
     }
     var view=new DataView(table);
     listing.DataSource=view;
     Action applySearch=()=>{
      string term=search.Text.Trim().Replace("'","''").Replace("[","[[]").Replace("%","[%]").Replace("*","[*]");
      view.RowFilter=term.Length==0?"":"[Nome] LIKE '%"+term+"%' OR [CPF / CNPJ] LIKE '%"+term+"%' OR [Telefone] LIKE '%"+term+"%'";
     };
     search.TextChanged+=(sender,args)=>applySearch();
     Action select=()=>{
      if(listing.CurrentRow==null)return;
      var row=listing.CurrentRow.DataBoundItem as DataRowView;
      if(row==null)return;
      dialog.Tag=Convert.ToString(row["Nome"]);dialog.DialogResult=DialogResult.OK;dialog.Close();
     };
     choose.Click+=(sender,args)=>select();
     listing.CellDoubleClick+=(sender,args)=>{if(args.RowIndex>=0)select();};
     dialog.AcceptButton=choose;dialog.CancelButton=cancel;
     dialog.Controls.Add(listing);dialog.Controls.Add(actionBar);dialog.Controls.Add(search);dialog.Controls.Add(heading);
     dialog.Shown+=(sender,args)=>search.Focus();
     if(dialog.ShowDialog(this)==DialogResult.OK&&!string.IsNullOrWhiteSpace(Convert.ToString(dialog.Tag))){
      customer.Text=Convert.ToString(dialog.Tag);
      filter.Text=customer.Text;
      RefreshRows();
      customer.Focus();
     }
    }
   }catch(Exception ex){MessageBox.Show("Nao foi possivel consultar os clientes: "+ex.Message,"Cascos",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
  }
  SQLiteConnection Open(){var c=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=db,FailIfMissing=true}.ToString());c.Open();return c;}
  void Backup(){
   string baseDir=Path.GetDirectoryName(db);
   string config=Path.Combine(baseDir,"backup-location.txt");
   string backupDir=File.Exists(config)?File.ReadAllText(config).Trim():Path.Combine(baseDir,"Backups");
   if(string.IsNullOrWhiteSpace(backupDir)||!Path.IsPathRooted(backupDir))throw new Exception("A pasta de backup configurada e invalida.");
   Directory.CreateDirectory(backupDir);
   string path=Path.Combine(backupDir,"antes-cascos-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+".db");
   using(var source=Open())using(var dest=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=path}.ToString())){
    dest.Open();source.BackupDatabase(dest,"main","main",-1,null,0);
    using(var check=new SQLiteCommand("PRAGMA integrity_check",dest))if(Convert.ToString(check.ExecuteScalar())!="ok")throw new Exception("Backup não passou na verificação de integridade.");
   }
   if(new FileInfo(path).Length==0)throw new Exception("Backup vazio; operação cancelada.");
  }
  public static void EnsureSaleLink(string database){
   using(var c=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=database,FailIfMissing=true}.ToString())){
    c.Open();
    bool loans=false,returns=false,linked=false,paid=false;
    using(var check=new SQLiteCommand("SELECT name FROM sqlite_master WHERE type='table' AND name IN ('bottle_loans','bottle_returns')",c))using(var rd=check.ExecuteReader())while(rd.Read()){if(Convert.ToString(rd[0])=="bottle_loans")loans=true;else returns=true;}
    if(loans)using(var info=new SQLiteCommand("PRAGMA table_info(bottle_loans)",c))using(var rd=info.ExecuteReader())while(rd.Read()){if(Convert.ToString(rd["name"])=="sale_id")linked=true;if(Convert.ToString(rd["name"])=="paid_deposit")paid=true;}
    if(loans&&returns&&linked&&paid)return;
    string path=database+".antes-cascos-pdv-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+".db";
    using(var dest=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=path}.ToString())){
     dest.Open();c.BackupDatabase(dest,"main","main",-1,null,0);
     using(var check=new SQLiteCommand("PRAGMA integrity_check",dest))if(Convert.ToString(check.ExecuteScalar())!="ok")throw new Exception("Backup de cascos inválido; venda não iniciada.");
    }
    if(new FileInfo(path).Length==0)throw new Exception("Backup de cascos vazio; venda não iniciada.");
    using(var tx=c.BeginTransaction()){
     using(var cmd=new SQLiteCommand("CREATE TABLE IF NOT EXISTS bottle_loans(id INTEGER PRIMARY KEY AUTOINCREMENT,customer_name TEXT NOT NULL,quantity INTEGER NOT NULL CHECK(quantity>0),unit_deposit NUMERIC NOT NULL,delivered_at TEXT NOT NULL); CREATE TABLE IF NOT EXISTS bottle_returns(id INTEGER PRIMARY KEY AUTOINCREMENT,loan_id INTEGER NOT NULL,quantity INTEGER NOT NULL CHECK(quantity>0),amount NUMERIC NOT NULL,returned_at TEXT NOT NULL,refund_method TEXT NOT NULL,FOREIGN KEY(loan_id) REFERENCES bottle_loans(id));",c,tx))cmd.ExecuteNonQuery();
     if(!linked)using(var cmd=new SQLiteCommand("ALTER TABLE bottle_loans ADD COLUMN sale_id INTEGER",c,tx))cmd.ExecuteNonQuery();
     if(!paid){using(var cmd=new SQLiteCommand("ALTER TABLE bottle_loans ADD COLUMN paid_deposit NUMERIC NOT NULL DEFAULT 0",c,tx))cmd.ExecuteNonQuery();if(loans)using(var cmd=new SQLiteCommand("UPDATE bottle_loans SET paid_deposit=quantity*unit_deposit WHERE sale_id IS NULL",c,tx))cmd.ExecuteNonQuery();}
     tx.Commit();
    }
   }
  }
  public static void RecordFromSale(SQLiteConnection c,SQLiteTransaction tx,long saleId,string name,int count,bool paid,string deliveredAt){
   using(var cmd=new SQLiteCommand("INSERT INTO bottle_loans(customer_name,quantity,unit_deposit,delivered_at,sale_id,paid_deposit) VALUES(@name,@qty,4,@date,@sale,@paid)",c,tx)){
    cmd.Parameters.AddWithValue("@name",name);cmd.Parameters.AddWithValue("@qty",count);cmd.Parameters.AddWithValue("@date",deliveredAt);cmd.Parameters.AddWithValue("@sale",saleId);cmd.Parameters.AddWithValue("@paid",paid?count*Deposit:0m);
    if(cmd.ExecuteNonQuery()!=1)throw new Exception("Casco não registrado; venda revertida.");
   }
  }
  void EnsureSchema(){
   EnsureSaleLink(db);
  }
  void Deliver(){
   string name=customer.Text.Trim();int count=(int)quantity.Value;
   if(name.Length<2){MessageBox.Show("Informe quem retirou o casco.");customer.Focus();return;}
   if(MessageBox.Show(name+" retirou "+count+" casco(s). Receber R$ "+(count*Deposit).ToString("N2")+" como caução?\nNão será registrado como venda.","Confirmar retirada",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
   try{Backup();using(var c=Open())using(var tx=c.BeginTransaction())using(var cmd=new SQLiteCommand("INSERT INTO bottle_loans(customer_name,quantity,unit_deposit,delivered_at,paid_deposit) VALUES(@name,@qty,@unit,@date,@paid)",c,tx)){
    cmd.Parameters.AddWithValue("@name",name);cmd.Parameters.AddWithValue("@qty",count);cmd.Parameters.AddWithValue("@unit",Deposit);cmd.Parameters.AddWithValue("@paid",count*Deposit);cmd.Parameters.AddWithValue("@date",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));cmd.ExecuteNonQuery();tx.Commit();
   }RefreshRows();}catch(Exception ex){MessageBox.Show("Retirada não registrada: "+ex.Message,"Cascos");}
  }
  void Return(){
   if(grid.CurrentRow==null){MessageBox.Show("Selecione a retirada da pessoa que está devolvendo.");return;}
   long id=Convert.ToInt64(grid.CurrentRow.Cells["ID"].Value);
   string name=Convert.ToString(grid.CurrentRow.Cells["Pessoa"].Value);
   int count=(int)quantity.Value;decimal refund=0m;
   try{
    using(var c=Open())using(var cmd=new SQLiteCommand("SELECT l.quantity-COALESCE((SELECT SUM(r.quantity) FROM bottle_returns r WHERE r.loan_id=l.id),0),l.paid_deposit,l.quantity FROM bottle_loans l WHERE l.id=@id",c)){
     cmd.Parameters.AddWithValue("@id",id);using(var rd=cmd.ExecuteReader()){if(!rd.Read()||count>Convert.ToInt32(rd[0]))throw new Exception("Quantidade maior que os cascos pendentes desta pessoa.");refund=Convert.ToDecimal(rd[1])*count/Convert.ToDecimal(rd[2]);}
    }
    if(MessageBox.Show("Receber "+count+" casco(s) de "+name+" e devolver R$ "+refund.ToString("N2")+(refund>0?" em dinheiro?":" (sem caução paga)?"),"Confirmar devolução",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
    Backup();
    using(var c=Open())using(var tx=c.BeginTransaction()){
     using(var cmd=new SQLiteCommand("INSERT INTO bottle_returns(loan_id,quantity,amount,returned_at,refund_method) SELECT id,@qty,@amount,@date,'DINHEIRO' FROM bottle_loans WHERE id=@id AND quantity-COALESCE((SELECT SUM(quantity) FROM bottle_returns WHERE loan_id=@id),0)>=@qty",c,tx)){
      cmd.Parameters.AddWithValue("@id",id);cmd.Parameters.AddWithValue("@qty",count);cmd.Parameters.AddWithValue("@amount",refund);cmd.Parameters.AddWithValue("@date",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
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
   try{using(var c=Open())using(var cmd=new SQLiteCommand("SELECT l.id AS ID,l.customer_name AS Pessoa,l.quantity AS Retirados,COALESCE(SUM(r.quantity),0) AS Devolvidos,l.quantity-COALESCE(SUM(r.quantity),0) AS Pendentes,l.delivered_at AS Retirada,MAX(r.returned_at) AS Ultima_devolucao,l.unit_deposit AS Caucao_unitaria,l.paid_deposit AS Caucao_paga,l.sale_id AS Venda FROM bottle_loans l LEFT JOIN bottle_returns r ON r.loan_id=l.id WHERE l.customer_name LIKE @name GROUP BY l.id ORDER BY l.id DESC LIMIT 2000",c)){
    cmd.Parameters.AddWithValue("@name","%"+filter.Text.Trim()+"%");using(var adapter=new SQLiteDataAdapter(cmd)){var data=new DataTable();adapter.Fill(data);grid.DataSource=data;}
    using(var totals=new SQLiteCommand("SELECT COALESCE((SELECT SUM(paid_deposit) FROM bottle_loans),0),COALESCE((SELECT SUM(amount) FROM bottle_returns),0),COALESCE((SELECT SUM(quantity) FROM bottle_loans),0)-COALESCE((SELECT SUM(quantity) FROM bottle_returns),0)",c))using(var reader=totals.ExecuteReader())if(reader.Read())summary.Text="Cauções recebidas: R$ "+Convert.ToDecimal(reader[0]).ToString("N2")+"  |  Devolvidas: R$ "+Convert.ToDecimal(reader[1]).ToString("N2")+"  |  Cascos pendentes: "+reader[2]+"  (fora das vendas)";
   }}catch(Exception ex){summary.Text="Falha ao consultar cascos: "+ex.Message;}
  }
 }
}
