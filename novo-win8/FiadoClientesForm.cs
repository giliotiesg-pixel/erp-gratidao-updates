using System;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
namespace ArmazemGratidao {
 public sealed class FiadoClientesForm:Form {
  readonly string db;
  readonly FlowLayoutPanel clients=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(8)};
  readonly DataGridView openSales=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
  readonly DataGridView paymentHistory=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
  readonly DataGridView paidSales=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
  readonly Label selected=new Label {Dock=DockStyle.Top,Height=42,Font=new Font("Segoe UI",14,FontStyle.Bold)};
  readonly NumericUpDown amount=new NumericUpDown {DecimalPlaces=2,Maximum=100000000,Width=130};
  readonly ComboBox method=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=160};
  string customer="";
  TabControl saleTabs;
  Panel clientHome;
  SplitContainer detailSplit;
  readonly TextBox clientSearch=new TextBox {Dock=DockStyle.Top,Height=34,Font=new Font("Segoe UI",11)};
  readonly Label overview=new Label {Dock=DockStyle.Top,Height=55,Font=new Font("Segoe UI",10,FontStyle.Bold),ForeColor=Color.FromArgb(18,51,99),BackColor=Color.FromArgb(232,242,252),TextAlign=ContentAlignment.MiddleLeft};
  public FiadoClientesForm(string database) {
   db=database;Text="Armazem Gratidao | Fiado por Cliente";Width=1250;Height=790;StartPosition=FormStartPosition.CenterScreen;FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;MinimizeBox=false;
   var split=new SplitContainer {Dock=DockStyle.Fill,SplitterDistance=265,IsSplitterFixed=true};
   detailSplit=split;
   split.Panel1.Controls.Add(clients);
   clientSearch.TextChanged+=(s,e)=>LoadClients();
   split.Panel1.Controls.Add(clientSearch);
   split.Panel1.Controls.Add(new Label {Text="  PESQUISAR CLIENTE",Dock=DockStyle.Top,Height=28,Font=new Font("Segoe UI",10,FontStyle.Bold),ForeColor=Color.White,BackColor=Color.FromArgb(20,73,158)});
   var tabs=new TabControl {Dock=DockStyle.Fill};
   saleTabs=tabs;
   var pending=new TabPage("Fiado atual - em aberto");pending.Controls.Add(openSales);
   var previous=new TabPage("Fiados anteriores - pagos");previous.Controls.Add(paidSales);
   tabs.TabPages.Add(pending);tabs.TabPages.Add(previous);
   var historyTab=new TabPage("Histórico de recebimentos");historyTab.Controls.Add(paymentHistory);tabs.TabPages.Add(historyTab);
   var switchBar=new FlowLayoutPanel {Dock=DockStyle.Top,Height=44,Padding=new Padding(5),BackColor=Color.FromArgb(232,242,252)};
   var showOpen=new Button {Text="Fiado atual - em aberto",Width=190,Height=32,BackColor=Color.FromArgb(20,73,158),ForeColor=Color.White};showOpen.Click+=(s,e)=>tabs.SelectedIndex=0;switchBar.Controls.Add(showOpen);
   var showPrevious=new Button {Text="Fiados anteriores - pagos",Width=210,Height=32,BackColor=Color.White,ForeColor=Color.FromArgb(20,73,158)};showPrevious.Click+=(s,e)=>tabs.SelectedIndex=1;switchBar.Controls.Add(showPrevious);
   tabs.SelectedIndexChanged+=(s,e)=>{showOpen.BackColor=tabs.SelectedIndex==0?Color.FromArgb(20,73,158):Color.White;showOpen.ForeColor=tabs.SelectedIndex==0?Color.White:Color.FromArgb(20,73,158);showPrevious.BackColor=tabs.SelectedIndex==1?Color.FromArgb(20,73,158):Color.White;showPrevious.ForeColor=tabs.SelectedIndex==1?Color.White:Color.FromArgb(20,73,158);};
   var bottom=new FlowLayoutPanel {Dock=DockStyle.Bottom,Height=122,Padding=new Padding(8),WrapContents=true,AutoScroll=true,BackColor=Color.FromArgb(235,241,250)};
   bottom.Controls.Add(new Label {Text="Forma de pagamento:",AutoSize=true,Padding=new Padding(0,9,0,0)});
   method.Items.AddRange(new object[]{"DINHEIRO","PIX","PIX QR CODE","DEBITO","CREDITO","ALIMENTACAO","ALELO","PLUXXE","TICKET","VR"});method.SelectedIndex=0;bottom.Controls.Add(method);
   bottom.Controls.Add(new Label {Text="Valor R$:",AutoSize=true,Padding=new Padding(0,9,0,0)});bottom.Controls.Add(amount);
   var pay=new Button {Text="Registrar pagamento",AutoSize=true,Height=36,BackColor=Color.FromArgb(24,82,170),ForeColor=Color.White};pay.Click+=(s,e)=>Pay();bottom.Controls.Add(pay);
   var settle=new Button {Text="Receber todas as vendas",AutoSize=true,Height=36,BackColor=Color.FromArgb(12,110,80),ForeColor=Color.White};settle.Click+=(s,e)=>PayAll();bottom.Controls.Add(settle);
   var rename=new Button {Text="Corrigir cliente da venda",AutoSize=true,Height=36,BackColor=Color.FromArgb(98,60,154),ForeColor=Color.White};rename.Click+=(s,e)=>CorrectCustomer();bottom.Controls.Add(rename);
   var audit=new Button {Text="Conferir saldos",AutoSize=true,Height=36,BackColor=Color.FromArgb(145,73,32),ForeColor=Color.White};audit.Click+=(s,e)=>AuditBalances();bottom.Controls.Add(audit);
   var inspect=new Button {Text="Itens e pagamentos da venda",AutoSize=true,Height=36};inspect.Click+=(s,e)=>ShowSaleDetails();bottom.Controls.Add(inspect);
   var auditHistory=new Button {Text="Histórico de alterações",AutoSize=true,Height=36,BackColor=Color.FromArgb(80,60,125),ForeColor=Color.White};auditHistory.Click+=(s,e)=>ShowItemAuditHistory();bottom.Controls.Add(auditHistory);
   var receipt=new Button {Text="Cupom / WhatsApp",AutoSize=true,Height=36,BackColor=Color.FromArgb(15,119,78),ForeColor=Color.White};receipt.Click+=(s,e)=>SendReceipt();bottom.Controls.Add(receipt);
   var refresh=new Button {Text="Atualizar",AutoSize=true,Height=36};refresh.Click+=(s,e)=>LoadClients();bottom.Controls.Add(refresh);
   split.Panel2.Controls.Add(tabs);split.Panel2.Controls.Add(bottom);split.Panel2.Controls.Add(switchBar);split.Panel2.Controls.Add(selected);split.Panel2.Controls.Add(overview);
   foreach(var table in new[]{openSales,paidSales,paymentHistory}){table.RowTemplate.Height=34;table.RowHeadersVisible=false;table.EnableHeadersVisualStyles=false;table.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(20,73,158);table.ColumnHeadersDefaultCellStyle.ForeColor=Color.White;table.DefaultCellStyle.SelectionBackColor=Color.FromArgb(26,88,166);table.DefaultCellStyle.SelectionForeColor=Color.White;table.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(239,245,252);table.Font=new Font("Segoe UI",10);table.SelectionMode=DataGridViewSelectionMode.FullRowSelect;}
   Controls.Add(split);
   clientHome=new Panel {Dock=DockStyle.Fill,BackColor=Color.White};
   var homeHeader=new Label {Dock=DockStyle.Top,Height=70,Text="FIADO  |  SELECIONE UM CLIENTE",Font=new Font("Segoe UI",17,FontStyle.Bold),ForeColor=Color.FromArgb(20,73,158),TextAlign=ContentAlignment.MiddleCenter};
   clientHome.Controls.Add(new Label {Dock=DockStyle.Fill,Text="Clique no nome do cliente à esquerda para consultar vendas, itens, pagamentos e emitir cupom.",Font=new Font("Segoe UI",12),TextAlign=ContentAlignment.MiddleCenter});
   clientHome.Controls.Add(homeHeader);
   split.Panel2.Controls.Add(clientHome);clientHome.BringToFront();
   var back=new Button {Text="← Voltar aos clientes",Dock=DockStyle.Top,Height=37,BackColor=Color.FromArgb(20,73,158),ForeColor=Color.White};
   back.Click+=(s,e)=>{customer="";clientHome.Visible=true;clientHome.BringToFront();};
   split.Panel2.Controls.Add(back);back.BringToFront();
   LoadClients();
  }
  SQLiteConnection Open(bool readOnly=false) {var c=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=db,ReadOnly=readOnly,FailIfMissing=true}.ToString());c.Open();return c;}
  DataTable Query(string sql,string p=null,object value=null) {
   using(var c=Open(true))using(var cmd=new SQLiteCommand(sql,c)) {
    if(p!=null)cmd.Parameters.AddWithValue(p,value);
    var dt=new DataTable();using(var a=new SQLiteDataAdapter(cmd))a.Fill(dt);return dt;
   }
  }
  const string CreditFilter="(UPPER(COALESCE(s.payment_method,'')) LIKE '%FIADO%' OR UPPER(COALESCE(s.status,'')) LIKE '%FIADO%')";
  const string Balance="COALESCE(s.gross_total,0)-COALESCE((SELECT SUM(p.amount) FROM credit_payments p WHERE p.sale_id=s.id),0)";
  void LoadClients(){
   try {
    var dt=Query("SELECT COALESCE(NULLIF(TRIM(s.customer_name),''),'SEM CLIENTE') AS cliente,COUNT(*) AS vendas,SUM(CASE WHEN "+Balance+">0.005 THEN "+Balance+" ELSE 0 END) AS saldo FROM sales s WHERE "+CreditFilter+" GROUP BY COALESCE(NULLIF(TRIM(s.customer_name),''),'SEM CLIENTE') ORDER BY cliente");
    clients.SuspendLayout();clients.Controls.Clear();
    decimal allBalance=0;int visibleClients=0;
    foreach(DataRow r in dt.Rows) {
     var name=Convert.ToString(r["cliente"]);var balance=Convert.ToDecimal(r["saldo"]);
     allBalance+=balance;
     if(!name.ToUpperInvariant().Contains(clientSearch.Text.Trim().ToUpperInvariant()))continue;
     visibleClients++;
     var b=new Button {Text=name+"  |  R$ "+balance.ToString("N2"),Width=225,Height=52,TextAlign=ContentAlignment.MiddleLeft,Font=new Font("Segoe UI",9,FontStyle.Bold),BackColor=Color.White,ForeColor=Color.FromArgb(25,75,150),Tag=name};
     b.Click+=(s,e)=>SelectClient(Convert.ToString(((Button)s).Tag));clients.Controls.Add(b);
    }
    clients.ResumeLayout();
    overview.Text="  FIADO  |  "+dt.Rows.Count+" CLIENTES  |  EM ABERTO: R$ "+allBalance.ToString("N2");
    if(customer.Length>0)SelectClient(customer);
   }catch(Exception ex){MessageBox.Show(ex.Message,"Erro ao listar clientes");}
  }
  void SelectClient(string name){
   customer=name;clientHome.Visible=false;selected.Text="FIADO | "+customer;
   try {
    var sql="SELECT s.id AS venda,s.created_at AS data,s.status AS situacao,s.gross_total AS valor,COALESCE((SELECT SUM(p.amount) FROM credit_payments p WHERE p.sale_id=s.id),0) AS pago,"+Balance+" AS saldo FROM sales s WHERE "+CreditFilter+" AND COALESCE(NULLIF(TRIM(s.customer_name),''),'SEM CLIENTE')=@name";
    openSales.DataSource=Query(sql+" AND "+Balance+">0.005 ORDER BY s.id DESC","@name",name);
    paidSales.DataSource=Query(sql+" AND "+Balance+"<=0.005 ORDER BY s.id DESC","@name",name);
    paymentHistory.DataSource=Query("SELECT p.paid_at AS data,p.sale_id AS venda,p.payment_method AS forma,p.amount AS valor FROM credit_payments p INNER JOIN sales s ON s.id=p.sale_id WHERE "+CreditFilter+" AND COALESCE(NULLIF(TRIM(s.customer_name),''),'SEM CLIENTE')=@name ORDER BY p.paid_at DESC,p.id DESC","@name",name);
    var dt=(DataTable)openSales.DataSource;decimal total=0;foreach(DataRow r in dt.Rows)total+=Convert.ToDecimal(r["saldo"]);
    selected.Text="FIADO | "+name+" | "+dt.Rows.Count+" em aberto | "+paidSales.Rows.Count+" pagas | Saldo R$ "+total.ToString("N2");
   }catch(Exception ex){MessageBox.Show(ex.Message,"Erro ao abrir fiado");}
  }
  void AuditBalances(){
   try{
    var data=Query("SELECT s.id AS venda,COALESCE(NULLIF(TRIM(s.customer_name),''),'SEM CLIENTE') AS cliente,s.gross_total AS valor,COALESCE((SELECT SUM(p.amount) FROM credit_payments p WHERE p.sale_id=s.id),0) AS recebido,"+Balance+" AS saldo FROM sales s WHERE "+CreditFilter+" ORDER BY s.id DESC");
    int overpaid=0,negative=0,unassigned=0;decimal outstanding=0;decimal received=0;
    foreach(DataRow row in data.Rows){
     decimal gross=Convert.ToDecimal(row["valor"]),paid=Convert.ToDecimal(row["recebido"]),balance=Convert.ToDecimal(row["saldo"]);
     if(paid>gross+0.005M)overpaid++;
     if(balance< -0.005M)negative++;
     if(Convert.ToString(row["cliente"])=="SEM CLIENTE")unassigned++;
     if(balance>0)outstanding+=balance;
     received+=paid;
    }
    using(var form=new Form {Text="Conferência do Fiado - somente leitura",Width=1080,Height=650,StartPosition=FormStartPosition.CenterParent}){
     var summary=new Label {Dock=DockStyle.Top,Height=76,Font=new Font("Segoe UI",11,FontStyle.Bold),ForeColor=Color.FromArgb(20,45,83),BackColor=Color.FromArgb(232,242,251),Text="  Vendas: "+data.Rows.Count+"    |    Saldo em aberto: R$ "+outstanding.ToString("N2")+"    |    Recebido: R$ "+received.ToString("N2")+"\\n  Pagamentos acima do valor: "+overpaid+"    |    Saldos negativos: "+negative+"    |    Vendas sem cliente: "+unassigned+"\\n  Consulta sem alteração dos registros."};
     var table=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,DataSource=data,RowHeadersVisible=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect};
     table.RowTemplate.Height=32;table.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(240,246,253);
     form.Controls.Add(table);form.Controls.Add(summary);form.ShowDialog(this);
    }
   }catch(Exception ex){MessageBox.Show(ex.Message,"Não foi possível conferir o Fiado",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  }
  DataGridView SelectedSaleGrid(){
   if(saleTabs!=null && saleTabs.SelectedIndex==1)return paidSales;
   return openSales;
  }
  void CorrectCustomer(){
   var view=SelectedSaleGrid();
   if(view.CurrentRow==null){MessageBox.Show("Selecione uma venda na aba de vendas em aberto ou pagas.");return;}
   long id=Convert.ToInt64(view.CurrentRow.Cells["venda"].Value);
   using(var form=new Form {Text="Corrigir cliente - venda #"+id,Width=510,Height=205,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false}){
    var input=new TextBox {Left=18,Top=52,Width=455,Font=new Font("Segoe UI",12),Text=customer=="SEM CLIENTE"?"":customer};
    var label=new Label {Left=18,Top=16,Width=450,Height=30,Text="Nome correto do cliente:",Font=new Font("Segoe UI",11,FontStyle.Bold)};
    var save=new Button {Text="Salvar cliente",Left=290,Top=103,Width=183,Height=39,BackColor=Color.FromArgb(20,73,158),ForeColor=Color.White};
    form.Controls.Add(label);form.Controls.Add(input);form.Controls.Add(save);
    save.Click+=(s,e)=>{
     string newName=input.Text.Trim();
     if(newName.Length==0){MessageBox.Show("Informe o nome do cliente.");return;}
     try{
      using(var connection=Open(false)){
       string previous;
       using(var check=new SQLiteCommand("SELECT COALESCE(s.customer_name,'') FROM sales s WHERE s.id=@id AND "+CreditFilter,connection)){
        check.Parameters.AddWithValue("@id",id);object result=check.ExecuteScalar();
        if(result==null){MessageBox.Show("Venda fiado não encontrada.");return;}
        previous=Convert.ToString(result);
       }
       if(string.Equals(previous.Trim(),newName,StringComparison.Ordinal)){form.DialogResult=DialogResult.Cancel;form.Close();return;}
       if(MessageBox.Show("Venda #"+id+"\\nCliente atual: "+(previous.Trim().Length==0?"SEM CLIENTE":previous)+"\\nNovo cliente: "+newName+"\\n\\nConfirmar correção?","Confirmar vínculo",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
       string backup=db+".antes-corrigir-cliente-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+".db";
       using(var copy=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=backup}.ToString())){copy.Open();connection.BackupDatabase(copy,"main","main",-1,null,0);}
       using(var tx=connection.BeginTransaction())using(var update=new SQLiteCommand("UPDATE sales AS s SET customer_name=@name WHERE s.id=@id AND COALESCE(s.customer_name,'')=@previous AND "+CreditFilter,connection,tx)){
        update.Parameters.AddWithValue("@name",newName);update.Parameters.AddWithValue("@id",id);update.Parameters.AddWithValue("@previous",previous);
        if(update.ExecuteNonQuery()!=1)throw new Exception("A venda foi modificada. Atualize e tente novamente.");
        tx.Commit();
       }
      }
      customer=newName;form.DialogResult=DialogResult.OK;form.Close();
     }catch(Exception ex){MessageBox.Show(ex.Message,"Cliente não alterado",MessageBoxButtons.OK,MessageBoxIcon.Error);}
    };
    if(form.ShowDialog(this)==DialogResult.OK){LoadClients();SelectClient(customer);MessageBox.Show("Cliente da venda atualizado. Valores e pagamentos preservados.");}
   }
  }
  void ShowSaleDetails(){
   if(saleTabs!=null && saleTabs.SelectedIndex==2){MessageBox.Show("Selecione uma venda na aba de vendas em aberto ou pagas.");return;}
   DataGridView view=SelectedSaleGrid();
   if(view.CurrentRow==null){MessageBox.Show("Selecione uma venda para consultar.");return;}
   try{
    var id=Convert.ToInt64(view.CurrentRow.Cells["venda"].Value);
    var form=new Form {Text="Venda #"+id+" - Itens e recebimentos",Width=960,Height=620,StartPosition=FormStartPosition.CenterParent};
    var tabs=new TabControl {Dock=DockStyle.Fill};
    var items=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
    items.DataSource=Query("SELECT name AS produto,quantity AS quantidade,unit_price AS preco,total AS total FROM sale_items WHERE sale_id=@sale ORDER BY id","@sale",id);
    var payments=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
    payments.DataSource=Query("SELECT paid_at AS data,payment_method AS forma,amount AS valor FROM credit_payments WHERE sale_id=@sale ORDER BY paid_at,id","@sale",id);
    var a=new TabPage("Itens da venda");a.Controls.Add(items);var b=new TabPage("Pagamentos anteriores");b.Controls.Add(payments);
    tabs.TabPages.Add(a);tabs.TabPages.Add(b);form.Controls.Add(tabs);
    var edit=new Button {Text="Editar item selecionado (com auditoria)",Dock=DockStyle.Bottom,Height=42,BackColor=Color.FromArgb(20,73,158),ForeColor=Color.White};
    edit.Click+=(s,e)=>{form.Close();EditSaleItem(id);};form.Controls.Add(edit);
    using(form)form.ShowDialog(this);
   }catch(Exception ex){MessageBox.Show(ex.Message,"Erro na consulta");}
  }
  void EditSaleItem(long saleId){
   try{
    var rows=Query("SELECT id, name AS produto, quantity AS quantidade, unit_price AS preco, total AS valor FROM sale_items WHERE sale_id=@sale ORDER BY id","@sale",saleId);
    using(var form=new Form {Text="Auditoria de itens - venda #"+saleId,Width=880,Height=550,StartPosition=FormStartPosition.CenterParent}){
     var table=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,DataSource=rows};
     var footer=new FlowLayoutPanel {Dock=DockStyle.Bottom,Height=60,Padding=new Padding(8)};
     var save=new Button {Text="Alterar item selecionado",Width=210,Height=38,BackColor=Color.FromArgb(20,73,158),ForeColor=Color.White};
     footer.Controls.Add(save);form.Controls.Add(table);form.Controls.Add(footer);
     save.Click+=(s,e)=>{
      if(table.CurrentRow==null)return;
      var row=((DataRowView)table.CurrentRow.DataBoundItem).Row;
      long itemId=Convert.ToInt64(row["id"]);
      string oldName=Convert.ToString(row["produto"]);
      decimal oldQty=Convert.ToDecimal(row["quantidade"]),oldPrice=Convert.ToDecimal(row["preco"]),oldTotal=Convert.ToDecimal(row["valor"]);
      using(var dialog=new Form {Text="Editar item #"+itemId,Width=530,Height=340,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false}){
       var name=new TextBox {Left=20,Top=42,Width=465,Text=oldName};
       var qty=new NumericUpDown {Left=20,Top=112,Width=210,DecimalPlaces=3,Maximum=1000000,Minimum=0.001M,Value=Math.Max(0.001M,oldQty)};
       var price=new NumericUpDown {Left=250,Top=112,Width=235,DecimalPlaces=2,Maximum=1000000,Value=Math.Max(0,oldPrice)};
       var reason=new TextBox {Left=20,Top=184,Width=465};
       var confirm=new Button {Text="Conferir e salvar",Left=290,Top=232,Width=195,Height=38};
       dialog.Controls.Add(new Label {Text="Descrição do produto",Left=20,Top=20,AutoSize=true});
       dialog.Controls.Add(name);
       dialog.Controls.Add(new Label {Text="Quantidade",Left=20,Top=90,AutoSize=true});
       dialog.Controls.Add(qty);
       dialog.Controls.Add(new Label {Text="Preço unitário R$",Left=250,Top=90,AutoSize=true});
       dialog.Controls.Add(price);
       dialog.Controls.Add(new Label {Text="Motivo da alteração (obrigatório)",Left=20,Top=162,AutoSize=true});
       dialog.Controls.Add(reason);dialog.Controls.Add(confirm);
       confirm.Click+=(sender,args)=>{
        string newName=name.Text.Trim(),why=reason.Text.Trim();
        decimal newQty=qty.Value,newPrice=price.Value,newTotal=decimal.Round(newQty*newPrice,2,MidpointRounding.AwayFromZero);
        if(newName.Length==0 || why.Length<5){MessageBox.Show("Informe produto e motivo com pelo menos 5 caracteres.");return;}
        if(newName==oldName && newQty==oldQty && newPrice==oldPrice){MessageBox.Show("Nenhuma alteração.");return;}
        try{
         using(var connection=Open(false)){
          using(var check=new SQLiteCommand("SELECT COUNT(*) FROM sale_items WHERE id=@item AND sale_id=@sale AND name=@name AND ABS(quantity-@qty)<0.00001 AND ABS(unit_price-@price)<0.00001 AND ABS(total-@total)<0.01",connection)){
           check.Parameters.AddWithValue("@item",itemId);check.Parameters.AddWithValue("@sale",saleId);check.Parameters.AddWithValue("@name",oldName);check.Parameters.AddWithValue("@qty",oldQty);check.Parameters.AddWithValue("@price",oldPrice);check.Parameters.AddWithValue("@total",oldTotal);
           if(Convert.ToInt32(check.ExecuteScalar())!=1)throw new Exception("Item alterado por outro processo. Atualize.");
          }
          decimal received;
          using(var paid=new SQLiteCommand("SELECT COALESCE(SUM(amount),0) FROM credit_payments WHERE sale_id=@sale",connection)){paid.Parameters.AddWithValue("@sale",saleId);received=Convert.ToDecimal(paid.ExecuteScalar());}
          decimal previousGross;
          using(var totalCmd=new SQLiteCommand("SELECT gross_total FROM sales WHERE id=@sale AND (UPPER(COALESCE(payment_method,'')) LIKE '%FIADO%' OR UPPER(COALESCE(status,'')) LIKE '%FIADO%')",connection)){
           totalCmd.Parameters.AddWithValue("@sale",saleId);var result=totalCmd.ExecuteScalar();
           if(result==null)throw new Exception("Venda fiado não encontrada.");
           previousGross=Convert.ToDecimal(result);
          }
          decimal newGross=decimal.Round(previousGross-oldTotal+newTotal,2);
          if(newGross<received-0.005M)throw new Exception("Novo total menor que os pagamentos já recebidos. Faça a conferência antes de alterar.");
          if(MessageBox.Show("Venda #"+saleId+"\\nItem: "+oldName+"\\nAntes: R$ "+oldTotal.ToString("N2")+"\\nDepois: R$ "+newTotal.ToString("N2")+"\\nTotal da venda: R$ "+previousGross.ToString("N2")+" -> R$ "+newGross.ToString("N2")+"\\nMotivo: "+why+"\\n\\nConfirmar alteração auditada?","Confirmar edição",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
          string backup=db+".antes-editar-fiado-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+".db";
          using(var copy=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=backup}.ToString())){copy.Open();connection.BackupDatabase(copy,"main","main",-1,null,0);}
          using(var tx=connection.BeginTransaction()){
           using(var update=new SQLiteCommand("UPDATE sale_items SET name=@name,quantity=@qty,unit_price=@price,total=@total WHERE id=@id AND sale_id=@sale AND name=@oldName AND ABS(quantity-@oldQty)<0.00001 AND ABS(unit_price-@oldPrice)<0.00001 AND ABS(total-@oldTotal)<0.01",connection,tx)){
            update.Parameters.AddWithValue("@name",newName);update.Parameters.AddWithValue("@qty",newQty);update.Parameters.AddWithValue("@price",newPrice);update.Parameters.AddWithValue("@total",newTotal);update.Parameters.AddWithValue("@id",itemId);update.Parameters.AddWithValue("@sale",saleId);update.Parameters.AddWithValue("@oldName",oldName);update.Parameters.AddWithValue("@oldQty",oldQty);update.Parameters.AddWithValue("@oldPrice",oldPrice);update.Parameters.AddWithValue("@oldTotal",oldTotal);
            if(update.ExecuteNonQuery()!=1)throw new Exception("O item foi alterado. Operação cancelada.");
           }
           using(var update=new SQLiteCommand("UPDATE sales SET gross_total=@new WHERE id=@sale AND ABS(gross_total-@old)<0.01",connection,tx)){
            update.Parameters.AddWithValue("@new",newGross);update.Parameters.AddWithValue("@old",previousGross);update.Parameters.AddWithValue("@sale",saleId);
            if(update.ExecuteNonQuery()!=1)throw new Exception("Total da venda alterado. Operação cancelada.");
           }
           using(var create=new SQLiteCommand("CREATE TABLE IF NOT EXISTS fiado_item_audit (id INTEGER PRIMARY KEY AUTOINCREMENT,sale_id INTEGER NOT NULL,item_id INTEGER NOT NULL,old_name TEXT,new_name TEXT,old_qty REAL,new_qty REAL,old_price REAL,new_price REAL,old_total REAL,new_total REAL,old_sale_total REAL,new_sale_total REAL,reason TEXT NOT NULL,changed_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP)",connection,tx))create.ExecuteNonQuery();
           using(var log=new SQLiteCommand("INSERT INTO fiado_item_audit(sale_id,item_id,old_name,new_name,old_qty,new_qty,old_price,new_price,old_total,new_total,old_sale_total,new_sale_total,reason) VALUES(@sale,@item,@oldName,@name,@oldQty,@qty,@oldPrice,@price,@oldTotal,@total,@oldGross,@gross,@reason)",connection,tx)){
            log.Parameters.AddWithValue("@sale",saleId);log.Parameters.AddWithValue("@item",itemId);log.Parameters.AddWithValue("@oldName",oldName);log.Parameters.AddWithValue("@name",newName);log.Parameters.AddWithValue("@oldQty",oldQty);log.Parameters.AddWithValue("@qty",newQty);log.Parameters.AddWithValue("@oldPrice",oldPrice);log.Parameters.AddWithValue("@price",newPrice);log.Parameters.AddWithValue("@oldTotal",oldTotal);log.Parameters.AddWithValue("@total",newTotal);log.Parameters.AddWithValue("@oldGross",previousGross);log.Parameters.AddWithValue("@gross",newGross);log.Parameters.AddWithValue("@reason",why);log.ExecuteNonQuery();
           }
           tx.Commit();
          }
         }
         dialog.DialogResult=DialogResult.OK;dialog.Close();
        }catch(Exception ex){MessageBox.Show(ex.Message,"Alteração não salva",MessageBoxButtons.OK,MessageBoxIcon.Error);}
       };
       if(dialog.ShowDialog(form)==DialogResult.OK){
        table.DataSource=Query("SELECT id, name AS produto,quantity AS quantidade,unit_price AS preco,total AS valor FROM sale_items WHERE sale_id=@sale ORDER BY id","@sale",saleId);
        LoadClients();MessageBox.Show("Item alterado com registro de auditoria e backup. Estoque não modificado.");
       }
      }
     };
     form.ShowDialog(this);
    }
   }catch(Exception ex){MessageBox.Show(ex.Message,"Erro ao editar venda");}
  }
  void ShowItemAuditHistory(){
   if(string.IsNullOrWhiteSpace(customer)){MessageBox.Show("Selecione um cliente.");return;}
   if(saleTabs.SelectedIndex==2){MessageBox.Show("Selecione uma venda em aberto ou paga.");return;}
   var view=SelectedSaleGrid();
   if(view.CurrentRow==null){MessageBox.Show("Selecione uma venda.");return;}
   long saleId=Convert.ToInt64(view.CurrentRow.Cells["venda"].Value);
   try{
    using(var connection=Open(true)){
     using(var exists=new SQLiteCommand("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='fiado_item_audit'",connection)){
      if(Convert.ToInt32(exists.ExecuteScalar())==0){MessageBox.Show("Esta venda ainda não possui alterações de itens registradas.");return;}
     }
    }
    var data=Query("SELECT changed_at AS data,item_id AS item,old_name AS produto_anterior,new_name AS produto_novo,old_qty AS quantidade_anterior,new_qty AS quantidade_nova,old_price AS preco_anterior,new_price AS preco_novo,old_total AS item_anterior,new_total AS item_novo,old_sale_total AS venda_anterior,new_sale_total AS venda_nova,reason AS motivo FROM fiado_item_audit WHERE sale_id=@sale ORDER BY id DESC","@sale",saleId);
    if(data.Rows.Count==0){MessageBox.Show("Nenhuma alteração de itens registrada para a venda #"+saleId+".");return;}
    using(var form=new Form {Text="Histórico de alterações - venda #"+saleId,Width=1100,Height=580,StartPosition=FormStartPosition.CenterParent}){
     var header=new Label {Text="  AUDITORIA DA VENDA #"+saleId+" | "+data.Rows.Count+" alteração(ões) | Somente leitura",Dock=DockStyle.Top,Height=46,Font=new Font("Segoe UI",11,FontStyle.Bold),BackColor=Color.FromArgb(232,242,252),ForeColor=Color.FromArgb(20,73,158),TextAlign=ContentAlignment.MiddleLeft};
     var table=new DataGridView {Dock=DockStyle.Fill,DataSource=data,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells};
     form.Controls.Add(table);form.Controls.Add(header);form.ShowDialog(this);
    }
   }catch(Exception ex){MessageBox.Show(ex.Message,"Não foi possível consultar auditoria",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  }
  void SendReceipt(){
   if(string.IsNullOrWhiteSpace(customer)){MessageBox.Show("Selecione um cliente.");return;}
   var view=SelectedSaleGrid();
   if(saleTabs.SelectedIndex==2 || view.CurrentRow==null){MessageBox.Show("Selecione uma venda na aba em aberto ou pagas.");return;}
   try{
    long id=Convert.ToInt64(view.CurrentRow.Cells["venda"].Value);
    var rows=Query("SELECT name AS produto,quantity AS quantidade,unit_price AS preco,total AS valor FROM sale_items WHERE sale_id=@sale ORDER BY id","@sale",id);
    var sb=new System.Text.StringBuilder();
    sb.AppendLine("ARMAZEM GRATIDAO - COMPROVANTE DE FIADO");
    sb.AppendLine("Cliente: "+customer);
    sb.AppendLine("Venda: #"+id+" | Data: "+Convert.ToString(view.CurrentRow.Cells["data"].Value));
    sb.AppendLine("--------------------------------");
    foreach(DataRow item in rows.Rows)sb.AppendLine(Convert.ToString(item["produto"])+" | "+Convert.ToDecimal(item["quantidade"]).ToString("N3")+" x R$ "+Convert.ToDecimal(item["preco"]).ToString("N2")+" = R$ "+Convert.ToDecimal(item["valor"]).ToString("N2"));
    sb.AppendLine("--------------------------------");
    sb.AppendLine("Total: R$ "+Convert.ToDecimal(view.CurrentRow.Cells["valor"].Value).ToString("N2"));
    sb.AppendLine("Recebido: R$ "+Convert.ToDecimal(view.CurrentRow.Cells["pago"].Value).ToString("N2"));
    sb.AppendLine("Saldo: R$ "+Convert.ToDecimal(view.CurrentRow.Cells["saldo"].Value).ToString("N2"));
    sb.AppendLine("Documento informativo, nao fiscal.");
    using(var form=new Form {Text="Cupom da venda #"+id,Width=640,Height=610,StartPosition=FormStartPosition.CenterParent}){
     var preview=new TextBox {Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill,Font=new Font("Consolas",11),Text=sb.ToString()};
     var actions=new FlowLayoutPanel {Dock=DockStyle.Bottom,Height=55,Padding=new Padding(8)};
     var copy=new Button {Text="Copiar cupom",Width=145,Height=34};copy.Click+=(s,e)=>{Clipboard.SetText(sb.ToString());MessageBox.Show("Cupom copiado.");};
     var send=new Button {Text="Enviar pelo WhatsApp",Width=200,Height=34,BackColor=Color.FromArgb(15,119,78),ForeColor=Color.White};
     send.Click+=(s,e)=>{System.Diagnostics.Process.Start("https://api.whatsapp.com/send?text="+Uri.EscapeDataString(sb.ToString()));};
     actions.Controls.Add(copy);actions.Controls.Add(send);form.Controls.Add(preview);form.Controls.Add(actions);form.ShowDialog(this);
    }
   }catch(Exception ex){MessageBox.Show(ex.Message,"Nao foi possivel gerar o cupom");}
  }
  void PayAll(){
   if(string.IsNullOrWhiteSpace(customer)){MessageBox.Show("Selecione um cliente.");return;}
   if(customer=="SEM CLIENTE"){MessageBox.Show("Identifique o cliente antes de receber todas as vendas.");return;}
   try{
    DataTable sales=Query("SELECT s.id AS venda,"+Balance+" AS saldo FROM sales s WHERE "+CreditFilter+" AND COALESCE(NULLIF(TRIM(s.customer_name),''),'SEM CLIENTE')=@name AND "+Balance+">0.005 ORDER BY s.id","@name",customer);
    if(sales.Rows.Count==0){MessageBox.Show("Este cliente não possui vendas em aberto.");return;}
    decimal total=0;foreach(DataRow row in sales.Rows)total+=Convert.ToDecimal(row["saldo"]);
    string paymentMethod=Convert.ToString(method.SelectedItem);
    if(MessageBox.Show("Cliente: "+customer+"\\nVendas em aberto: "+sales.Rows.Count+"\\nTotal a receber: R$ "+total.ToString("N2")+"\\nForma: "+paymentMethod+"\\n\\nConfirmar recebimento integral?","Conferir baixa de fiado",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
    using(var c=Open(false)){
     // Backup antes da transação. Nenhuma venda ou estoque será reescrito.
     string backup=db+".antes-fiado-total-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+".db";
     using(var copy=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=backup}.ToString())){copy.Open();c.BackupDatabase(copy,"main","main",-1,null,0);}
     using(var tx=c.BeginTransaction()){
      using(var q=new SQLiteCommand("SELECT s.id,"+Balance+" AS saldo FROM sales s WHERE "+CreditFilter+" AND COALESCE(NULLIF(TRIM(s.customer_name),''),'SEM CLIENTE')=@name AND "+Balance+">0.005 ORDER BY s.id",c,tx)){
       q.Parameters.AddWithValue("@name",customer);
       var pending=new DataTable();using(var adapter=new SQLiteDataAdapter(q))adapter.Fill(pending);
       if(pending.Rows.Count!=sales.Rows.Count)throw new Exception("As vendas foram alteradas. Atualize a tela.");
       decimal actualTotal=0;foreach(DataRow row in pending.Rows)actualTotal+=Convert.ToDecimal(row["saldo"]);
       if(Math.Abs(actualTotal-total)>0.005M)throw new Exception("O saldo mudou. Atualize a tela.");
       using(var insert=new SQLiteCommand("INSERT INTO credit_payments(sale_id,amount,payment_method,paid_at,note) VALUES(@sale,@amount,@method,datetime('now','localtime'),'Quitação integral por cliente no novo Fiado')",c,tx)){
        foreach(DataRow row in pending.Rows){
         insert.Parameters.Clear();insert.Parameters.AddWithValue("@sale",Convert.ToInt64(row["id"]));
         insert.Parameters.AddWithValue("@amount",Convert.ToDecimal(row["saldo"]));
         insert.Parameters.AddWithValue("@method",paymentMethod);
         insert.ExecuteNonQuery();
        }
       }
      }
      tx.Commit();
     }
    }
    amount.Value=0;LoadClients();SelectClient(customer);
    MessageBox.Show("Recebimento confirmado: R$ "+total.ToString("N2")+"\\nVendas quitadas: "+sales.Rows.Count);
   }catch(Exception ex){MessageBox.Show(ex.Message,"Recebimento não realizado",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  }
  void Pay(){
   if(saleTabs!=null && saleTabs.SelectedIndex!=0){MessageBox.Show("Selecione uma venda na aba Fiado atual - em aberto.");return;}
   if(openSales.CurrentRow==null){MessageBox.Show("Selecione uma venda em aberto.");return;}
   if(amount.Value<=0){MessageBox.Show("Informe o valor recebido.");return;}
   long saleId=Convert.ToInt64(openSales.CurrentRow.Cells["venda"].Value);
   decimal balance=Convert.ToDecimal(openSales.CurrentRow.Cells["saldo"].Value);
   if(amount.Value>balance+0.005M){MessageBox.Show("O pagamento nao pode ultrapassar o saldo da venda.");return;}
   if(MessageBox.Show("Registrar recebimento de R$ "+amount.Value.ToString("N2")+" na venda #"+saleId+"?","Confirmar baixa",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
   try {
    var backup=db+".antes-fiado-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+".db";
    using(var source=Open(true))using(var copy=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=backup,FailIfMissing=false}.ToString())){copy.Open();source.BackupDatabase(copy,"main","main",-1,null,0);}
    using(var c=Open())using(var tx=c.BeginTransaction()) {
     decimal actual;
     using(var q=new SQLiteCommand("SELECT "+Balance+" FROM sales s WHERE s.id=@id AND "+CreditFilter+" AND COALESCE(NULLIF(TRIM(s.customer_name),''),'SEM CLIENTE')=@name",c,tx)) {
      q.Parameters.AddWithValue("@id",saleId);q.Parameters.AddWithValue("@name",customer);
      var obj=q.ExecuteScalar();if(obj==null||obj==DBNull.Value)throw new Exception("Venda ou cliente nao encontrado.");
      actual=Convert.ToDecimal(obj);
     }
     if(amount.Value>actual+0.005M||actual<=0)throw new Exception("Saldo alterado. Atualize a tela antes de pagar.");
     using(var q=new SQLiteCommand("INSERT INTO credit_payments(sale_id,amount,payment_method,paid_at,note) VALUES(@sale,@amount,@method,datetime('now','localtime'),'Recebimento registrado no novo Fiado')",c,tx)) {
      q.Parameters.AddWithValue("@sale",saleId);q.Parameters.AddWithValue("@amount",amount.Value);q.Parameters.AddWithValue("@method",Convert.ToString(method.SelectedItem));q.ExecuteNonQuery();
     }
     // No banco original 12.1.6, a quitacao e calculada pelos registros
     // de credit_payments. Nao alterar colunas inexistentes em sales.
     tx.Commit();
    }
    amount.Value=0;SelectClient(customer);MessageBox.Show("Pagamento registrado. Saldo atualizado.");LoadClients();
   }catch(Exception ex){MessageBox.Show(ex.Message,"Baixa nao realizada",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  }
 }
}