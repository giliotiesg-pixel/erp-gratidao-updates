using System;
using System.Data;
using System.Data.SQLite;
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
   grid.Font=new Font("Segoe UI",11,FontStyle.Regular);grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;grid.RowTemplate.Height=38;
   grid.DataBindingComplete+=(s,e)=>{foreach(DataGridViewColumn column in grid.Columns){string n=column.Name.ToLowerInvariant();if(n=="name"||n.Contains("descri")||n=="produto"||n=="cliente"){column.MinimumWidth=240;column.FillWeight=230;column.DefaultCellStyle.WrapMode=DataGridViewTriState.True;}}grid.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells;};
   Shown+=(s,e)=>{var bounds=Screen.FromControl(this).WorkingArea;Width=Math.Min(Width,Math.Max(360,bounds.Width-24));Height=Math.Min(Height,Math.Max(300,bounds.Height-24));};
   Controls.Add(grid);Controls.Add(status);Controls.Add(top);LoadData();
  }
  void LoadData() {
   if(!File.Exists(db)){status.Text="Importe o banco antes de consultar.";return;}
   string sql;
   switch(module) {
    case "Estoque":sql="SELECT id,barcode,name,stock,min_stock,price,cost,status FROM products ORDER BY name LIMIT 3000";break;
    case "Clientes":sql="SELECT id,name,phone,document FROM customers ORDER BY name LIMIT 3000";break;
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
 }
}