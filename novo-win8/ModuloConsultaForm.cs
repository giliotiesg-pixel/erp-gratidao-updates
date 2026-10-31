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
  readonly TextBox stockSearch=new TextBox {Width=220,Font=new Font("Segoe UI",10)};
  readonly DataGridView grid=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells,SelectionMode=DataGridViewSelectionMode.FullRowSelect};
  readonly Label status=new Label {Dock=DockStyle.Bottom,Height=35,TextAlign=ContentAlignment.MiddleLeft};
  public ModuloConsultaForm(string path,string name) {
   db=path;module=name;Text="Armazem Gratidao | "+name+" (Consulta)";Width=1100;Height=700;BackColor=Color.FromArgb(229,242,255);
   var top=new FlowLayoutPanel {Dock=DockStyle.Top,Height=55,Padding=new Padding(8),BackColor=Color.FromArgb(220,95,105)};
   var reload=new Button {Text="Atualizar consulta",AutoSize=true,Height=33};reload.Click+=(s,e)=>LoadData();top.Controls.Add(reload);
   if(module=="Estoque"){
    top.Height=65;
    top.Controls.Add(new Label {Text="Buscar produto:",AutoSize=true,Padding=new Padding(3,8,1,0),ForeColor=Color.White,Font=new Font("Segoe UI",10,FontStyle.Bold)});
    stockSearch.TextChanged+=(s,e)=>LoadStock();top.Controls.Add(stockSearch);
    var entry=new Button {Text="Inserir QTD",Width=115,Height=34};entry.Click+=(s,e)=>StockEntry();top.Controls.Add(entry);
    var edit=new Button {Text="Editar / Salvar",Width=130,Height=34};edit.Click+=(s,e)=>StockEdit();top.Controls.Add(edit);
    var history=new Button {Text="Auditoria",Width=115,Height=34};history.Click+=(s,e)=>StockHistory();top.Controls.Add(history);
   }
   if(module=="Clientes"){var import=new Button {Text="Importar clientes da versão 12.1.6",AutoSize=true,Height=33};import.Click+=(s,e)=>ImportClients();top.Controls.Add(import);}
   grid.Font=new Font("Segoe UI",10,FontStyle.Regular);grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;grid.RowTemplate.Height=29;
   grid.BackgroundColor=Color.White;grid.BorderStyle=BorderStyle.None;grid.RowHeadersVisible=false;
   grid.EnableHeadersVisualStyles=false;grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(21,75,156);grid.ColumnHeadersDefaultCellStyle.ForeColor=Color.White;
   grid.ColumnHeadersDefaultCellStyle.Font=new Font("Segoe UI",10,FontStyle.Bold);grid.ColumnHeadersHeight=38;
   grid.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(238,246,255);
   grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(207,227,248);grid.DefaultCellStyle.SelectionForeColor=Color.Black;
   grid.DataBindingComplete+=(s,e)=>{foreach(DataGridViewColumn column in grid.Columns){string n=column.Name.ToLowerInvariant();if(n=="name"||n.Contains("descri")||n=="produto"||n=="cliente"){column.MinimumWidth=240;column.FillWeight=230;column.DefaultCellStyle.WrapMode=DataGridViewTriState.True;}}grid.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.None;};
   Shown+=(s,e)=>{var bounds=Screen.FromControl(this).WorkingArea;Width=Math.Min(Width,Math.Max(360,bounds.Width-24));Height=Math.Min(Height,Math.Max(300,bounds.Height-24));};
   Controls.Add(grid);Controls.Add(status);Controls.Add(top);LoadData();
  }
  void LoadData() {
   if(!File.Exists(db)){status.Text="Importe o banco antes de consultar.";return;}
   if(module=="Clientes"){LoadClients();return;}
   if(module=="Estoque"){LoadStock();return;}
   string sql;
   switch(module) {
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

  SQLiteConnection StockOpen(bool readOnly){
   var builder=new SQLiteConnectionStringBuilder {DataSource=db,ReadOnly=readOnly,FailIfMissing=true};
   var conn=new SQLiteConnection(builder.ToString());conn.Open();return conn;
  }
  static string StockField(HashSet<string> columns,string column,string title){
   return columns.Contains(column)?("\""+column+"\" AS \""+title+"\""):"NULL AS \""+title+"\"";
  }
  void LoadStock(){
   try{
    using(var conn=StockOpen(true)){
     if(!HasTable(conn,"products"))throw new Exception("Tabela de produtos não encontrada.");
     var cols=Columns(conn,"products");
     if(!cols.Contains("id")||!cols.Contains("name")||!cols.Contains("stock"))throw new Exception("O banco não possui as colunas essenciais do estoque.");
     string sql="SELECT id AS \"Código interno\","+
      StockField(cols,"barcode","Código de barras")+",name AS \"Produto\",stock AS \"Quantidade atual\","+
      StockField(cols,"min_stock","Estoque mínimo")+","+
      StockField(cols,"price","Preço de venda")+","+
      StockField(cols,"cost","Custo")+","+
      StockField(cols,"status","Situação")+" FROM products WHERE (@term='' OR name LIKE @like OR "+
      (cols.Contains("barcode")?"barcode LIKE @like":"0")+
      " OR CAST(id AS TEXT)=@term) ORDER BY name,id LIMIT 500";
     using(var cmd=new SQLiteCommand(sql,conn)){
      string term=stockSearch.Text.Trim();
      cmd.Parameters.AddWithValue("@term",term);cmd.Parameters.AddWithValue("@like","%"+term+"%");
      using(var adapter=new SQLiteDataAdapter(cmd)){
       var table=new DataTable();adapter.Fill(table);grid.DataSource=table;
       foreach(DataGridViewColumn col in grid.Columns){
        col.DefaultCellStyle.WrapMode=DataGridViewTriState.False;
        if(col.Name=="Produto"){col.FillWeight=260;col.MinimumWidth=230;}
        else if(col.Name=="Código de barras")col.FillWeight=130;
        else col.FillWeight=95;
        if(col.Name=="Quantidade atual"||col.Name=="Estoque mínimo")col.DefaultCellStyle.Format="N3";
        if(col.Name=="Preço de venda"||col.Name=="Custo")col.DefaultCellStyle.Format="N2";
       }
       status.Text="ESTOQUE  •  "+table.Rows.Count+" produtos exibidos (máximo 500 por consulta). Selecione um produto para inserir quantidade, editar ou consultar a auditoria.";
      }
     }
    }
   }catch(Exception ex){grid.DataSource=null;status.Text="Erro ao consultar estoque: "+ex.Message;}
  }
  long SelectedStockId(){
   if(grid.CurrentRow==null)throw new Exception("Selecione um produto na tabela.");
   return Convert.ToInt64(grid.CurrentRow.Cells["Código interno"].Value);
  }
  static decimal Dec(object v){return v==null||v==DBNull.Value?0:Convert.ToDecimal(v,System.Globalization.CultureInfo.InvariantCulture);}
  static void Execute(SQLiteConnection conn,SQLiteTransaction tx,string sql,params SQLiteParameter[] parameters){
   using(var cmd=new SQLiteCommand(sql,conn,tx)){cmd.Parameters.AddRange(parameters);cmd.ExecuteNonQuery();}
  }
  static SQLiteParameter P(string name,object value){return new SQLiteParameter(name,value??DBNull.Value);}
  string BackupStock(SQLiteConnection conn){
   string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ArmazemGratidao","backups");
   Directory.CreateDirectory(dir);
   string file=Path.Combine(dir,"estoque-antes-alteracao-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+".db");
   using(var backup=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=file,FailIfMissing=false}.ToString())){
    backup.Open();conn.BackupDatabase(backup,"main","main",-1,null,0);
   }
   return file;
  }
  void StockEntry(){
   long id;try{id=SelectedStockId();}catch(Exception ex){MessageBox.Show(ex.Message);return;}
   using(var form=new Form {Text="Estoque | Inserir quantidade",Width=500,Height=280,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false}){
    var quantity=new NumericUpDown{Left=180,Top=27,Width=200,DecimalPlaces=3,Minimum=0.001M,Maximum=10000000M,Value=1M};
    var reason=new TextBox{Left=180,Top=80,Width=255,MaxLength=250};
    var save=new Button{Left=180,Top=147,Width=180,Height=38,Text="Salvar entrada"};
    form.Controls.Add(new Label{Text="Quantidade a inserir",Left=15,Top=30,AutoSize=true});
    form.Controls.Add(new Label{Text="Motivo da entrada",Left=15,Top=84,AutoSize=true});
    form.Controls.AddRange(new Control[]{quantity,reason,save});
    save.Click+=(s,e)=>{
     if(string.IsNullOrWhiteSpace(reason.Text)){MessageBox.Show("Informe o motivo da entrada.");return;}
     try{
      string backup;
      using(var conn=StockOpen(false)){
       if(!HasTable(conn,"runtime_operation_context")||!HasTable(conn,"stock_audit_log"))throw new Exception("Banco sem as tabelas de proteção e auditoria. Nenhum estoque foi alterado.");
       backup=BackupStock(conn);
       using(var tx=conn.BeginTransaction()){
        decimal before;string name,barcode;
        using(var q=new SQLiteCommand("SELECT name,barcode,stock FROM products WHERE id=@id",conn,tx)){
         q.Parameters.AddWithValue("@id",id);
         using(var rd=q.ExecuteReader()){
          if(!rd.Read())throw new Exception("Produto não encontrado.");
          name=Convert.ToString(rd["name"]);barcode=Convert.ToString(rd["barcode"]);before=Dec(rd["stock"]);
         }
        }
        decimal after=checked(before+quantity.Value);
        int oldAuthorization;string oldOperation;
        using(var q=new SQLiteCommand("SELECT authorized,operation FROM runtime_operation_context WHERE id=1",conn,tx))
        using(var rd=q.ExecuteReader()){
         if(!rd.Read())throw new Exception("Contexto de proteção do estoque não encontrado.");
         oldAuthorization=Convert.ToInt32(rd["authorized"]);oldOperation=Convert.ToString(rd["operation"]);
        }
        Execute(conn,tx,"UPDATE runtime_operation_context SET authorized=1,operation='ENTRADA_MANUAL_AUDITADA' WHERE id=1");
        using(var q=new SQLiteCommand("UPDATE products SET stock=@after WHERE id=@id AND stock=@before",conn,tx)){
         q.Parameters.AddWithValue("@after",after);q.Parameters.AddWithValue("@id",id);q.Parameters.AddWithValue("@before",before);
         if(q.ExecuteNonQuery()!=1)throw new Exception("Estoque mudou durante a operação. Atualize e tente novamente.");
        }
        Execute(conn,tx,"INSERT INTO stock_audit_log(product_id,product_name,barcode,event_type,quantity_before,quantity_after,quantity_delta,reference_type,reason) VALUES(@id,@name,@barcode,'ENTRADA_MANUAL',@before,@after,@delta,'ESTOQUE',@reason)",
         P("@id",id),P("@name",name),P("@barcode",barcode),P("@before",before),P("@after",after),P("@delta",quantity.Value),P("@reason",reason.Text.Trim()));
        Execute(conn,tx,"UPDATE runtime_operation_context SET authorized=@authorized,operation=@operation WHERE id=1",P("@authorized",oldAuthorization),P("@operation",oldOperation));
        tx.Commit();
       }
      }
      form.DialogResult=DialogResult.OK;form.Close();
      LoadStock();MessageBox.Show("Entrada registrada com auditoria. Backup: "+backup,"Estoque");
     }catch(Exception ex){MessageBox.Show("Nenhuma entrada concluída: "+ex.Message,"Estoque",MessageBoxButtons.OK,MessageBoxIcon.Error);}
    };
    form.ShowDialog(this);
   }
  }
  void StockEdit(){
   long id;try{id=SelectedStockId();}catch(Exception ex){MessageBox.Show(ex.Message);return;}
   try{
    using(var conn=StockOpen(true)){
     var cols=Columns(conn,"products");
     if(!cols.Contains("min_stock"))throw new Exception("Este banco não possui o campo Estoque mínimo.");
     decimal original;
     using(var q=new SQLiteCommand("SELECT min_stock FROM products WHERE id=@id",conn)){
      q.Parameters.AddWithValue("@id",id);var value=q.ExecuteScalar();
      if(value==null)throw new Exception("Produto não encontrado.");
      original=Dec(value);
     }
     using(var form=new Form{Text="Estoque | Editar e salvar",Width=535,Height=295,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false}){
      var minimum=new NumericUpDown{Left=190,Top=35,Width=220,DecimalPlaces=3,Minimum=0,Maximum=10000000,Value=Math.Max(0,original)};
      var reason=new TextBox{Left=190,Top=95,Width=280,MaxLength=250};
      var save=new Button{Text="Salvar alterações",Left=190,Top=154,Width=200,Height=39};
      form.Controls.Add(new Label{Text="Estoque mínimo",Left=15,Top=39,AutoSize=true});
      form.Controls.Add(new Label{Text="Motivo da alteração",Left=15,Top=99,AutoSize=true});
      form.Controls.AddRange(new Control[]{minimum,reason,save});
      save.Click+=(s,e)=>{
       if(string.IsNullOrWhiteSpace(reason.Text)){MessageBox.Show("Informe o motivo da alteração.");return;}
       if(minimum.Value==original){MessageBox.Show("Nenhuma alteração para salvar.");return;}
       try{
        string backup;
        using(var target=StockOpen(false)){
         backup=BackupStock(target);
         using(var tx=target.BeginTransaction()){
          Execute(target,tx,"CREATE TABLE IF NOT EXISTS stock_field_audit (id INTEGER PRIMARY KEY AUTOINCREMENT,product_id INTEGER NOT NULL,field_name TEXT NOT NULL,old_value TEXT,new_value TEXT,reason TEXT NOT NULL,changed_at TEXT NOT NULL)");
          using(var command=new SQLiteCommand("UPDATE products SET min_stock=@next WHERE id=@id AND COALESCE(min_stock,0)=@old",target,tx)){
           command.Parameters.AddWithValue("@next",minimum.Value);command.Parameters.AddWithValue("@id",id);command.Parameters.AddWithValue("@old",original);
           if(command.ExecuteNonQuery()!=1)throw new Exception("Valor modificado por outra operação. Atualize a tela.");
          }
          Execute(target,tx,"INSERT INTO stock_field_audit(product_id,field_name,old_value,new_value,reason,changed_at) VALUES(@id,'Estoque mínimo',@old,@new,@reason,@date)",
           P("@id",id),P("@old",original.ToString(System.Globalization.CultureInfo.InvariantCulture)),P("@new",minimum.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)),P("@reason",reason.Text.Trim()),P("@date",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
          tx.Commit();
         }
        }
        form.DialogResult=DialogResult.OK;form.Close();LoadStock();MessageBox.Show("Alteração salva e auditada. Backup: "+backup,"Estoque");
       }catch(Exception ex){MessageBox.Show("Alteração não salva: "+ex.Message,"Estoque",MessageBoxButtons.OK,MessageBoxIcon.Error);}
      };
      form.ShowDialog(this);
     }
    }
   }catch(Exception ex){MessageBox.Show(ex.Message,"Estoque",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  }
  void StockHistory(){
   long id;try{id=SelectedStockId();}catch(Exception ex){MessageBox.Show(ex.Message);return;}
   try{
    using(var conn=StockOpen(true)){
     var all=new DataTable();
     all.Columns.Add("Data e hora");all.Columns.Add("Tipo de alteração");all.Columns.Add("Valor anterior");
     all.Columns.Add("Valor novo");all.Columns.Add("Diferença");all.Columns.Add("Motivo");
     if(HasTable(conn,"stock_audit_log")){
      using(var cmd=new SQLiteCommand("SELECT created_at,event_type,quantity_before,quantity_after,quantity_delta,reason FROM stock_audit_log WHERE product_id=@id ORDER BY id DESC LIMIT 200",conn)){
       cmd.Parameters.AddWithValue("@id",id);
       using(var rd=cmd.ExecuteReader())while(rd.Read())all.Rows.Add(Convert.ToString(rd.GetValue(0)),Convert.ToString(rd.GetValue(1)),Convert.ToString(rd.GetValue(2)),Convert.ToString(rd.GetValue(3)),Convert.ToString(rd.GetValue(4)),Convert.ToString(rd.GetValue(5)));
      }
     }
     if(HasTable(conn,"stock_field_audit")){
      using(var cmd=new SQLiteCommand("SELECT changed_at,field_name,old_value,new_value,reason FROM stock_field_audit WHERE product_id=@id ORDER BY id DESC LIMIT 200",conn)){
       cmd.Parameters.AddWithValue("@id",id);
       using(var rd=cmd.ExecuteReader())while(rd.Read())all.Rows.Add(Convert.ToString(rd.GetValue(0)),"Edição: "+Convert.ToString(rd.GetValue(1)),Convert.ToString(rd.GetValue(2)),Convert.ToString(rd.GetValue(3)),"—",Convert.ToString(rd.GetValue(4)));
      }
     }
     using(var form=new Form{Text="Estoque | Histórico auditável do produto #"+id,Width=980,Height=510,StartPosition=FormStartPosition.CenterParent}){
      var history=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,RowHeadersVisible=false,AllowUserToAddRows=false,DataSource=all,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,BackgroundColor=Color.White};
      form.Controls.Add(history);form.ShowDialog(this);
     }
    }
   }catch(Exception ex){MessageBox.Show("Não foi possível abrir o histórico: "+ex.Message,"Auditoria");}
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