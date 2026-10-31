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
  readonly TextBox clientSearch=new TextBox {Width=205,Font=new Font("Segoe UI",10)};
  readonly Label stockValuation=new Label {Dock=DockStyle.Top,Height=68,Padding=new Padding(12,9,8,4),BackColor=Color.FromArgb(227,240,255),ForeColor=Color.FromArgb(17,57,110),Font=new Font("Segoe UI",11,FontStyle.Bold),Text="Apuração financeira do estoque"};
  readonly DataGridView grid=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells,SelectionMode=DataGridViewSelectionMode.FullRowSelect};
  readonly Label status=new Label {Dock=DockStyle.Bottom,Height=35,TextAlign=ContentAlignment.MiddleLeft};
  public ModuloConsultaForm(string path,string name) {
   db=path;module=name;Text="Armazem Gratidao | "+name+(name=="Clientes"?" - Cadastro":" (Consulta)");Width=1100;Height=700;BackColor=Color.FromArgb(229,242,255);
   var top=new FlowLayoutPanel {Dock=DockStyle.Top,Height=55,Padding=new Padding(8),BackColor=Color.FromArgb(220,95,105)};
   var reload=new Button {Text="Atualizar consulta",AutoSize=true,Height=33};reload.Click+=(s,e)=>LoadData();top.Controls.Add(reload);
   if(module=="Estoque"){
    top.Height=65;
    top.Controls.Add(new Label {Text="Buscar produto:",AutoSize=true,Padding=new Padding(3,8,1,0),ForeColor=Color.White,Font=new Font("Segoe UI",10,FontStyle.Bold)});
    stockSearch.TextChanged+=(s,e)=>LoadStock();top.Controls.Add(stockSearch);
    var entry=new Button {Text="Inserir QTD",Width=115,Height=34};entry.Click+=(s,e)=>StockEntry();top.Controls.Add(entry);
    var edit=new Button {Text="Editar / Salvar",Width=130,Height=34};edit.Click+=(s,e)=>StockEdit();top.Controls.Add(edit);
    var minimum=new Button {Text="Estoque mínimo",AutoSize=true,Height=34};minimum.Click+=(s,e)=>StockEditMinimum();top.Controls.Add(minimum);
    var history=new Button {Text="Auditoria",Width=115,Height=34};history.Click+=(s,e)=>StockHistory();top.Controls.Add(history);
    var verify=new Button {Text="Conferir baixas",AutoSize=true,Height=34};verify.Click+=(s,e)=>CheckStockLosses();top.Controls.Add(verify);
    var valuation=new Button {Text="Auditoria financeira",AutoSize=true,Height=34};valuation.Click+=(s,e)=>StockValuationAudit();top.Controls.Add(valuation);
    grid.CellDoubleClick+=(s,e)=>{if(e.RowIndex>=0&&e.ColumnIndex>=0){grid.CurrentCell=grid.Rows[e.RowIndex].Cells[e.ColumnIndex];StockEdit();}};
   }
   if(module=="Clientes"){
    top.Height=65;
    top.Controls.Add(new Label{Text="Buscar:",AutoSize=true,Padding=new Padding(0,9,0,0),ForeColor=Color.White});
    clientSearch.TextChanged+=(s,e)=>LoadClients();top.Controls.Add(clientSearch);
    var add=new Button{Text="Cadastrar cliente",AutoSize=true,Height=34};add.Click+=(s,e)=>EditClient(false);top.Controls.Add(add);
    var edit=new Button{Text="Editar cliente",AutoSize=true,Height=34};edit.Click+=(s,e)=>EditClient(true);top.Controls.Add(edit);
    var import=new Button{Text="Importar 12.1.6",AutoSize=true,Height=34};import.Click+=(s,e)=>ImportClients();top.Controls.Add(import);
    grid.CellDoubleClick+=(s,e)=>{if(e.RowIndex>=0)EditClient(true);};
   }
   grid.Font=new Font("Segoe UI",10,FontStyle.Regular);grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;grid.RowTemplate.Height=29;
   grid.BackgroundColor=Color.White;grid.BorderStyle=BorderStyle.None;grid.RowHeadersVisible=false;
   grid.EnableHeadersVisualStyles=false;grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(21,75,156);grid.ColumnHeadersDefaultCellStyle.ForeColor=Color.White;
   grid.ColumnHeadersDefaultCellStyle.Font=new Font("Segoe UI",10,FontStyle.Bold);grid.ColumnHeadersHeight=38;
   grid.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(238,246,255);
   grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(207,227,248);grid.DefaultCellStyle.SelectionForeColor=Color.Black;
   grid.DataBindingComplete+=(s,e)=>{foreach(DataGridViewColumn column in grid.Columns){string n=column.Name.ToLowerInvariant();if(n=="name"||n.Contains("descri")||n=="produto"||n=="cliente"){column.MinimumWidth=240;column.FillWeight=230;column.DefaultCellStyle.WrapMode=DataGridViewTriState.True;}}grid.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.None;};
   Shown+=(s,e)=>{var bounds=Screen.FromControl(this).WorkingArea;Width=Math.Min(Width,Math.Max(360,bounds.Width-24));Height=Math.Min(Height,Math.Max(300,bounds.Height-24));};
   Controls.Add(grid);Controls.Add(status);if(module=="Estoque")Controls.Add(stockValuation);Controls.Add(top);LoadData();
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
  static Dictionary<long,int> SortedProductCodes(SQLiteConnection conn){
   var map=new Dictionary<long,int>();int n=0;
   var cols=Columns(conn,"products");
   string active=cols.Contains("active")?"active":cols.Contains("is_active")?"is_active":cols.Contains("ativo")?"ativo":null;
   string filter=active==null?"":" WHERE COALESCE(\""+active+"\",1)<>0";
   using(var cmd=new SQLiteCommand("SELECT id FROM products"+filter+" ORDER BY name COLLATE NOCASE,id",conn))
   using(var rd=cmd.ExecuteReader())while(rd.Read())map[Convert.ToInt64(rd[0])]=++n;
   return map;
  }

  void UpdateStockValuation(SQLiteConnection conn){
   var cols=Columns(conn,"products");
   if(!cols.Contains("stock")||!cols.Contains("cost")||!cols.Contains("price")){
    stockValuation.Text="Apuração indisponível: falta quantidade, custo ou preço de venda no cadastro.";
    return;
   }
   string sql="SELECT COUNT(*),COALESCE(SUM(CASE WHEN COALESCE(stock,0)>0 THEN stock ELSE 0 END),0),"+
    "COALESCE(SUM(CASE WHEN COALESCE(stock,0)>0 THEN stock*COALESCE(cost,0) ELSE 0 END),0),"+
    "COALESCE(SUM(CASE WHEN COALESCE(stock,0)>0 THEN stock*COALESCE(price,0) ELSE 0 END),0),"+
    "SUM(CASE WHEN COALESCE(stock,0)<0 THEN 1 ELSE 0 END),"+
    "SUM(CASE WHEN COALESCE(stock,0)>0 AND COALESCE(cost,0)<=0 THEN 1 ELSE 0 END) FROM products";
   using(var cmd=new SQLiteCommand(sql,conn))using(var reader=cmd.ExecuteReader()){
    if(reader.Read()){
     decimal units=Dec(reader.GetValue(1)),cost=Dec(reader.GetValue(2)),sale=Dec(reader.GetValue(3));
     long missing=Convert.ToInt64(reader.GetValue(5)==DBNull.Value?0:reader.GetValue(5));
     long negative=Convert.ToInt64(reader.GetValue(4)==DBNull.Value?0:reader.GetValue(4));
     stockValuation.Text="ESTOQUE CADASTRADO  •  "+reader.GetValue(0)+" produtos  |  Quantidade: "+units.ToString("N3")+
      "  |  CUSTO TOTAL: R$ "+cost.ToString("N2")+"  |  VENDA POTENCIAL: R$ "+sale.ToString("N2")+
      Environment.NewLine+"Margem bruta potencial: R$ "+(sale-cost).ToString("N2")+
      "  |  Sem custo informado: "+missing+"  |  Quantidades negativas: "+negative+
      "  |  Dê duplo clique para detalhar.";
    }
   }
  }
  void StockValuationAudit(){
   try{
    using(var conn=StockOpen(true)){
     var columns=Columns(conn,"products");
     if(!columns.Contains("stock")||!columns.Contains("cost")||!columns.Contains("price"))throw new Exception("O banco não possui os campos necessários para avaliação.");
     var data=new DataTable();
     string sql="SELECT id AS 'ID interno',name AS 'Produto',COALESCE(stock,0) AS 'Quantidade',"+
      "COALESCE(cost,0) AS 'Custo unitário',ROUND(COALESCE(stock,0)*COALESCE(cost,0),2) AS 'Valor em custo',"+
      "COALESCE(price,0) AS 'Preço de venda',ROUND(COALESCE(stock,0)*COALESCE(price,0),2) AS 'Valor em venda' "+
      "FROM products ORDER BY name COLLATE NOCASE,id";
     using(var cmd=new SQLiteCommand(sql,conn))using(var adapter=new SQLiteDataAdapter(cmd))adapter.Fill(data);
     using(var form=new Form{Text="Auditoria financeira do estoque - todos os produtos",Width=1100,Height=660,StartPosition=FormStartPosition.CenterParent,BackColor=Color.FromArgb(229,242,255)}){
      var summary=new Label{Dock=DockStyle.Top,Height=108,Padding=new Padding(12,8,8,6),ForeColor=Color.FromArgb(19,64,123),Font=new Font("Segoe UI",11,FontStyle.Bold)};
      decimal totalCost=0,totalSale=0;int missing=0,negative=0;decimal quantity=0;
      foreach(DataRow row in data.Rows){
       decimal q=Dec(row["Quantidade"]);if(q<0)negative++;else quantity+=q;
       if(q>0&&Dec(row["Custo unitário"])<=0)missing++;
       if(q>0){totalCost+=q*Dec(row["Custo unitário"]);totalSale+=q*Dec(row["Preço de venda"]);}
      }
      summary.Text="Produtos cadastrados: "+data.Rows.Count+"  |  Quantidade total: "+quantity.ToString("N3")+
       Environment.NewLine+"Capital em mercadorias (custo): R$ "+totalCost.ToString("N2")+
       "  |  Valor potencial de venda: R$ "+totalSale.ToString("N2")+
       Environment.NewLine+"Diferença potencial: R$ "+(totalSale-totalCost).ToString("N2")+
       "  |  Sem custo: "+missing+"  |  Estoque negativo: "+negative;
      var note=new Label{Dock=DockStyle.Bottom,Height=47,Padding=new Padding(10,7,8,5),
       Text="Apuração baseada no estoque e preços atuais de TODOS os cadastros (inclusive inativos). Não é saldo de caixa nem lucro realizado. Sem custo informado pode subestimar o capital.",Font=new Font("Segoe UI",9,FontStyle.Regular)};
      var view=new DataGridView{Dock=DockStyle.Fill,DataSource=data,ReadOnly=true,AllowUserToAddRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,BackgroundColor=Color.White};
      form.Controls.Add(view);form.Controls.Add(note);form.Controls.Add(summary);form.ShowDialog(this);
     }
    }
   }catch(Exception ex){MessageBox.Show("Falha na auditoria financeira: "+ex.Message,"Estoque",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  }
  void StockProductDetails(){
   long id;try{id=SelectedStockId();}catch(Exception ex){MessageBox.Show(ex.Message);return;}
   try{
    using(var conn=StockOpen(true)){
     var cols=Columns(conn,"products");
     string barcode=cols.Contains("barcode")?"barcode":"' '";
     string sql="SELECT name,"+barcode+" AS barcode,COALESCE(stock,0) AS stock,"+
      (cols.Contains("cost")?"COALESCE(cost,0)":"0")+" AS cost,"+
      (cols.Contains("price")?"COALESCE(price,0)":"0")+" AS price FROM products WHERE id=@id";
     using(var cmd=new SQLiteCommand(sql,conn)){
      cmd.Parameters.AddWithValue("@id",id);
      using(var reader=cmd.ExecuteReader()){
       if(!reader.Read())throw new Exception("Produto não encontrado.");
       string name=Convert.ToString(reader["name"]),bar=Convert.ToString(reader["barcode"]);
       decimal q=Dec(reader["stock"]),cost=Dec(reader["cost"]),price=Dec(reader["price"]);
       using(var form=new Form{Text="Estoque | Detalhes do produto",Width=630,Height=370,StartPosition=FormStartPosition.CenterParent,BackColor=Color.FromArgb(229,242,255),FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false}){
        var label=new Label{Dock=DockStyle.Fill,Padding=new Padding(18),ForeColor=Color.FromArgb(20,57,118),
         Font=new Font("Segoe UI",12,FontStyle.Bold),Text="PRODUTO: "+name+
         Environment.NewLine+"Código de barras: "+bar+
         Environment.NewLine+"Quantidade atual: "+q.ToString("N3")+
         Environment.NewLine+"Custo unitário: R$ "+cost.ToString("N2")+
         Environment.NewLine+"Total em custo: R$ "+(q*cost).ToString("N2")+
         Environment.NewLine+"Preço de venda: R$ "+price.ToString("N2")+
         Environment.NewLine+"Total potencial de venda: R$ "+(q*price).ToString("N2")};
        var barActions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=58,Padding=new Padding(10)};
        var audit=new Button{Text="Ver histórico de auditoria",Width=205,Height=35};
        audit.Click+=(s,e)=>{form.Close();StockHistory();};
        var close=new Button{Text="Fechar",Width=105,Height=35};close.Click+=(s,e)=>form.Close();
        barActions.Controls.Add(audit);barActions.Controls.Add(close);
        form.Controls.Add(label);form.Controls.Add(barActions);form.ShowDialog(this);
       }
      }
     }
    }
   }catch(Exception ex){MessageBox.Show("Falha ao abrir detalhes: "+ex.Message,"Estoque",MessageBoxButtons.OK,MessageBoxIcon.Error);}
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
      StockField(cols,"status","Situação")+","+
      "ROUND(COALESCE(stock,0)*COALESCE(cost,0),2) AS \"Valor em custo\","+
      "ROUND(COALESCE(stock,0)*COALESCE(price,0),2) AS \"Valor em venda\" FROM products WHERE (@term='' OR name LIKE @like OR "+
      (cols.Contains("barcode")?"barcode LIKE @like":"0")+
      " OR CAST(id AS TEXT)=@term) ORDER BY name COLLATE NOCASE,id LIMIT 500";
     using(var cmd=new SQLiteCommand(sql,conn)){
      string term=stockSearch.Text.Trim();
      cmd.Parameters.AddWithValue("@term",term);cmd.Parameters.AddWithValue("@like","%"+term+"%");
      using(var adapter=new SQLiteDataAdapter(cmd)){
       var table=new DataTable();adapter.Fill(table);
       var codes=SortedProductCodes(conn);
       table.Columns.Add("Código",typeof(int)).SetOrdinal(0);
       foreach(DataRow row in table.Rows){int code;if(codes.TryGetValue(Convert.ToInt64(row["Código interno"]),out code))row["Código"]=code;else row["Código"]=DBNull.Value;}
       grid.DataSource=table;
       if(grid.Columns.Contains("Código interno"))grid.Columns["Código interno"].Visible=false;
       foreach(DataGridViewColumn col in grid.Columns){
        col.DefaultCellStyle.WrapMode=DataGridViewTriState.False;
        if(col.Name=="Produto"){col.FillWeight=260;col.MinimumWidth=230;}
        else if(col.Name=="Código de barras")col.FillWeight=130;
        else col.FillWeight=95;
        if(col.Name=="Quantidade atual"||col.Name=="Estoque mínimo")col.DefaultCellStyle.Format="N3";
        if(col.Name=="Preço de venda"||col.Name=="Custo"||col.Name=="Valor em custo"||col.Name=="Valor em venda")col.DefaultCellStyle.Format="N2";
       }
       status.Text="ESTOQUE  •  "+table.Rows.Count+" produtos exibidos (máximo 500 por consulta). Duplo clique para detalhes e auditoria do produto.";
       UpdateStockValuation(conn);
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
  // O duplo clique e o botão Editar / Salvar abrem a mesma janela de entrada auditada.
  void StockEdit(){StockEntry();}
  void StockEntry(){
   long id;try{id=SelectedStockId();}catch(Exception ex){MessageBox.Show(ex.Message);return;}
   string productName;decimal initialStock;
   try{
    using(var check=StockOpen(true))
    using(var q=new SQLiteCommand("SELECT name,stock FROM products WHERE id=@id",check)){
     q.Parameters.AddWithValue("@id",id);
     using(var reader=q.ExecuteReader()){
      if(!reader.Read())throw new Exception("Produto não encontrado.");
      productName=Convert.ToString(reader["name"]);initialStock=Dec(reader["stock"]);
     }
    }
   }catch(Exception ex){MessageBox.Show(ex.Message,"Estoque");return;}
   using(var form=new Form {Text="Estoque | Editar e Salvar - Entrada de Produto",Width=670,Height=445,MinimumSize=new Size(610,415),StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,BackColor=Color.FromArgb(229,242,255)}){
    var title=new Label{Left=17,Top=14,Width=610,Height=48,Text="PRODUTO: "+productName,ForeColor=Color.FromArgb(12,59,137),Font=new Font("Segoe UI",12,FontStyle.Bold)};
    var current=new Label{Left=17,Top=63,Width=605,Height=27,Text="Quantidade atual em estoque: "+initialStock.ToString("N3"),Font=new Font("Segoe UI",10,FontStyle.Bold)};
    var date=new DateTimePicker{Left=195,Top=105,Width=205,Format=DateTimePickerFormat.Custom,CustomFormat="dd/MM/yyyy",Value=DateTime.Today};
    var quantity=new NumericUpDown{Left=195,Top=147,Width=205,DecimalPlaces=3,Minimum=0.001M,Maximum=10000000M,Value=1M};
    var description=new TextBox{Left=195,Top=190,Width=410,Height=90,Multiline=true,ScrollBars=ScrollBars.Vertical,MaxLength=400};
    var projected=new Label{Left=195,Top=295,Width=405,Height=27,Font=new Font("Segoe UI",10,FontStyle.Bold),ForeColor=Color.FromArgb(12,59,137)};
    Action refresh=()=>projected.Text="Quantidade após salvar: "+(initialStock+quantity.Value).ToString("N3");
    quantity.ValueChanged+=(s,e)=>refresh();refresh();
    var save=new Button{Left=195,Top=330,Width=193,Height=40,Text="Salvar entrada",BackColor=Color.FromArgb(19,92,169),ForeColor=Color.White,Font=new Font("Segoe UI",10,FontStyle.Bold)};
    var cancel=new Button{Left=402,Top=330,Width=120,Height=40,Text="Cancelar"};
    cancel.Click+=(s,e)=>form.Close();
    form.Controls.AddRange(new Control[]{
     title,current,
     new Label{Text="Data da entrada",Left=17,Top=110,AutoSize=true,Font=new Font("Segoe UI",10,FontStyle.Bold)},
     new Label{Text="Quantidade a adicionar",Left=17,Top=152,AutoSize=true,Font=new Font("Segoe UI",10,FontStyle.Bold)},
     new Label{Text="Descrição da entrada",Left=17,Top=194,AutoSize=true,Font=new Font("Segoe UI",10,FontStyle.Bold)},
     date,quantity,description,projected,save,cancel
    });
    save.Click+=(s,e)=>{
     if(string.IsNullOrWhiteSpace(description.Text)){MessageBox.Show("Informe a descrição da entrada do produto.");return;}
     try{
      string backup;
      decimal before,after;
      using(var conn=StockOpen(false)){
       if(!HasTable(conn,"runtime_operation_context")||!HasTable(conn,"stock_audit_log"))
        throw new Exception("Banco sem proteção e auditoria de estoque. A entrada não será gravada.");
       backup=BackupStock(conn);
       using(var tx=conn.BeginTransaction()){
        string name,barcode;
        using(var q=new SQLiteCommand("SELECT name,barcode,stock FROM products WHERE id=@id",conn,tx)){
         q.Parameters.AddWithValue("@id",id);
         using(var rd=q.ExecuteReader()){
          if(!rd.Read())throw new Exception("Produto não encontrado.");
          name=Convert.ToString(rd["name"]);barcode=Convert.ToString(rd["barcode"]);before=Dec(rd["stock"]);
         }
        }
        if(before!=initialStock)throw new Exception("A quantidade foi alterada após abrir a janela. Atualize e tente novamente.");
        after=checked(before+quantity.Value);
        int oldAuthorization;string oldOperation;
        using(var q=new SQLiteCommand("SELECT authorized,operation FROM runtime_operation_context WHERE id=1",conn,tx))
        using(var rd=q.ExecuteReader()){
         if(!rd.Read())throw new Exception("Proteção de estoque não configurada.");
         oldAuthorization=Convert.ToInt32(rd["authorized"]);oldOperation=Convert.ToString(rd["operation"]);
        }
        Execute(conn,tx,"UPDATE runtime_operation_context SET authorized=1,operation='ENTRADA_MANUAL_AUDITADA' WHERE id=1");
        using(var q=new SQLiteCommand("UPDATE products SET stock=@after WHERE id=@id AND stock=@before",conn,tx)){
         q.Parameters.AddWithValue("@after",after);q.Parameters.AddWithValue("@id",id);q.Parameters.AddWithValue("@before",before);
         if(q.ExecuteNonQuery()!=1)throw new Exception("A quantidade mudou durante a gravação. Nenhuma entrada foi concluída.");
        }
        string note="Data da entrada: "+date.Value.ToString("dd/MM/yyyy")+" | "+description.Text.Trim();
        Execute(conn,tx,"INSERT INTO stock_audit_log(product_id,product_name,barcode,event_type,quantity_before,quantity_after,quantity_delta,reference_type,reason) VALUES(@id,@name,@barcode,'ENTRADA_MANUAL',@before,@after,@delta,'ESTOQUE',@reason)",
         P("@id",id),P("@name",name),P("@barcode",barcode),P("@before",before),P("@after",after),P("@delta",quantity.Value),P("@reason",note));
        Execute(conn,tx,"UPDATE runtime_operation_context SET authorized=@authorized,operation=@operation WHERE id=1",P("@authorized",oldAuthorization),P("@operation",oldOperation));
        tx.Commit();
       }
      }
      form.DialogResult=DialogResult.OK;form.Close();
      LoadStock();
      MessageBox.Show("Entrada salva em "+date.Value.ToString("dd/MM/yyyy")+".\nAnterior: "+before.ToString("N3")+"\nAdicionada: "+quantity.Value.ToString("N3")+"\nAtual: "+after.ToString("N3")+"\nBackup: "+backup,"Estoque");
     }catch(Exception ex){MessageBox.Show("Entrada não salva: "+ex.Message,"Estoque",MessageBoxButtons.OK,MessageBoxIcon.Error);}
    };
    form.ShowDialog(this);
   }
  }
  void StockEditMinimum(){
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
  void CheckStockLosses(){
   try{
    using(var conn=StockOpen(true)){
     if(!HasTable(conn,"stock_audit_log")){
      MessageBox.Show("O banco não possui registro de auditoria de estoque. Não é possível confirmar as baixas neste banco.","Conferência de estoque");return;
     }
     var dt=new DataTable();
     dt.Columns.Add("Data e hora");dt.Columns.Add("Produto");dt.Columns.Add("Código interno");
     dt.Columns.Add("Estoque anterior");dt.Columns.Add("Estoque novo");dt.Columns.Add("Quantidade baixada");
     dt.Columns.Add("Evento");dt.Columns.Add("Venda vinculada");dt.Columns.Add("Situação");
     // Relatório somente de leitura: nunca ajusta quantidade nem apaga lançamentos.
     string sql="SELECT a.created_at,a.product_name,a.product_id,a.quantity_before,a.quantity_after,a.quantity_delta,a.event_type,a.reference_id,"+
       " CASE WHEN UPPER(COALESCE(a.event_type,''))='VENDA' AND EXISTS(SELECT 1 FROM sales s JOIN sale_items si ON si.sale_id=s.id WHERE s.id=a.reference_id AND si.product_id=a.product_id) THEN 'Venda encontrada' "+
       " WHEN UPPER(COALESCE(a.event_type,''))='VENDA' THEN 'VERIFICAR: venda ou item não encontrado' "+
       " ELSE 'VERIFICAR: baixa sem evento de venda' END AS situacao "+
       "FROM stock_audit_log a WHERE COALESCE(a.quantity_delta,0)<-0.000001 ORDER BY a.id DESC LIMIT 1000";
     using(var cmd=new SQLiteCommand(sql,conn))using(var reader=cmd.ExecuteReader()){
      while(reader.Read()){
       var row=dt.NewRow();
       for(int i=0;i<9;i++)row[i]=reader.IsDBNull(i)?"":Convert.ToString(reader.GetValue(i));
       dt.Rows.Add(row);
      }
     }
     using(var form=new Form {Text="Conferência de baixas de estoque - somente leitura",Width=1180,Height=650,StartPosition=FormStartPosition.CenterParent}){
      var explanation=new Label {Dock=DockStyle.Top,Height=70,Padding=new Padding(12),Text="Lista das últimas 1.000 baixas registradas. Uma baixa marcada VERIFICAR não é prova de perda: pode ser cancelamento, ajuste ou registro antigo. Confira as informações antes de qualquer correção.",ForeColor=Color.FromArgb(20,52,95),Font=new Font("Segoe UI",10,FontStyle.Bold)};
      var view=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,BackgroundColor=Color.White,DataSource=dt};
      form.Controls.Add(view);form.Controls.Add(explanation);form.ShowDialog(this);
     }
    }
   }catch(Exception ex){MessageBox.Show("Não foi possível verificar as baixas: "+ex.Message,"Conferência de estoque",MessageBoxButtons.OK,MessageBoxIcon.Error);}
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

  static string OnlyDigits(string value){
   var sb=new System.Text.StringBuilder();foreach(char ch in value??"")if(ch>='0'&&ch<='9')sb.Append(ch);return sb.ToString();
  }
  static bool ValidCpfCnpj(string source){
   string n=OnlyDigits(source);
   if(n.Length!=11&&n.Length!=14)return false;
   if(n==new string(n[0],n.Length))return false;
   if(n.Length==11){
    int sum=0;for(int i=0;i<9;i++)sum+=(n[i]-'0')*(10-i);
    int d=sum*10%11;if(d==10)d=0;if(d!=n[9]-'0')return false;
    sum=0;for(int i=0;i<10;i++)sum+=(n[i]-'0')*(11-i);
    d=sum*10%11;if(d==10)d=0;return d==n[10]-'0';
   }
   int[] w1={5,4,3,2,9,8,7,6,5,4,3,2},w2={6,5,4,3,2,9,8,7,6,5,4,3,2};
   int total=0;for(int i=0;i<12;i++)total+=(n[i]-'0')*w1[i];
   int digit=total%11<2?0:11-total%11;if(digit!=n[12]-'0')return false;
   total=0;for(int i=0;i<13;i++)total+=(n[i]-'0')*w2[i];
   digit=total%11<2?0:11-total%11;return digit==n[13]-'0';
  }
  static void PrepareCustomerTable(SQLiteConnection conn,SQLiteTransaction tx){
   Execute(conn,tx,"CREATE TABLE IF NOT EXISTS customers (id INTEGER PRIMARY KEY AUTOINCREMENT,name TEXT NOT NULL,phone TEXT,document TEXT)");
   var cols=Columns(conn,"customers");
   if(!cols.Contains("id")||!cols.Contains("name"))throw new Exception("Tabela customers incompatível.");
   if(!cols.Contains("phone"))Execute(conn,tx,"ALTER TABLE customers ADD COLUMN phone TEXT");
   if(!cols.Contains("document"))Execute(conn,tx,"ALTER TABLE customers ADD COLUMN document TEXT");
   Execute(conn,tx,"CREATE TABLE IF NOT EXISTS customer_registration_audit(id INTEGER PRIMARY KEY AUTOINCREMENT,customer_id INTEGER,action TEXT NOT NULL,old_name TEXT,old_phone TEXT,old_document TEXT,new_name TEXT,new_phone TEXT,new_document TEXT,changed_at TEXT NOT NULL)");
  }
  void LoadClients(){
   try{
    using(var conn=StockOpen(true)){
     var clients=ReadClients(conn);
     var dt=new DataTable();dt.Columns.Add("Código",typeof(int));dt.Columns.Add("ID interno",typeof(long));dt.Columns.Add("Cliente / Razão social");dt.Columns.Add("CPF / CNPJ");dt.Columns.Add("Telefone");dt.Columns.Add("Origem");
     var ids=new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);
     if(HasTable(conn,"customers")&&Columns(conn,"customers").Contains("id")){
      using(var cmd=new SQLiteCommand("SELECT id,name FROM customers",conn))
      using(var reader=cmd.ExecuteReader())while(reader.Read()){string name=Convert.ToString(reader["name"]).Trim();if(name.Length>0&&!ids.ContainsKey(name))ids.Add(name,Convert.ToInt64(reader["id"]));}
     }
     string term=clientSearch.Text.Trim(),number=OnlyDigits(term);
     int alphabeticalNumber=0;
     foreach(var client in clients){
      alphabeticalNumber++;
      if(term.Length>0&&client.Item1.IndexOf(term,StringComparison.OrdinalIgnoreCase)<0&&client.Item2.IndexOf(term,StringComparison.OrdinalIgnoreCase)<0&&(number.Length==0||OnlyDigits(client.Item3).IndexOf(number,StringComparison.Ordinal)<0))continue;
      long id;bool registered=ids.TryGetValue(client.Item1,out id);
      dt.Rows.Add(alphabeticalNumber,registered?(object)id:DBNull.Value,client.Item1,client.Item3,client.Item2,registered?"Cadastrado":"Vendas / Fiado");
     }
     grid.DataSource=dt;
     if(grid.Columns.Contains("ID interno"))grid.Columns["ID interno"].Visible=false;
     if(grid.Columns.Contains("Cliente / Razão social")){grid.Columns["Cliente / Razão social"].MinimumWidth=240;grid.Columns["Cliente / Razão social"].FillWeight=220;}
     status.Text="CLIENTES: "+dt.Rows.Count+" exibidos. Cadastrar, editar, importar CPF/CNPJ. Vendas e Fiado preservados.";
    }
   }catch(Exception ex){grid.DataSource=null;status.Text="Erro em Clientes: "+ex.Message;}
  }
  void EditClient(bool edit){
   long id=0;string oldName="",oldPhone="",oldDoc="";
   if(edit){
    if(grid.CurrentRow==null){MessageBox.Show("Selecione um cliente.");return;}
    oldName=Convert.ToString(grid.CurrentRow.Cells["Cliente / Razão social"].Value);
    oldPhone=Convert.ToString(grid.CurrentRow.Cells["Telefone"].Value);
    oldDoc=Convert.ToString(grid.CurrentRow.Cells["CPF / CNPJ"].Value);
    object value=grid.CurrentRow.Cells["ID interno"].Value;if(value!=null&&value!=DBNull.Value)id=Convert.ToInt64(value);
   }
   using(var form=new Form{Text=edit?"Editar cliente":"Cadastrar cliente",Width=650,Height=390,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,BackColor=Color.FromArgb(229,242,255)}){
    var panel=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=5,Padding=new Padding(18)};
    panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,175));panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
    var name=new TextBox{Text=oldName,MaxLength=180,Dock=DockStyle.Top};
    var type=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=190};
    type.Items.AddRange(new object[]{"Pessoa física (CPF)","Pessoa jurídica (CNPJ)"});type.SelectedIndex=OnlyDigits(oldDoc).Length==14?1:0;
    var document=new TextBox{Text=oldDoc,MaxLength=20,Width=230};
    var phone=new TextBox{Text=oldPhone,MaxLength=40,Width=230};
    Control[] fields={name,type,document,phone};string[] labels={"Nome / Razão social","Tipo de cliente","CPF ou CNPJ","Telefone"};
    for(int i=0;i<fields.Length;i++){panel.RowStyles.Add(new RowStyle(SizeType.Absolute,54));panel.Controls.Add(new Label{Text=labels[i],AutoSize=true,Font=new Font("Segoe UI",10,FontStyle.Bold)},0,i);panel.Controls.Add(fields[i],1,i);}
    var save=new Button{Text="Salvar cadastro",Width=190,Height=38,BackColor=Color.FromArgb(21,75,156),ForeColor=Color.White};
    save.Click+=(s,e)=>{
     string newName=name.Text.Trim(),doc=OnlyDigits(document.Text);
     if(newName.Length<2){MessageBox.Show("Informe nome ou razão social.");return;}
     if(doc.Length>0&&(doc.Length!=(type.SelectedIndex==0?11:14)||!ValidCpfCnpj(doc))){MessageBox.Show("CPF ou CNPJ inválido.");return;}
     try{
      string backup;
      using(var conn=StockOpen(false)){
       backup=BackupStock(conn);
       using(var tx=conn.BeginTransaction()){
        PrepareCustomerTable(conn,tx);
        long currentId=id;string beforeName="",beforePhone="",beforeDoc="";
        if(currentId==0&&edit){
         using(var lookup=new SQLiteCommand("SELECT id FROM customers WHERE LOWER(TRIM(name))=LOWER(TRIM(@name)) LIMIT 1",conn,tx)){
          lookup.Parameters.AddWithValue("@name",oldName);object found=lookup.ExecuteScalar();if(found!=null)currentId=Convert.ToInt64(found);
         }
        }
        if(currentId>0){
         using(var old=new SQLiteCommand("SELECT name,phone,document FROM customers WHERE id=@id",conn,tx)){
          old.Parameters.AddWithValue("@id",currentId);
          using(var rd=old.ExecuteReader()){if(!rd.Read())throw new Exception("Cadastro não localizado; atualize a consulta.");beforeName=Convert.ToString(rd["name"]);beforePhone=Convert.ToString(rd["phone"]);beforeDoc=Convert.ToString(rd["document"]);}
         }
        }
        using(var check=new SQLiteCommand("SELECT COUNT(*) FROM customers WHERE id<>@id AND (LOWER(TRIM(name))=LOWER(TRIM(@name)) OR (@document<>'' AND REPLACE(REPLACE(REPLACE(COALESCE(document,''),'.',''),'-',''),'/','')=@document))",conn,tx)){
         check.Parameters.AddWithValue("@id",currentId);check.Parameters.AddWithValue("@name",newName);check.Parameters.AddWithValue("@document",doc);
         if(Convert.ToInt32(check.ExecuteScalar())>0)throw new Exception("Já existe um cliente com esse nome ou CPF/CNPJ.");
        }
        if(currentId>0)Execute(conn,tx,"UPDATE customers SET name=@name,phone=@phone,document=@doc WHERE id=@id",P("@name",newName),P("@phone",phone.Text.Trim()),P("@doc",doc),P("@id",currentId));
        else{
         Execute(conn,tx,"INSERT INTO customers(name,phone,document) VALUES(@name,@phone,@doc)",P("@name",newName),P("@phone",phone.Text.Trim()),P("@doc",doc));
         using(var last=new SQLiteCommand("SELECT last_insert_rowid()",conn,tx))currentId=Convert.ToInt64(last.ExecuteScalar());
        }
        Execute(conn,tx,"INSERT INTO customer_registration_audit(customer_id,action,old_name,old_phone,old_document,new_name,new_phone,new_document,changed_at) VALUES(@id,@action,@oldName,@oldPhone,@oldDoc,@newName,@newPhone,@newDoc,@date)",
          P("@id",currentId),P("@action",beforeName.Length==0?"CADASTRO":"EDICAO"),P("@oldName",beforeName),P("@oldPhone",beforePhone),P("@oldDoc",beforeDoc),P("@newName",newName),P("@newPhone",phone.Text.Trim()),P("@newDoc",doc),P("@date",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
        tx.Commit();
       }
      }
      form.DialogResult=DialogResult.OK;form.Close();LoadClients();MessageBox.Show("Cadastro salvo e auditado. Backup: "+backup,"Clientes");
     }catch(Exception ex){MessageBox.Show("Não foi possível salvar: "+ex.Message,"Clientes",MessageBoxButtons.OK,MessageBoxIcon.Error);}
    };
    panel.RowStyles.Add(new RowStyle(SizeType.Absolute,50));panel.Controls.Add(save,1,4);form.Controls.Add(panel);form.ShowDialog(this);
   }
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