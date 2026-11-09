using System;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using System.IO;
namespace ArmazemGratidao {
 public sealed class LucroForm : Form {
  readonly string db;
  readonly DataGridView grid=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
  readonly Label[] metricValues=new Label[7];
  readonly Label summary=new Label {Dock=DockStyle.Bottom,Height=28,Font=new Font("Segoe UI",9,FontStyle.Regular),ForeColor=Color.FromArgb(35,65,110),BackColor=Color.FromArgb(229,242,255),Padding=new Padding(12,4,0,0)};
  readonly TextBox search=new TextBox {Width=370,Font=new Font("Segoe UI",11)};
  readonly DataGridView returnsGrid=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
  decimal available;
  readonly Label reserveAvailable=new Label{Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(14,5,0,5),Font=new Font("Segoe UI",15,FontStyle.Bold),ForeColor=Color.FromArgb(18,90,64)};
  readonly Label remainingLabel=new Label{Dock=DockStyle.Top,Height=65,Padding=new Padding(14,12,10,8),Font=new Font("Segoe UI",14,FontStyle.Bold),ForeColor=Color.FromArgb(20,69,147),BackColor=Color.White};
  static readonly Color MainBlue=Color.FromArgb(22,75,160);
  static readonly Color LightBackground=Color.FromArgb(229,242,255);
  static readonly Color HeaderColor=Color.FromArgb(20,58,117);
  static void StyleGrid(DataGridView table){
   table.BackgroundColor=Color.White;
   table.BorderStyle=BorderStyle.None;
   table.RowHeadersVisible=false;
   table.SelectionMode=DataGridViewSelectionMode.FullRowSelect;
   table.MultiSelect=false;
   table.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
   table.RowTemplate.Height=35;
   table.ColumnHeadersHeight=43;
   table.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
   table.EnableHeadersVisualStyles=false;
   table.ColumnHeadersDefaultCellStyle.BackColor=MainBlue;
   table.ColumnHeadersDefaultCellStyle.ForeColor=Color.White;
   table.ColumnHeadersDefaultCellStyle.Font=new Font("Segoe UI",10,FontStyle.Bold);
   table.DefaultCellStyle.Font=new Font("Segoe UI",10);
   table.DefaultCellStyle.SelectionBackColor=Color.FromArgb(40,100,170);
   table.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(239,245,252);
   table.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;
   table.ScrollBars=ScrollBars.Both;
  }
  Control MetricCard(int index,string title){
   var card=new Panel{Width=214,Height=80,Margin=new Padding(4,4,4,4),BackColor=Color.White,BorderStyle=BorderStyle.FixedSingle};
   var caption=new Label{Text=title,Left=11,Top=8,Width=192,Height=23,Font=new Font("Segoe UI",9,FontStyle.Bold),ForeColor=Color.FromArgb(60,80,105)};
   var value=new Label{Text="R$ 0,00",Left=11,Top=34,Width=192,Height=34,Font=new Font("Segoe UI",15,FontStyle.Bold),ForeColor=index==6?Color.FromArgb(20,115,82):MainBlue};
   metricValues[index]=value;
   card.Controls.Add(caption);card.Controls.Add(value);
   return card;
  }
  public LucroForm(string database){
   db=database;Text="Armazém Gratidão | Lucro";Width=1250;Height=800;MinimumSize=new Size(870,610);BackColor=LightBackground;
   var working=Screen.FromControl(this).WorkingArea;Size=new Size(Math.Min(Width,working.Width),Math.Min(Height,working.Height));
   var tabs=new TabControl{Dock=DockStyle.Fill,Font=new Font("Segoe UI",10,FontStyle.Bold),Padding=new Point(16,9)};
   var products=new TabPage("Lucro por produto"){BackColor=LightBackground};
   var history=new TabPage("Devoluções dos 10%"){BackColor=LightBackground};
   var bar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=65,BackColor=LightBackground,Padding=new Padding(12,11,8,5),WrapContents=false,AutoScroll=true};
   bar.Controls.Add(new Label{Text="Pesquisar produto ou código:",AutoSize=true,Padding=new Padding(0,9,7,0),ForeColor=HeaderColor,Font=new Font("Segoe UI",10,FontStyle.Bold)});
   bar.Controls.Add(search);
   var refresh=new Button{Text="Atualizar",Width=110,Height=36,BackColor=MainBlue,ForeColor=Color.White,FlatStyle=FlatStyle.Flat};
   refresh.Click+=(sender,args)=>LoadData();bar.Controls.Add(refresh);
   var clear=new Button{Text="Limpar busca",Width=130,Height=36};
   clear.Click+=(sender,args)=>{search.Clear();search.Focus();};bar.Controls.Add(clear);
   var cards=new FlowLayoutPanel{Dock=DockStyle.Top,Height=190,Padding=new Padding(8),WrapContents=true,AutoScroll=true,BackColor=LightBackground};
   string[] labels={"Total vendido","Custo dos produtos","Taxas","Lucro líquido","Reserva 10% acumulada","Total já devolvido","Reserva 10% disponível"};
   for(int i=0;i<labels.Length;i++)cards.Controls.Add(MetricCard(i,labels[i]));
   var reserveBar=new TableLayoutPanel{Dock=DockStyle.Top,Height=67,ColumnCount=2,RowCount=1,BackColor=Color.White,Padding=new Padding(6)};
   reserveBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,70));
   reserveBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,30));
   reserveBar.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   reserveBar.Controls.Add(reserveAvailable,0,0);
   var debitReserve=new Button{Text="Registrar devolução / Abater 10%",Dock=DockStyle.Fill,BackColor=MainBlue,ForeColor=Color.White,Font=new Font("Segoe UI",11,FontStyle.Bold)};
   debitReserve.Click+=(sender,args)=>{LoadData();AddReturn();};
   reserveBar.Controls.Add(debitReserve,1,0);
   var section=new Label{Text="RESULTADO POR PRODUTO  |  Quantidades, vendas, custos e lucro",Dock=DockStyle.Top,Height=38,TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(12,0,0,0),ForeColor=HeaderColor,Font=new Font("Segoe UI",11,FontStyle.Bold)};
   StyleGrid(grid);StyleGrid(returnsGrid);
   products.Controls.Add(grid);products.Controls.Add(summary);products.Controls.Add(section);products.Controls.Add(cards);products.Controls.Add(reserveBar);products.Controls.Add(bar);
   var historyBar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=70,BackColor=LightBackground,Padding=new Padding(12),WrapContents=true,AutoScroll=true};
   var add=new Button{Text="Registrar data e valor",Width=210,Height=37,BackColor=MainBlue,ForeColor=Color.White,FlatStyle=FlatStyle.Flat};
   add.Click+=(sender,args)=>AddReturn();
   var remove=new Button{Text="Excluir devolução selecionada",Width=245,Height=37,BackColor=Color.FromArgb(180,43,55),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};
   remove.Click+=(sender,args)=>DeleteReturn();
   historyBar.Controls.Add(add);historyBar.Controls.Add(remove);
   var historyHeading=new Label{Text="HISTÓRICO DAS DEVOLUÇÕES DOS 10%",Dock=DockStyle.Top,Height=40,Font=new Font("Segoe UI",11,FontStyle.Bold),ForeColor=HeaderColor,TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(12,0,0,0)};
   history.Controls.Add(returnsGrid);history.Controls.Add(historyHeading);history.Controls.Add(historyBar);history.Controls.Add(remainingLabel);
   tabs.TabPages.Add(products);tabs.TabPages.Add(history);Controls.Add(tabs);
   search.TextChanged+=(sender,args)=>LoadData();
   Shown+=(sender,args)=>LoadData();
  }
  SQLiteConnection Open(){var c=new SQLiteConnection("Data Source="+db+";Version=3;");c.Open();return c;}
  static decimal Number(object o){if(o==null||o==DBNull.Value)return 0;return Convert.ToDecimal(o,CultureInfo.InvariantCulture);}
  static string Money(decimal n){return n.ToString("C2",new CultureInfo("pt-BR"));}
  static bool Table(SQLiteConnection c,string name){using(var cmd=new SQLiteCommand("SELECT count(*) FROM sqlite_master WHERE type='table' AND name=@n",c)){cmd.Parameters.AddWithValue("@n",name);return Convert.ToInt64(cmd.ExecuteScalar())>0;}}
  static bool Column(SQLiteConnection c,string table,string col){using(var cmd=new SQLiteCommand("PRAGMA table_info("+table+")",c))using(var rd=cmd.ExecuteReader()){while(rd.Read())if(string.Equals(Convert.ToString(rd["name"]),col,StringComparison.OrdinalIgnoreCase))return true;}return false;}
  void EnsureReturns(SQLiteConnection c){using(var cmd=new SQLiteCommand("CREATE TABLE IF NOT EXISTS profit_returns (id INTEGER PRIMARY KEY AUTOINCREMENT,amount REAL NOT NULL,return_date TEXT NOT NULL,note TEXT,created_at TEXT DEFAULT CURRENT_TIMESTAMP)",c))cmd.ExecuteNonQuery();}
  void LoadData(){
   try{using(var c=Open()){
    if(!Table(c,"sales")||!Table(c,"sale_items")){summary.Text="Banco sem tabelas de vendas e itens.";return;}
    EnsureReturns(c);
    bool status=Column(c,"sales","status"),fee=Column(c,"sales","machine_fee"),gross=Column(c,"sales","gross_total");
    string cost=Column(c,"sale_items","unit_cost")?"si.unit_cost":"0";
    string where=status?" WHERE (s.status IS NULL OR s.status!='CANCELADA')":"";
    string fees=fee&&gross?"COALESCE(s.machine_fee,0)*(si.total/NULLIF(s.gross_total,0))":"0";
    string sql="SELECT si.name Produto,si.barcode Codigo,SUM(si.quantity) Quantidade,SUM(si.total) ValorVendido,SUM(si.quantity*("+cost+")) Custo,SUM(COALESCE("+fees+",0)) Taxas,SUM(si.total-si.quantity*("+cost+")-COALESCE("+fees+",0)) LucroLiquido FROM sale_items si JOIN sales s ON s.id=si.sale_id"+where+" GROUP BY si.name,si.barcode ORDER BY ValorVendido DESC";
    var all=new DataTable();using(var adapter=new SQLiteDataAdapter(sql,c))adapter.Fill(all);
    var shown=all.Clone();decimal sales=0,costs=0,feesTotal=0,profit=0,fullProfit=0;
    foreach(DataRow r in all.Rows){
     decimal p=Number(r["LucroLiquido"]);fullProfit+=p;
     string term=search.Text.Trim();
     if(term.Length>0&&Convert.ToString(r["Produto"]).IndexOf(term,StringComparison.OrdinalIgnoreCase)<0&&Convert.ToString(r["Codigo"]).IndexOf(term,StringComparison.OrdinalIgnoreCase)<0)continue;
     shown.ImportRow(r);sales+=Number(r["ValorVendido"]);costs+=Number(r["Custo"]);feesTotal+=Number(r["Taxas"]);profit+=p;
    }
    decimal returned=0;var ret=new DataTable();
    using(var a=new SQLiteDataAdapter("SELECT id,return_date Data,amount Valor,note Observacao,created_at Registro FROM profit_returns ORDER BY return_date DESC,id DESC",c))a.Fill(ret);
    foreach(DataRow r in ret.Rows)returned+=Number(r["Valor"]);
    decimal tenPercent=Math.Round(Math.Max(fullProfit,0)*0.10M,2);
    decimal remaining=tenPercent-returned;
    available=Math.Max(0,remaining);
    reserveAvailable.Text="SALDO DISPONÍVEL DOS 10%: "+Money(available);
    remainingLabel.Text="TOTAL DOS 10%: "+Money(tenPercent)+"     |     JÁ DEVOLVIDO: "+Money(returned)+
     Environment.NewLine+"AINDA FALTA DEVOLVER: "+Money(available)+(remaining<0?"  |  Excesso registrado: "+Money(-remaining):"");
    decimal[] totals={sales,costs,feesTotal,profit,Math.Max(fullProfit,0)*0.10M,returned,available};
    for(int i=0;i<metricValues.Length;i++)metricValues[i].Text=Money(totals[i]);
    summary.Text=string.IsNullOrWhiteSpace(search.Text)?"Reserva dos 10%: "+Money(tenPercent)+"  |  Devolvido: "+Money(returned)+"  |  SALDO: "+Money(available):"Filtro: "+search.Text.Trim()+"  |  "+shown.Rows.Count+" produto(s). A reserva é calculada sobre todas as vendas.";
    grid.DataSource=shown;returnsGrid.DataSource=ret;
    string[] monetary={"ValorVendido","Custo","Taxas","LucroLiquido"};
    foreach(string column in monetary)if(grid.Columns.Contains(column))grid.Columns[column].DefaultCellStyle.Format="N2";
    if(grid.Columns.Contains("Produto"))grid.Columns["Produto"].FillWeight=210;
    if(grid.Columns.Contains("Codigo"))grid.Columns["Codigo"].FillWeight=120;
   }}catch(Exception ex){MessageBox.Show("Não foi possível consultar o lucro: "+ex.Message);}
  }
  // Salva a devolucao datada separadamente do caixa e das vendas.
  // Nunca atualizar dados financeiros sem backup SQLite verificado.
  void BackupBeforeProfitChange(string action){
   string home=Path.GetDirectoryName(db);
   string config=Path.Combine(home,"backup-location.txt");
   string folder=File.Exists(config)?File.ReadAllText(config).Trim():Path.Combine(home,"Backups");
   if(string.IsNullOrWhiteSpace(folder)||!Path.IsPathRooted(folder))throw new Exception("Pasta de backup invalida.");
   Directory.CreateDirectory(folder);
   string destination=Path.Combine(folder,"lucro-"+action+"-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+".db");
   using(var source=Open())using(var copy=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=destination}.ToString())){
    copy.Open();source.BackupDatabase(copy,"main","main",-1,null,0);
    using(var check=new SQLiteCommand("PRAGMA integrity_check",copy))
     if(Convert.ToString(check.ExecuteScalar())!="ok")throw new Exception("Falha na verificacao do backup. Operacao bloqueada.");
   }
   if(new FileInfo(destination).Length<=0)throw new Exception("Backup vazio. Operacao bloqueada.");
  }
  void AddReturn(){
   // O registro so e confirmado depois da data, do valor e do novo saldo.
   using(var dialog=new Form{Text="Devolucao pessoal dos 10%",Width=520,Height=350,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,BackColor=LightBackground}){
    var info=new Label{Left=18,Top=16,Width=460,Height=52,Text="RESERVA DISPONÍVEL DOS 10%: "+Money(available),Font=new Font("Segoe UI",12,FontStyle.Bold),ForeColor=MainBlue};
    var dateLabel=new Label{Text="Data da devolucao:",Left=18,Top=82,Width=215,Height=26};
    var date=new DateTimePicker{Left=18,Top=109,Width=215,Format=DateTimePickerFormat.Short,Value=DateTime.Today,MaxDate=DateTime.Today};
    var amountLabel=new Label{Text="Valor devolvido (R$):",Left=255,Top=82,Width=220,Height=26};
    var amount=new NumericUpDown{Left=255,Top=109,Width=220,DecimalPlaces=2,Maximum=999999999,Font=new Font("Segoe UI",12,FontStyle.Bold)};
    var balance=new Label{Text="Saldo da reserva após devolução: "+Money(available),Left=18,Top=157,Width=458,Height=38,Font=new Font("Segoe UI",12,FontStyle.Bold),ForeColor=MainBlue};
    amount.ValueChanged+=(sender,args)=>{
     decimal after=available-amount.Value;
     balance.Text=after<0?"Valor maior que o saldo dos 10%":"Saldo da reserva após devolução: "+Money(after);
     balance.ForeColor=after<0?Color.FromArgb(175,25,45):MainBlue;
    };
    var note=new TextBox{Left=18,Top=225,Width=457,MaxLength=255};
    var noteLabel=new Label{Text="Observacao (opcional):",Left=18,Top=201,Width=280,Height=24};
    var ok=new Button{Text="Registrar e subtrair",Left=18,Top=271,Width=225,Height=36,BackColor=MainBlue,ForeColor=Color.White};
    var cancel=new Button{Text="Cancelar",Left=255,Top=271,Width=220,Height=36,DialogResult=DialogResult.Cancel};
    dialog.Controls.AddRange(new Control[]{info,dateLabel,date,amountLabel,amount,balance,noteLabel,note,ok,cancel});
    ok.Click+=(sender,args)=>{
     if(amount.Value<=0||amount.Value>available){MessageBox.Show("Informe um valor maior que zero e ate "+Money(available));return;}
     if(MessageBox.Show("Data: "+date.Value.ToString("dd/MM/yyyy")+"\nValor devolvido: "+Money(amount.Value)+
      "\nSaldo antes: "+Money(available)+"\nSaldo depois de abater: "+Money(available-amount.Value)+
      "\n\nConfirmar registro?", "Conferir devolucao",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
     try{
      BackupBeforeProfitChange("antes-devolucao");
      using(var c=Open())using(var tx=c.BeginTransaction()){
       using(var cmd=new SQLiteCommand("INSERT INTO profit_returns(amount,return_date,note,created_at) VALUES(@v,@d,@n,datetime('now','localtime'))",c,tx)){
        cmd.Parameters.AddWithValue("@v",amount.Value);
        cmd.Parameters.AddWithValue("@d",date.Value.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@n",note.Text.Trim());
        if(cmd.ExecuteNonQuery()!=1)throw new Exception("Nao foi possivel salvar a devolucao.");
       }
       tx.Commit();
      }
      dialog.DialogResult=DialogResult.OK;dialog.Close();
     }catch(Exception ex){MessageBox.Show("Devolucao nao registrada: "+ex.Message,"Lucro",MessageBoxButtons.OK,MessageBoxIcon.Error);}
    };
    if(dialog.ShowDialog(this)==DialogResult.OK)LoadData();
   }
  }
  void DeleteReturn(){
   if(returnsGrid.CurrentRow==null)return;
   long id=Convert.ToInt64(returnsGrid.CurrentRow.Cells["id"].Value);
   if(MessageBox.Show("Excluir a devolução selecionada?","Confirmar",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
   try{BackupBeforeProfitChange("antes-excluir-devolucao");using(var c=Open())using(var cmd=new SQLiteCommand("DELETE FROM profit_returns WHERE id=@id",c)){cmd.Parameters.AddWithValue("@id",id);cmd.ExecuteNonQuery();}LoadData();}catch(Exception ex){MessageBox.Show("Exclusao nao realizada: "+ex.Message);}
  }
 }
}