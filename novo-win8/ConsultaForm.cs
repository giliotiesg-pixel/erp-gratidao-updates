using System;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
namespace ArmazemGratidao {
 public sealed class ConsultaForm:Form {
  readonly string db;readonly bool credit;bool previous=false;bool selectedDate=false;readonly DateTimePicker dateFilter=new DateTimePicker {Format=DateTimePickerFormat.Short,Width=115};
  readonly TextBox saleSearch=new TextBox {Width=110};
  readonly Label summary=new Label {Dock=DockStyle.Bottom,Height=36,TextAlign=ContentAlignment.MiddleLeft,Font=new Font("Segoe UI",10,FontStyle.Bold),BackColor=Color.FromArgb(229,242,255)};
  readonly DataGridView master=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
  readonly DataGridView details=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
  bool loadingMaster=false;
  readonly Label heading=new Label {Dock=DockStyle.Top,Height=46,Font=new Font("Segoe UI",13,FontStyle.Bold),TextAlign=ContentAlignment.MiddleLeft};
  public ConsultaForm(string path,bool fiado) {
   db=path;credit=fiado;Text=fiado?"Fiado - Clientes e Vendas":"Vendas - Itens por Venda";Width=1200;Height=780;BackColor=Color.FromArgb(229,242,255);
   var split=new SplitContainer {Dock=DockStyle.Fill,Orientation=Orientation.Horizontal,SplitterDistance=340};
   split.Panel1.Controls.Add(master);split.Panel2.Controls.Add(details);Controls.Add(split);Controls.Add(heading);
   if(!fiado){
    var bar=new FlowLayoutPanel {Dock=DockStyle.Top,Height=92,Padding=new Padding(5),WrapContents=true,BackColor=Color.FromArgb(190,225,250)};
    var today=new Button {Text="Vendas do dia",Width=150,Height=33};today.Click+=(s,e)=>{previous=false;selectedDate=false;LoadMaster();};
    var older=new Button {Text="Vendas anteriores",Width=170,Height=33};older.Click+=(s,e)=>{previous=true;selectedDate=false;LoadMaster();};
    var refresh=new Button {Text="Atualizar",Width=110,Height=33};refresh.Click+=(s,e)=>LoadMaster();
    bar.Controls.Add(today);bar.Controls.Add(older);bar.Controls.Add(refresh);
    bar.Controls.Add(new Label {Text="Data:",AutoSize=true,Padding=new Padding(5,8,0,0)});bar.Controls.Add(dateFilter);
    var filter=new Button {Text="Ver data",Width=85,Height=33};filter.Click+=(s,e)=>{selectedDate=true;LoadMaster();};bar.Controls.Add(filter);
    var editDate=new Button {Text="Atualizar data / hora",Width=175,Height=33};editDate.Click+=(s,e)=>EditSaleDate();bar.Controls.Add(editDate);
    var changeProduct=new Button {Text="Alterar produto",Width=125,Height=33};changeProduct.Click+=(s,e)=>ChangeProduct();bar.Controls.Add(changeProduct);
    var remove=new Button {Text="Excluir venda",Width=115,Height=33};remove.Click+=(s,e)=>DeleteSale();bar.Controls.Add(remove);
    bar.Controls.Add(new Label {Text="Numero da venda:",ForeColor=Color.White,AutoSize=true,Padding=new Padding(5,8,0,0)});
    bar.Controls.Add(saleSearch);
    var find=new Button {Text="Buscar venda",Width=120,Height=33};find.Click+=(s,e)=>SearchSale();bar.Controls.Add(find);
    var export=new Button {Text="Exportar itens CSV",Width=145,Height=33};export.Click+=(s,e)=>ExportSale();bar.Controls.Add(export);
    var daily=new Button {Text="Totais por dia",Width=145,Height=33};daily.Click+=(s,e)=>ShowDailyTotals();bar.Controls.Add(daily);
    Controls.Add(bar);
   }
   heading.Text=fiado?"FIADO | Selecione um cliente":"VENDAS | Selecione uma venda para visualizar todos os itens";
   if(!fiado)Controls.Add(summary);
   foreach(var table in new[]{master,details}){
    table.Font=new Font("Segoe UI",11,FontStyle.Regular);
    table.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.None;
    table.RowTemplate.Height=38;
    table.DataBindingComplete+=(s,e)=>{
     if(table.IsDisposed || table.Disposing || table.DataSource==null)return;
     table.SuspendLayout();
     try{
      foreach(DataGridViewColumn column in table.Columns){
       if(column==null || column.IsDataBound==false)continue;
       string n=(column.Name??"").ToLowerInvariant();
       if(n.Contains("descri")||n.Contains("produto")||n.Contains("cliente")||n=="name"){
        column.MinimumWidth=220;
        column.FillWeight=220;
        column.DefaultCellStyle.WrapMode=DataGridViewTriState.True;
       }
      }
     }finally{table.ResumeLayout();}
    };
   }
   master.SelectionChanged+=(s,e)=>{if(!loadingMaster)LoadDetails();};
   master.CellClick+=(s,e)=>{if(e.RowIndex>=0&&!loadingMaster)LoadDetails();};
   dateFilter.ValueChanged+=(s,e)=>{if(!credit){selectedDate=true;LoadMaster();}};
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
  // Total por data calculado em todas as vendas do banco, sem limite de linhas visíveis.
  // Uma venda corrigida de data é refletida automaticamente no dia de destino.
  static string SalesWithDailyTotalsSql(){
   return "SELECT s.id AS venda,s.created_at AS data,s.customer_name AS cliente,"+
    "s.payment_method AS pagamento,s.gross_total AS total,"+
    "COALESCE(day.total,0) AS 'Total do dia' "+
    "FROM sales s LEFT JOIN (SELECT date(created_at) AS dia,SUM(COALESCE(gross_total,0)) AS total "+
    "FROM sales GROUP BY date(created_at)) day ON day.dia=date(s.created_at)";
  }
  void ShowDailyTotals(){
   if(credit)return;
   try{
    string where=selectedDate?"WHERE date(created_at)=@date":previous?"WHERE date(created_at)<date('now','localtime')":"WHERE date(created_at)=date('now','localtime')";
    var days=Query("SELECT date(created_at) AS 'Data',COUNT(*) AS 'Vendas',"+
     "ROUND(SUM(COALESCE(gross_total,0)),2) AS 'Total vendido (R$)' FROM sales "+
     where+" GROUP BY date(created_at) ORDER BY date(created_at) DESC",
     selectedDate?"@date":null,selectedDate?(object)dateFilter.Value.ToString("yyyy-MM-dd"):null);
    using(var dialog=new Form{Text="Fechamento de vendas - totais diários",Width=680,Height=520,StartPosition=FormStartPosition.CenterParent,BackColor=Color.FromArgb(229,242,255)}){
     decimal total=0;long count=0;
     foreach(DataRow row in days.Rows){total+=Convert.ToDecimal(row["Total vendido (R$)"]);count+=Convert.ToInt64(row["Vendas"]);}
     var header=new Label{Dock=DockStyle.Top,Height=72,Padding=new Padding(12),Font=new Font("Segoe UI",11,FontStyle.Bold),
      Text="Dias: "+days.Rows.Count+"  |  Vendas: "+count+"  |  Total vendido: R$ "+total.ToString("N2")+
      Environment.NewLine+"Valores por data de realização (não por horário de consulta)."};
     var table=new DataGridView{Dock=DockStyle.Fill,DataSource=days,ReadOnly=true,AllowUserToAddRows=false,
      RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,BackgroundColor=Color.White};
     if(table.Columns.Contains("Total vendido (R$)"))table.Columns["Total vendido (R$)"].DefaultCellStyle.Format="N2";
     dialog.Controls.Add(table);dialog.Controls.Add(header);dialog.ShowDialog(this);
    }
   }catch(Exception ex){MessageBox.Show("Não foi possível calcular os totais diários: "+ex.Message,"Vendas",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  }
  void LoadMaster() {
   loadingMaster=true;
   try {
    master.DataSource=credit?
     Query("SELECT COALESCE(NULLIF(TRIM(customer_name),''),'SEM CLIENTE') AS cliente,COUNT(*) AS vendas,SUM(COALESCE(gross_total,0)) AS total_fiado FROM sales WHERE UPPER(COALESCE(payment_method,'')) LIKE '%FIADO%' GROUP BY COALESCE(NULLIF(TRIM(customer_name),''),'SEM CLIENTE') ORDER BY cliente"):
     Query(SalesWithDailyTotalsSql()+" WHERE s.created_at "+(selectedDate?">= @date AND created_at < date(@date, '+1 day')":previous?"< date('now','localtime')":">= date('now','localtime') AND created_at < date('now','localtime','+1 day')")+" ORDER BY s.id DESC LIMIT 3000",selectedDate?"@date":null,selectedDate?(object)dateFilter.Value.ToString("yyyy-MM-dd"):null);
    if(!credit){
     heading.Text=selectedDate?"VENDAS DE "+dateFilter.Value.ToString("dd/MM/yyyy"):previous?"VENDAS ANTERIORES | Selecione uma venda":"VENDAS DO DIA | Selecione uma venda";
     var dt=Query("SELECT COUNT(*) AS quantidade,COALESCE(SUM(gross_total),0) AS total FROM sales WHERE created_at "+(selectedDate?">= @date AND created_at < date(@date, '+1 day')":previous?"< date('now','localtime')":">= date('now','localtime') AND created_at < date('now','localtime','+1 day')"),selectedDate?"@date":null,selectedDate?(object)dateFilter.Value.ToString("yyyy-MM-dd"):null);
     var monthly=Query("SELECT COUNT(*) AS quantidade,COALESCE(SUM(gross_total),0) AS total FROM sales WHERE date(created_at) >= date(\'now\',\'localtime\',\'start of month\') AND date(created_at) < date(\'now\',\'localtime\',\'start of month\',\'+1 month\')");
     summary.Text="  "+(selectedDate?"Vendas de "+dateFilter.Value.ToString("dd/MM/yyyy"):previous?"Vendas anteriores":"Vendas de hoje")+"  |  Quantidade: "+Convert.ToInt64(dt.Rows[0]["quantidade"])+"  |  TOTAL VENDIDO: R$ "+Convert.ToDecimal(dt.Rows[0]["total"]).ToString("N2")+"  |  TOTAL DO MÊS: R$ "+Convert.ToDecimal(monthly.Rows[0]["total"]).ToString("N2")+"  |  Consulte Totais por dia";
    }
   }catch(Exception ex){MessageBox.Show(ex.Message,"Erro ao abrir modulo");}
   finally{loadingMaster=false;LoadDetails();}
  }

  long SelectedSale(){
   if(master.CurrentRow==null||master.CurrentRow.IsNewRow||!master.Columns.Contains("venda"))throw new Exception("Selecione uma venda.");
   return Convert.ToInt64(master.CurrentRow.Cells["venda"].Value);
  }
  static void WriteAudit(SQLiteConnection conn,SQLiteTransaction tx,long sale,string action,string before,string after){
   using(var cmd=new SQLiteCommand("CREATE TABLE IF NOT EXISTS sales_edit_audit (id INTEGER PRIMARY KEY AUTOINCREMENT,sale_id INTEGER NOT NULL,action TEXT NOT NULL,before_value TEXT,after_value TEXT,changed_at TEXT NOT NULL)",conn,tx))cmd.ExecuteNonQuery();
   using(var cmd=new SQLiteCommand("INSERT INTO sales_edit_audit(sale_id,action,before_value,after_value,changed_at) VALUES(@id,@action,@before,@after,@at)",conn,tx)){
    cmd.Parameters.AddWithValue("@id",sale);cmd.Parameters.AddWithValue("@action",action);cmd.Parameters.AddWithValue("@before",before);cmd.Parameters.AddWithValue("@after",after);cmd.Parameters.AddWithValue("@at",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));cmd.ExecuteNonQuery();
   }
  }
  void EditSaleDate(){
   try{
    long id=SelectedSale();var dt=Query("SELECT created_at FROM sales WHERE id=@id","@id",id);
    if(dt.Rows.Count==0)throw new Exception("Venda não encontrada.");
    string before=Convert.ToString(dt.Rows[0][0]);DateTime original;
    if(!DateTime.TryParse(before,out original))original=DateTime.Now;
    using(var dialog=new Form{Text="Corrigir data e hora da venda #"+id,Width=440,Height=260,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false}){
     var dateLabel=new Label{Text="Data da venda",Left=16,Top=15,Width=385,Height=23};
     var datePicker=new DateTimePicker{Format=DateTimePickerFormat.Custom,CustomFormat="dd/MM/yyyy",Value=original,Left=16,Top=42,Width=385};
     var timeLabel=new Label{Text="Horário da venda (horas, minutos e segundos)",Left=16,Top=85,Width=385,Height=23};
     var timePicker=new DateTimePicker{Format=DateTimePickerFormat.Custom,CustomFormat="HH:mm:ss",ShowUpDown=true,Value=original,Left=16,Top=112,Width=385};
     var save=new Button{Text="Salvar data e hora",DialogResult=DialogResult.OK,Left=16,Top=161,Width=180,Height=34};
     var cancel=new Button{Text="Cancelar",DialogResult=DialogResult.Cancel,Left=218,Top=161,Width=120,Height=34};
     dialog.Controls.AddRange(new Control[]{dateLabel,datePicker,timeLabel,timePicker,save,cancel});
     dialog.AcceptButton=save;dialog.CancelButton=cancel;
     if(dialog.ShowDialog(this)!=DialogResult.OK)return;
     DateTime corrected=datePicker.Value.Date+timePicker.Value.TimeOfDay;
     string after=corrected.ToString("yyyy-MM-dd HH:mm:ss");
     if(after==before){MessageBox.Show("A data e a hora já estão corretas.");return;}
     if(MessageBox.Show("Alterar data/hora da venda #"+id+"?"+Environment.NewLine+
      "De: "+before+Environment.NewLine+"Para: "+after+Environment.NewLine+
      "Os totais de cada dia serão recalculados; itens, pagamentos e estoque não mudam.",
      "Confirmar correção de data e hora",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
     using(var conn=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=db,FailIfMissing=true}.ToString())){
      conn.Open();
      string backup=db+".antes-correcao-data-hora-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+".db";
      using(var saved=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=backup}.ToString())){
       saved.Open();conn.BackupDatabase(saved,"main","main",-1,null,0);
      }
      using(var tx=conn.BeginTransaction()){
       using(var cmd=new SQLiteCommand("UPDATE sales SET created_at=@date WHERE id=@id",conn,tx)){
        cmd.Parameters.AddWithValue("@date",after);cmd.Parameters.AddWithValue("@id",id);
        if(cmd.ExecuteNonQuery()!=1)throw new Exception("Venda não atualizada.");
       }
       WriteAudit(conn,tx,id,"ALTERAR_DATA_HORA",before,after);tx.Commit();
      }
     }
     selectedDate=true;dateFilter.Value=corrected;LoadMaster();
    }
   }catch(Exception ex){MessageBox.Show(ex.Message,"Atualização não concluída");}
  }
  void DeleteSale(){
   try{
    long id=SelectedSale();
    if(MessageBox.Show("Excluir venda #"+id+"? Os itens e pagamentos serão removidos e registrados na auditoria. O estoque não será alterado.","Confirmar exclusão",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
    using(var conn=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=db,FailIfMissing=true}.ToString())){
     conn.Open();using(var tx=conn.BeginTransaction()){
      var snapshot=new StringBuilder();
      foreach(string table in new[]{"sales","sale_items","sale_payments","credit_payments"}){
       string filter=table=="sales"?"id":"sale_id";
       using(var q=new SQLiteCommand("SELECT * FROM "+table+" WHERE "+filter+"=@id",conn,tx)){
        q.Parameters.AddWithValue("@id",id);
        using(var rd=q.ExecuteReader())while(rd.Read()){
         snapshot.Append(table).Append(":");
         for(int i=0;i<rd.FieldCount;i++)snapshot.Append(rd.GetName(i)).Append("=").Append(Convert.ToString(rd.GetValue(i))).Append(";");
         snapshot.AppendLine();
        }
       }
      }
      if(snapshot.Length==0)throw new Exception("Venda não encontrada.");
      WriteAudit(conn,tx,id,"EXCLUIR_VENDA",snapshot.ToString(),"EXCLUIDA");
      foreach(string table in new[]{"sale_items","sale_payments","credit_payments","sales"}){
       using(var q=new SQLiteCommand("DELETE FROM "+table+" WHERE "+(table=="sales"?"id":"sale_id")+"=@id",conn,tx)){
        q.Parameters.AddWithValue("@id",id);q.ExecuteNonQuery();
       }
      }
      tx.Commit();
     }
    }
    LoadMaster();MessageBox.Show("Venda excluída e registrada na auditoria.");
   }catch(Exception ex){MessageBox.Show(ex.Message,"Exclusão não concluída");}
  }

  void ChangeProduct(){
   if(credit)return;
   try{
    long sale=SelectedSale();
    if(details.CurrentRow==null||!details.Columns.Contains("item_id"))throw new Exception("Clique primeiro no item da venda que deseja alterar.");
    long item=Convert.ToInt64(details.CurrentRow.Cells["item_id"].Value);
    using(var dialog=new Form{Text="Alterar produto da venda #"+sale,Width=650,Height=440,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog}){
     var search=new TextBox{Left=15,Top=15,Width=440};
     var find=new Button{Text="Buscar produto",Left=465,Top=13,Width=150};
     var list=new DataGridView{Left=15,Top=55,Width=600,Height=230,ReadOnly=true,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,AllowUserToAddRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
     var quantity=new NumericUpDown{Left=15,Top=315,Width=125,DecimalPlaces=3,Maximum=1000000,Minimum=0.001M,Value=1};
     var price=new NumericUpDown{Left=160,Top=315,Width=125,DecimalPlaces=2,Maximum=1000000,Minimum=0};
     var save=new Button{Text="Salvar alteração",Left=325,Top=312,Width=160,Height=32};
     dialog.Controls.AddRange(new Control[]{search,find,list,new Label{Text="Quantidade",Left=15,Top=294,Width=120},quantity,new Label{Text="Preço unitário",Left=160,Top=294,Width=140},price,save});
     Action lookup=()=>{
      using(var conn=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=db,ReadOnly=true,FailIfMissing=true}.ToString())){
       conn.Open();using(var cmd=new SQLiteCommand("SELECT id,barcode,name,price,cost FROM products WHERE name LIKE @term OR barcode LIKE @term ORDER BY name LIMIT 100",conn)){
        cmd.Parameters.AddWithValue("@term","%"+search.Text.Trim()+"%");
        var dt=new DataTable();using(var adapter=new SQLiteDataAdapter(cmd))adapter.Fill(dt);list.DataSource=dt;
       }
      }
     };
     find.Click+=(s,e)=>{try{lookup();}catch(Exception ex){MessageBox.Show(ex.Message);}};
     list.SelectionChanged+=(s,e)=>{if(list.CurrentRow!=null&&list.Columns.Contains("price")){decimal p; if(decimal.TryParse(Convert.ToString(list.CurrentRow.Cells["price"].Value),out p))price.Value=Math.Max(price.Minimum,Math.Min(price.Maximum,p));}};
     save.Click+=(s,e)=>{
      try{
       if(list.CurrentRow==null)throw new Exception("Selecione um produto encontrado na busca.");
       long product=Convert.ToInt64(list.CurrentRow.Cells["id"].Value);
       string name=Convert.ToString(list.CurrentRow.Cells["name"].Value);
       string barcode=Convert.ToString(list.CurrentRow.Cells["barcode"].Value);
       decimal cost=Convert.ToDecimal(list.CurrentRow.Cells["cost"].Value==DBNull.Value?0:list.CurrentRow.Cells["cost"].Value);
       using(var conn=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=db,FailIfMissing=true}.ToString())){
        conn.Open();using(var tx=conn.BeginTransaction()){
         string before="";
         using(var cmd=new SQLiteCommand("SELECT * FROM sale_items WHERE id=@item AND sale_id=@sale",conn,tx)){
          cmd.Parameters.AddWithValue("@item",item);cmd.Parameters.AddWithValue("@sale",sale);
          using(var rd=cmd.ExecuteReader()){
           if(!rd.Read())throw new Exception("Item não encontrado.");
           for(int i=0;i<rd.FieldCount;i++)before+=rd.GetName(i)+"="+Convert.ToString(rd.GetValue(i))+"; ";
          }
         }
         decimal total=decimal.Round(quantity.Value*price.Value,2);
         using(var cmd=new SQLiteCommand("UPDATE sale_items SET product_id=@product,barcode=@barcode,name=@name,quantity=@qty,unit_price=@price,unit_cost=@cost,total=@total WHERE id=@item AND sale_id=@sale",conn,tx)){
          cmd.Parameters.AddWithValue("@product",product);cmd.Parameters.AddWithValue("@barcode",barcode);cmd.Parameters.AddWithValue("@name",name);
          cmd.Parameters.AddWithValue("@qty",quantity.Value);cmd.Parameters.AddWithValue("@price",price.Value);cmd.Parameters.AddWithValue("@cost",cost);cmd.Parameters.AddWithValue("@total",total);
          cmd.Parameters.AddWithValue("@item",item);cmd.Parameters.AddWithValue("@sale",sale);
          if(cmd.ExecuteNonQuery()!=1)throw new Exception("Item não atualizado.");
         }
         using(var cmd=new SQLiteCommand(@"UPDATE sales SET subtotal=(SELECT COALESCE(SUM(total),0) FROM sale_items WHERE sale_id=@sale),
          gross_total=(SELECT COALESCE(SUM(total),0) FROM sale_items WHERE sale_id=@sale)-COALESCE(discount_total,0),
          total_cost=(SELECT COALESCE(SUM(quantity*unit_cost),0) FROM sale_items WHERE sale_id=@sale),
          net_profit=(SELECT COALESCE(SUM(total-quantity*unit_cost),0) FROM sale_items WHERE sale_id=@sale)-COALESCE(discount_total,0)-COALESCE(machine_fee,0),
          profit_reserve_10=MAX(0,((SELECT COALESCE(SUM(total-quantity*unit_cost),0) FROM sale_items WHERE sale_id=@sale)-COALESCE(discount_total,0)-COALESCE(machine_fee,0))*0.1)
          WHERE id=@sale",conn,tx)){cmd.Parameters.AddWithValue("@sale",sale);cmd.ExecuteNonQuery();}
         WriteAudit(conn,tx,sale,"ALTERAR_ITEM_"+item,before,"product_id="+product+"; name="+name+"; quantity="+quantity.Value+"; price="+price.Value+"; total="+total);
         tx.Commit();
        }
       }
       dialog.DialogResult=DialogResult.OK;dialog.Close();
      }catch(Exception ex){MessageBox.Show(ex.Message,"Alteração não salva");}
     };
     if(dialog.ShowDialog(this)==DialogResult.OK){LoadMaster();MessageBox.Show("Produto alterado. Totais recalculados e alteração auditada. Confira os pagamentos se o total mudou; o estoque não foi movimentado.");}
    }
   }catch(Exception ex){MessageBox.Show(ex.Message,"Alteração de produto");}
  }
  void SearchSale(){
   long id;
   if(!long.TryParse(saleSearch.Text.Trim(),out id)||id<=0){MessageBox.Show("Informe o numero da venda.");return;}
   try{
    loadingMaster=true;
    try{master.DataSource=Query(SalesWithDailyTotalsSql()+" WHERE s.id=@id","@id",id);}finally{loadingMaster=false;}
    LoadDetails();
    heading.Text=((DataTable)master.DataSource).Rows.Count==0?"Venda nao encontrada.":"VENDA #"+id+" | Resultado da busca";
    if(((DataTable)master.DataSource).Rows.Count==0)details.DataSource=null;
    else summary.Text="  Consulta individual: venda #"+id+" | R$ "+Convert.ToDecimal(((DataTable)master.DataSource).Rows[0]["total"]).ToString("N2");
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
  bool loadingDetails=false;
  void LoadDetails(){
   if(loadingMaster||loadingDetails||IsDisposed||Disposing)return;
   loadingDetails=true;
   try{
    string key=credit?"cliente":"venda";
    // A seleção do WinForms pode mudar enquanto o DataSource é substituído.
    DataRowView view=master.CurrentRow==null?null:master.CurrentRow.DataBoundItem as DataRowView;
    if(view==null||view.Row==null||!view.Row.Table.Columns.Contains(key)){
     details.DataSource=null;
     return;
    }
    object selected=view.Row[key];
    if(selected==null||selected==DBNull.Value){details.DataSource=null;return;}
    DataTable result;
    string title;
    if(credit){
     string name=Convert.ToString(selected);
     result=Query("SELECT s.id AS venda,s.created_at AS data,s.gross_total AS valor,COALESCE((SELECT SUM(cp.amount) FROM credit_payments cp WHERE cp.sale_id=s.id),0) AS recebido,s.gross_total-COALESCE((SELECT SUM(cp.amount) FROM credit_payments cp WHERE cp.sale_id=s.id),0) AS saldo FROM sales s WHERE UPPER(COALESCE(s.payment_method,'')) LIKE '%FIADO%' AND COALESCE(NULLIF(TRIM(s.customer_name),''),'SEM CLIENTE')=@name ORDER BY s.id DESC","@name",name);
     decimal balance=0;
     foreach(DataRow r in result.Rows){
      if(r["saldo"]!=null&&r["saldo"]!=DBNull.Value)balance+=Convert.ToDecimal(r["saldo"]);
     }
     title="FIADO | "+name+" | Saldo: R$ "+balance.ToString("N2");
    }else{
     result=Query("SELECT id AS item_id,barcode AS codigo,name AS produto,quantity AS quantidade,unit_price AS preco,total AS valor FROM sale_items WHERE sale_id=@id ORDER BY id","@id",selected);
     title="VENDA #"+selected+" | Todos os itens da venda";
    }
    if(!IsDisposed&&!details.IsDisposed){
     details.DataSource=result;
     heading.Text=title;
    }
   }catch(Exception ex){
    if(!details.IsDisposed)details.DataSource=null;
    // Mostrar a origem do erro, se ainda houver outra condição não prevista.
    MessageBox.Show("Não foi possível consultar os detalhes."+Environment.NewLine+ex.ToString(),"Erro ao consultar detalhes",MessageBoxButtons.OK,MessageBoxIcon.Error);
   }finally{loadingDetails=false;}
  }
 }
}
