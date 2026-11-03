using System;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
namespace ArmazemGratidao {
 public sealed class FiadoClientesForm:Form {
  readonly string db;
  readonly FlowLayoutPanel clients=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.LeftToRight,WrapContents=true,Padding=new Padding(8)};
  readonly DataGridView openSales=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
  readonly DataGridView paymentHistory=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
  readonly DataGridView paidSales=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
  readonly Label selected=new Label {Dock=DockStyle.Top,Height=42,Font=new Font("Segoe UI",14,FontStyle.Bold)};
  readonly NumericUpDown amount=new NumericUpDown {DecimalPlaces=2,Maximum=100000000,Width=130};
  readonly NumericUpDown creditDepositAmount=new NumericUpDown {DecimalPlaces=2,Maximum=100000000,Width=135};
  readonly ComboBox method=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=160};
  string customer="";
  bool refreshingClientSales=false;
  readonly Label receivedBalance=new Label {Text="Saldo selecionado: R$ 0,00",AutoSize=true,Padding=new Padding(5,9,0,0),Font=new Font("Segoe UI",10,FontStyle.Bold)};
  TabControl saleTabs;
  Panel clientHome;
  SplitContainer detailSplit;
  readonly TextBox clientSearch=new TextBox {Dock=DockStyle.Top,Height=34,Font=new Font("Segoe UI",11)};
  readonly Label overview=new Label {Dock=DockStyle.Top,Height=88,Font=new Font("Segoe UI",10,FontStyle.Bold),ForeColor=Color.FromArgb(18,51,99),BackColor=Color.FromArgb(229,242,255),TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(8,4,4,4)};
  public FiadoClientesForm(string database) {
   db=database;Text="Armazem Gratidao | Fiado por Cliente";BackColor=Color.FromArgb(229,242,255);StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.Sizable;MaximizeBox=true;MinimizeBox=false;ShowInTaskbar=false;SizeGripStyle=SizeGripStyle.Hide;AutoScaleMode=AutoScaleMode.Font;var area=Screen.PrimaryScreen.WorkingArea;Width=Math.Min(1250,Math.Max(680,area.Width-32));Height=Math.Min(790,Math.Max(520,area.Height-32));MinimumSize=new Size(640,480);
   foreach(var table in new[]{openSales,paidSales,paymentHistory}){table.ScrollBars=ScrollBars.Both;table.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells;}
   var split=new SplitContainer {Dock=DockStyle.Fill,SplitterDistance=265,IsSplitterFixed=true};
   detailSplit=split;
   split.Panel1.Controls.Add(clients);
   split.Panel1.Controls.Add(overview);
   clients.ClientSizeChanged+=(s,e)=>ArrangeClientBoxes();
   clientSearch.TextChanged+=(s,e)=>LoadClients();
   split.Panel1.Controls.Add(clientSearch);
   split.Panel1.Controls.Add(new Label {Text="  PESQUISAR CLIENTE",Dock=DockStyle.Top,Height=28,Font=new Font("Segoe UI",10,FontStyle.Bold),ForeColor=Color.White,BackColor=Color.FromArgb(220,95,105)});
   var tabs=new TabControl {Dock=DockStyle.Fill};
   saleTabs=tabs;
   var pending=new TabPage("Fiado atual - em aberto");
   var previous=new TabPage("Fiados anteriores - pagos");
   var pendingItems=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
   var previousItems=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
   var pendingSplit=new SplitContainer {Dock=DockStyle.Fill,Orientation=Orientation.Horizontal,SplitterDistance=235,IsSplitterFixed=false};
   var previousSplit=new SplitContainer {Dock=DockStyle.Fill,Orientation=Orientation.Horizontal,SplitterDistance=235,IsSplitterFixed=false};
   var pendingPayments=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
   var previousPayments=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
   var pendingDetailTabs=new TabControl {Dock=DockStyle.Fill};
   var previousDetailTabs=new TabControl {Dock=DockStyle.Fill};
   var pendingItemTab=new TabPage("Produtos da venda selecionada");pendingItemTab.Controls.Add(pendingItems);
   var pendingPaymentTab=new TabPage("Pagamentos desta venda");pendingPaymentTab.Controls.Add(pendingPayments);
   pendingDetailTabs.TabPages.Add(pendingItemTab);pendingDetailTabs.TabPages.Add(pendingPaymentTab);
   var previousItemTab=new TabPage("Produtos da venda selecionada");previousItemTab.Controls.Add(previousItems);
   var previousPaymentTab=new TabPage("Pagamentos desta venda");previousPaymentTab.Controls.Add(previousPayments);
   previousDetailTabs.TabPages.Add(previousItemTab);previousDetailTabs.TabPages.Add(previousPaymentTab);
   SetupSaleSelection(openSales,pendingSplit.Panel1,false);pendingSplit.Panel2.Controls.Add(pendingDetailTabs);
   SetupSaleSelection(paidSales,previousSplit.Panel1,true);previousSplit.Panel2.Controls.Add(previousDetailTabs);
   pending.Controls.Add(pendingSplit);previous.Controls.Add(previousSplit);
   openSales.SelectionChanged+=(s,e)=>{RefreshSelectedSaleDetails(openSales,pendingItems,pendingPayments);UpdatePaymentSelection(true);};
   paidSales.SelectionChanged+=(s,e)=>RefreshSelectedSaleDetails(paidSales,previousItems,previousPayments);
   tabs.TabPages.Add(pending);tabs.TabPages.Add(previous);
   var historyTab=new TabPage("Histórico de recebimentos");historyTab.Controls.Add(paymentHistory);tabs.TabPages.Add(historyTab);
   var switchBar=new FlowLayoutPanel {Dock=DockStyle.Top,Height=44,Padding=new Padding(5),BackColor=Color.FromArgb(229,242,255)};
   var showOpen=new Button {Text="Fiado atual - em aberto",Width=190,Height=32,BackColor=Color.FromArgb(220,95,105),ForeColor=Color.White};showOpen.Click+=(s,e)=>tabs.SelectedIndex=0;switchBar.Controls.Add(showOpen);
   var showPrevious=new Button {Text="Fiados anteriores - pagos",Width=210,Height=32,BackColor=Color.White,ForeColor=Color.FromArgb(20,73,158)};showPrevious.Click+=(s,e)=>tabs.SelectedIndex=1;switchBar.Controls.Add(showPrevious);
   var backSmall=new Button{Text="← Voltar",Width=95,Height=32,FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(220,95,105),ForeColor=Color.White,Font=new Font("Segoe UI",9,FontStyle.Bold)};
   backSmall.FlatAppearance.BorderSize=0;
   backSmall.Click+=(sender,args)=>{customer="";detailSplit.Panel2Collapsed=true;detailSplit.Panel1Collapsed=false;clientHome.Visible=true;clientHome.BringToFront();WindowState=FormWindowState.Normal;};
   switchBar.Controls.Add(backSmall);
   var fixClientName=new Button{Text="Corrigir nome do cliente",Width=220,Height=32,BackColor=Color.FromArgb(22,75,160),ForeColor=Color.White};
   fixClientName.Click+=(sender,args)=>CorrectWholeClientName();
   switchBar.Controls.Add(fixClientName);
   var transferClient=new Button{Text="Trocar por cliente cadastrado",Width=255,Height=32,BackColor=Color.FromArgb(12,113,88),ForeColor=Color.White};
   transferClient.Click+=(sender,args)=>TransferFiadoToRegisteredClient();
   switchBar.Controls.Add(transferClient);
   tabs.SelectedIndexChanged+=(s,e)=>{showOpen.BackColor=tabs.SelectedIndex==0?Color.FromArgb(20,73,158):Color.White;showOpen.ForeColor=tabs.SelectedIndex==0?Color.White:Color.FromArgb(20,73,158);showPrevious.BackColor=tabs.SelectedIndex==1?Color.FromArgb(20,73,158):Color.White;showPrevious.ForeColor=tabs.SelectedIndex==1?Color.White:Color.FromArgb(20,73,158);};
   var bottom=new FlowLayoutPanel {Dock=DockStyle.Bottom,Height=122,Padding=new Padding(8),WrapContents=true,AutoScroll=true,BackColor=Color.FromArgb(235,241,250)};
   bottom.Controls.Add(new Label {Text="Forma de pagamento:",AutoSize=true,Padding=new Padding(0,9,0,0)});
   method.Items.AddRange(new object[]{"DINHEIRO","PIX","PIX QR CODE","DEBITO","CREDITO","ALIMENTACAO","ALELO","PLUXXE","TICKET","VR","CREDITO DEPOSITADO"});method.SelectedIndex=0;bottom.Controls.Add(method);
   bottom.Controls.Add(new Label {Text="Valor R$:",AutoSize=true,Padding=new Padding(0,9,0,0)});bottom.Controls.Add(amount);
   bottom.Controls.Add(receivedBalance);
   var fillAll=new Button {Text="Preencher saldo de todas",AutoSize=true,Height=36};fillAll.Click+=(s,e)=>FillAllBalances();bottom.Controls.Add(fillAll);
   var pay=new Button {Text="Registrar pagamento",AutoSize=true,Height=36,BackColor=Color.FromArgb(24,82,170),ForeColor=Color.White};pay.Click+=(s,e)=>Pay();bottom.Controls.Add(pay);
   var saveCredit=new Button {Text="Guardar credito do cliente",AutoSize=true,Height=36,BackColor=Color.FromArgb(14,112,122),ForeColor=Color.White};saveCredit.Click+=(s,e)=>DepositCustomerCredit();bottom.Controls.Add(saveCredit);
   var creditLimit=new Button {Text="Definir limite semanal / mensal",AutoSize=true,Height=36,BackColor=Color.FromArgb(24,82,170),ForeColor=Color.White};creditLimit.Click+=(s,e)=>ConfigureClientCreditLimit();bottom.Controls.Add(creditLimit);
   var creditHistory=new Button {Text="Historico de credito",AutoSize=true,Height=36};creditHistory.Click+=(s,e)=>ShowCustomerCreditHistory();bottom.Controls.Add(creditHistory);
   var settle=new Button {Text="Receber nas vendas do cliente",AutoSize=true,Height=36,BackColor=Color.FromArgb(12,110,80),ForeColor=Color.White};settle.Click+=(s,e)=>PayAll();bottom.Controls.Add(settle);
   var rename=new Button {Text="Corrigir cliente da venda",AutoSize=true,Height=36,BackColor=Color.FromArgb(98,60,154),ForeColor=Color.White};rename.Click+=(s,e)=>CorrectCustomer();bottom.Controls.Add(rename);
   var fixDate=new Button {Text="Corrigir data do pagamento",AutoSize=true,Height=36,BackColor=Color.FromArgb(95,64,145),ForeColor=Color.White};fixDate.Click+=(s,e)=>CorrectPaymentDate();bottom.Controls.Add(fixDate);
   var audit=new Button {Text="Conferir saldos",AutoSize=true,Height=36,BackColor=Color.FromArgb(145,73,32),ForeColor=Color.White};audit.Click+=(s,e)=>AuditBalances();bottom.Controls.Add(audit);
   var inspect=new Button {Text="Itens e pagamentos da venda",AutoSize=true,Height=36};inspect.Click+=(s,e)=>ShowSaleDetails();bottom.Controls.Add(inspect);
   var auditHistory=new Button {Text="Histórico de alterações",AutoSize=true,Height=36,BackColor=Color.FromArgb(80,60,125),ForeColor=Color.White};auditHistory.Click+=(s,e)=>ShowItemAuditHistory();bottom.Controls.Add(auditHistory);
   var statement=new Button {Text="Extrato do cliente / WhatsApp",AutoSize=true,Height=36,BackColor=Color.FromArgb(15,119,78),ForeColor=Color.White};statement.Click+=(s,e)=>SendCustomerStatement();bottom.Controls.Add(statement);
   var receipt=new Button {Text="Cupom / WhatsApp",AutoSize=true,Height=36,BackColor=Color.FromArgb(15,119,78),ForeColor=Color.White};receipt.Click+=(s,e)=>SendReceipt();bottom.Controls.Add(receipt);
   var refresh=new Button {Text="Atualizar",AutoSize=true,Height=36};refresh.Click+=(s,e)=>LoadClients();bottom.Controls.Add(refresh);
   // A caixa de credito fica sempre visivel ao selecionar o cliente.
   var creditBar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=87,WrapContents=true,AutoScroll=true,Padding=new Padding(7),BackColor=Color.FromArgb(214,233,250)};
   creditBar.Controls.Add(new Label{Text="CREDITO DEPOSITADO R$:",AutoSize=true,Padding=new Padding(0,8,0,0),Font=new Font("Segoe UI",9,FontStyle.Bold)});
   creditBar.Controls.Add(creditDepositAmount);
   var creditSaveTop=new Button{Text="Guardar credito",Width=140,Height=35,BackColor=Color.FromArgb(14,112,122),ForeColor=Color.White};
   creditSaveTop.Click+=(sender,args)=>DepositCustomerCredit();creditBar.Controls.Add(creditSaveTop);
   var limitTop=new Button{Text="Definir / alterar limite",Width=180,Height=35,BackColor=Color.FromArgb(24,82,170),ForeColor=Color.White};
   limitTop.Click+=(sender,args)=>ConfigureClientCreditLimit();creditBar.Controls.Add(limitTop);
   var adjustTop=new Button{Text="Ajustar credito",Width=140,Height=35,BackColor=Color.FromArgb(133,74,20),ForeColor=Color.White};
   adjustTop.Click+=(sender,args)=>AdjustCustomerCredit();creditBar.Controls.Add(adjustTop);
   var creditPayTop=new Button{Text="Pagar Fiado com credito",Width=195,Height=35,BackColor=Color.FromArgb(12,110,80),ForeColor=Color.White};
   creditPayTop.Click+=(sender,args)=>PayFiadoWithCredit();creditBar.Controls.Add(creditPayTop);
   var creditExtractTop=new Button{Text="Extrato do credito",Width=150,Height=35};
   creditExtractTop.Click+=(sender,args)=>ShowCustomerCreditHistory();creditBar.Controls.Add(creditExtractTop);
   split.Panel2.Controls.Add(tabs);split.Panel2.Controls.Add(bottom);split.Panel2.Controls.Add(creditBar);split.Panel2.Controls.Add(switchBar);split.Panel2.Controls.Add(selected);
   foreach(var table in new[]{openSales,paidSales,paymentHistory}){table.RowTemplate.Height=34;table.RowHeadersVisible=false;table.EnableHeadersVisualStyles=false;table.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(220,95,105);table.ColumnHeadersDefaultCellStyle.ForeColor=Color.White;table.DefaultCellStyle.SelectionBackColor=Color.FromArgb(26,88,166);table.DefaultCellStyle.SelectionForeColor=Color.White;table.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(239,245,252);table.Font=new Font("Segoe UI",10);table.SelectionMode=DataGridViewSelectionMode.FullRowSelect;}
   openSales.CellDoubleClick+=(s,e)=>{if(e.RowIndex>=0){saleTabs.SelectedIndex=0;ShowSaleDetails();}};
   paidSales.CellDoubleClick+=(s,e)=>{if(e.RowIndex>=0){saleTabs.SelectedIndex=1;ShowSaleDetails();}};
   Controls.Add(split);
   clientHome=new Panel {Dock=DockStyle.Fill,BackColor=Color.White};
   var homeHeader=new Label {Dock=DockStyle.Top,Height=70,Text="FIADO  |  SELECIONE UM CLIENTE",Font=new Font("Segoe UI",17,FontStyle.Bold),ForeColor=Color.FromArgb(20,73,158),TextAlign=ContentAlignment.MiddleCenter};
   clientHome.Controls.Add(new Label {Dock=DockStyle.Fill,Text="Clique no nome do cliente à esquerda para consultar vendas, itens, pagamentos e emitir cupom.",Font=new Font("Segoe UI",12),TextAlign=ContentAlignment.MiddleCenter});
   clientHome.Controls.Add(homeHeader);
   split.Panel2.Controls.Add(clientHome);clientHome.BringToFront();

   split.Panel2Collapsed=true;
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
  static bool InternalFiado(string name){
   string normalized=string.Join(" ",(name??"").Trim().ToUpperInvariant().Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries));
   return normalized=="EMERSON SIMONETTI GILIOTI" || normalized=="EMERSON SIMONETTI GIGLIOTTI"
    || normalized=="EMERSON SIMONE GILIOTI" || normalized=="EMERSON SIMONE GIGLIOTTI"
    || normalized=="LUIZ PAULO GILIOTI" || normalized=="LUIZ PAULO GIGLIOTTI";
  }
  void SetupSaleSelection(DataGridView grid, SplitterPanel panel, bool paid) {
   var mark=new DataGridViewCheckBoxColumn();
   mark.Name="marcar";mark.HeaderText="Selecionar";mark.ReadOnly=false;mark.Width=75;mark.AutoSizeMode=DataGridViewAutoSizeColumnMode.None;
   grid.Columns.Add(mark);
   // Apenas a coluna de seleção é editável; as informações das vendas permanecem protegidas.
   grid.ReadOnly=false;
   grid.MultiSelect=true;
   grid.CellClick+=(s,e)=>{
    if(e.RowIndex<0||e.ColumnIndex<0||grid.Columns[e.ColumnIndex].Name=="marcar")return;
    var cell=grid.Rows[e.RowIndex].Cells["marcar"];
    cell.Value=!Convert.ToBoolean(cell.Value??false);
   };
   grid.DataBindingComplete+=(s,e)=>{
    foreach(DataGridViewColumn col in grid.Columns)col.ReadOnly=col.Name!="marcar";
   };
   var bar=new FlowLayoutPanel {Dock=DockStyle.Top,Height=44,WrapContents=false,AutoScroll=true,BackColor=Color.FromArgb(246,249,253)};
   var all=new CheckBox {Text="Selecionar todas as vendas",AutoSize=true,Font=new Font("Segoe UI",10,FontStyle.Bold),ForeColor=Color.FromArgb(20,73,158),Margin=new Padding(12,10,16,0)};
   var total=new Label {Text="Total selecionado: R$ 0,00",AutoSize=true,Margin=new Padding(5,10,0,0)};
   bar.Controls.Add(all);bar.Controls.Add(total);
   Action sum=()=>{
    decimal value=0;
    foreach(DataGridViewRow row in grid.Rows) {
     if(row.IsNewRow||!Convert.ToBoolean(row.Cells["marcar"].Value??false))continue;
     var v=row.Cells[paid?"valor":"saldo"].Value;
     if(v!=null&&v!=DBNull.Value)value+=Convert.ToDecimal(v);
    }
    total.Text="Total selecionado: R$ "+value.ToString("N2");
    if(!paid&&grid==openSales)UpdatePaymentSelection(true);
   };
   all.CheckedChanged+=(s,e)=>{foreach(DataGridViewRow row in grid.Rows)if(!row.IsNewRow)row.Cells["marcar"].Value=all.Checked;sum();};
   grid.CurrentCellDirtyStateChanged+=(s,e)=>{if(grid.IsCurrentCellDirty)grid.CommitEdit(DataGridViewDataErrorContexts.Commit);};
   grid.CellValueChanged+=(s,e)=>{if(e.RowIndex>=0&&e.ColumnIndex==grid.Columns["marcar"].Index)sum();};
   grid.DataBindingComplete+=(s,e)=>{all.Checked=false;sum();};
   panel.Controls.Add(grid);panel.Controls.Add(bar);
  }
  void ArrangeClientBoxes(){
   int available=Math.Max(160,clients.ClientSize.Width-clients.Padding.Horizontal-SystemInformation.VerticalScrollBarWidth-8);
   const int gap=12;
   const int targetWidth=190;
   int columns=Math.Max(1,(available+gap)/(targetWidth+gap));
   int width=Math.Max(145,(available-(columns-1)*gap)/columns);
   clients.SuspendLayout();
   foreach(Control control in clients.Controls){
    var button=control as Button;
    if(button==null)continue;
    button.AutoSize=false;
    button.MinimumSize=new Size(width,72);
    button.MaximumSize=new Size(width,72);
    button.Size=new Size(width,72);
    button.Margin=new Padding(0,0,gap,gap);
   }
   clients.ResumeLayout();
  }
  void LoadClients(){
   try {
    // Na tela do FIADO, so aparecem pessoas com VENDA FIADA registrada.
    // Cadastro isolado, limite de credito ou deposito sem venda nao geram box.
    // Agrupamento sem distinguir maiusculas e espacos evita dois boxes da mesma grafia.
    string names="SELECT MIN(TRIM(s.customer_name)) AS cliente FROM sales s WHERE "+CreditFilter+
     " AND TRIM(COALESCE(s.customer_name,''))<>'' AND UPPER(TRIM(s.customer_name)) NOT IN ('CONSUMIDOR','SEM CLIENTE')"+
     " GROUP BY TRIM(s.customer_name) COLLATE NOCASE";
    var dt=Query("SELECT names.cliente AS cliente,"+
     "(SELECT COUNT(*) FROM sales s WHERE "+CreditFilter+" AND TRIM(s.customer_name)=names.cliente COLLATE NOCASE) AS vendas,"+
     "COALESCE((SELECT SUM(CASE WHEN "+Balance+">0.005 THEN "+Balance+" ELSE 0 END) FROM sales s WHERE "+
     CreditFilter+" AND TRIM(s.customer_name)=names.cliente COLLATE NOCASE),0) AS saldo"+
     " FROM ("+names+") names ORDER BY names.cliente COLLATE NOCASE");
    clients.SuspendLayout();clients.Controls.Clear();
    decimal allBalance=0,internalBalance=0;
    bool selectedClientStillHasFiado=false;
    foreach(DataRow r in dt.Rows) {
     var name=Convert.ToString(r["cliente"]);var balance=Convert.ToDecimal(r["saldo"]);
     if(string.Equals(name,customer,StringComparison.OrdinalIgnoreCase))selectedClientStillHasFiado=true;
     if(InternalFiado(name))internalBalance+=balance;else allBalance+=balance;
     if(!name.ToUpperInvariant().Contains(clientSearch.Text.Trim().ToUpperInvariant()))continue;
     var b=new Button {Text=name+"  |  R$ "+balance.ToString("N2"),Size=new Size(190,72),AutoSize=false,AutoEllipsis=true,TextAlign=ContentAlignment.MiddleCenter,Font=new Font("Segoe UI",9,FontStyle.Bold),BackColor=Color.White,ForeColor=Color.FromArgb(25,75,150),Tag=name};
     b.Click+=(sender,args)=>SelectClient(Convert.ToString(((Button)sender).Tag));clients.Controls.Add(b);
    }
    clients.ResumeLayout();
    ArrangeClientBoxes();
    overview.Text="TOTAL EM ABERTO\\n(SEM INTERNOS): R$ "+allBalance.ToString("N2")+"\\nInternos fora do total: R$ "+internalBalance.ToString("N2");
    if(customer.Length>0){
     if(selectedClientStillHasFiado)SelectClient(customer);
     else{
      customer="";
      detailSplit.Panel2Collapsed=true;detailSplit.Panel1Collapsed=false;
      clientHome.Visible=true;clientHome.BringToFront();
     }
    }
   }catch(Exception ex){MessageBox.Show(ex.Message,"Erro ao listar clientes");}
  }
  void SelectClient(string name){
   customer=name;detailSplit.Panel1Collapsed=true;detailSplit.Panel2Collapsed=false;clientHome.Visible=false;selected.Text="FIADO | "+customer;
   refreshingClientSales=true;
   try {
    var sql="SELECT s.id AS venda,s.created_at AS data,s.status AS situacao,s.gross_total AS valor,COALESCE((SELECT SUM(p.amount) FROM credit_payments p WHERE p.sale_id=s.id),0) AS pago,"+Balance+" AS saldo FROM sales s WHERE "+CreditFilter+" AND TRIM(s.customer_name)=@name COLLATE NOCASE";
    openSales.DataSource=Query(sql+" AND "+Balance+">0.005 ORDER BY s.id DESC","@name",name);
    paidSales.DataSource=Query(sql+" AND "+Balance+"<=0.005 ORDER BY s.id DESC","@name",name);
    paymentHistory.DataSource=Query("SELECT p.paid_at AS data,p.sale_id AS venda,p.payment_method AS forma,p.amount AS valor FROM credit_payments p INNER JOIN sales s ON s.id=p.sale_id WHERE "+CreditFilter+" AND TRIM(s.customer_name)=@name COLLATE NOCASE ORDER BY p.paid_at DESC,p.id DESC","@name",name);
    var dt=(DataTable)openSales.DataSource;decimal total=0;foreach(DataRow r in dt.Rows)total+=Convert.ToDecimal(r["saldo"]);
    decimal credit=0m;
    using(var cn=Open(true))using(var check=new SQLiteCommand("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='customer_credit_entries'",cn)){
     if(Convert.ToInt32(check.ExecuteScalar())>0)credit=CustomerCreditBalance(cn,null,name);
    }
    string limitText="Limite: nao configurado";
    using(var cn=Open(true))using(var exists=new SQLiteCommand("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='customer_fiado_limits'",cn)){
     if(Convert.ToInt32(exists.ExecuteScalar())>0){
      using(var cmd=new SQLiteCommand("SELECT limit_amount,period FROM customer_fiado_limits WHERE customer_name=@client COLLATE NOCASE",cn)){
       cmd.Parameters.AddWithValue("@client",name);
       using(var rd=cmd.ExecuteReader())if(rd.Read()){
        decimal limit=Convert.ToDecimal(rd[0]);string period=Convert.ToString(rd[1]);rd.Close();
        decimal used=PeriodFiadoUsed(cn,null,name,period);
        limitText="Limite "+period+": R$ "+limit.ToString("N2")+" | Disponivel R$ "+Math.Max(0,limit-used).ToString("N2");
       }
      }
     }
    }
    selected.Text="FIADO | "+name+" | Divida R$ "+total.ToString("N2")+" | Credito R$ "+credit.ToString("N2")+" | "+limitText;
   }catch(Exception ex){MessageBox.Show(ex.Message,"Erro ao abrir fiado");}
   finally{refreshingClientSales=false;UpdatePaymentSelection(true);}
  }
  void CorrectPaymentDate(){
   if(string.IsNullOrWhiteSpace(customer)){MessageBox.Show("Selecione um cliente.");return;}
   try{
    var rows=Query("SELECT p.id AS id,p.paid_at AS data,p.sale_id AS venda,p.payment_method AS forma,p.amount AS valor FROM credit_payments p INNER JOIN sales s ON s.id=p.sale_id WHERE "+CreditFilter+" AND COALESCE(NULLIF(TRIM(s.customer_name),''),'SEM CLIENTE')=@name ORDER BY p.paid_at DESC,p.id DESC","@name",customer);
    if(rows.Rows.Count==0){MessageBox.Show("Este cliente não possui pagamentos registrados.");return;}
    using(var form=new Form {Text="Corrigir data de pagamento - "+customer,Width=850,Height=530,StartPosition=FormStartPosition.CenterParent}){
     var grid=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,DataSource=rows};
     var bottomBar=new FlowLayoutPanel {Dock=DockStyle.Bottom,Height=54,Padding=new Padding(8)};
     var change=new Button {Text="Alterar data do pagamento selecionado",AutoSize=true,Height=36};
     change.Click+=(s,e)=>{
      if(grid.CurrentRow==null)return;
      var row=((DataRowView)grid.CurrentRow.DataBoundItem).Row;
      long id=Convert.ToInt64(row["id"]);string original=Convert.ToString(row["data"]);
      DateTime parsed;if(!DateTime.TryParse(original,out parsed))parsed=DateTime.Today;
      using(var dialog=new Form {Text="Data do pagamento #"+id,Width=430,Height=265,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false}){
       var date=new DateTimePicker {Left=18,Top=38,Width=370,Format=DateTimePickerFormat.Custom,CustomFormat="dd/MM/yyyy HH:mm",ShowUpDown=true,Value=parsed};
       var reason=new TextBox {Left=18,Top=116,Width=370};
       var save=new Button {Text="Salvar correção",Left=210,Top=156,Width=178,Height=34};
       dialog.Controls.Add(new Label {Text="Data e hora correta do recebimento",Left=18,Top=17,AutoSize=true});
       dialog.Controls.Add(date);dialog.Controls.Add(new Label {Text="Motivo da correção (obrigatório)",Left=18,Top=94,AutoSize=true});dialog.Controls.Add(reason);dialog.Controls.Add(save);
       save.Click+=(sender,args)=>{
        if(reason.Text.Trim().Length<5){MessageBox.Show("Informe o motivo com pelo menos 5 caracteres.");return;}
        string corrected=date.Value.ToString("yyyy-MM-dd HH:mm:ss");
        if(corrected==original){MessageBox.Show("A data não foi alterada.");return;}
        if(date.Value>DateTime.Now.AddMinutes(5)){MessageBox.Show("A data não pode estar no futuro.");return;}
        if(MessageBox.Show("Corrigir pagamento #"+id+" de "+original+" para "+corrected+"?","Confirmar correção auditável",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
        try{
         using(var connection=Open(false)){
          string backup=db+".antes-data-fiado-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+".db";
          using(var copy=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=backup}.ToString())){copy.Open();connection.BackupDatabase(copy,"main","main",-1,null,0);}
          using(var tx=connection.BeginTransaction()){
           using(var create=new SQLiteCommand("CREATE TABLE IF NOT EXISTS fiado_payment_date_audit(id INTEGER PRIMARY KEY AUTOINCREMENT,payment_id INTEGER NOT NULL,sale_id INTEGER NOT NULL,old_paid_at TEXT NOT NULL,new_paid_at TEXT NOT NULL,reason TEXT NOT NULL,changed_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP)",connection,tx))create.ExecuteNonQuery();
           using(var update=new SQLiteCommand("UPDATE credit_payments SET paid_at=@new WHERE id=@id AND paid_at=@old AND sale_id IN (SELECT s.id FROM sales s WHERE "+CreditFilter+" AND COALESCE(NULLIF(TRIM(s.customer_name),''),'SEM CLIENTE')=@customer)",connection,tx)){
            update.Parameters.AddWithValue("@new",corrected);update.Parameters.AddWithValue("@id",id);update.Parameters.AddWithValue("@old",original);update.Parameters.AddWithValue("@customer",customer);
            if(update.ExecuteNonQuery()!=1)throw new Exception("Pagamento alterado por outro processo. Atualize a tela.");
           }
           using(var log=new SQLiteCommand("INSERT INTO fiado_payment_date_audit(payment_id,sale_id,old_paid_at,new_paid_at,reason) VALUES(@id,@sale,@old,@new,@reason)",connection,tx)){
            log.Parameters.AddWithValue("@id",id);log.Parameters.AddWithValue("@sale",Convert.ToInt64(row["venda"]));log.Parameters.AddWithValue("@old",original);log.Parameters.AddWithValue("@new",corrected);log.Parameters.AddWithValue("@reason",reason.Text.Trim());log.ExecuteNonQuery();
           }
           tx.Commit();
          }
         }
         dialog.DialogResult=DialogResult.OK;dialog.Close();
        }catch(Exception ex){MessageBox.Show(ex.Message,"Correção não realizada",MessageBoxButtons.OK,MessageBoxIcon.Error);}
       };
       if(dialog.ShowDialog(form)==DialogResult.OK){form.DialogResult=DialogResult.OK;form.Close();}
      }
     };
     bottomBar.Controls.Add(change);form.Controls.Add(grid);form.Controls.Add(bottomBar);
     if(form.ShowDialog(this)==DialogResult.OK){SelectClient(customer);MessageBox.Show("Data corrigida e auditada. Valor e forma de pagamento preservados.");}
    }
   }catch(Exception ex){MessageBox.Show(ex.Message,"Erro ao corrigir data",MessageBoxButtons.OK,MessageBoxIcon.Error);}
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
  // Corrige o apelido ou nome errado do cliente em todas as suas operacoes.
  // Nunca soma contas de pessoas diferentes: nome de destino existente e bloqueado.

  // Troca o titular de TODAS as vendas fiadas do apelido anterior por um cliente
  // ja cadastrado. Preserva registros de pagamentos por ID; nao modifica
  // carteiras de credito ou limites (sao saldos financeiros distintos).
  void TransferFiadoToRegisteredClient(){
   string source=(customer??"").Trim();
   if(source.Length==0||source=="SEM CLIENTE"){MessageBox.Show("Selecione o cliente de origem no Fiado.");return;}
   try{
    var names=new DataTable();
    names.Columns.Add("Nome do Cliente");
    names.Columns.Add("Documento");
    using(var cn=Open(true)){
     using(var exists=new SQLiteCommand("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='customers'",cn)){
      if(Convert.ToInt32(exists.ExecuteScalar())==0){MessageBox.Show("Ainda nao existe cadastro de Clientes. Cadastre o cliente correto primeiro.");return;}
     }
     using(var cmd=new SQLiteCommand("SELECT DISTINCT TRIM(name) AS nome,COALESCE(document,'') AS documento FROM customers WHERE TRIM(COALESCE(name,''))<>'' ORDER BY nome COLLATE NOCASE",cn))
     using(var rd=cmd.ExecuteReader()){
      while(rd.Read()){
       string name=Convert.ToString(rd["nome"]);
       if(!string.Equals(name,source,StringComparison.OrdinalIgnoreCase))
        names.Rows.Add(name,Convert.ToString(rd["documento"]));
      }
     }
    }
    if(names.Rows.Count==0){MessageBox.Show("Nenhum outro cliente cadastrado foi encontrado. Cadastre o cliente correto antes de transferir.");return;}
    using(var dlg=new Form{Text="Fiado | Transferir para cliente cadastrado",Width=690,Height=565,MinimumSize=new Size(550,430),StartPosition=FormStartPosition.CenterParent,BackColor=Color.FromArgb(229,242,255)}){
     var header=new Label{Text="Origem: "+source+"\nEscolha o cadastro correto para receber as vendas fiadas.",Dock=DockStyle.Top,Height=77,Padding=new Padding(12,11,5,5),Font=new Font("Segoe UI",11,FontStyle.Bold)};
     var search=new TextBox{Dock=DockStyle.Top,Font=new Font("Segoe UI",12)};
     var list=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,MultiSelect=false,AllowUserToAddRows=false,RowHeadersVisible=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,BackgroundColor=Color.White};
     var bar=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=58,Padding=new Padding(9),BackColor=Color.White};
     var choose=new Button{Text="Transferir vendas fiadas",Width=240,Height=36,BackColor=Color.FromArgb(12,113,88),ForeColor=Color.White};
     var cancel=new Button{Text="Cancelar",Width=140,Height=36,DialogResult=DialogResult.Cancel};
     bar.Controls.Add(choose);bar.Controls.Add(cancel);
     var view=new DataView(names);list.DataSource=view;
     search.TextChanged+=(sender,args)=>{
      string value=search.Text.Trim().Replace("'","''").Replace("[","[[]").Replace("%","[%]").Replace("*","[*]");
      view.RowFilter=value.Length==0?"":"[Nome do Cliente] LIKE '%"+value+"%' OR Documento LIKE '%"+value+"%'";
     };
     Action transfer=()=>{
      if(list.CurrentRow==null){MessageBox.Show("Selecione o cliente correto.");return;}
      var selected=list.CurrentRow.DataBoundItem as DataRowView;
      if(selected==null)return;
      string destination=Convert.ToString(selected["Nome do Cliente"]).Trim();
      if(destination.Length==0||string.Equals(source,destination,StringComparison.OrdinalIgnoreCase)){
       MessageBox.Show("Selecione outro cliente.");return;
      }
      try{
       long count;decimal debt;
       using(var cn=Open(true)){
        using(var cmd=new SQLiteCommand("SELECT COUNT(*) AS quantity, COALESCE(SUM(CASE WHEN "+Balance+">0 THEN "+Balance+" ELSE 0 END),0) AS debt FROM sales s WHERE "+CreditFilter+" AND s.customer_name=@source COLLATE NOCASE",cn)){
         cmd.Parameters.AddWithValue("@source",source);
         using(var rd=cmd.ExecuteReader()){
          if(!rd.Read())throw new Exception("Nao foi possivel calcular as vendas.");
          count=Convert.ToInt64(rd["quantity"]);debt=Convert.ToDecimal(rd["debt"]);
         }
        }
        using(var cmd=new SQLiteCommand("SELECT COUNT(*) FROM customers WHERE name=@name COLLATE NOCASE",cn)){
         cmd.Parameters.AddWithValue("@name",destination);
         if(Convert.ToInt64(cmd.ExecuteScalar())==0)throw new Exception("O cadastro de destino nao existe mais. Atualize a lista.");
        }
       }
       if(count==0){MessageBox.Show("O nome de origem nao possui vendas fiadas para transferir.");return;}
       string warning="Transferir "+count+" venda(s) fiada(s) de:\n"+source+"\n\nPara o cliente cadastrado:\n"+destination+
        "\n\nSaldo em aberto destas vendas: R$ "+debt.ToString("N2")+
        "\n\nOs pagamentos feitos continuam vinculados as mesmas vendas. O saldo do destino somara as dividas transferidas."+
        "\n\nCredito depositado, limite de Fiado e cascos da origem NAO serao transferidos. Confirme somente se for a mesma pessoa.";
       if(MessageBox.Show(warning,"CONFIRMAR TROCA DE CLIENTE",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
       string backup=CreditBackupPath(db,"antes-transferir-fiado-");
       using(var cn=Open(false)){
        using(var copy=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=backup}.ToString())){
         copy.Open();cn.BackupDatabase(copy,"main","main",-1,null,0);
         using(var check=new SQLiteCommand("PRAGMA integrity_check",copy))
          if(Convert.ToString(check.ExecuteScalar())!="ok")throw new Exception("Backup invalido. Transferencia bloqueada.");
        }
        if(new FileInfo(backup).Length==0)throw new Exception("Backup vazio. Transferencia bloqueada.");
        using(var tx=cn.BeginTransaction()){
         using(var check=new SQLiteCommand("SELECT COUNT(*) FROM customers WHERE name=@dest COLLATE NOCASE",cn,tx)){
          check.Parameters.AddWithValue("@dest",destination);
          if(Convert.ToInt64(check.ExecuteScalar())==0)throw new Exception("Cliente de destino nao existe mais.");
         }
         using(var create=new SQLiteCommand("CREATE TABLE IF NOT EXISTS fiado_customer_transfers(id INTEGER PRIMARY KEY AUTOINCREMENT,source_name TEXT NOT NULL,destination_name TEXT NOT NULL,sales_transferred INTEGER NOT NULL,changed_at TEXT NOT NULL,description TEXT NOT NULL)",cn,tx))
          create.ExecuteNonQuery();
         int changed;
         using(var cmd=new SQLiteCommand("UPDATE sales SET customer_name=@dest WHERE customer_name=@source COLLATE NOCASE AND (UPPER(COALESCE(payment_method,'')) LIKE '%FIADO%' OR UPPER(COALESCE(status,'')) LIKE '%FIADO%')",cn,tx)){
          cmd.Parameters.AddWithValue("@dest",destination);cmd.Parameters.AddWithValue("@source",source);
          changed=cmd.ExecuteNonQuery();
         }
         if(changed!=count)throw new Exception("O numero de vendas mudou durante a transferencia. Atualize a tela e tente novamente.");
         using(var audit=new SQLiteCommand("INSERT INTO fiado_customer_transfers(source_name,destination_name,sales_transferred,changed_at,description) VALUES(@source,@dest,@count,datetime('now','localtime'),@detail)",cn,tx)){
          audit.Parameters.AddWithValue("@source",source);audit.Parameters.AddWithValue("@dest",destination);audit.Parameters.AddWithValue("@count",changed);
          audit.Parameters.AddWithValue("@detail","Vendas e pagamentos mantidos nos mesmos IDs; carteira, limites e cascos nao transferidos. Saldo original R$ "+debt.ToString("N2"));
          if(audit.ExecuteNonQuery()!=1)throw new Exception("Nao foi possivel auditar a transferencia.");
         }
         tx.Commit();
        }
       }
       customer=destination;
       dlg.DialogResult=DialogResult.OK;dlg.Close();
       clientSearch.Clear();LoadClients();SelectClient(destination);
       MessageBox.Show(count+" vendas fiadas transferidas para "+destination+". Valores e pagamentos preservados.\nBackup: "+backup,"Fiado",MessageBoxButtons.OK,MessageBoxIcon.Information);
      }catch(Exception ex){MessageBox.Show("Transferencia nao realizada: "+ex.Message,"Fiado",MessageBoxButtons.OK,MessageBoxIcon.Error);}
     };
     choose.Click+=(sender,args)=>transfer();
     list.CellDoubleClick+=(sender,args)=>{if(args.RowIndex>=0)transfer();};
     dlg.AcceptButton=choose;dlg.CancelButton=cancel;
     dlg.Controls.Add(list);dlg.Controls.Add(bar);dlg.Controls.Add(search);dlg.Controls.Add(header);
     dlg.Shown+=(sender,args)=>search.Focus();
     dlg.ShowDialog(this);
    }
   }catch(Exception ex){MessageBox.Show("Nao foi possivel escolher o cliente: "+ex.Message,"Fiado",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  }
  void CorrectWholeClientName(){
   string oldName=(customer??"").Trim();
   if(oldName.Length<2||oldName=="SEM CLIENTE"){MessageBox.Show("Selecione o cliente do Fiado primeiro.");return;}
   using(var dlg=new Form{Text="Fiado | Corrigir nome do cliente",Width=550,Height=310,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,BackColor=Color.FromArgb(229,242,255)}){
    var caption=new Label{Text="Nome cadastrado atualmente:",Left=16,Top=15,Width=470,Height=27,Font=new Font("Segoe UI",10,FontStyle.Bold)};
    var current=new Label{Text=oldName,Left=16,Top=44,Width=485,Height=34,ForeColor=Color.FromArgb(22,75,160),Font=new Font("Segoe UI",14,FontStyle.Bold)};
    var newLabel=new Label{Text="Nome correto do cliente:",Left=16,Top=96,Width=470,Height=25};
    var input=new TextBox{Text=oldName,Left=16,Top=122,Width=490,MaxLength=180,Font=new Font("Segoe UI",12)};
    var help=new Label{Text="A correção mantém todas as vendas, pagamentos, crédito depositado, limite de Fiado e cascos vinculados a este nome.",Left=16,Top=164,Width=485,Height=45};
    var confirm=new Button{Text="Confirmar correção",Left=16,Top=222,Width=245,Height=36,BackColor=Color.FromArgb(22,75,160),ForeColor=Color.White};
    var cancel=new Button{Text="Cancelar",Left=280,Top=222,Width=225,Height=36,DialogResult=DialogResult.Cancel};
    dlg.Controls.AddRange(new Control[]{caption,current,newLabel,input,help,confirm,cancel});
    dlg.CancelButton=cancel;
    confirm.Click+=(sender,args)=>{
     string updated=input.Text.Trim();
     if(updated.Length<2){MessageBox.Show("Informe o nome correto.");return;}
     if(string.Equals(oldName,updated,StringComparison.Ordinal)){dlg.DialogResult=DialogResult.Cancel;dlg.Close();return;}
     try{
      using(var cn=Open(true)){
       // Nome ja existente pode pertencer a outro cliente; nao mesclar contas sem auditoria.
       foreach(var target in new[]{new[]{"customers","name"},new[]{"sales","customer_name"},new[]{"bottle_loans","customer_name"},
          new[]{"customer_credit_entries","customer_name"},new[]{"customer_fiado_limits","customer_name"}}){
        string table=target[0],col=target[1];
        using(var exists=new SQLiteCommand("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@table",cn)){
         exists.Parameters.AddWithValue("@table",table);
         if(Convert.ToInt32(exists.ExecuteScalar())==0)continue;
        }
        using(var check=new SQLiteCommand("SELECT COUNT(*) FROM \""+table+"\" WHERE LOWER(TRIM(\""+col+"\"))=LOWER(TRIM(@new)) AND LOWER(TRIM(\""+col+"\"))<>LOWER(TRIM(@old))",cn)){
         check.Parameters.AddWithValue("@new",updated);check.Parameters.AddWithValue("@old",oldName);
         if(Convert.ToInt64(check.ExecuteScalar())>0)throw new Exception("O nome \""+updated+"\" ja possui registros. Para evitar misturar dividas de duas pessoas, use um nome diferente ou confira os cadastros primeiro.");
        }
       }
      }
      if(MessageBox.Show("Alterar o nome em TODOS os registros deste cliente?\\n\\nDe: "+oldName+"\\nPara: "+updated+
       "\\n\\nVendas, pagamentos, fiado, carteira, limite e cascos permanecerao com os valores atuais. Um backup sera feito antes.",
       "Confirmar correção de nome",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
      string backup=CreditBackupPath(db,"antes-corrigir-nome-fiado-");
      using(var cn=Open(false)){
       using(var copy=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=backup}.ToString())){
        copy.Open();cn.BackupDatabase(copy,"main","main",-1,null,0);
        using(var check=new SQLiteCommand("PRAGMA integrity_check",copy))
         if(Convert.ToString(check.ExecuteScalar())!="ok")throw new Exception("Backup nao passou na verificacao. Nada foi alterado.");
       }
       if(new FileInfo(backup).Length<=0)throw new Exception("Backup vazio. Nenhuma alteracao feita.");
       using(var tx=cn.BeginTransaction()){
        using(var create=new SQLiteCommand("CREATE TABLE IF NOT EXISTS customer_name_corrections(id INTEGER PRIMARY KEY AUTOINCREMENT,old_name TEXT NOT NULL,new_name TEXT NOT NULL,changed_at TEXT NOT NULL,details TEXT NOT NULL)",cn,tx))create.ExecuteNonQuery();
        int changed=0;
        foreach(var target in new[]{new[]{"customers","name"},new[]{"sales","customer_name"},new[]{"bottle_loans","customer_name"},
           new[]{"customer_credit_entries","customer_name"},new[]{"customer_fiado_limits","customer_name"},new[]{"customer_fiado_limit_audit","customer_name"}}){
         string table=target[0],col=target[1];
         using(var exists=new SQLiteCommand("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@table",cn,tx)){
          exists.Parameters.AddWithValue("@table",table);
          if(Convert.ToInt32(exists.ExecuteScalar())==0)continue;
         }
         // Revalidar dentro da transacao para prevenir a fusao de clientes.
         using(var check=new SQLiteCommand("SELECT COUNT(*) FROM \""+table+"\" WHERE LOWER(TRIM(\""+col+"\"))=LOWER(TRIM(@new)) AND LOWER(TRIM(\""+col+"\"))<>LOWER(TRIM(@old))",cn,tx)){
          check.Parameters.AddWithValue("@new",updated);check.Parameters.AddWithValue("@old",oldName);
          if(Convert.ToInt64(check.ExecuteScalar())>0)throw new Exception("Ja existem movimentacoes com o novo nome. Correcao cancelada.");
         }
         using(var update=new SQLiteCommand("UPDATE \""+table+"\" SET \""+col+"\"=@new WHERE LOWER(TRIM(\""+col+"\"))=LOWER(TRIM(@old))",cn,tx)){
          update.Parameters.AddWithValue("@new",updated);update.Parameters.AddWithValue("@old",oldName);
          changed+=update.ExecuteNonQuery();
         }
        }
        if(changed==0)throw new Exception("Nenhum registro do cliente encontrado.");
        using(var log=new SQLiteCommand("INSERT INTO customer_name_corrections(old_name,new_name,changed_at,details) VALUES(@old,@new,datetime('now','localtime'),@description)",cn,tx)){
         log.Parameters.AddWithValue("@old",oldName);log.Parameters.AddWithValue("@new",updated);
         log.Parameters.AddWithValue("@description","Correcao completa no modulo Fiado; "+changed+" registros atualizados");
         if(log.ExecuteNonQuery()!=1)throw new Exception("Falha ao registrar auditoria da correcao.");
        }
        if(changed>0){
         // Evita que um nome anteriormente excluido seja ocultado por engano.
         using(var check=new SQLiteCommand("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='deleted_customer_names'",cn,tx)){
          if(Convert.ToInt32(check.ExecuteScalar())>0)
           using(var visible=new SQLiteCommand("DELETE FROM deleted_customer_names WHERE customer_name=@new COLLATE NOCASE",cn,tx)){
            visible.Parameters.AddWithValue("@new",updated);visible.ExecuteNonQuery();
           }
         }
        }
        tx.Commit();
       }
      }
      customer=updated;
      dlg.DialogResult=DialogResult.OK;dlg.Close();
      LoadClients();SelectClient(updated);
      MessageBox.Show("Nome corrigido para "+updated+". As vendas e os saldos foram preservados.\\nBackup: "+backup,"Fiado",MessageBoxButtons.OK,MessageBoxIcon.Information);
     }catch(Exception ex){MessageBox.Show("Nome nao alterado: "+ex.Message,"Fiado",MessageBoxButtons.OK,MessageBoxIcon.Error);}
    };
    dlg.ShowDialog(this);
   }
  }
  void CorrectCustomer(){
   var view=SelectedSaleGrid();
   if(view.CurrentRow==null){MessageBox.Show("Selecione uma venda na aba de vendas em aberto ou pagas.");return;}
   long id=Convert.ToInt64(view.CurrentRow.Cells["venda"].Value);
   using(var form=new Form {Text="Corrigir cliente - venda #"+id,Width=510,Height=205,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false}){
    var input=new TextBox {Left=18,Top=52,Width=455,Font=new Font("Segoe UI",12),Text=customer=="SEM CLIENTE"?"":customer};
    var label=new Label {Left=18,Top=16,Width=450,Height=30,Text="Nome correto do cliente:",Font=new Font("Segoe UI",11,FontStyle.Bold)};
    var save=new Button {Text="Salvar cliente",Left=290,Top=103,Width=183,Height=39,BackColor=Color.FromArgb(220,95,105),ForeColor=Color.White};
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
  void RefreshSelectedSaleDetails(DataGridView sales,DataGridView items,DataGridView payments){
   items.DataSource=null;payments.DataSource=null;
   if(sales.CurrentRow==null || sales.CurrentRow.IsNewRow || !sales.Columns.Contains("venda"))return;
   try{
    var sale=sales.CurrentRow.Cells["venda"].Value;
    if(sale==null || sale==DBNull.Value)return;
    long id=Convert.ToInt64(sale);
    items.DataSource=Query("SELECT name AS produto,quantity AS quantidade,unit_price AS preco,total AS valor FROM sale_items WHERE sale_id=@sale ORDER BY id","@sale",id);
    payments.DataSource=Query("SELECT paid_at AS data,payment_method AS forma,amount AS valor FROM credit_payments WHERE sale_id=@sale ORDER BY paid_at,id","@sale",id);
   }catch(Exception ex){items.DataSource=null;payments.DataSource=null;MessageBox.Show(ex.Message,"Erro ao consultar venda");}
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
    items.DataSource=Query("SELECT id,name AS produto,quantity AS quantidade,unit_price AS preco,total AS total FROM sale_items WHERE sale_id=@sale ORDER BY id","@sale",id);
    var payments=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
    payments.DataSource=Query("SELECT paid_at AS data,payment_method AS forma,amount AS valor FROM credit_payments WHERE sale_id=@sale ORDER BY paid_at,id","@sale",id);
    var a=new TabPage("Itens da venda");a.Controls.Add(items);var b=new TabPage("Pagamentos anteriores");b.Controls.Add(payments);
    tabs.TabPages.Add(a);tabs.TabPages.Add(b);form.Controls.Add(tabs);
    var edit=new Button {Text="Editar item selecionado (com auditoria)",Dock=DockStyle.Bottom,Height=42,BackColor=Color.FromArgb(220,95,105),ForeColor=Color.White};
    edit.Click+=(s,e)=>{if(items.CurrentRow==null){MessageBox.Show("Selecione um item da venda.");return;}long itemId=Convert.ToInt64(items.CurrentRow.Cells["id"].Value);form.Close();EditSaleItem(id,itemId);};form.Controls.Add(edit);
    using(form)form.ShowDialog(this);
   }catch(Exception ex){MessageBox.Show(ex.Message,"Erro na consulta");}
  }
  void EditSaleItem(long saleId,long selectedItemId){
   try{
    var rows=Query("SELECT id, name AS produto, quantity AS quantidade, unit_price AS preco, total AS valor FROM sale_items WHERE sale_id=@sale ORDER BY id","@sale",saleId);
    using(var form=new Form {Text="Auditoria de itens - venda #"+saleId,Width=880,Height=550,StartPosition=FormStartPosition.CenterParent}){
     var table=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,DataSource=rows};
     foreach(DataGridViewRow candidate in table.Rows){if(candidate.Cells["id"].Value!=null && Convert.ToInt64(candidate.Cells["id"].Value)==selectedItemId){candidate.Selected=true;table.CurrentCell=candidate.Cells["produto"];break;}}
     var footer=new FlowLayoutPanel {Dock=DockStyle.Bottom,Height=60,Padding=new Padding(8)};
     var save=new Button {Text="Alterar item selecionado",Width=210,Height=38,BackColor=Color.FromArgb(220,95,105),ForeColor=Color.White};
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
     var header=new Label {Text="  AUDITORIA DA VENDA #"+saleId+" | "+data.Rows.Count+" alteração(ões) | Somente leitura",Dock=DockStyle.Top,Height=46,Font=new Font("Segoe UI",11,FontStyle.Bold),BackColor=Color.FromArgb(229,242,255),ForeColor=Color.FromArgb(20,73,158),TextAlign=ContentAlignment.MiddleLeft};
     var table=new DataGridView {Dock=DockStyle.Fill,DataSource=data,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells};
     form.Controls.Add(table);form.Controls.Add(header);form.ShowDialog(this);
    }
   }catch(Exception ex){MessageBox.Show(ex.Message,"Não foi possível consultar auditoria",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  }
  void SendCustomerStatement(){
   if(string.IsNullOrWhiteSpace(customer)){MessageBox.Show("Selecione um cliente.");return;}
   try{
    var sales=Query("SELECT s.id AS venda,s.created_at AS data,s.gross_total AS total,COALESCE((SELECT SUM(p.amount) FROM credit_payments p WHERE p.sale_id=s.id),0) AS pago,"+Balance+" AS saldo FROM sales s WHERE "+CreditFilter+" AND COALESCE(NULLIF(TRIM(s.customer_name),''),'SEM CLIENTE')=@name AND "+Balance+">0.005 ORDER BY s.created_at,s.id","@name",customer);
    if(sales.Rows.Count==0){MessageBox.Show("Este cliente não possui vendas em aberto.");return;}
    var sb=new System.Text.StringBuilder();
    sb.AppendLine("ARMAZEM GRATIDAO - EXTRATO DE FIADO");
    sb.AppendLine("Cliente: "+customer);
    sb.AppendLine("Emitido em: "+DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
    sb.AppendLine("--------------------------------");
    decimal totalDue=0;
    foreach(DataRow sale in sales.Rows){
     long id=Convert.ToInt64(sale["venda"]);
     decimal balance=Convert.ToDecimal(sale["saldo"]);
     totalDue+=balance;
     sb.AppendLine("Venda #"+id+" | "+Convert.ToString(sale["data"]));
     var items=Query("SELECT name,quantity,unit_price,total FROM sale_items WHERE sale_id=@sale ORDER BY id","@sale",id);
     foreach(DataRow item in items.Rows)sb.AppendLine("  "+Convert.ToString(item["name"])+" - "+Convert.ToDecimal(item["quantity"]).ToString("N3")+" x R$ "+Convert.ToDecimal(item["unit_price"]).ToString("N2")+" = R$ "+Convert.ToDecimal(item["total"]).ToString("N2"));
     var payments=Query("SELECT paid_at,payment_method,amount FROM credit_payments WHERE sale_id=@sale ORDER BY paid_at,id","@sale",id);
     if(payments.Rows.Count>0){
      sb.AppendLine("  PAGAMENTOS REGISTRADOS:");
      foreach(DataRow paymentRow in payments.Rows)
       sb.AppendLine("  "+Convert.ToString(paymentRow["paid_at"])+" | "+Convert.ToString(paymentRow["payment_method"])+" | R$ "+Convert.ToDecimal(paymentRow["amount"]).ToString("N2"));
     }
     sb.AppendLine("  Total: R$ "+Convert.ToDecimal(sale["total"]).ToString("N2")+" | Pago: R$ "+Convert.ToDecimal(sale["pago"]).ToString("N2")+" | Saldo: R$ "+balance.ToString("N2"));
     sb.AppendLine("--------------------------------");
    }
    sb.AppendLine("TOTAL EM ABERTO: R$ "+totalDue.ToString("N2"));
    sb.AppendLine("Documento informativo, nao fiscal.");
    using(var form=new Form {Text="Extrato do cliente - "+customer,Width=720,Height=660,StartPosition=FormStartPosition.CenterParent}){
     var preview=new TextBox {Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,Dock=DockStyle.Fill,Font=new Font("Consolas",10),Text=sb.ToString()};
     var actions=new FlowLayoutPanel {Dock=DockStyle.Bottom,Height=60,Padding=new Padding(8)};
     var copy=new Button {Text="Copiar extrato",Width=160,Height=38};
     copy.Click+=(s,e)=>{Clipboard.SetText(sb.ToString());MessageBox.Show("Extrato copiado.");};
     var share=new Button {Text="Abrir WhatsApp",Width=180,Height=38,BackColor=Color.FromArgb(15,119,78),ForeColor=Color.White};
     share.Click+=(s,e)=>{System.Diagnostics.Process.Start("https://api.whatsapp.com/send?text="+Uri.EscapeDataString(sb.ToString()));};
     actions.Controls.Add(copy);actions.Controls.Add(share);form.Controls.Add(preview);form.Controls.Add(actions);form.ShowDialog(this);
    }
   }catch(Exception ex){MessageBox.Show(ex.Message,"Não foi possível gerar extrato",MessageBoxButtons.OK,MessageBoxIcon.Error);}
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
    var payments=Query("SELECT paid_at,payment_method,amount FROM credit_payments WHERE sale_id=@sale ORDER BY paid_at,id","@sale",id);
    if(payments.Rows.Count>0){
     sb.AppendLine("PAGAMENTOS REGISTRADOS:");
     foreach(DataRow paymentRow in payments.Rows)
      sb.AppendLine(Convert.ToString(paymentRow["paid_at"])+" | "+Convert.ToString(paymentRow["payment_method"])+" | R$ "+Convert.ToDecimal(paymentRow["amount"]).ToString("N2"));
     sb.AppendLine("--------------------------------");
    }
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
  // A venda marcada prevalece sobre a linha destacada; sem marcação, vale a linha clicada.
  System.Collections.Generic.List<long> SelectedPendingSaleIds(){
   var ids=new System.Collections.Generic.List<long>();
   foreach(DataGridViewRow row in openSales.Rows){
    if(row.IsNewRow||row.Cells["venda"].Value==null||row.Cells["venda"].Value==DBNull.Value)continue;
    if(Convert.ToBoolean(row.Cells["marcar"].Value??false))ids.Add(Convert.ToInt64(row.Cells["venda"].Value));
   }
   if(ids.Count==0&&openSales.CurrentRow!=null&&!openSales.CurrentRow.IsNewRow&&openSales.Columns.Contains("venda")){
    object value=openSales.CurrentRow.Cells["venda"].Value;
    if(value!=null&&value!=DBNull.Value)ids.Add(Convert.ToInt64(value));
   }
   return ids;
  }
  decimal VisibleOpenBalance(){
   decimal total=0;
   foreach(DataGridViewRow row in openSales.Rows)
    if(!row.IsNewRow&&row.Cells["saldo"].Value!=null&&row.Cells["saldo"].Value!=DBNull.Value)
     total+=Math.Max(0,Convert.ToDecimal(row.Cells["saldo"].Value));
   return total;
  }
  void SetPaymentAmount(decimal value){
   value=Math.Max(0,Math.Min(amount.Maximum,value));
   amount.Value=value;
  }
  void UpdatePaymentSelection(bool fill){
   if(refreshingClientSales)return;
   if(saleTabs!=null&&saleTabs.SelectedIndex!=0)return;
   try{
    var ids=SelectedPendingSaleIds();
    decimal selectedTotal=0;
    foreach(DataGridViewRow row in openSales.Rows){
     if(row.IsNewRow||row.Cells["venda"].Value==null||row.Cells["saldo"].Value==null)continue;
     if(ids.Contains(Convert.ToInt64(row.Cells["venda"].Value)))selectedTotal+=Math.Max(0,Convert.ToDecimal(row.Cells["saldo"].Value));
    }
    receivedBalance.Text="Saldo selecionado: R$ "+selectedTotal.ToString("N2")+"  |  Total em aberto: R$ "+VisibleOpenBalance().ToString("N2");
    if(fill)SetPaymentAmount(selectedTotal);
   }catch(Exception ex){receivedBalance.Text="Selecione uma venda para receber. "+ex.Message;}
  }
  void FillAllBalances(){
   if(string.IsNullOrWhiteSpace(customer)){MessageBox.Show("Selecione um cliente.");return;}
   if(saleTabs!=null&&saleTabs.SelectedIndex!=0){MessageBox.Show("Abra Fiado atual - em aberto.");return;}
   SetPaymentAmount(VisibleOpenBalance());
   receivedBalance.Text="Todas as vendas: R$ "+VisibleOpenBalance().ToString("N2")+" (pode diminuir o valor)";
   amount.Focus();
   amount.Select(0,amount.Text.Length);
  }
  // Baixa parcial distribuída pelas vendas mais antigas. Tudo na mesma transação,
  // sem modificar os itens da venda, valores originais ou o estoque.
  void ReceivePayments(System.Collections.Generic.List<long> ids,decimal requested,string scope){
   if(string.IsNullOrWhiteSpace(customer)){MessageBox.Show("Selecione um cliente.");return;}
   if(ids==null||ids.Count==0){MessageBox.Show("Selecione uma venda em aberto.");return;}
   if(requested<=0){MessageBox.Show("Informe um valor recebido maior que zero.");return;}
   string payMethod=Convert.ToString(method.SelectedItem);
   if(string.IsNullOrWhiteSpace(payMethod)){MessageBox.Show("Selecione a forma de pagamento.");return;}
   bool useDepositedCredit=payMethod=="CREDITO DEPOSITADO";
   if(useDepositedCredit){
    try{
     EnsureCustomerCredit(db);
     using(var check=Open(true)){
      decimal available=CustomerCreditBalance(check,null,customer);
      if(requested>available+0.005m){
       MessageBox.Show("Credito disponivel: R$ "+available.ToString("N2")+". Informe valor menor ou igual ao credito do cliente.","Saldo insuficiente");
       return;
      }
     }
    }catch(Exception ex){MessageBox.Show("Credito indisponivel: "+ex.Message,"Fiado");return;}
   }
   try{
    var balances=new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<long,decimal>>();
    using(var conn=Open(true)){
     foreach(long id in ids){
      using(var cmd=new SQLiteCommand("SELECT "+Balance+" FROM sales s WHERE s.id=@id AND "+CreditFilter+" AND COALESCE(NULLIF(TRIM(s.customer_name),''),'SEM CLIENTE')=@customer",conn)){
       cmd.Parameters.AddWithValue("@id",id);cmd.Parameters.AddWithValue("@customer",customer);
       object value=cmd.ExecuteScalar();
       if(value==null||value==DBNull.Value)throw new Exception("Venda #"+id+" não pertence ao cliente ou foi removida.");
       decimal saldo=Convert.ToDecimal(value);
       if(saldo>0.005M)balances.Add(new System.Collections.Generic.KeyValuePair<long,decimal>(id,saldo));
      }
     }
    }
    if(balances.Count==0){MessageBox.Show("Nenhuma venda em aberto para dar baixa.");return;}
    decimal due=0;
    foreach(var row in balances)due+=row.Value;
    if(requested>due+0.005M){MessageBox.Show("O recebido não pode ultrapassar o saldo em aberto de R$ "+due.ToString("N2")+".");return;}
    decimal planned=0;int affected=0;
    foreach(var row in balances){
     decimal toPay=Math.Min(row.Value,Math.Max(0,requested-planned));
     if(toPay<=0)break;
     planned+=toPay;affected++;
    }
    string left="R$ "+Math.Max(0,due-requested).ToString("N2");
    string message="Cliente: "+customer+Environment.NewLine+"Vendas consideradas: "+balances.Count+
     Environment.NewLine+"Saldo em aberto: R$ "+due.ToString("N2")+
     Environment.NewLine+"Receber agora: R$ "+requested.ToString("N2")+
     Environment.NewLine+"Forma: "+payMethod+
     Environment.NewLine+"Saldo que ficará em aberto: "+left+
     Environment.NewLine+"Vendas com baixa neste recebimento: "+affected+
     Environment.NewLine+Environment.NewLine+"Confirmar recebimento "+scope+"?";
    if(MessageBox.Show(message,"Conferir pagamento do Fiado",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
    using(var conn=Open(false)){
     string backup=CreditBackupPath(db,"antes-receber-fiado-");
     using(var copy=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=backup}.ToString())){
      copy.Open();conn.BackupDatabase(copy,"main","main",-1,null,0);
      using(var integrity=new SQLiteCommand("PRAGMA integrity_check",copy))
       if(Convert.ToString(integrity.ExecuteScalar())!="ok")throw new Exception("Backup do Fiado nao passou na verificacao. Pagamento cancelado.");
     }
     if(new FileInfo(backup).Length<=0)throw new Exception("Backup vazio. Pagamento cancelado.");
     using(var tx=conn.BeginTransaction()){
      if(useDepositedCredit && CustomerCreditBalance(conn,tx,customer)+0.005m<requested)
       throw new Exception("Credito do cliente foi alterado. Atualize os saldos antes de receber.");
      decimal remaining=requested;
      foreach(var row in balances){
       using(var check=new SQLiteCommand("SELECT "+Balance+" FROM sales s WHERE s.id=@id AND "+CreditFilter+" AND COALESCE(NULLIF(TRIM(s.customer_name),''),'SEM CLIENTE')=@customer",conn,tx)){
        check.Parameters.AddWithValue("@id",row.Key);check.Parameters.AddWithValue("@customer",customer);
        object value=check.ExecuteScalar();
        if(value==null||value==DBNull.Value||Math.Abs(Convert.ToDecimal(value)-row.Value)>0.005M)
         throw new Exception("O saldo da venda #"+row.Key+" mudou. Atualize e confira antes de receber.");
       }
      }
      foreach(var row in balances){
       if(remaining<=0.005M)break;
       decimal received=Math.Min(row.Value,remaining);
       using(var insert=new SQLiteCommand("INSERT INTO credit_payments(sale_id,amount,payment_method,paid_at,note) VALUES(@sale,@amount,@method,datetime('now','localtime'),@note)",conn,tx)){
        insert.Parameters.AddWithValue("@sale",row.Key);
        insert.Parameters.AddWithValue("@amount",received);
        insert.Parameters.AddWithValue("@method",payMethod);
        insert.Parameters.AddWithValue("@note","Recebimento "+scope+" - baixa parcial ou total auditada no Fiado");
        if(insert.ExecuteNonQuery()!=1)throw new Exception("Falha ao registrar pagamento da venda #"+row.Key);
       }
       if(useDepositedCredit){
        // A mesma transacao grava a baixa do fiado e retira o dinheiro da carteira.
        using(var debit=new SQLiteCommand("INSERT INTO customer_credit_entries(customer_name,amount,entry_type,method,sale_id,details) VALUES(@name,@amount,'UTILIZACAO_FIADO','CREDITO_CLIENTE',NULL,@note)",conn,tx)){
         debit.Parameters.AddWithValue("@name",customer.Trim());
         debit.Parameters.AddWithValue("@amount",-received);
         debit.Parameters.AddWithValue("@note","Pagamento com credito antecipado - venda #"+row.Key);
         if(debit.ExecuteNonQuery()!=1)throw new Exception("Falha ao debitar o credito do cliente.");
        }
       }
       remaining-=received;
      }
      if(Math.Abs(remaining)>0.005M)throw new Exception("Diferença no valor distribuído. Recebimento cancelado.");
      tx.Commit();
     }
    }
    SetPaymentAmount(0);
    LoadClients();
    SelectClient(customer);
    MessageBox.Show("Recebido: R$ "+requested.ToString("N2")+" ("+payMethod+")."+
     Environment.NewLine+"Saldo restante nas vendas consideradas: "+left+
     Environment.NewLine+"Pagamentos registrados por venda no histórico.","Fiado");
   }catch(Exception ex){MessageBox.Show(ex.Message,"Baixa não realizada",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  }

  static string CreditBackupPath(string database,string prefix){
   string dir=System.IO.Path.GetDirectoryName(database);
   string file=System.IO.Path.Combine(dir,"backup-location.txt");
   string target=System.IO.File.Exists(file)?System.IO.File.ReadAllText(file).Trim():System.IO.Path.Combine(dir,"Backups");
   if(string.IsNullOrWhiteSpace(target)||!System.IO.Path.IsPathRooted(target))throw new Exception("Pasta de backup nao configurada corretamente.");
   System.IO.Directory.CreateDirectory(target);
   return System.IO.Path.Combine(target,prefix+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+".db");
  }
  // Carteira de credito separada da divida, com movimentacoes imutaveis.
  public static void EnsureCustomerCredit(string database){
   using(var cn=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=database,FailIfMissing=true}.ToString())){
    cn.Open();
    using(var check=new SQLiteCommand("SELECT count(*) FROM sqlite_master WHERE type='table' AND name='customer_credit_entries'",cn))
     if(Convert.ToInt32(check.ExecuteScalar())>0)return;
    string backup=CreditBackupPath(database,"antes-credito-clientes-");
    using(var dest=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=backup}.ToString())){
     dest.Open();cn.BackupDatabase(dest,"main","main",-1,null,0);
     using(var check=new SQLiteCommand("PRAGMA integrity_check",dest))
      if(Convert.ToString(check.ExecuteScalar())!="ok")throw new Exception("Backup de credito invalido; operacao cancelada.");
    }
    if(new FileInfo(backup).Length<=0)throw new Exception("Backup de credito vazio; operacao cancelada.");
    using(var cmd=new SQLiteCommand("CREATE TABLE IF NOT EXISTS customer_credit_entries(id INTEGER PRIMARY KEY AUTOINCREMENT,customer_name TEXT NOT NULL COLLATE NOCASE,amount NUMERIC NOT NULL CHECK(amount<>0),entry_type TEXT NOT NULL,method TEXT NOT NULL,created_at TEXT NOT NULL DEFAULT (datetime('now','localtime')),sale_id INTEGER,details TEXT NOT NULL DEFAULT '')",cn))cmd.ExecuteNonQuery();
    using(var index=new SQLiteCommand("CREATE UNIQUE INDEX IF NOT EXISTS ux_customer_credit_sale ON customer_credit_entries(sale_id) WHERE sale_id IS NOT NULL",cn))index.ExecuteNonQuery();
   }
  }
  public static decimal CustomerCreditBalance(SQLiteConnection cn,SQLiteTransaction tx,string name){
   using(var cmd=new SQLiteCommand("SELECT COALESCE(SUM(amount),0) FROM customer_credit_entries WHERE customer_name=@name COLLATE NOCASE",cn,tx)){
    cmd.Parameters.AddWithValue("@name",name.Trim());return Convert.ToDecimal(cmd.ExecuteScalar());
   }
  }
  // Chamar apenas dentro da transacao da venda, depois de gravar sales.
  public static decimal ApplyCustomerCredit(SQLiteConnection cn,SQLiteTransaction tx,long saleId,string name,decimal fiadoAmount){
   if(fiadoAmount<=0 || string.IsNullOrWhiteSpace(name) || string.Equals(name.Trim(),"Consumidor",StringComparison.OrdinalIgnoreCase))return 0m;
   decimal available=CustomerCreditBalance(cn,tx,name);
   decimal applied=Math.Min(Math.Max(0m,available),fiadoAmount);
   applied=Math.Round(applied,2);
   if(applied<=0)return 0m;
   using(var ledger=new SQLiteCommand("INSERT INTO customer_credit_entries(customer_name,amount,entry_type,method,sale_id,details) VALUES(@client,@amount,'UTILIZACAO','CREDITO_CLIENTE',@sale,'Abatimento automatico da venda fiada')",cn,tx)){
    ledger.Parameters.AddWithValue("@client",name.Trim());ledger.Parameters.AddWithValue("@amount",-applied);ledger.Parameters.AddWithValue("@sale",saleId);
    if(ledger.ExecuteNonQuery()!=1)throw new Exception("Nao foi possivel abater credito do cliente.");
   }
   using(var payment=new SQLiteCommand("INSERT INTO credit_payments(sale_id,amount,payment_method,paid_at,note) VALUES(@sale,@amount,'CREDITO_CLIENTE',datetime('now','localtime'),'Credito antecipado aplicado automaticamente')",cn,tx)){
    payment.Parameters.AddWithValue("@sale",saleId);payment.Parameters.AddWithValue("@amount",applied);
    if(payment.ExecuteNonQuery()!=1)throw new Exception("Nao foi possivel registrar o abatimento no fiado.");
   }
   return applied;
  }
  void DepositCustomerCredit(){
   if(string.IsNullOrWhiteSpace(customer)||customer=="SEM CLIENTE"){MessageBox.Show("Selecione um cliente cadastrado.");return;}
   decimal value=creditDepositAmount.Value;
   if(value<=0){MessageBox.Show("Informe o valor antecipado no campo CREDITO DEPOSITADO R$.");return;}
   string payMethod=Convert.ToString(method.SelectedItem);
   if(payMethod=="CREDITO DEPOSITADO"){MessageBox.Show("Escolha a forma realmente recebida (DINHEIRO, PIX etc.), nao CREDITO DEPOSITADO.");return;}
   if(MessageBox.Show("Cliente: "+customer+"\\nCredito recebido: R$ "+value.ToString("N2")+"\\nForma: "+payMethod+"\\n\\nEste valor sera guardado para abater compras fiadas futuras. Confirmar?","Credito do cliente",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
   try{
    EnsureCustomerCredit(db);
    using(var cn=Open(false)){
     string backup=CreditBackupPath(db,"antes-credito-deposito-");
     using(var dest=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=backup}.ToString())){
      dest.Open();cn.BackupDatabase(dest,"main","main",-1,null,0);
      using(var check=new SQLiteCommand("PRAGMA integrity_check",dest))if(Convert.ToString(check.ExecuteScalar())!="ok")throw new Exception("Backup invalido. Credito nao gravado.");
     }
     if(new FileInfo(backup).Length==0)throw new Exception("Backup vazio. Credito nao gravado.");
     using(var tx=cn.BeginTransaction()){
      using(var cmd=new SQLiteCommand("INSERT INTO customer_credit_entries(customer_name,amount,entry_type,method,details) VALUES(@client,@amount,'DEPOSITO',@method,'Credito antecipado recebido no Fiado')",cn,tx)){
       cmd.Parameters.AddWithValue("@client",customer.Trim());cmd.Parameters.AddWithValue("@amount",value);cmd.Parameters.AddWithValue("@method",payMethod);
       if(cmd.ExecuteNonQuery()!=1)throw new Exception("Falha ao registrar credito.");
      }
      tx.Commit();
     }
    }
    creditDepositAmount.Value=0;LoadClients();SelectClient(customer);
    MessageBox.Show("Credito registrado para "+customer+". O valor sera abatido automaticamente das proximas compras fiadas.","Fiado");
   }catch(Exception ex){MessageBox.Show("Credito nao registrado: "+ex.Message,"Fiado",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  }
  // Corrigir o saldo da carteira com lancamento auditavel; nunca reescrever historico.
  void AdjustCustomerCredit(){
   if(string.IsNullOrWhiteSpace(customer)||customer=="SEM CLIENTE"){MessageBox.Show("Selecione o cliente.");return;}
   try{
    EnsureCustomerCredit(db);
    decimal current;
    using(var cn=Open(true))current=CustomerCreditBalance(cn,null,customer);
    using(var dlg=new Form{Text="Ajustar credito depositado - "+customer,Width=500,Height=310,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false}){
     var old=new Label{Text="Credito atual: R$ "+current.ToString("N2"),Left=18,Top=17,Width=435,Height=32,Font=new Font("Segoe UI",11,FontStyle.Bold)};
     var label=new Label{Text="Novo saldo de credito (R$):",Left=18,Top=62,Width=420,Height=26};
     var amountNew=new NumericUpDown{Left=18,Top=91,Width=220,DecimalPlaces=2,Maximum=100000000,Value=Math.Max(0,Math.Min(100000000,current))};
     var reasonLabel=new Label{Text="Motivo da correcao (obrigatorio):",Left=18,Top=135,Width=430,Height=24};
     var reason=new TextBox{Left=18,Top=160,Width=445,MaxLength=250};
     var confirm=new Button{Text="Salvar ajuste auditado",Left=18,Top=216,Width=215,Height=36,DialogResult=DialogResult.OK};
     var cancel=new Button{Text="Cancelar",Left=245,Top=216,Width=160,Height=36,DialogResult=DialogResult.Cancel};
     dlg.Controls.AddRange(new Control[]{old,label,amountNew,reasonLabel,reason,confirm,cancel});
     if(dlg.ShowDialog(this)!=DialogResult.OK)return;
     if(string.IsNullOrWhiteSpace(reason.Text)){MessageBox.Show("Informe o motivo do ajuste.");return;}
     decimal desired=amountNew.Value;
     if(desired==current){MessageBox.Show("O saldo nao foi alterado.");return;}
     if(MessageBox.Show("Cliente: "+customer+"\\nSaldo anterior: R$ "+current.ToString("N2")+"\\nNovo saldo: R$ "+desired.ToString("N2")+"\\nMotivo: "+reason.Text.Trim()+"\\n\\nConfirmar correcao auditada?","Ajustar credito depositado",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
     string backup=CreditBackupPath(db,"antes-ajustar-credito-");
     using(var cn=Open(false)){
      using(var dest=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=backup}.ToString())){
       dest.Open();cn.BackupDatabase(dest,"main","main",-1,null,0);
       using(var integrity=new SQLiteCommand("PRAGMA integrity_check",dest))
        if(Convert.ToString(integrity.ExecuteScalar())!="ok")throw new Exception("Falha ao validar backup. Alteracao cancelada.");
      }
      if(new FileInfo(backup).Length<=0)throw new Exception("Backup vazio. Alteracao cancelada.");
      using(var tx=cn.BeginTransaction()){
       decimal now=CustomerCreditBalance(cn,tx,customer);
       if(Math.Abs(now-current)>0.005m)throw new Exception("Credito foi alterado por outro recebimento. Atualize a tela.");
       decimal delta=desired-now;
       if(delta==0)return;
       using(var cmd=new SQLiteCommand("INSERT INTO customer_credit_entries(customer_name,amount,entry_type,method,details) VALUES(@client,@delta,'AJUSTE_AUDITADO','CORRECAO',@details)",cn,tx)){
        cmd.Parameters.AddWithValue("@client",customer.Trim());
        cmd.Parameters.AddWithValue("@delta",delta);
        cmd.Parameters.AddWithValue("@details","Ajuste de saldo. Anterior R$ "+now.ToString("N2")+", novo R$ "+desired.ToString("N2")+". Motivo: "+reason.Text.Trim());
        if(cmd.ExecuteNonQuery()!=1)throw new Exception("Nao foi possivel gravar ajuste.");
       }
       tx.Commit();
      }
     }
     LoadClients();SelectClient(customer);
     MessageBox.Show("Saldo ajustado e registrado no historico do credito.","Fiado");
    }
   }catch(Exception ex){MessageBox.Show("Ajuste nao realizado: "+ex.Message,"Fiado",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  }
  void ShowCustomerCreditHistory(){
   if(string.IsNullOrWhiteSpace(customer))return;
   try{
    EnsureCustomerCredit(db);
    using(var cn=Open(true)){
     var data=new DataTable();
     using(var cmd=new SQLiteCommand("SELECT created_at AS Data,entry_type AS Operacao,amount AS Valor,method AS Forma,sale_id AS Venda,details AS Detalhes FROM customer_credit_entries WHERE customer_name=@client COLLATE NOCASE ORDER BY id DESC LIMIT 1000",cn)){
      cmd.Parameters.AddWithValue("@client",customer.Trim());
      using(var adapter=new SQLiteDataAdapter(cmd))adapter.Fill(data);
     }
     using(var dlg=new Form{Text="Historico de credito - "+customer,Width=860,Height=530,StartPosition=FormStartPosition.CenterParent}){
      dlg.Controls.Add(new DataGridView{Dock=DockStyle.Fill,DataSource=data,ReadOnly=true,AllowUserToAddRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill});
      dlg.ShowDialog(this);
     }
    }
   }catch(Exception ex){MessageBox.Show("Historico indisponivel: "+ex.Message);}
  }

  // Limite de novas compras fiadas por periodo (semanal ou mensal), separado do saldo pago antecipadamente.
  public static void EnsureCreditLimits(string database){
   using(var cn=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=database,FailIfMissing=true}.ToString())){
    cn.Open();
    using(var check=new SQLiteCommand("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='customer_fiado_limits'",cn))
     if(Convert.ToInt32(check.ExecuteScalar())>0)return;
    string backup=CreditBackupPath(database,"antes-limites-fiado-");
    using(var dest=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=backup}.ToString())){
     dest.Open();cn.BackupDatabase(dest,"main","main",-1,null,0);
     using(var check=new SQLiteCommand("PRAGMA integrity_check",dest))
      if(Convert.ToString(check.ExecuteScalar())!="ok")throw new Exception("Backup do Fiado nao passou na verificacao.");
    }
    if(new FileInfo(backup).Length<=0)throw new Exception("Backup vazio; operacao bloqueada.");
    using(var tx=cn.BeginTransaction()){
     using(var create=new SQLiteCommand("CREATE TABLE IF NOT EXISTS customer_fiado_limits(customer_name TEXT PRIMARY KEY COLLATE NOCASE,limit_amount NUMERIC NOT NULL CHECK(limit_amount>=0),period TEXT NOT NULL CHECK(period IN ('SEMANAL','MENSAL')),changed_at TEXT NOT NULL)",cn,tx))create.ExecuteNonQuery();
     using(var audit=new SQLiteCommand("CREATE TABLE IF NOT EXISTS customer_fiado_limit_audit(id INTEGER PRIMARY KEY AUTOINCREMENT,customer_name TEXT NOT NULL,old_limit NUMERIC,old_period TEXT,new_limit NUMERIC NOT NULL,new_period TEXT NOT NULL,changed_at TEXT NOT NULL)",cn,tx))audit.ExecuteNonQuery();
     tx.Commit();
    }
   }
  }
  public static decimal PeriodFiadoUsed(SQLiteConnection cn,SQLiteTransaction tx,string name,string period){
   DateTime date=DateTime.Today;
   if(period=="SEMANAL")date=date.AddDays(-(((int)date.DayOfWeek+6)%7));
   else date=new DateTime(date.Year,date.Month,1);
   string sql="SELECT COALESCE(SUM(CASE WHEN COALESCE(s.gross_total,0)-COALESCE((SELECT SUM(sp.amount) FROM sale_payments sp WHERE sp.sale_id=s.id AND UPPER(COALESCE(sp.payment_method,''))<>'FIADO'),0)>0 THEN COALESCE(s.gross_total,0)-COALESCE((SELECT SUM(sp.amount) FROM sale_payments sp WHERE sp.sale_id=s.id AND UPPER(COALESCE(sp.payment_method,''))<>'FIADO'),0) ELSE 0 END),0) FROM sales s WHERE s.customer_name=@client COLLATE NOCASE AND substr(s.created_at,1,10)>=@start AND (UPPER(COALESCE(s.payment_method,'')) LIKE '%FIADO%' OR UPPER(COALESCE(s.status,'')) LIKE '%FIADO%') AND UPPER(COALESCE(s.status,'')) NOT LIKE '%EXCLUID%'";
   using(var cmd=new SQLiteCommand(sql,cn,tx)){
    cmd.Parameters.AddWithValue("@client",name.Trim());
    cmd.Parameters.AddWithValue("@start",date.ToString("yyyy-MM-dd"));
    return Convert.ToDecimal(cmd.ExecuteScalar());
   }
  }
  public static void EnforceCreditLimit(SQLiteConnection cn,SQLiteTransaction tx,string name,decimal newFiado){
   if(newFiado<=0)return;
   using(var cmd=new SQLiteCommand("SELECT limit_amount,period FROM customer_fiado_limits WHERE customer_name=@client COLLATE NOCASE",cn,tx)){
    cmd.Parameters.AddWithValue("@client",name.Trim());
    using(var rd=cmd.ExecuteReader()){
     if(!rd.Read())return; // Clientes antigos sem limite configurado mantem comportamento anterior.
     decimal limit=Convert.ToDecimal(rd[0]);string period=Convert.ToString(rd[1]);
     rd.Close();
     decimal used=PeriodFiadoUsed(cn,tx,name,period);
     if(used+newFiado>limit+0.005m)
      throw new Exception("Limite de Fiado "+period.ToLowerInvariant()+" excedido para "+name+". Limite R$ "+limit.ToString("N2")+", usado R$ "+used.ToString("N2")+", nova compra fiada R$ "+newFiado.ToString("N2")+". Venda nao registrada.");
    }
   }
  }
  void ConfigureClientCreditLimit(){
   if(string.IsNullOrWhiteSpace(customer)||customer=="SEM CLIENTE"){MessageBox.Show("Selecione um cliente.");return;}
   try{
    EnsureCreditLimits(db);
    decimal existing=0m;string existingPeriod="MENSAL";
    using(var cn=Open(true))using(var cmd=new SQLiteCommand("SELECT limit_amount,period FROM customer_fiado_limits WHERE customer_name=@client COLLATE NOCASE",cn)){
     cmd.Parameters.AddWithValue("@client",customer);
     using(var rd=cmd.ExecuteReader())if(rd.Read()){existing=Convert.ToDecimal(rd[0]);existingPeriod=Convert.ToString(rd[1]);}
    }
    using(var dialog=new Form{Text="Limite de Fiado - "+customer,Width=480,Height=265,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false}){
     var label=new Label{Text="Limite de compras fiadas (R$):",Left=16,Top=18,Width=290,Height=28};
     var value=new NumericUpDown{Left=16,Top=51,Width=195,DecimalPlaces=2,Maximum=100000000,Value=Math.Max(0,Math.Min(existing,100000000))};
     var periodLabel=new Label{Text="Renovacao do limite:",Left=235,Top=18,Width=190,Height=28};
     var period=new ComboBox{Left=235,Top=51,Width=195,DropDownStyle=ComboBoxStyle.DropDownList};
     period.Items.AddRange(new object[]{"SEMANAL","MENSAL"});period.SelectedItem=existingPeriod;
     var hint=new Label{Text="Semanal: segunda a domingo. Mensal: do dia 1 ao ultimo dia. Limite 0 bloqueia novas vendas fiadas.",Left=16,Top=97,Width=420,Height=52};
     var save=new Button{Text="Salvar limite do cliente",Left=16,Top=164,Width=220,Height=36,DialogResult=DialogResult.OK};
     var cancel=new Button{Text="Cancelar",Left=250,Top=164,Width=140,Height=36,DialogResult=DialogResult.Cancel};
     dialog.Controls.AddRange(new Control[]{label,value,periodLabel,period,hint,save,cancel});
     if(dialog.ShowDialog(this)!=DialogResult.OK)return;
     string selectedPeriod=Convert.ToString(period.SelectedItem);
     decimal selectedLimit=value.Value;
     if(MessageBox.Show("Confirmar limite "+selectedPeriod+" de R$ "+selectedLimit.ToString("N2")+" para "+customer+"?","Limite do Fiado",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
     using(var cn=Open(false)){
      string backup=CreditBackupPath(db,"antes-mudar-limite-");
      using(var dest=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=backup}.ToString())){
       dest.Open();cn.BackupDatabase(dest,"main","main",-1,null,0);
       using(var integrity=new SQLiteCommand("PRAGMA integrity_check",dest))
        if(Convert.ToString(integrity.ExecuteScalar())!="ok")throw new Exception("Falha na integridade do backup. Limite nao alterado.");
      }
      if(new FileInfo(backup).Length==0)throw new Exception("Backup vazio; limite nao alterado.");
      using(var tx=cn.BeginTransaction()){
       using(var audit=new SQLiteCommand("INSERT INTO customer_fiado_limit_audit(customer_name,old_limit,old_period,new_limit,new_period,changed_at) VALUES(@c,@ol,@op,@nl,@np,datetime('now','localtime'))",cn,tx)){
        audit.Parameters.AddWithValue("@c",customer);audit.Parameters.AddWithValue("@ol",existing);audit.Parameters.AddWithValue("@op",existingPeriod);audit.Parameters.AddWithValue("@nl",selectedLimit);audit.Parameters.AddWithValue("@np",selectedPeriod);audit.ExecuteNonQuery();
       }
       using(var update=new SQLiteCommand("INSERT OR REPLACE INTO customer_fiado_limits(customer_name,limit_amount,period,changed_at) VALUES(@c,@l,@p,datetime('now','localtime'))",cn,tx)){
        update.Parameters.AddWithValue("@c",customer);update.Parameters.AddWithValue("@l",selectedLimit);update.Parameters.AddWithValue("@p",selectedPeriod);update.ExecuteNonQuery();
       }
       tx.Commit();
      }
     }
    }
    LoadClients();SelectClient(customer);
   }catch(Exception ex){MessageBox.Show("Limite nao salvo: "+ex.Message,"Fiado",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  }
  void PayFiadoWithCredit(){
   if(string.IsNullOrWhiteSpace(customer)||customer=="SEM CLIENTE"){MessageBox.Show("Selecione um cliente.");return;}
   try{
    EnsureCustomerCredit(db);
    decimal available;
    using(var cn=Open(true))available=CustomerCreditBalance(cn,null,customer);
    decimal total=0;
    foreach(DataGridViewRow row in openSales.Rows)
     if(!row.IsNewRow && row.Cells["saldo"].Value!=null && row.Cells["saldo"].Value!=DBNull.Value)
      total+=Math.Max(0,Convert.ToDecimal(row.Cells["saldo"].Value));
    decimal apply=Math.Round(Math.Min(total,available),2);
    if(apply<=0){MessageBox.Show(available<=0?"Este cliente nao tem credito depositado disponivel.":"Este cliente nao tem divida fiada em aberto.","Fiado");return;}
    int creditIndex=method.Items.IndexOf("CREDITO DEPOSITADO");
    if(creditIndex<0)throw new Exception("Forma de credito depositado indisponivel.");
    saleTabs.SelectedIndex=0;
    method.SelectedIndex=creditIndex;
    amount.Value=Math.Min(amount.Maximum,apply);
    PayAll();
   }catch(Exception ex){MessageBox.Show("Nao foi possivel aplicar credito: "+ex.Message,"Fiado",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  }
  void PayAll(){
   if(string.IsNullOrWhiteSpace(customer)){MessageBox.Show("Selecione um cliente.");return;}
   if(saleTabs!=null&&saleTabs.SelectedIndex!=0){MessageBox.Show("Abra Fiado atual - em aberto.");return;}
   var rows=new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<long,DateTime>>();
   foreach(DataGridViewRow row in openSales.Rows){
    if(row.IsNewRow||row.Cells["venda"].Value==null)continue;
    DateTime time;
    if(!DateTime.TryParse(Convert.ToString(row.Cells["data"].Value),out time))time=DateTime.MinValue;
    rows.Add(new System.Collections.Generic.KeyValuePair<long,DateTime>(Convert.ToInt64(row.Cells["venda"].Value),time));
   }
   rows.Sort((a,b)=>{int cmp=a.Value.CompareTo(b.Value);return cmp!=0?cmp:a.Key.CompareTo(b.Key);});
   var ids=new System.Collections.Generic.List<long>();
   foreach(var row in rows)ids.Add(row.Key);
   ReceivePayments(ids,amount.Value,"de todas as vendas (mais antigas primeiro)");
  }
  void Pay(){
   if(saleTabs!=null&&saleTabs.SelectedIndex!=0){MessageBox.Show("Selecione Fiado atual - em aberto.");return;}
   var ids=SelectedPendingSaleIds();
   ReceivePayments(ids,amount.Value,ids.Count>1?"das vendas selecionadas":"da venda selecionada");
  }

 }
}
