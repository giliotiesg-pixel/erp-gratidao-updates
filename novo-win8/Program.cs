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
  readonly DataGridView grid=new DataGridView { Dock=DockStyle.Fill,ReadOnly=true,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells,AllowUserToAddRows=false };
  readonly Label status=new Label { Dock=DockStyle.Bottom,Height=34,TextAlign=ContentAlignment.MiddleLeft };
  public MainForm() {
   dbPath=Path.Combine(dataDir,"armazem-gratidao.db");
   Text="Armazem Gratidao - Vendas e Fiado";Width=1100;Height=720;
   var bar=new FlowLayoutPanel {Dock=DockStyle.Top,Height=52,BackColor=Color.FromArgb(18,75,163),Padding=new Padding(8)};
   AddButton(bar,"Importar banco",ImportDatabase);
   AddButton(bar,"Vendas",(s,e)=>ShowTable("sales","SELECT id,created_at,customer_name,payment_method,gross_total,status FROM sales ORDER BY created_at DESC LIMIT 2000"));
   AddButton(bar,"Itens",(s,e)=>ShowTable("sale_items","SELECT sale_id,name,quantity,unit_price,total FROM sale_items ORDER BY sale_id DESC LIMIT 5000"));
   AddButton(bar,"Fiado",(s,e)=>ShowTable("credit_payments","SELECT id,sale_id,amount,payment_method,paid_at FROM credit_payments ORDER BY paid_at DESC LIMIT 2000"));
   AddButton(bar,"Produtos",(s,e)=>ShowTable("products","SELECT id,barcode,name,price,stock FROM products ORDER BY name LIMIT 3000"));
   Controls.Add(grid);Controls.Add(status);Controls.Add(bar);Directory.CreateDirectory(dataDir);
   status.Text=File.Exists(dbPath)?"Banco encontrado. Selecione Vendas ou Fiado.":"Importe uma copia do banco SQLite.";
  }
  void AddButton(Control parent,string title,EventHandler click) {var b=new Button {Text=title,AutoSize=true,Height=34,BackColor=Color.White,ForeColor=Color.FromArgb(160,20,40),Font=new Font("Segoe UI",9,FontStyle.Bold)};b.Click+=click;parent.Controls.Add(b);}
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
  void ShowTable(string table,string sql) {
   if(!File.Exists(dbPath)) {MessageBox.Show("Importe primeiro o banco de dados.");return;}
   try {using(var conn=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=dbPath,ReadOnly=true}.ToString())) {conn.Open();var dt=new DataTable();using(var cmd=new SQLiteCommand(sql,conn))using(var adapter=new SQLiteDataAdapter(cmd))adapter.Fill(dt);grid.DataSource=dt;status.Text=table+": "+dt.Rows.Count+" registros exibidos (somente leitura).";}}
   catch(Exception ex) {MessageBox.Show(ex.Message,"Erro na consulta",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  }
 }
}