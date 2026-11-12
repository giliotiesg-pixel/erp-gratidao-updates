using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ArmazemGratidao {
 // Caixa físico e conferência. Nunca modifica vendas nem pagamentos do PDV.
 public sealed class CashControlForm : StyledDialogForm {
  readonly string database;
  readonly DataGridView today=new DataGridView();
  readonly DataGridView history=new DataGridView();
  readonly DataGridView movements=new DataGridView();
  readonly DataGridView previousMovements=new DataGridView();
  readonly Label totals=new Label();
  readonly Label caption=new Label();
  readonly TabControl pages=new TabControl();
  readonly DateTimePicker historyDate=new DateTimePicker();
  readonly Button archiveButton=new Button();
  readonly Button dayButton=new Button();
  long? activeSelection;
  static readonly Color blue=Color.FromArgb(21,75,156);
  static readonly Color red=Color.FromArgb(187,40,58);
  const string Stamp="yyyy-MM-dd HH:mm:ss";

  public CashControlForm(string path){
   database=path;Text="Armazém Gratidão | Caixa do Dia";
   Size=new Size(990,665);MinimumSize=new Size(640,470);
   StartPosition=FormStartPosition.CenterScreen;
   BackColor=Color.FromArgb(239,246,254);
   Font=new Font("Segoe UI",9.3f);
   var bar=new FlowLayoutPanel{Name="amzgCashActions",Dock=DockStyle.Top,Height=46,
    FlowDirection=FlowDirection.LeftToRight,WrapContents=false,AutoScroll=true,
    Padding=new Padding(7,6,6,2),BackColor=Color.FromArgb(222,235,253)};
   bar.Controls.Add(ActionButton("Abrir Caixa",101,blue,()=>OpenDialog()));
   bar.Controls.Add(ActionButton("Troco / Entrada",118,blue,()=>MovementDialog("ENTRADA")));
   bar.Controls.Add(ActionButton("Sangria",84,red,()=>MovementDialog("SANGRIA")));
   bar.Controls.Add(ActionButton("Saída",71,red,()=>MovementDialog("SAIDA")));
   bar.Controls.Add(ActionButton("Fechar Caixa",111,red,()=>CloseDialog()));
   bar.Controls.Add(ActionButton("Editar Caixa",101,blue,()=>EditSessionDialog()));
   bar.Controls.Add(ActionButton("Editar Movimento",132,blue,()=>MovementEditDialog()));
   bar.Controls.Add(ActionButton("Excluir Movimento",132,red,()=>DeleteMovement()));
   bar.Controls.Add(ActionButton("Auditoria",82,blue,()=>ShowAudit()));
   bar.Controls.Add(ActionButton("Atualizar",78,blue,()=>RefreshAll()));
   totals.Name="amzgCashSummary";totals.Dock=DockStyle.Top;totals.Height=92;
   totals.BackColor=Color.White;totals.ForeColor=Color.FromArgb(26,63,116);
   totals.Padding=new Padding(12,9,9,5);totals.Font=new Font("Segoe UI",10,FontStyle.Bold);
   totals.Text="CAIXA DO DIA | Selecione um caixa para conferir";
   caption.Dock=DockStyle.Bottom;caption.Height=29;
   caption.Padding=new Padding(10,4,5,4);
   caption.ForeColor=Color.FromArgb(42,76,126);
   caption.Text="Somente caixa do dia. Registros antigos em Caixas Anteriores. Alterações são auditadas.";
   var nav=new FlowLayoutPanel{Dock=DockStyle.Top,Height=43,WrapContents=false,
    Padding=new Padding(8,5,4,1),BackColor=Color.FromArgb(231,240,252)};
   dayButton.Text="Caixa do Dia";dayButton.Name="amzgCashTodayButton";
   archiveButton.Text="Caixas Anteriores";archiveButton.Name="amzgCashArchiveButton";
   StyleButton(dayButton,blue,128);
   StyleButton(archiveButton,Color.FromArgb(73,91,124),156);
   nav.Controls.Add(dayButton);nav.Controls.Add(archiveButton);
   pages.Name="amzgCashPages";pages.Dock=DockStyle.Fill;pages.Appearance=TabAppearance.FlatButtons;
   pages.ItemSize=new Size(1,1);pages.SizeMode=TabSizeMode.Fixed;
   var day=new TabPage("Caixa do Dia"){BackColor=Color.White};
   var previous=new TabPage("Caixas Anteriores"){BackColor=Color.White};
   pages.TabPages.Add(day);pages.TabPages.Add(previous);
   dayButton.Click+=(s,e)=>{pages.SelectedIndex=0;RefreshAll();};
   archiveButton.Click+=(s,e)=>{pages.SelectedIndex=1;RefreshAll();};
   pages.SelectedIndexChanged+=(s,e)=>{
    dayButton.BackColor=pages.SelectedIndex==0?blue:Color.FromArgb(73,91,124);
    archiveButton.BackColor=pages.SelectedIndex==1?blue:Color.FromArgb(73,91,124);
    RefreshAll();
   };
   var todayLayout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,
    Padding=new Padding(7,6,7,5),BackColor=Color.White};
   todayLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   todayLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,27));
   todayLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,118));
   todayLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,27));
   todayLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   todayLayout.Controls.Add(Heading("CAIXAS ABERTOS OU FECHADOS HOJE"),0,0);
   todayLayout.Controls.Add(today,0,1);
   todayLayout.Controls.Add(Heading("ENTRADAS, SANGRIAS E SAÍDAS DO CAIXA SELECIONADO"),0,2);
   todayLayout.Controls.Add(movements,0,3);
   day.Controls.Add(todayLayout);
   var archiveLayout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,
    Padding=new Padding(7),BackColor=Color.White};
   archiveLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   archiveLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,43));
   archiveLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,182));
   archiveLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,29));
   archiveLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   var archiveHead=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};
   archiveHead.Controls.Add(new Label{Text="Caixas Anteriores  |  Buscar por data:",
    AutoSize=true,Margin=new Padding(3,10,12,0),
    Font=new Font("Segoe UI",9,FontStyle.Bold),ForeColor=blue});
   historyDate.Format=DateTimePickerFormat.Custom;historyDate.CustomFormat="dd/MM/yyyy";
   historyDate.Width=118;historyDate.ShowCheckBox=true;historyDate.Checked=false;
   historyDate.ValueChanged+=(s,e)=>LoadHistory();archiveHead.Controls.Add(historyDate);
   archiveHead.Controls.Add(ActionButton("Ver todos",99,blue,()=>{historyDate.Checked=false;LoadHistory();}));
   archiveLayout.Controls.Add(archiveHead,0,0);
   archiveLayout.Controls.Add(history,0,1);
   archiveLayout.Controls.Add(Heading("MOVIMENTOS DO CAIXA ANTERIOR SELECIONADO"),0,2);
   archiveLayout.Controls.Add(previousMovements,0,3);
   previous.Controls.Add(archiveLayout);
   foreach(var view in new[]{today,history,movements,previousMovements})StyleGrid(view);
   today.SelectionChanged+=(s,e)=>{
    if(pages.SelectedIndex!=0)return;
    activeSelection=SelectedId(today);
    LoadMovements();
    ShowSummary();
   };
   history.SelectionChanged+=(s,e)=>{
    if(pages.SelectedIndex!=1)return;
    activeSelection=SelectedId(history);
    LoadMovements();ShowSummary();
   };
   today.CellDoubleClick+=(s,e)=>{if(e.RowIndex>=0)EditSessionDialog();};
   history.CellDoubleClick+=(s,e)=>{if(e.RowIndex>=0)EditSessionDialog();};
   movements.CellDoubleClick+=(s,e)=>{if(e.RowIndex>=0)MovementEditDialog();};
   Controls.Add(pages);Controls.Add(caption);Controls.Add(totals);Controls.Add(nav);Controls.Add(bar);
   try{EnsureSchema();RefreshAll();}
   catch(Exception ex){caption.Text="CAIXA INDISPONÍVEL: "+ex.Message;
    MessageBox.Show("Não foi possível preparar o Caixa: "+ex.Message,"Caixa",
     MessageBoxButtons.OK,MessageBoxIcon.Error);}
  }
  static Label Heading(string text){
   return new Label{Text=text,Font=new Font("Segoe UI",8.8f,FontStyle.Bold),
    ForeColor=red,TextAlign=ContentAlignment.MiddleLeft,Dock=DockStyle.Fill};
  }
  static void StyleButton(Button b,Color color,int width){
   b.Width=width;b.Height=31;b.TextAlign=ContentAlignment.MiddleCenter;
   b.Margin=new Padding(2,2,2,1);b.FlatStyle=FlatStyle.Flat;
   b.FlatAppearance.BorderSize=0;b.BackColor=color;b.ForeColor=Color.White;
   b.Font=new Font("Segoe UI",8.5f,FontStyle.Bold);
  }
  static Button ActionButton(string text,int width,Color color,Action click){
   var b=new Button{Text=text};StyleButton(b,color,width);
   b.Click+=(s,e)=>click();return b;
  }
  static void StyleGrid(DataGridView grid){
   grid.Dock=DockStyle.Fill;grid.ReadOnly=true;grid.AllowUserToAddRows=false;
   grid.AllowUserToDeleteRows=false;grid.MultiSelect=false;
   grid.RowHeadersVisible=false;grid.ScrollBars=ScrollBars.Both;
   grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
   grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;
   grid.BackgroundColor=Color.White;grid.BorderStyle=BorderStyle.None;
   grid.ColumnHeadersHeight=36;grid.RowTemplate.Height=29;
   grid.EnableHeadersVisualStyles=false;
   grid.ColumnHeadersDefaultCellStyle.BackColor=blue;
   grid.ColumnHeadersDefaultCellStyle.ForeColor=Color.White;
   grid.ColumnHeadersDefaultCellStyle.Font=new Font("Segoe UI",9,FontStyle.Bold);
   grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(219,234,253);
   grid.DefaultCellStyle.SelectionForeColor=Color.Black;
   grid.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(244,248,254);
   grid.DataBindingComplete+=(s,e)=>{
    foreach(DataGridViewColumn c in grid.Columns){
     string n=c.Name.ToLowerInvariant();
     if(n.Contains("r$")||n.Contains("valor")||n.Contains("diferença")||
       n.Contains("troco")||n.Contains("saldo")||n.Contains("dinheiro")||
       n.Contains("esperado")||n.Contains("contado"))c.DefaultCellStyle.Format="N2";
     if(n=="id")c.Width=55;
     if(n.Contains("observação")||n.Contains("descrição"))c.FillWeight=175;
    }
   };
  }
  SQLiteConnection Connect(bool readOnly=false){
   if(!File.Exists(database))throw new Exception("Banco de dados não encontrado.");
   var c=new SQLiteConnection(new SQLiteConnectionStringBuilder{
    DataSource=database,ReadOnly=readOnly,FailIfMissing=true}.ToString());
   c.Open();return c;
  }
  static bool Table(SQLiteConnection c,string name){
   using(var q=new SQLiteCommand("SELECT 1 FROM sqlite_master WHERE type='table' AND name=@n",c)){
    q.Parameters.AddWithValue("@n",name);return q.ExecuteScalar()!=null;
   }
  }
  static bool Field(SQLiteConnection c,string table,string column){
   if(!Table(c,table))return false;
   using(var q=new SQLiteCommand("PRAGMA table_info(\""+table+"\")",c))
   using(var r=q.ExecuteReader())while(r.Read())if(string.Equals(Convert.ToString(r["name"]),column,StringComparison.OrdinalIgnoreCase))return true;
   return false;
  }
  static void Exec(SQLiteConnection c,SQLiteTransaction tx,string sql,params SQLiteParameter[] args){
   using(var q=new SQLiteCommand(sql,c,tx)){q.Parameters.AddRange(args);q.ExecuteNonQuery();}
  }
  static SQLiteParameter P(string key,object value){return new SQLiteParameter(key,value??DBNull.Value);}
  static decimal Dec(object o){return o==null||o==DBNull.Value?0m:Convert.ToDecimal(o,System.Globalization.CultureInfo.InvariantCulture);}
  static decimal ScalarMoney(SQLiteConnection c,string sql,DateTime begin,DateTime end){
   using(var q=new SQLiteCommand(sql,c)){
    q.Parameters.AddWithValue("@a",begin.ToString(Stamp));
    q.Parameters.AddWithValue("@b",end.ToString(Stamp));
    return Dec(q.ExecuteScalar());
   }
  }
  string Backup(SQLiteConnection c){
   string dataDir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ArmazemGratidaoNovo");
   string choice=Path.Combine(dataDir,"backup-location.txt");
   string unified=Path.Combine(Application.StartupPath,"Arquivos","Backups");
   string folder=File.Exists(choice)?File.ReadAllText(choice).Trim():
    Directory.Exists(unified)?unified:Path.Combine(dataDir,"Backups");
   if(string.IsNullOrWhiteSpace(folder)||!Path.IsPathRooted(folder))
    throw new Exception("A pasta configurada para backup é inválida; operação bloqueada.");
   Directory.CreateDirectory(folder);
   string file=Path.Combine(folder,"caixa-antes-alteracao-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+"-"+Guid.NewGuid().ToString("N").Substring(0,7)+".db");
   try{
    using(var dest=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=file}.ToString())){
     dest.Open();c.BackupDatabase(dest,"main","main",-1,null,0);
     using(var check=new SQLiteCommand("PRAGMA integrity_check",dest))
      if(Convert.ToString(check.ExecuteScalar())!="ok")throw new Exception("Backup não passou na verificação SQLite.");
    }
    if(new FileInfo(file).Length<128)throw new Exception("Backup gerado vazio.");
    return file;
   }catch{try{if(File.Exists(file))File.Delete(file);}catch{}throw;}
  }
  void EnsureSchema(){
   using(var c=Connect()){
    bool needs=!Table(c,"cash_sessions")||!Table(c,"cash_movements")||!Table(c,"cash_change_audit");
    if(!needs)return;
    Backup(c); // Mesmo a criação das tabelas exige backup prévio válido.
    using(var tx=c.BeginTransaction()){
     Exec(c,tx,"CREATE TABLE IF NOT EXISTS cash_sessions ("+
      "id INTEGER PRIMARY KEY AUTOINCREMENT,opened_at TEXT NOT NULL,opening_amount NUMERIC NOT NULL DEFAULT 0,"+
      "closed_at TEXT,closing_amount NUMERIC,expected_cash NUMERIC,difference NUMERIC,status TEXT NOT NULL DEFAULT 'ABERTO')");
     Exec(c,tx,"CREATE TABLE IF NOT EXISTS cash_movements ("+
      "id INTEGER PRIMARY KEY AUTOINCREMENT,session_id INTEGER NOT NULL,occurred_at TEXT NOT NULL,"+
      "kind TEXT NOT NULL,amount NUMERIC NOT NULL,description TEXT NOT NULL,recorded_at TEXT NOT NULL)");
     Exec(c,tx,"CREATE TABLE IF NOT EXISTS cash_change_audit ("+
      "id INTEGER PRIMARY KEY AUTOINCREMENT,session_id INTEGER,movement_id INTEGER,"+
      "action TEXT NOT NULL,previous_values TEXT,new_values TEXT,reason TEXT NOT NULL,changed_at TEXT NOT NULL)");
     tx.Commit();
    }
    foreach(string col in new[]{"id","opened_at","opening_amount","closed_at","closing_amount","expected_cash","difference","status"})
     if(!Field(c,"cash_sessions",col))throw new Exception("Caixa legado não possui a coluna "+col+". Os dados foram preservados.");
   }
  }
  static string ToStamp(DateTime d){return d.ToString(Stamp);}
  static DateTime ParseDate(object o){DateTime date;if(!DateTime.TryParse(Convert.ToString(o),out date))return DateTime.Now;return date;}
  void Audit(SQLiteConnection c,SQLiteTransaction tx,long? sid,long? mid,string action,string oldValue,string newValue,string reason){
   Exec(c,tx,"INSERT INTO cash_change_audit(session_id,movement_id,action,previous_values,new_values,reason,changed_at) "+
    "VALUES(@s,@m,@action,@old,@new,@reason,@dt)",
    P("@s",sid.HasValue?(object)sid.Value:DBNull.Value),P("@m",mid.HasValue?(object)mid.Value:DBNull.Value),
    P("@action",action),P("@old",oldValue),P("@new",newValue),
    P("@reason",reason),P("@dt",ToStamp(DateTime.Now)));
  }
  void Write(Action<SQLiteConnection,SQLiteTransaction> job){
   using(var c=Connect()){
    Backup(c); // SEM backup, não faz nenhuma alteração financeira.
    using(var tx=c.BeginTransaction()){
     job(c,tx);tx.Commit();
    }
   }
   RefreshAll();
  }
  static long Id(SQLiteConnection c,SQLiteTransaction tx){
   using(var q=new SQLiteCommand("SELECT last_insert_rowid()",c,tx))return Convert.ToInt64(q.ExecuteScalar());
  }
  internal long OpenSession(DateTime when,decimal opening,string note){
   if(opening<0)throw new Exception("Troco inicial não pode ser negativo.");
   if(string.IsNullOrWhiteSpace(note))throw new Exception("Informe uma observação da abertura.");
   long result=0;
   Write((c,tx)=>{
    using(var q=new SQLiteCommand("SELECT COUNT(*) FROM cash_sessions WHERE UPPER(COALESCE(status,'')) IN ('ABERTO','OPEN','ABERTA')",c,tx))
     if(Convert.ToInt64(q.ExecuteScalar())>0)throw new Exception("Existe outro caixa aberto. Feche-o antes de abrir um novo.");
    Exec(c,tx,"INSERT INTO cash_sessions(opened_at,opening_amount,status) VALUES(@at,@amount,'ABERTO')",
     P("@at",ToStamp(when)),P("@amount",opening));
    result=Id(c,tx);Audit(c,tx,result,null,"ABERTURA",null,"Troco R$ "+opening.ToString("N2"),note);
   });return result;
  }
  internal long AddMovement(long sessionId,DateTime when,string kind,decimal amount,string note){
   if(amount<=0)throw new Exception("O valor da movimentação deve ser maior que zero.");
   if(string.IsNullOrWhiteSpace(note))throw new Exception("Informe o motivo ou a descrição.");
   if(!new[]{"ENTRADA","SANGRIA","SAIDA","AJUSTE_ENTRADA","AJUSTE_SAIDA"}.Contains(kind))
    throw new Exception("Tipo de movimentação inválido.");
   long result=0;
   Write((c,tx)=>{
    if(!HasSession(c,tx,sessionId))throw new Exception("Caixa não encontrado.");
    Exec(c,tx,"INSERT INTO cash_movements(session_id,occurred_at,kind,amount,description,recorded_at) "+
     "VALUES(@sid,@at,@kind,@amount,@note,@rec)",
     P("@sid",sessionId),P("@at",ToStamp(when)),P("@kind",kind),P("@amount",amount),
     P("@note",note.Trim()),P("@rec",ToStamp(DateTime.Now)));
    result=Id(c,tx);
    Audit(c,tx,sessionId,result,"MOVIMENTO NOVO",null,kind+" R$ "+amount.ToString("N2")+" em "+ToStamp(when),note);
    RecalculateClosed(c,tx,sessionId);
   });return result;
  }
  static bool HasSession(SQLiteConnection c,SQLiteTransaction tx,long id){
   using(var q=new SQLiteCommand("SELECT 1 FROM cash_sessions WHERE id=@id",c,tx)){
    q.Parameters.AddWithValue("@id",id);return q.ExecuteScalar()!=null;
   }
  }
  static string SessionSnapshot(SQLiteConnection c,SQLiteTransaction tx,long id){
   using(var q=new SQLiteCommand("SELECT opened_at,opening_amount,closed_at,closing_amount,expected_cash,difference,status FROM cash_sessions WHERE id=@id",c,tx)){
    q.Parameters.AddWithValue("@id",id);
    using(var rd=q.ExecuteReader()){
     if(!rd.Read())throw new Exception("Caixa não encontrado.");
     return string.Join(" | ",Enumerable.Range(0,7).Select(i=>rd.IsDBNull(i)?"":Convert.ToString(rd.GetValue(i))).ToArray());
    }
   }
  }
  internal void EditSession(long id,DateTime opened,decimal initial,bool closed,DateTime closeAt,
   decimal closing,decimal expected,string reason){
   if(string.IsNullOrWhiteSpace(reason))throw new Exception("Informe o motivo da correção.");
   if(initial<0||closing<0||expected<0)throw new Exception("Os valores do caixa não podem ser negativos.");
   if(closed&&closeAt<opened)throw new Exception("Fechamento anterior à abertura.");
   Write((c,tx)=>{
    string old=SessionSnapshot(c,tx,id);
    if(!closed){
     using(var q=new SQLiteCommand("SELECT COUNT(*) FROM cash_sessions WHERE id<>@id AND UPPER(COALESCE(status,'')) IN ('ABERTO','OPEN','ABERTA')",c,tx)){
      q.Parameters.AddWithValue("@id",id);
      if(Convert.ToInt64(q.ExecuteScalar())>0)throw new Exception("Já existe outro caixa aberto.");
     }
    }
    Exec(c,tx,"UPDATE cash_sessions SET opened_at=@at,opening_amount=@initial,"+
     "closed_at=@close,closing_amount=@closing,expected_cash=@expected,difference=@diff,status=@status WHERE id=@id",
     P("@at",ToStamp(opened)),P("@initial",initial),
     P("@close",closed?(object)ToStamp(closeAt):DBNull.Value),
     P("@closing",closed?(object)closing:DBNull.Value),
     P("@expected",closed?(object)expected:DBNull.Value),
     P("@diff",closed?(object)(closing-expected):DBNull.Value),
     P("@status",closed?"FECHADO":"ABERTO"),P("@id",id));
    Audit(c,tx,id,null,"CORREÇÃO DO CAIXA",old,SessionSnapshot(c,tx,id),reason);
   });
  }
  internal void CloseSession(long sessionId,DateTime when,decimal counted,string reason){
   if(string.IsNullOrWhiteSpace(reason))throw new Exception("Informe observações do fechamento.");
   if(counted<0)throw new Exception("Valor contado não pode ser negativo.");
   DateTime opened;decimal initial;
   using(var c=Connect(true))using(var q=new SQLiteCommand("SELECT opened_at,opening_amount FROM cash_sessions WHERE id=@id",c)){
    q.Parameters.AddWithValue("@id",sessionId);
    using(var rd=q.ExecuteReader()){if(!rd.Read())throw new Exception("Caixa não encontrado.");opened=ParseDate(rd[0]);initial=Dec(rd[1]);}
   }
   if(when<opened)throw new Exception("O fechamento não pode ser anterior à abertura.");
   decimal expected=Expected(sessionId,opened,when,initial);
   EditSession(sessionId,opened,initial,true,when,counted,expected,"FECHAMENTO: "+reason);
  }
  internal void EditMovement(long id,DateTime when,string kind,decimal amount,string description,string reason){
   if(amount<=0||string.IsNullOrWhiteSpace(description)||string.IsNullOrWhiteSpace(reason))
    throw new Exception("Informe valor positivo, descrição e motivo da correção.");
   if(!new[]{"ENTRADA","SANGRIA","SAIDA","AJUSTE_ENTRADA","AJUSTE_SAIDA"}.Contains(kind))
    throw new Exception("Tipo inválido.");
   Write((c,tx)=>{
    long sid;string before;
    using(var q=new SQLiteCommand("SELECT session_id,occurred_at,kind,amount,description FROM cash_movements WHERE id=@id",c,tx)){
     q.Parameters.AddWithValue("@id",id);
     using(var rd=q.ExecuteReader()){
      if(!rd.Read())throw new Exception("Movimento não encontrado.");
      sid=Convert.ToInt64(rd[0]);before=string.Join(" | ",Enumerable.Range(1,4).Select(i=>Convert.ToString(rd.GetValue(i))).ToArray());
     }
    }
    Exec(c,tx,"UPDATE cash_movements SET occurred_at=@at,kind=@kind,amount=@amount,description=@description WHERE id=@id",
     P("@at",ToStamp(when)),P("@kind",kind),P("@amount",amount),P("@description",description.Trim()),P("@id",id));
    Audit(c,tx,sid,id,"CORREÇÃO DE MOVIMENTO",before,ToStamp(when)+" | "+kind+" | "+amount.ToString("N2")+" | "+description,reason);
    RecalculateClosed(c,tx,sid);
   });
  }
  internal void DeleteMovement(long id,string reason){
   if(string.IsNullOrWhiteSpace(reason))throw new Exception("Informe o motivo da exclusão.");
   Write((c,tx)=>{
    long sid;string before;
    using(var q=new SQLiteCommand("SELECT session_id,occurred_at,kind,amount,description FROM cash_movements WHERE id=@id",c,tx)){
     q.Parameters.AddWithValue("@id",id);
     using(var rd=q.ExecuteReader()){
      if(!rd.Read())throw new Exception("Movimento não encontrado.");
      sid=Convert.ToInt64(rd[0]);before=string.Join(" | ",Enumerable.Range(1,4).Select(i=>Convert.ToString(rd.GetValue(i))).ToArray());
     }
    }
    Exec(c,tx,"DELETE FROM cash_movements WHERE id=@id",P("@id",id));
    Audit(c,tx,sid,id,"EXCLUSÃO DE MOVIMENTO",before,null,reason);
    RecalculateClosed(c,tx,sid);
   });
  }
  decimal Expected(long sessionId,DateTime opened,DateTime at,decimal initial){
   using(var c=Connect(true))return Calculate(c,sessionId,opened,at,initial);
  }
  static decimal Calculate(SQLiteConnection c,long sessionId,DateTime opened,DateTime at,decimal initial){
   decimal income=0,outflow=0;
   using(var q=new SQLiteCommand("SELECT kind,amount FROM cash_movements WHERE session_id=@id AND occurred_at<=@at",c)){
    q.Parameters.AddWithValue("@id",sessionId);q.Parameters.AddWithValue("@at",ToStamp(at));
    using(var rd=q.ExecuteReader())while(rd.Read()){
     string kind=Convert.ToString(rd[0]);decimal money=Dec(rd[1]);
     if(kind=="ENTRADA"||kind=="AJUSTE_ENTRADA")income+=money;else outflow+=money;
    }
   }
   decimal sales=0,credit=0;
   if(Table(c,"sales")){
    if(Table(c,"sale_payments")){
     sales+=ScalarMoney(c,"SELECT COALESCE(SUM(p.amount),0) FROM sale_payments p JOIN sales s ON s.id=p.sale_id "+
      "WHERE UPPER(TRIM(COALESCE(p.payment_method,''))) LIKE '%DINHEIRO%' "+
      "AND s.created_at BETWEEN @a AND @b "+
      "AND UPPER(COALESCE(s.status,'')) NOT LIKE 'CANCEL%' AND UPPER(COALESCE(s.status,'')) NOT LIKE 'EXCLU%'",opened,at);
    }
    sales+=ScalarMoney(c,"SELECT COALESCE(SUM(s.gross_total),0) FROM sales s "+
     "WHERE UPPER(TRIM(COALESCE(s.payment_method,'')))='DINHEIRO' "+
     "AND s.created_at BETWEEN @a AND @b "+
     "AND UPPER(COALESCE(s.status,'')) NOT LIKE 'CANCEL%' AND UPPER(COALESCE(s.status,'')) NOT LIKE 'EXCLU%'"+
     (Table(c,"sale_payments")?" AND NOT EXISTS (SELECT 1 FROM sale_payments p WHERE p.sale_id=s.id)":""),
     opened,at);
   }
   if(Table(c,"credit_payments")){
    string filter=Field(c,"credit_payments","note")?
     " AND UPPER(COALESCE(note,'')) NOT LIKE 'PAGAMENTO INICIAL DE VENDA DIVIDIDA%'":"";
    credit+=ScalarMoney(c,"SELECT COALESCE(SUM(amount),0) FROM credit_payments "+
     "WHERE UPPER(TRIM(COALESCE(payment_method,''))) LIKE '%DINHEIRO%' "+
     "AND paid_at BETWEEN @a AND @b"+filter,opened,at);
   }
   return Math.Round(initial+income-outflow+sales+credit,2);
  }
  static void RecalculateClosed(SQLiteConnection c,SQLiteTransaction tx,long sid){
   DateTime opened,closed;decimal initial,counted;bool isClosed;
   using(var q=new SQLiteCommand("SELECT opened_at,opening_amount,closed_at,closing_amount,status FROM cash_sessions WHERE id=@id",c,tx)){
    q.Parameters.AddWithValue("@id",sid);
    using(var rd=q.ExecuteReader()){
     if(!rd.Read())return;
     opened=ParseDate(rd[0]);initial=Dec(rd[1]);
     isClosed=!rd.IsDBNull(2)&&Convert.ToString(rd[4]).ToUpperInvariant().Contains("FECH");
     closed=isClosed?ParseDate(rd[2]):DateTime.Now;counted=Dec(rd[3]);
    }
   }
   if(!isClosed)return;
   decimal expected=Calculate(c,sid,opened,closed,initial);
   Exec(c,tx,"UPDATE cash_sessions SET expected_cash=@ex,difference=@dif WHERE id=@id",
    P("@ex",expected),P("@dif",counted-expected),P("@id",sid));
  }
  static long? SelectedId(DataGridView grid){
   if(grid.CurrentRow==null||!grid.Columns.Contains("ID"))return null;
   object value=grid.CurrentRow.Cells["ID"].Value;
   return value==null||value==DBNull.Value? (long?)null:Convert.ToInt64(value);
  }
  long SelectedSession(){
   long? id=activeSelection??SelectedId(pages.SelectedIndex==0?today:history);
   if(!id.HasValue)throw new Exception("Selecione um caixa primeiro.");
   return id.Value;
  }
  long SelectedMovement(){
   var view=pages.SelectedIndex==1?previousMovements:movements;
   if(view.CurrentRow==null||!view.Columns.Contains("ID"))
    throw new Exception("Selecione um movimento da lista antes de editar.");
   return Convert.ToInt64(view.CurrentRow.Cells["ID"].Value);
  }
  void RefreshAll(){
   if(IsDisposed||!File.Exists(database))return;
   try{
    LoadToday();LoadHistory();LoadMovements();ShowSummary();
    caption.Text="Caixa do Dia • Histórico em Caixas Anteriores • Duplo clique para editar. Toda alteração tem backup e auditoria.";
   }catch(Exception ex){caption.Text="Erro no Caixa: "+ex.Message;}
  }
  static DataTable Read(SQLiteConnection c,string sql,params SQLiteParameter[] pars){
   var dt=new DataTable();
   using(var q=new SQLiteCommand(sql,c)){
    q.Parameters.AddRange(pars);
    using(var adapter=new SQLiteDataAdapter(q))adapter.Fill(dt);
   }
   return dt;
  }
  const string SessionColumns="id AS 'ID',opened_at AS 'Abertura',opening_amount AS 'Troco inicial (R$)',"+
   "closed_at AS 'Fechamento',closing_amount AS 'Contado (R$)',expected_cash AS 'Esperado (R$)',"+
   "difference AS 'Diferença (R$)',status AS 'Situação'";
  void LoadToday(){
   long? prev=SelectedId(today);
   using(var c=Connect(true))
    today.DataSource=Read(c,"SELECT "+SessionColumns+" FROM cash_sessions "+
     "WHERE date(opened_at)=date('now','localtime') ORDER BY opened_at DESC,id DESC");
   if(prev.HasValue)SelectId(today,prev.Value);
  }
  void LoadHistory(){
   long? prev=SelectedId(history);
   using(var c=Connect(true)){
    string query="SELECT "+SessionColumns+" FROM cash_sessions WHERE date(opened_at)<>date('now','localtime')";
    if(historyDate.Checked)query+=" AND date(opened_at)=@date";
    query+=" ORDER BY opened_at DESC,id DESC LIMIT 2000";
    history.DataSource=historyDate.Checked?
     Read(c,query,P("@date",historyDate.Value.ToString("yyyy-MM-dd"))):
     Read(c,query);
   }
   if(prev.HasValue)SelectId(history,prev.Value);
  }
  static void SelectId(DataGridView grid,long id){
   foreach(DataGridViewRow row in grid.Rows)
    if(!row.IsNewRow&&Convert.ToInt64(row.Cells["ID"].Value)==id){
     grid.CurrentCell=row.Cells["ID"];return;
    }
  }
  void LoadMovements(){
   bool older=pages.SelectedIndex==1;
   var target=older?previousMovements:movements;
   if(older)movements.DataSource=null;else previousMovements.DataSource=null;
   long? id=SelectedId(older?history:today);
   if(!id.HasValue){target.DataSource=null;return;}
   using(var c=Connect(true))
    target.DataSource=Read(c,"SELECT id AS 'ID',occurred_at AS 'Data / hora',"+
     "kind AS 'Tipo',amount AS 'Valor (R$)',description AS 'Descrição' "+
     "FROM cash_movements WHERE session_id=@id ORDER BY occurred_at DESC,id DESC",P("@id",id.Value));
  }
  void ShowSummary(){
   long? id=SelectedId(pages.SelectedIndex==0?today:history);
   if(!id.HasValue){totals.Text=pages.SelectedIndex==0?
     "CAIXA DO DIA  |  Nenhum caixa aberto hoje. Clique em Abrir Caixa.":
     "CAIXAS ANTERIORES  |  Selecione um registro para consultar.";return;}
   try{
    using(var c=Connect(true))
    using(var q=new SQLiteCommand("SELECT opened_at,opening_amount,closed_at,closing_amount,status,expected_cash,difference FROM cash_sessions WHERE id=@id",c)){
     q.Parameters.AddWithValue("@id",id.Value);
     using(var rd=q.ExecuteReader()){
      if(!rd.Read())return;
      DateTime opened=ParseDate(rd[0]),at=rd.IsDBNull(2)?DateTime.Now:ParseDate(rd[2]);
      decimal initial=Dec(rd[1]),counted=Dec(rd[3]);
      string state=Convert.ToString(rd[4]);
      bool finished=!rd.IsDBNull(2);
      decimal savedExpected=Dec(rd[5]),savedDifference=Dec(rd[6]);
      rd.Close();
      decimal systemExpected=Calculate(c,id.Value,opened,at,initial);
      totals.Text="CAIXA #"+id+"  •  "+state+"  •  Abertura: "+opened.ToString("dd/MM/yyyy HH:mm")+
       Environment.NewLine+"Troco inicial R$ "+initial.ToString("N2")+
       "   |   Esperado R$ "+(finished?savedExpected:systemExpected).ToString("N2")+
       "   |   Contado R$ "+counted.ToString("N2")+
       "   |   Diferença R$ "+(finished?savedDifference:counted-systemExpected).ToString("N2")+
       Environment.NewLine+"Cálculo automático R$ "+systemExpected.ToString("N2")+
       (finished&&savedExpected!=systemExpected?"   •   Existe correção manual do previsto":"")+
       "   •   Vendas e pagamentos originais preservados.";
     }
    }
   }catch(Exception ex){totals.Text="Não foi possível apurar o caixa: "+ex.Message;}
  }

  // Formularios de edição: todas as datas, horários e valores são editáveis.
  static NumericUpDown Money(decimal value=0){
   var v=new NumericUpDown{DecimalPlaces=2,Minimum=0,Maximum=1000000000m,
    ThousandsSeparator=true,Width=180,Value=Math.Min(1000000000m,Math.Max(0,value))};
   return v;
  }
  static DateTimePicker DateField(DateTime date){
   return new DateTimePicker{Format=DateTimePickerFormat.Custom,CustomFormat="dd/MM/yyyy HH:mm:ss",
    ShowUpDown=true,Width=195,Value=date};
  }
  static TextBox Note(string initial=""){return new TextBox{Width=270,Text=initial,MaxLength=600};}
  static ComboBox Kinds(string current){
   var b=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=190};
   b.Items.AddRange(new object[]{"ENTRADA","SANGRIA","SAIDA","AJUSTE_ENTRADA","AJUSTE_SAIDA"});
   b.SelectedItem=current;if(b.SelectedIndex<0)b.SelectedIndex=0;return b;
  }
  // O cabecalho possui espaco proprio; a Calculadora nao cobre os campos.
  static void PlaceCashDialog(StyledDialogForm dialog,Control content,string heading){
   var header=new Panel{Name="amzgDialogActionHeader",Dock=DockStyle.None,
    BackColor=Color.FromArgb(225,237,252),Height=42};
   var title=new Label{Name="amzgCashDialogHeading",Text=heading,
    Left=14,Top=8,Width=330,Height=26,
    Font=new Font("Segoe UI",9f,FontStyle.Bold),
    ForeColor=Color.FromArgb(20,68,137),AutoEllipsis=true};
   header.Controls.Add(title);
   content.Dock=DockStyle.None;
   content.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;
   header.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
   dialog.Controls.Add(content);
   dialog.Controls.Add(header);
   Action position=()=>{
    if(dialog.IsDisposed)return;
    int width=dialog.ClientSize.Width,height=dialog.ClientSize.Height;
    if(width<=0||height<=42)return;
    var headerRect=new Rectangle(0,0,width,42);
    var contentRect=new Rectangle(0,42,width,height-42);
    if(header.Bounds!=headerRect)header.Bounds=headerRect;
    if(content.Bounds!=contentRect)content.Bounds=contentRect;
    title.Width=Math.Max(120,width-175);
    header.BringToFront();
   };
   dialog.ClientSizeChanged+=(s,e)=>position();
   dialog.Shown+=(s,e)=>position();
   position();
  }
  bool FormFields(string title,Control[] fields,string[] labels,Action save){
   if(fields.Length!=labels.Length)throw new ArgumentException("Rótulos e campos divergentes.");
   var available=Screen.FromControl(this).WorkingArea;
   int dialogWidth=Math.Min(670,Math.Max(480,available.Width-36));
   int dialogHeight=Math.Min(Math.Max(340,108+fields.Length*52+54),
    Math.Max(330,available.Height-38));
   using(var dialog=new StyledDialogForm{Text=title,
    Name="amzgCashEditorDialog",StartPosition=FormStartPosition.CenterParent,
    Width=dialogWidth,Height=dialogHeight,
    MinimumSize=new Size(Math.Min(520,dialogWidth),Math.Min(310,dialogHeight)),
    BackColor=Color.FromArgb(243,248,254),
    AutoScaleMode=AutoScaleMode.Dpi,FormBorderStyle=FormBorderStyle.Sizable}){
    var layout=new TableLayoutPanel{Name="amzgCashEditorFields",
     Dock=DockStyle.None,ColumnCount=2,RowCount=fields.Length+1,
     AutoScroll=true,Padding=new Padding(16,11,15,11),
     BackColor=Color.FromArgb(246,250,255),Margin=Padding.Empty};
    layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,205));
    layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
    for(int i=0;i<fields.Length;i++){
     layout.RowStyles.Add(new RowStyle(SizeType.Absolute,52));
     var captionLabel=new Label{Name="amzgCashFieldLabel"+i,
      Text=labels[i],Dock=DockStyle.Fill,AutoEllipsis=false,
      Padding=new Padding(0,0,7,0),TextAlign=ContentAlignment.MiddleLeft,
      ForeColor=blue,Font=new Font("Segoe UI",9f,FontStyle.Bold)};
     layout.Controls.Add(captionLabel,0,i);
     fields[i].Name=string.IsNullOrEmpty(fields[i].Name)?
      "amzgCashField"+i:fields[i].Name;
     fields[i].Dock=DockStyle.Fill;
     fields[i].Margin=new Padding(4,10,5,10);
     layout.Controls.Add(fields[i],1,i);
    }
    layout.RowStyles.Add(new RowStyle(SizeType.Absolute,57));
    var actions=new FlowLayoutPanel{Name="amzgCashSaveActions",
     Dock=DockStyle.Fill,WrapContents=false,AutoScroll=true,Padding=new Padding(2,5,0,0)};
    var confirm=ActionButton("Salvar",117,blue,()=>{
     try{save();dialog.DialogResult=DialogResult.OK;dialog.Close();}
     catch(Exception ex){MessageBox.Show(dialog,ex.Message,"Caixa: operação não salva",
      MessageBoxButtons.OK,MessageBoxIcon.Warning);}
    });
    var cancel=ActionButton("Cancelar",117,red,()=>dialog.Close());
    actions.Controls.Add(confirm);actions.Controls.Add(cancel);
    layout.Controls.Add(actions,1,fields.Length);
    PlaceCashDialog(dialog,layout,title);
    dialog.AcceptButton=confirm;dialog.CancelButton=cancel;
    return dialog.ShowDialog(this)==DialogResult.OK;
   }
  }
  void OpenDialog(){
   var date=DateField(DateTime.Now);var opening=Money(0);var note=Note("Troco inicial do caixa");
   FormFields("Abrir Caixa",new Control[]{date,opening,note},
    new[]{"Data e hora","Troco de abertura R$","Observação obrigatória"},
    ()=>OpenSession(date.Value,opening.Value,note.Text.Trim()));
  }
  void MovementDialog(string type){
   try{
    long sid=SelectedSession();var dt=DateField(DateTime.Now);var kind=Kinds(type);var amount=Money(0);var note=Note();
    FormFields("Registrar entrada, sangria ou saída | Caixa #"+sid,
     new Control[]{dt,kind,amount,note},
     new[]{"Data e hora","Tipo de movimento","Valor R$","Motivo / descrição"},
     ()=>AddMovement(sid,dt.Value,Convert.ToString(kind.SelectedItem),amount.Value,note.Text.Trim()));
   }catch(Exception ex){MessageBox.Show(ex.Message,"Caixa");}
  }
  void CloseDialog(){
   try{
    long id=SelectedSession();
    DateTime dt=DateTime.Now,opened=DateTime.Now;decimal initial=0;
    using(var c=Connect(true))using(var q=new SQLiteCommand("SELECT opened_at,opening_amount,status FROM cash_sessions WHERE id=@id",c)){
     q.Parameters.AddWithValue("@id",id);
     using(var rd=q.ExecuteReader()){
      if(!rd.Read())throw new Exception("Caixa não encontrado.");
      opened=ParseDate(rd[0]);initial=Dec(rd[1]);
      if(Convert.ToString(rd[2]).ToUpperInvariant().Contains("FECH"))
       throw new Exception("Caixa já fechado. Use Editar Caixa para corrigi-lo.");
     }
    }
    decimal expected=Expected(id,opened,dt,initial);
    var date=DateField(dt);
    var counted=Money(expected<0?0:expected);
    var note=Note("Contagem de dinheiro em espécie");
    var tip=new Label{Text="Saldo previsto: R$ "+expected.ToString("N2")+
     "  |  Compare com a contagem física.",Dock=DockStyle.Fill,ForeColor=blue};
    FormFields("Fechar Caixa #"+id,new Control[]{date,counted,note,tip},
     new[]{"Data e hora final","Dinheiro contado R$","Observação","Conferência"},
     ()=>CloseSession(id,date.Value,counted.Value,note.Text.Trim()));
   }catch(Exception ex){MessageBox.Show(ex.Message,"Caixa");}
  }
  void EditSessionDialog(){
   try{
    long id=SelectedSession();
    DateTime opened,closedAt;decimal opening,closing,expected;bool closed;
    using(var c=Connect(true))using(var q=new SQLiteCommand("SELECT opened_at,opening_amount,closed_at,closing_amount,expected_cash,status FROM cash_sessions WHERE id=@id",c)){
     q.Parameters.AddWithValue("@id",id);
     using(var rd=q.ExecuteReader()){
      if(!rd.Read())throw new Exception("Caixa não encontrado.");
      opened=ParseDate(rd[0]);opening=Dec(rd[1]);
      closed=!rd.IsDBNull(2)&&Convert.ToString(rd[5]).ToUpperInvariant().Contains("FECH");
      closedAt=closed?ParseDate(rd[2]):DateTime.Now;
      closing=Dec(rd[3]);expected=Dec(rd[4]);
     }
    }
    var date=DateField(opened);
    var initial=Money(opening);
    var isClosed=new CheckBox{Text="Caixa fechado",Checked=closed};
    var finish=DateField(closedAt);
    var counted=Money(closing);
    var forecast=Money(expected);
    var reason=Note();
    FormFields("Editar todos os dados | Caixa #"+id,
     new Control[]{date,initial,isClosed,finish,counted,forecast,reason},
     new[]{"Abertura: data e hora","Troco inicial R$","Situação","Fechamento: data e hora",
      "Valor contado R$","Valor esperado R$","Motivo da correção"},
     ()=>EditSession(id,date.Value,initial.Value,isClosed.Checked,finish.Value,
      counted.Value,forecast.Value,reason.Text.Trim()));
   }catch(Exception ex){MessageBox.Show(ex.Message,"Caixa");}
  }
  void MovementEditDialog(){
   try{
    long id=SelectedMovement();
    DateTime date;string kind,description;decimal amount;
    using(var c=Connect(true))using(var q=new SQLiteCommand("SELECT occurred_at,kind,amount,description FROM cash_movements WHERE id=@id",c)){
     q.Parameters.AddWithValue("@id",id);
     using(var rd=q.ExecuteReader()){
      if(!rd.Read())throw new Exception("Movimento não encontrado.");
      date=ParseDate(rd[0]);kind=Convert.ToString(rd[1]);
      amount=Dec(rd[2]);description=Convert.ToString(rd[3]);
     }
    }
    var when=DateField(date);
    var type=Kinds(kind);
    var money=Money(amount);
    var text=Note(description);
    var reason=Note();
    FormFields("Editar movimento #"+id,
     new Control[]{when,type,money,text,reason},
     new[]{"Data e hora","Tipo","Valor R$","Descrição","Motivo da correção"},
     ()=>EditMovement(id,when.Value,Convert.ToString(type.SelectedItem),money.Value,
      text.Text.Trim(),reason.Text.Trim()));
   }catch(Exception ex){MessageBox.Show(ex.Message,"Caixa");}
  }
  void DeleteMovement(){
   try{
    long id=SelectedMovement();
    if(MessageBox.Show("Excluir lançamento #"+id+"? O histórico da exclusão ficará na auditoria.",
     "Caixa",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
    var reason=Note();FormFields("Excluir movimento #"+id,
     new Control[]{reason},new[]{"Motivo da exclusão"},()=>DeleteMovement(id,reason.Text.Trim()));
   }catch(Exception ex){MessageBox.Show(ex.Message,"Caixa");}
  }
  void ShowAudit(){
   try{
    var dt=new DataTable();
    using(var c=Connect(true))dt=Read(c,"SELECT changed_at AS 'Quando',session_id AS 'Caixa',"+
     "movement_id AS 'Movimento',action AS 'Alteração',previous_values AS 'Antes',"+
     "new_values AS 'Depois',reason AS 'Motivo' FROM cash_change_audit ORDER BY id DESC LIMIT 1000");
    using(var form=new StyledDialogForm{Text="Caixa | Auditoria de correções",Width=890,Height=510,
     StartPosition=FormStartPosition.CenterParent}){
     var view=new DataGridView{Dock=DockStyle.None,ReadOnly=true,
      DataSource=dt,AllowUserToAddRows=false,RowHeadersVisible=false,
      AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
     PlaceCashDialog(form,view,"AUDITORIA DE CORREÇÕES");
     form.ShowDialog(this);
    }
   }catch(Exception ex){MessageBox.Show(ex.Message,"Auditoria do Caixa");}
  }
 }
}
