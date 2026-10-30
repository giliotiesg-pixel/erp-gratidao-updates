using System;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
namespace ArmazemGratidao {
 public sealed class LucroForm : Form {
  readonly string db;
  readonly DataGridView grid=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
  readonly Label summary=new Label {Dock=DockStyle.Top,Height=82,Font=new Font("Segoe UI",11,FontStyle.Bold),ForeColor=Color.FromArgb(10,52,145),BackColor=Color.FromArgb(229,242,255)};
  readonly TextBox search=new TextBox {Width=260};
  readonly DataGridView returnsGrid=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
  decimal available;
  public LucroForm(string database){
   db=database;Text="Armazém Gratidão | Lucro";Width=1200;Height=740;BackColor=Color.FromArgb(229,242,255);
   var tabs=new TabControl {Dock=DockStyle.Fill};
   var products=new TabPage("Lucro por produto"),history=new TabPage("Devoluções dos 10%");
   var bar=new FlowLayoutPanel {Dock=DockStyle.Top,Height=52,BackColor=Color.FromArgb(220,95,105),Padding=new Padding(8)};
   bar.Controls.Add(new Label {Text="Pesquisar produto / código:",AutoSize=true,Padding=new Padding(0,8,0,0),ForeColor=Color.FromArgb(10,52,145),Font=new Font("Segoe UI",10,FontStyle.Bold)});
   bar.Controls.Add(search);
   var refresh=new Button {Text="Atualizar",Width=110,Height=32};refresh.Click+=(s,e)=>LoadData();bar.Controls.Add(refresh);
   products.Controls.Add(grid);products.Controls.Add(summary);products.Controls.Add(bar);
   var historyBar=new FlowLayoutPanel {Dock=DockStyle.Top,Height=50,BackColor=Color.FromArgb(220,95,105),Padding=new Padding(7)};
   var add=new Button {Text="Registrar devolução",Width=175,Height=32};add.Click+=(s,e)=>AddReturn();
   var remove=new Button {Text="Excluir devolução selecionada",Width=225,Height=32};remove.Click+=(s,e)=>DeleteReturn();
   historyBar.Controls.Add(add);historyBar.Controls.Add(remove);history.Controls.Add(returnsGrid);history.Controls.Add(historyBar);
   tabs.TabPages.Add(products);tabs.TabPages.Add(history);Controls.Add(tabs);
   search.TextChanged+=(s,e)=>LoadData();
   Shown+=(s,e)=>LoadData();
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
    available=Math.Max(0,Math.Round(Math.Max(fullProfit,0)*0.10M,2)-returned);
    summary.Text="Vendido: "+Money(sales)+"   Custo: "+Money(costs)+"   Taxas: "+Money(feesTotal)+"\nLucro líquido: "+Money(profit)+"   10% (geral): "+Money(Math.Max(fullProfit,0)*0.10M)+"   Devolvido: "+Money(returned)+"   Saldo dos 10%: "+Money(available);
    grid.DataSource=shown;returnsGrid.DataSource=ret;
   }}catch(Exception ex){MessageBox.Show("Não foi possível consultar o lucro: "+ex.Message);}
  }
  void AddReturn(){
   using(var dialog=new Form {Text="Registrar devolução dos 10%",Width=450,Height=275,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false}){
    var info=new Label {Left=15,Top=12,Width=400,Text="Saldo disponível: "+Money(available)};
    var amount=new NumericUpDown {Left=15,Top=45,Width=180,DecimalPlaces=2,Maximum=999999999};
    var date=new DateTimePicker {Left=215,Top=45,Width=200,Format=DateTimePickerFormat.Short};
    var note=new TextBox {Left=15,Top=105,Width=400};
    var ok=new Button {Text="Salvar devolução",Left=230,Top=150,Width=185,Height=38};
    dialog.Controls.AddRange(new Control[]{info,amount,date,new Label {Text="Observação:",Left=15,Top=84,Width=180},note,ok});
    ok.Click+=(s,e)=>{if(amount.Value<=0||amount.Value>available){MessageBox.Show("Informe um valor positivo até "+Money(available));return;}try{using(var c=Open()){EnsureReturns(c);using(var cmd=new SQLiteCommand("INSERT INTO profit_returns(amount,return_date,note,created_at) VALUES(@v,@d,@n,datetime('now','localtime'))",c)){cmd.Parameters.AddWithValue("@v",amount.Value);cmd.Parameters.AddWithValue("@d",date.Value.ToString("yyyy-MM-dd"));cmd.Parameters.AddWithValue("@n",note.Text);cmd.ExecuteNonQuery();}}dialog.DialogResult=DialogResult.OK;}catch(Exception ex){MessageBox.Show(ex.Message);}};
    if(dialog.ShowDialog(this)==DialogResult.OK)LoadData();
   }
  }
  void DeleteReturn(){
   if(returnsGrid.CurrentRow==null)return;
   long id=Convert.ToInt64(returnsGrid.CurrentRow.Cells["id"].Value);
   if(MessageBox.Show("Excluir a devolução selecionada?","Confirmar",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
   try{using(var c=Open())using(var cmd=new SQLiteCommand("DELETE FROM profit_returns WHERE id=@id",c)){cmd.Parameters.AddWithValue("@id",id);cmd.ExecuteNonQuery();}LoadData();}catch(Exception ex){MessageBox.Show(ex.Message);}
  }
 }
}