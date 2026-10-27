using System;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
namespace ArmazemGratidao {
 static class Program {
  [STAThread] static void Main() { Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new MainForm()); }
 }
 public sealed class MainForm : Form {
  readonly string dataDir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ArmazemGratidaoNovo");
  readonly string dbPath;
  readonly FlowLayoutPanel sideMenu=new FlowLayoutPanel {Dock=DockStyle.Left,Width=205,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(8),BackColor=Color.FromArgb(20,69,147)};
  readonly Button menuToggle=new Button {Text="☰  Menu",Width=120,Height=38,BackColor=Color.White,ForeColor=Color.FromArgb(20,69,147)};
  readonly DataGridView grid=new DataGridView { Dock=DockStyle.Fill,ReadOnly=true,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells,AllowUserToAddRows=false };
  readonly Label status=new Label { Dock=DockStyle.Bottom,Height=34,TextAlign=ContentAlignment.MiddleLeft };
  public MainForm() {
   dbPath=Path.Combine(dataDir,"armazem-gratidao.db");
   Text="Armazém Gratidão Novo";Width=1200;Height=760;Font=new Font("Segoe UI",10);
   var bar=sideMenu;
   var header=new Panel {Dock=DockStyle.Top,Height=55,BackColor=Color.FromArgb(18,75,163)};
   menuToggle.Left=10;menuToggle.Top=8;menuToggle.Click+=(s,e)=>{sideMenu.Visible=!sideMenu.Visible;};header.Controls.Add(menuToggle);
   var brand=new Label {Text="ARMAZÉM GRATIDÃO",ForeColor=Color.White,Font=new Font("Segoe UI",16,FontStyle.Bold),AutoSize=true,Left=150,Top=12};header.Controls.Add(brand);
   AddButton(bar,"Importar banco",ImportDatabase);
   AddButton(bar,"Backup do banco",BackupDatabase);
   AddButton(bar,"Verificar banco",CheckDatabase);
   AddButton(bar,"Localizar vendas",CompareSales);
   AddButton(bar,"PDV - Nova Venda",(s,e)=>{if(!File.Exists(dbPath)){MessageBox.Show("Importe o banco antes de vender.");return;}using(var pdv=new PdvForm(dbPath))pdv.ShowDialog(this);});
   AddButton(bar,"Vendas",(s,e)=>OpenConsult(false));
   AddButton(bar,"Itens",(s,e)=>ShowTable("sale_items","SELECT sale_id,name,quantity,unit_price,total FROM sale_items ORDER BY sale_id DESC LIMIT 5000"));
   AddButton(bar,"Fiado",(s,e)=>{if(!File.Exists(dbPath)){MessageBox.Show("Importe primeiro o banco.");return;}using(var form=new FiadoClientesForm(dbPath))form.ShowDialog(this);});
   AddButton(bar,"Produtos",(s,e)=>{if(!File.Exists(dbPath)){MessageBox.Show("Importe primeiro o banco.");return;}using(var form=new ProdutosForm(dbPath))form.ShowDialog(this);});
   foreach(var module in new[]{"Estoque","Clientes","Compras","Caixa","Financeiro","Relatorios","Sistema"}) AddButton(bar,module,(s,e)=>{if(!File.Exists(dbPath)){MessageBox.Show("Importe primeiro o banco.");return;}using(var form=new ModuloConsultaForm(dbPath,module))form.ShowDialog(this);});
   Controls.Add(grid);Controls.Add(status);Controls.Add(bar);Controls.Add(header);Directory.CreateDirectory(dataDir);
   status.Text=File.Exists(dbPath)?"Banco encontrado. Selecione Vendas ou Fiado.":"Importe uma copia do banco SQLite.";
  }
  void AddButton(Control parent,string title,EventHandler click) {var b=new Button {Text=title,Width=175,Height=39,BackColor=Color.White,ForeColor=Color.FromArgb(160,20,40),Font=new Font("Segoe UI",9,FontStyle.Bold)};b.Click+=click;parent.Controls.Add(b);}
  void BackupDatabase(object sender,EventArgs e) {
   if(!File.Exists(dbPath)){MessageBox.Show("Importe primeiro o banco.");return;}
   using(var dlg=new SaveFileDialog {Title="Salvar copia de seguranca SQLite",Filter="Banco SQLite (*.db)|*.db",FileName="armazem-gratidao-backup-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".db"}) {
    if(dlg.ShowDialog()!=DialogResult.OK)return;
    try {
     if(Path.GetFullPath(dlg.FileName).Equals(Path.GetFullPath(dbPath),StringComparison.OrdinalIgnoreCase))throw new Exception("Escolha um destino diferente do banco em uso.");
     if(File.Exists(dlg.FileName))throw new Exception("Escolha um nome de arquivo novo para nao sobrescrever backups anteriores.");
     using(var source=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=dbPath,ReadOnly=true,FailIfMissing=true}.ToString()))
     using(var dest=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=dlg.FileName,FailIfMissing=false}.ToString())) {
      source.Open();dest.Open();source.BackupDatabase(dest,"main","main",-1,null,0);
     }
     using(var check=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=dlg.FileName,ReadOnly=true,FailIfMissing=true}.ToString())) {
      check.Open();using(var cmd=new SQLiteCommand("PRAGMA integrity_check",check))if(Convert.ToString(cmd.ExecuteScalar())!="ok")throw new Exception("Backup criado, mas a verificacao de integridade falhou.");
     }
     status.Text="Backup SQLite criado e validado: "+dlg.FileName;
     MessageBox.Show("Backup concluido e verificado. O banco em uso nao foi modificado.");
    }catch(Exception ex){MessageBox.Show(ex.Message,"Backup nao concluido",MessageBoxButtons.OK,MessageBoxIcon.Error);}
   }
  }
  void CheckDatabase(object sender,EventArgs e) {
   if(!File.Exists(dbPath)){MessageBox.Show("Importe primeiro o banco.");return;}
   try {
    using(var conn=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=dbPath,ReadOnly=true,FailIfMissing=true}.ToString())){
     conn.Open();
     string result;using(var cmd=new SQLiteCommand("PRAGMA integrity_check",conn))result=Convert.ToString(cmd.ExecuteScalar());
     if(result!="ok")throw new Exception("Integridade SQLite: "+result);
     var counts=new System.Text.StringBuilder("Integridade SQLite: OK"+Environment.NewLine);
     foreach(var table in new[]{"sales","sale_items","credit_payments","products"}){
      using(var cmd=new SQLiteCommand("SELECT COUNT(*) FROM "+table,conn))counts.AppendLine(table+": "+cmd.ExecuteScalar());
     }
     status.Text="Integridade do banco: OK. Consultas realizadas sem alteracoes.";
     MessageBox.Show(counts.ToString(),"Verificacao do banco");
    }
   }catch(Exception ex){MessageBox.Show(ex.Message,"Problema na verificacao",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
  }
  static string SalesSummary(string path) {
   using(var conn=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=path,ReadOnly=true,FailIfMissing=true}.ToString())){
    conn.Open();
    using(var cmd=new SQLiteCommand("SELECT COUNT(*),COALESCE(MAX(id),0),COALESCE(MAX(created_at),'SEM DATA') FROM sales",conn))
    using(var reader=cmd.ExecuteReader()){
     if(!reader.Read())return "Nenhuma venda encontrada.";
     return "Quantidade: "+reader.GetValue(0)+" | Último número: "+reader.GetValue(1)+" | Data mais recente: "+reader.GetValue(2);
    }
   }
  }
  void CompareSales(object sender,EventArgs e){
   if(!File.Exists(dbPath)){MessageBox.Show("Importe primeiro um banco para comparar.");return;}
   using(var dlg=new OpenFileDialog {Title="Selecione o banco original para localizar vendas posteriores (somente leitura)",Filter="Bancos SQLite|*.db;*.sqlite;*.sqlite3|Todos os arquivos|*.*"}){
    if(dlg.ShowDialog()!=DialogResult.OK)return;
    try{
     if(Path.GetFullPath(dlg.FileName).Equals(Path.GetFullPath(dbPath),StringComparison.OrdinalIgnoreCase))throw new Exception("Escolha o banco original, diferente do banco importado.");
     string imported=SalesSummary(dbPath),original=SalesSummary(dlg.FileName);
     long lastImported;
     using(var c=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=dbPath,ReadOnly=true}.ToString())){
      c.Open();using(var q=new SQLiteCommand("SELECT COALESCE(MAX(id),0) FROM sales",c))lastImported=Convert.ToInt64(q.ExecuteScalar());
     }
     long newer;string lastDate;
     using(var c=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=dlg.FileName,ReadOnly=true}.ToString())){
      c.Open();using(var q=new SQLiteCommand("SELECT COUNT(*),COALESCE(MAX(created_at),'SEM DATA') FROM sales WHERE id>@last",c)){
       q.Parameters.AddWithValue("@last",lastImported);
       using(var reader=q.ExecuteReader()){reader.Read();newer=Convert.ToInt64(reader.GetValue(0));lastDate=Convert.ToString(reader.GetValue(1));}
      }
     }
     MessageBox.Show("BANCO IMPORTADO"+Environment.NewLine+imported+Environment.NewLine+Environment.NewLine+
      "BANCO ORIGINAL SELECIONADO"+Environment.NewLine+original+Environment.NewLine+Environment.NewLine+
      "Vendas com número superior ao último importado: "+newer+Environment.NewLine+
      "Data mais recente entre elas: "+lastDate+Environment.NewLine+Environment.NewLine+
      "Comparação somente de leitura. Nenhuma venda foi copiada, excluída ou alterada."+
      Environment.NewLine+"Observação: números iguais não garantem que os registros sejam idênticos.","Localizar vendas ausentes");
    }catch(Exception ex){MessageBox.Show(ex.Message,"Comparação não concluída",MessageBoxButtons.OK,MessageBoxIcon.Error);}
   }
  }
  void ImportDatabase(object sender,EventArgs e) {
   using(var dlg=new OpenFileDialog {Title="Selecionar banco antigo (somente leitura)",Filter="Bancos SQLite|*.db;*.sqlite;*.sqlite3|Todos os arquivos|*.*"}) {
    if(dlg.ShowDialog()!=DialogResult.OK)return;
    try {
     if(Path.GetFullPath(dlg.FileName).Equals(Path.GetFullPath(dbPath),StringComparison.OrdinalIgnoreCase))throw new Exception("Selecione um banco de origem diferente.");
     using(var source=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=dlg.FileName,ReadOnly=true,FailIfMissing=true}.ToString())) {
      source.Open();
      foreach(var table in new[]{"sales","sale_items","credit_payments","products"}) {
       using(var cmd=new SQLiteCommand("SELECT count(*) FROM sqlite_master WHERE type='table' AND name=@name",source)) {cmd.Parameters.AddWithValue("@name",table);if(Convert.ToInt32(cmd.ExecuteScalar())==0)throw new Exception("Tabela ausente: "+table);}
      }
      var temp=Path.Combine(dataDir,"importacao-"+Guid.NewGuid().ToString("N")+".db");
      try {
       using(var dest=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=temp,FailIfMissing=false}.ToString())) {dest.Open();source.BackupDatabase(dest,"main","main",-1,null,0);}
       using(var verify=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=temp,ReadOnly=true}.ToString())) {verify.Open();using(var check=new SQLiteCommand("PRAGMA integrity_check",verify))if(Convert.ToString(check.ExecuteScalar())!="ok")throw new Exception("Falha na verificacao SQLite");}
       if(File.Exists(dbPath))File.Copy(dbPath,dbPath+".backup-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"),false);
       File.Copy(temp,dbPath,true);
      } finally {if(File.Exists(temp))File.Delete(temp);}
     }
     status.Text="Banco importado. Original preservado.";ShowTable("sales","SELECT id,created_at,customer_name,payment_method,gross_total,status FROM sales ORDER BY created_at DESC LIMIT 2000");
    } catch(Exception ex) {MessageBox.Show(ex.Message,"Importacao nao concluida",MessageBoxButtons.OK,MessageBoxIcon.Error);}
   }
  }
  void OpenConsult(bool credit) {if(!File.Exists(dbPath)){MessageBox.Show("Importe primeiro o banco.");return;}using(var form=new ConsultaForm(dbPath,credit))form.ShowDialog(this);}
  void ShowTable(string table,string sql) {
   if(!File.Exists(dbPath)) {MessageBox.Show("Importe primeiro o banco de dados.");return;}
   try {using(var conn=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=dbPath,ReadOnly=true}.ToString())) {conn.Open();var dt=new DataTable();using(var cmd=new SQLiteCommand(sql,conn))using(var adapter=new SQLiteDataAdapter(cmd))adapter.Fill(dt);grid.DataSource=dt;status.Text=table+": "+dt.Rows.Count+" registros exibidos (somente leitura).";}}
   catch(Exception ex) {MessageBox.Show(ex.Message,"Erro na consulta",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  }
 }
}