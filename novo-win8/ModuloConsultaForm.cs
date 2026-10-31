using System;
using System.Data;
using System.Data.SQLite;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
namespace ArmazemGratidao {
 public sealed class ModuloConsultaForm:Form {
  readonly string db;readonly string module;
  readonly DataGridView grid=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells,SelectionMode=DataGridViewSelectionMode.FullRowSelect};
  readonly Label status=new Label {Dock=DockStyle.Bottom,Height=35,TextAlign=ContentAlignment.MiddleLeft};
  public ModuloConsultaForm(string path,string name) {
   db=path;module=name;Text="Armazem Gratidao | "+name+" (Consulta)";Width=1100;Height=700;BackColor=Color.FromArgb(229,242,255);
   var top=new FlowLayoutPanel {Dock=DockStyle.Top,Height=55,Padding=new Padding(8),BackColor=Color.FromArgb(220,95,105)};
   var reload=new Button {Text="Atualizar consulta",AutoSize=true,Height=33};reload.Click+=(s,e)=>LoadData();top.Controls.Add(reload);
   if(module=="Clientes"){var import=new Button {Text="Importar clientes da versão 12.1.6",AutoSize=true,Height=33};import.Click+=(s,e)=>ImportClients();top.Controls.Add(import);}
   grid.Font=new Font("Segoe UI",11,FontStyle.Regular);grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;grid.RowTemplate.Height=38;
   grid.DataBindingComplete+=(s,e)=>{foreach(DataGridViewColumn column in grid.Columns){string n=column.Name.ToLowerInvariant();if(n=="name"||n.Contains("descri")||n=="produto"||n=="cliente"){column.MinimumWidth=240;column.FillWeight=230;column.DefaultCellStyle.WrapMode=DataGridViewTriState.True;}}grid.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells;};
   Shown+=(s,e)=>{var bounds=Screen.FromControl(this).WorkingArea;Width=Math.Min(Width,Math.Max(360,bounds.Width-24));Height=Math.Min(Height,Math.Max(300,bounds.Height-24));};
   Controls.Add(grid);Controls.Add(status);Controls.Add(top);LoadData();
  }
  void LoadData() {
   if(!File.Exists(db)){status.Text="Importe o banco antes de consultar.";return;}
   if(module=="Clientes"){LoadClients();return;}
   string sql;
   switch(module) {
    case "Estoque":sql="SELECT id,barcode,name,stock,min_stock,price,cost,status FROM products ORDER BY name LIMIT 3000";break;
    case "Compras":sql="SELECT * FROM purchases ORDER BY id DESC LIMIT 1000";break;
    case "Caixa":sql="SELECT id,opened_at,opening_amount,closed_at,closing_amount,expected_cash,difference,status FROM cash_sessions ORDER BY id DESC LIMIT 1000";break;
    case "Financeiro":sql="SELECT id,sale_id,payment_method,amount,fee_percent,fee_amount,created_at FROM sale_payments ORDER BY id DESC LIMIT 2000";break;
    case "Relatorios":sql="SELECT date(created_at) AS dia,COUNT(*) AS vendas,SUM(gross_total) AS total_vendido,SUM(net_profit) AS lucro FROM sales GROUP BY date(created_at) ORDER BY dia DESC LIMIT 365";break;
    case "Sistema":sql="SELECT name AS tabela FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name";break;
    default:status.Text="Modulo nao disponivel.";return;
   }
   try{
    using(var c=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=db,ReadOnly=true,FailIfMissing=true}.ToString())){
     c.Open();
     using(var cmd=new SQLiteCommand(sql,c))using(var adapter=new SQLiteDataAdapter(cmd)){
      var dt=new DataTable();adapter.Fill(dt);grid.DataSource=dt;status.Text=module+": "+dt.Rows.Count+" registros (somente consulta, banco preservado).";
     }
    }
   }catch(SQLiteException ex){grid.DataSource=null;status.Text="Tabela ou coluna nao disponivel no banco importado: "+ex.Message;}
   catch(Exception ex){grid.DataSource=null;status.Text="Erro: "+ex.Message;}
  }
  static HashSet<string> Columns(SQLiteConnection connection,string table){
   var columns=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   using(var cmd=new SQLiteCommand("PRAGMA table_info("+table+")",connection))
   using(var reader=cmd.ExecuteReader())while(reader.Read())columns.Add(Convert.ToString(reader["name"]));
   return columns;
  }
  static bool HasTable(SQLiteConnection connection,string table){
   using(var cmd=new SQLiteCommand("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@name",connection)){
    cmd.Parameters.AddWithValue("@name",table);return Convert.ToInt32(cmd.ExecuteScalar())>0;
   }
  }
  static List<Tuple<string,string,string>> ReadClients(SQLiteConnection connection){
   var results=new Dictionary<string,Tuple<string,string,string>>(StringComparer.OrdinalIgnoreCase);
   foreach(string table in new[]{"customers","sales"}){
    if(!HasTable(connection,table))continue;
    var cols=Columns(connection,table);
    string field=table=="sales"?"customer_name":"name";
    if(!cols.Contains(field))continue;
    string phone=cols.Contains(table=="sales"?"customer_phone":"phone")?(table=="sales"?"customer_phone":"phone"):"NULL";
    string document=cols.Contains(table=="sales"?"customer_doc":"document")?(table=="sales"?"customer_doc":"document"):"NULL";
    string sql="SELECT TRIM("+field+") AS nome,MAX(TRIM(COALESCE("+phone+",''))) AS telefone,MAX(TRIM(COALESCE("+document+",''))) AS documento FROM "+table+" WHERE "+field+" IS NOT NULL AND TRIM("+field+")<>'' GROUP BY LOWER(TRIM("+field+"))";
    using(var cmd=new SQLiteCommand(sql,connection))using(var rd=cmd.ExecuteReader()){
     while(rd.Read()){
      string name=Convert.ToString(rd["nome"]).Trim();if(name.Length==0)continue;
      string phoneValue=Convert.ToString(rd["telefone"]),docValue=Convert.ToString(rd["documento"]);
      Tuple<string,string,string> existing;
      if(results.TryGetValue(name,out existing)){
       results[name]=Tuple.Create(existing.Item1,string.IsNullOrWhiteSpace(existing.Item2)?phoneValue:existing.Item2,string.IsNullOrWhiteSpace(existing.Item3)?docValue:existing.Item3);
      }else results.Add(name,Tuple.Create(name,phoneValue,docValue));
     }
    }
   }
   var list=new List<Tuple<string,string,string>>(results.Values);
   list.Sort((a,b)=>StringComparer.CurrentCultureIgnoreCase.Compare(a.Item1,b.Item1));
   return list;
  }
  void LoadClients(){
   try{
    using(var c=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=db,ReadOnly=true,FailIfMissing=true}.ToString())){
     c.Open();
     var list=ReadClients(c);
     var dt=new DataTable();dt.Columns.Add("Cliente");dt.Columns.Add("Telefone");dt.Columns.Add("Documento");
     foreach(var client in list)dt.Rows.Add(client.Item1,client.Item2,client.Item3);
     grid.DataSource=dt;
     status.Text="Clientes: "+list.Count+" encontrados (cadastros e nomes das vendas/fiado). Somente consulta.";
    }
   }catch(Exception ex){grid.DataSource=null;status.Text="Erro ao consultar clientes: "+ex.Message;}
  }
  void ImportClients(){
   using(var dlg=new OpenFileDialog {Title="Selecionar banco da versão 12.1.6 (SQLite)",Filter="Bancos SQLite (*.db;*.sqlite;*.sqlite3)|*.db;*.sqlite;*.sqlite3|Todos os arquivos (*.*)|*.*"}){
    if(dlg.ShowDialog(this)!=DialogResult.OK)return;
    try{
     if(Path.GetFullPath(dlg.FileName).Equals(Path.GetFullPath(db),StringComparison.OrdinalIgnoreCase))throw new Exception("Selecione um banco antigo diferente do banco atual.");
     List<Tuple<string,string,string>> sourceClients;
     using(var source=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=dlg.FileName,ReadOnly=true,FailIfMissing=true}.ToString())){
      source.Open();sourceClients=ReadClients(source);
     }
     if(sourceClients.Count==0){MessageBox.Show("Nenhum cadastro de cliente encontrado no banco selecionado.");return;}
     if(MessageBox.Show("Foram encontrados "+sourceClients.Count+" clientes no banco antigo.\\nSerá criado um backup antes de importar. Clientes já cadastrados não serão duplicados.\\nDeseja continuar?","Importar clientes",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
     string backupDir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ArmazemGratidao","backups");
     Directory.CreateDirectory(backupDir);
     string backupPath=Path.Combine(backupDir,"clientes-antes-importacao-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+".db");
     int imported=0;
     using(var target=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=db,FailIfMissing=true}.ToString())){
      target.Open();
      using(var backup=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=backupPath,FailIfMissing=false}.ToString())){
       backup.Open();target.BackupDatabase(backup,"main","main",-1,null,0);
      }
      using(var tx=target.BeginTransaction()){
       using(var create=new SQLiteCommand("CREATE TABLE IF NOT EXISTS customers (id INTEGER PRIMARY KEY AUTOINCREMENT,name TEXT NOT NULL,phone TEXT,document TEXT)",target,tx))create.ExecuteNonQuery();
       var columns=Columns(target,"customers");
       if(!columns.Contains("name"))throw new Exception("A tabela customers existente não possui coluna name. Nenhum dado será importado.");
       string phoneColumn=columns.Contains("phone")?",phone":"";
       string documentColumn=columns.Contains("document")?",document":"";
       string phoneParam=columns.Contains("phone")?",@phone":"";
       string docParam=columns.Contains("document")?",@document":"";
       string sql="INSERT INTO customers (name"+phoneColumn+documentColumn+") SELECT @name"+phoneParam+docParam+" WHERE NOT EXISTS (SELECT 1 FROM customers WHERE LOWER(TRIM(name))=LOWER(TRIM(@name)))";
       using(var cmd=new SQLiteCommand(sql,target,tx)){
        var name=cmd.Parameters.Add("@name",DbType.String);
        var phone=cmd.Parameters.Add("@phone",DbType.String);
        var document=cmd.Parameters.Add("@document",DbType.String);
        foreach(var client in sourceClients){
         name.Value=client.Item1;phone.Value=client.Item2;document.Value=client.Item3;
         imported+=cmd.ExecuteNonQuery();
        }
       }
       tx.Commit();
      }
     }
     LoadClients();
     MessageBox.Show("Importação concluída: "+imported+" novos clientes. Duplicados ignorados.\\nBackup antes da importação: "+backupPath,"Clientes");
    }catch(Exception ex){MessageBox.Show("Importação não concluída: "+ex.Message,"Clientes",MessageBoxButtons.OK,MessageBoxIcon.Error);}
   }
  }

 }
}