using System;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
namespace ArmazemGratidao {
 public sealed class ConsultaForm:Form {
  readonly string db;readonly bool credit;bool previous=false;
  readonly TextBox saleSearch=new TextBox {Width=110};
  readonly DataGridView master=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
  readonly DataGridView details=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
  readonly Label heading=new Label {Dock=DockStyle.Top,Height=46,Font=new Font("Segoe UI",13,FontStyle.Bold),TextAlign=ContentAlignment.MiddleLeft};
  public ConsultaForm(string path,bool fiado) {
   db=path;credit=fiado;Text=fiado?"Fiado - Clientes e Vendas":"Vendas - Itens por Venda";Width=1200;Height=780;
   var split=new SplitContainer {Dock=DockStyle.Fill,Orientation=Orientation.Horizontal,SplitterDistance=340};
   split.Panel1.Controls.Add(master);split.Panel2.Controls.Add(details);Controls.Add(split);Controls.Add(heading);
   if(!fiado){
    var bar=new FlowLayoutPanel {Dock=DockStyle.Top,Height=46,Padding=new Padding(5),BackColor=Color.FromArgb(22,78,165)};
    var today=new Button {Text="Vendas do dia",Width=150,Height=33};today.Click+=(s,e)=>{previous=false;LoadMaster();};
    var older=new Button {Text="Vendas anteriores",Width=170,Height=33};older.Click+=(s,e)=>{previous=true;LoadMaster();};
    var refresh=new Button {Text="Atualizar",Width=110,Height=33};refresh.Click+=(s,e)=>LoadMaster();
    bar.Controls.Add(today);bar.Controls.Add(older);bar.Controls.Add(refresh);
    bar.Controls.Add(new Label {Text="Numero da venda:",ForeColor=Color.White,AutoSize=true,Padding=new Padding(5,8,0,0)});
    bar.Controls.Add(saleSearch);
    var find=new Button {Text="Buscar venda",Width=120,Height=33};find.Click+=(s,e)=>SearchSale();bar.Controls.Add(find);
    var export=new Button {Text="Exportar itens CSV",Width=145,Height=33};export.Click+=(s,e)=>ExportSale();bar.Controls.Add(export);
    Controls.Add(bar);
   }
   heading.Text=fiado?"FIADO | Selecione um cliente":"VENDAS | Selecione uma venda para visualizar todos os itens";
   master.SelectionChanged+=(s,e)=>LoadDetails();
   LoadMaster();
  }
  DataTable Query(string sql,string param=null,object value=null) {
   var dt=new DataTable();
   using(var c=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=db,ReadOnly=true,FailIfMissing=true}.ToString())) {
    c.Open();using(var cmd=new SQLiteCommand(sql,c)) {
     if(param!=null)cmd.Parameters.AddWithValue(param,value??DBNull.Value);
     using(var a=new SQLiteDataAdapter(cmd))a.Fill(dt);
    }
   }return dt;
  }
  void LoadMaster() {
   try {
    master.DataSource=credit?
     Query("SELECT COALESCE(NULLIF(TRIM(customer_name),''),'SEM CLIENTE') AS cliente,COUNT(*) AS vendas,SUM(COALESCE(gross_total,0)) AS total_fiado FROM sales WHERE UPPER(COALESCE(payment_method,'')) LIKE '%FIADO%' GROUP BY COALESCE(NULLIF(TRIM(customer_name),''),'SEM CLIENTE') ORDER BY cliente"):
     Query("SELECT id AS venda,created_at AS data,customer_name AS cliente,payment_method AS pagamento,gross_total AS total FROM sales WHERE date(created_at) "+(previous?"<":"=")+" date('now','localtime') ORDER BY id DESC LIMIT 3000");
    if(!credit)heading.Text=previous?"VENDAS ANTERIORES | Selecione uma venda":"VENDAS DO DIA | Selecione uma venda";
   }catch(Exception ex){MessageBox.Show(ex.Message,"Erro ao abrir modulo");}
  }
  void SearchSale(){
   long id;
   if(!long.TryParse(saleSearch.Text.Trim(),out id)||id<=0){MessageBox.Show("Informe o numero da venda.");return;}
   try{
    master.DataSource=Query("SELECT id AS venda,created_at AS data,customer_name AS cliente,payment_method AS pagamento,gross_total AS total FROM sales WHERE id=@id","@id",id);
    heading.Text=((DataTable)master.DataSource).Rows.Count==0?"Venda nao encontrada.":"VENDA #"+id+" | Resultado da busca";
    if(((DataTable)master.DataSource).Rows.Count==0)details.DataSource=null;
   }catch(Exception ex){MessageBox.Show(ex.Message,"Erro na busca");}
  }
  static string Csv(object value){
   string s=Convert.ToString(value??"").Replace("\"","\"\"");
   return "\""+s+"\"";
  }
  void ExportSale(){
   if(master.CurrentRow==null||master.CurrentRow.IsNewRow){MessageBox.Show("Selecione uma venda.");return;}
   long id=Convert.ToInt64(master.CurrentRow.Cells["venda"].Value);
   try{
    var dt=Query("SELECT barcode AS codigo,name AS produto,quantity AS quantidade,unit_price AS preco,total AS valor FROM sale_items WHERE sale_id=@id ORDER BY id","@id",id);
    using(var dialog=new SaveFileDialog {Title="Exportar itens da venda",FileName="venda-"+id+".csv",Filter="CSV (*.csv)|*.csv"}){
     if(dialog.ShowDialog()!=DialogResult.OK)return;
     using(var writer=new StreamWriter(dialog.FileName,false,new UTF8Encoding(true))){
      writer.WriteLine("codigo;produto;quantidade;preco;valor");
      foreach(DataRow row in dt.Rows){
       for(int i=0;i<dt.Columns.Count;i++){if(i>0)writer.Write(";");writer.Write(Csv(row[i]));}
       writer.WriteLine();
      }
     }
    }
    MessageBox.Show("Itens exportados. Nenhum dado do banco foi alterado.");
   }catch(Exception ex){MessageBox.Show(ex.Message,"Erro ao exportar");}
  }
  void LoadDetails() {
   if(master.CurrentRow==null||master.CurrentRow.IsNewRow)return;
   try {
    if(credit) {
     var name=Convert.ToString(master.CurrentRow.Cells["cliente"].Value);
     details.DataSource=Query("SELECT s.id AS venda,s.created_at AS data,s.gross_total AS valor,COALESCE((SELECT SUM(cp.amount) FROM credit_payments cp WHERE cp.sale_id=s.id),0) AS recebido,s.gross_total-COALESCE((SELECT SUM(cp.amount) FROM credit_payments cp WHERE cp.sale_id=s.id),0) AS saldo FROM sales s WHERE UPPER(COALESCE(s.payment_method,'')) LIKE '%FIADO%' AND COALESCE(NULLIF(TRIM(s.customer_name),''),'SEM CLIENTE')=@name ORDER BY s.id DESC","@name",name);
     decimal balance=0;foreach(DataRow r in ((DataTable)details.DataSource).Rows)balance+=Convert.ToDecimal(r["saldo"]);
     heading.Text="FIADO | "+name+" | Saldo: R$ "+balance.ToString("N2");
    } else {
     var id=master.CurrentRow.Cells["venda"].Value;
     details.DataSource=Query("SELECT barcode AS codigo,name AS produto,quantity AS quantidade,unit_price AS preco,total AS valor FROM sale_items WHERE sale_id=@id ORDER BY id","@id",id);
     heading.Text="VENDA #"+id+" | Todos os itens da venda";
    }
   }catch(Exception ex){MessageBox.Show(ex.Message,"Erro ao consultar detalhes");}
  }
 }
}