using System;
using System.Data;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
namespace ArmazemGratidao {
 public sealed class FiadoClientesForm:StyledDialogForm {
  readonly string db;
  readonly FlowLayoutPanel clients=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.LeftToRight,WrapContents=true,Padding=new Padding(8)};
  readonly DataGridView openSales=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
  readonly DataGridView paymentHistory=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
  readonly DataGridView paidSales=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
  readonly Label selected=new Label {Dock=DockStyle.Top,Height=42,Padding=new Padding(7,4,5,2),Font=new Font("Segoe UI",10,FontStyle.Bold),AutoEllipsis=true};
  readonly NumericUpDown amount=new NumericUpDown {DecimalPlaces=2,Maximum=100000000,Width=130};
  readonly NumericUpDown creditDepositAmount=new NumericUpDown {DecimalPlaces=2,Maximum=100000000,Width=135};
  readonly ComboBox method=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=160};
  string customer="";
  string clientMarkedForRemoval="";
  bool refreshingClientSales=false;
  readonly Label receivedBalance=new Label {Text="Saldo selecionado: R$ 0,00",AutoSize=true,Padding=new Padding(5,9,0,0),Font=new Font("Segoe UI",10,FontStyle.Bold)};
  TabControl saleTabs;
  Panel clientHome;
  SplitContainer detailSplit;
  readonly TextBox clientSearch=new TextBox {Dock=DockStyle.Top,Height=34,Font=new Font("Segoe UI",11)};
  readonly Label overview=new Label {Dock=DockStyle.Top,Height=88,Font=new Font("Segoe UI",10,FontStyle.Bold),ForeColor=Color.FromArgb(18,51,99),BackColor=Color.FromArgb(229,242,255),TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(8,4,4,4)};
  public FiadoClientesForm(string database) {
   db=database;Text="Armazem Gratidao | Fiado por Cliente";BackColor=Color.FromArgb(229,242,255);StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.Sizable;MaximizeBox=true;MinimizeBox=false;ShowInTaskbar=false;SizeGripStyle=SizeGripStyle.Hide;AutoScaleMode=AutoScaleMode.Font;var area=Screen.PrimaryScreen.WorkingArea;Width=Math.Min(1250,Math.Max(680,area.Width-32));Height=Math.Min(790,Math.Max(520,area.Height-32));MinimumSize=new Size(640,480);
   foreach(var table in new[]{openSales,paidSales,paymentHistory}){
    table.ScrollBars=ScrollBars.Both;table.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
    table.DefaultCellStyle.Format="N2";
   }
   var split=new SplitContainer {Dock=DockStyle.Fill,SplitterDistance=205,IsSplitterFixed=true};
   detailSplit=split;
   split.Panel1.Controls.Add(clients);
   var removeClient=new Button{Name="amzgFiadoRemoveClient",Text="Excluir cliente selecionado",Dock=DockStyle.Bottom,Height=36,BackColor=Color.FromArgb(180,43,57),ForeColor=Color.White,Font=new Font("Segoe UI",9,FontStyle.Bold)};
   removeClient.FlatStyle=FlatStyle.Flat;removeClient.FlatAppearance.BorderSize=0;removeClient.UseVisualStyleBackColor=false;
   removeClient.Click+=(sender,args)=>RemoveClientFromFiadoList();
   split.Panel1.Controls.Add(removeClient);
   split.Panel1.Controls.Add(overview);
   clients.ClientSizeChanged+=(s,e)=>ArrangeClientBoxes();
   clientSearch.TextChanged+=(s,e)=>LoadClients();
   split.Panel1.Controls.Add(clientSearch);
   split.Panel1.Controls.Add(new Label {Text="  PESQUISAR CLIENTE",Dock=DockStyle.Top,Height=28,Font=new Font("Segoe UI",10,FontStyle.Bold),ForeColor=Color.White,BackColor=Color.FromArgb(220,95,105)});
   // As tres opcoes no topo substituem a fileira duplicada de abas.
   var tabs=new TabControl {Dock=DockStyle.Fill,Appearance=TabAppearance.FlatButtons,
    SizeMode=TabSizeMode.Fixed,ItemSize=new Size(1,1)};
   saleTabs=tabs;
   var pending=new TabPage("Fiado atual - em aberto");
   var previous=new TabPage("Fiados anteriores - pagos");
   // A lista de vendas ocupa toda a area. Produtos/pagamentos abrem em janela.
   SetupSaleSelection(openSales,pending,false);
   SetupSaleSelection(paidSales,previous,true);
   openSales.SelectionChanged+=(sender,args)=>UpdatePaymentSelection(true);
   var pendingTip=new Label{Dock=DockStyle.Bottom,Height=23,Text="Clique em Detalhes para ver produtos e pagamentos",
    TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.FromArgb(22,75,160),
    Font=new Font("Segoe UI",8.5f)};
   var paidTip=new Label{Dock=DockStyle.Bottom,Height=23,Text="Clique em Detalhes para consultar a venda paga",
    TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.FromArgb(22,75,160),
    Font=new Font("Segoe UI",8.5f)};
   pending.Controls.Add(pendingTip);previous.Controls.Add(paidTip);
   tabs.TabPages.Add(pending);tabs.TabPages.Add(previous);
   var historyTab=new TabPage("Histórico de recebimentos");historyTab.Controls.Add(paymentHistory);tabs.TabPages.Add(historyTab);
   var switchBar=new FlowLayoutPanel{Name="amzgFiadoTabs",Dock=DockStyle.Top,Height=38,WrapContents=false,AutoScroll=true,Padding=new Padding(3,2,3,2),BackColor=Color.FromArgb(229,242,255)};
   var showOpen=new Button {Text="Em aberto",Width=114,Height=29,BackColor=Color.FromArgb(220,95,105),ForeColor=Color.White};showOpen.Click+=(s,e)=>tabs.SelectedIndex=0;switchBar.Controls.Add(showOpen);
   var showPrevious=new Button {Text="Pagas",Width=86,Height=29,BackColor=Color.White,ForeColor=Color.FromArgb(20,73,158)};showPrevious.Click+=(s,e)=>tabs.SelectedIndex=1;switchBar.Controls.Add(showPrevious);
   var showHistory=new Button{Text="Histórico",Width=102,Height=29,
    BackColor=Color.White,ForeColor=Color.FromArgb(20,73,158)};
   showHistory.Click+=(sender,args)=>tabs.SelectedIndex=2;
   switchBar.Controls.Add(showHistory);
   var backSmall=new Button{Name="amzgFiadoBackClients",Text="← Voltar aos clientes",Width=174,Height=30,FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(220,95,105),ForeColor=Color.White,Font=new Font("Segoe UI",9,FontStyle.Bold)};
   backSmall.FlatAppearance.BorderSize=0;
   backSmall.Click+=(sender,args)=>BackToFiadoClients();
   // Seta para a tela anterior, separada do botao Menu Inicial global.
   var localNav=new Panel{Name="amzgFiadoLocalNav",Dock=DockStyle.Top,Height=38,
    BackColor=Color.FromArgb(228,239,252),Padding=new Padding(5,3,5,3)};
   backSmall.Dock=DockStyle.Left;localNav.Controls.Add(backSmall);
   var removeSelected=new Button{Name="amzgFiadoRemoveSelected",Text="Excluir cliente",Dock=DockStyle.Left,Width=130,BackColor=Color.FromArgb(180,43,57),ForeColor=Color.White};
   removeSelected.FlatStyle=FlatStyle.Flat;removeSelected.FlatAppearance.BorderSize=0;removeSelected.UseVisualStyleBackColor=false;
   removeSelected.Click+=(sender,args)=>RemoveClientFromFiadoList();localNav.Controls.Add(removeSelected);
   var servicesToggle=new Button{Name="amzgFiadoToggleServices",Text="☰ Serviços",
    Dock=DockStyle.Right,Width=133,Height=30,BackColor=Color.FromArgb(22,75,160),
    ForeColor=Color.White,FlatStyle=FlatStyle.Flat,TabStop=false,
    Font=new Font("Segoe UI",9f,FontStyle.Bold)};
   servicesToggle.FlatAppearance.BorderSize=0;
   localNav.Controls.Add(servicesToggle);
   var fixClientName=new Button{Text="Corrigir nome do cliente",Width=220,Height=32,BackColor=Color.FromArgb(22,75,160),ForeColor=Color.White};
   fixClientName.Click+=(sender,args)=>CorrectWholeClientName();
   switchBar.Controls.Add(fixClientName);
   var transferClient=new Button{Text="Trocar por cliente cadastrado",Width=255,Height=32,BackColor=Color.FromArgb(12,113,88),ForeColor=Color.White};
   transferClient.Click+=(sender,args)=>TransferFiadoToRegisteredClient();
   switchBar.Controls.Add(transferClient);
   tabs.SelectedIndexChanged+=(s,e)=>{
    showOpen.BackColor=tabs.SelectedIndex==0?Color.FromArgb(20,73,158):Color.White;
    showOpen.ForeColor=tabs.SelectedIndex==0?Color.White:Color.FromArgb(20,73,158);
    showPrevious.BackColor=tabs.SelectedIndex==1?Color.FromArgb(20,73,158):Color.White;
    showPrevious.ForeColor=tabs.SelectedIndex==1?Color.White:Color.FromArgb(20,73,158);
    showHistory.BackColor=tabs.SelectedIndex==2?Color.FromArgb(20,73,158):Color.White;
    showHistory.ForeColor=tabs.SelectedIndex==2?Color.White:Color.FromArgb(20,73,158);
   };
   var bottom=new Panel {Dock=DockStyle.Bottom,Height=225,Padding=new Padding(6),BackColor=Color.FromArgb(235,241,250)};
   var paymentRow=new FlowLayoutPanel{Dock=DockStyle.Top,Height=82,WrapContents=true,AutoScroll=true};
   var toolsRow=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=true,AutoScroll=true};
   var paymentHeader=new Label{Text="RECEBIMENTO DE FIADO",Dock=DockStyle.Top,Height=25,Font=new Font("Segoe UI",10,FontStyle.Bold),ForeColor=Color.DarkRed};
   var toolsHeader=new Label{Text="▾ CONSULTAS E AJUSTES (clique para ocultar)",Dock=DockStyle.Top,Height=28,Cursor=Cursors.Hand,Font=new Font("Segoe UI",10,FontStyle.Bold),ForeColor=Color.DarkRed};
   bottom.Controls.Add(toolsRow);bottom.Controls.Add(toolsHeader);bottom.Controls.Add(paymentRow);bottom.Controls.Add(paymentHeader);
   toolsRow.Visible=true;
   toolsHeader.Click+=(sender,args)=>{toolsRow.Visible=!toolsRow.Visible;bottom.Height=toolsRow.Visible?225:125;toolsHeader.Text=(toolsRow.Visible?"▾":"▸")+" CONSULTAS E AJUSTES (clique para "+(toolsRow.Visible?"ocultar":"abrir")+")";};

   paymentRow.Controls.Add(new Label {Text="Forma de pagamento:",AutoSize=true,Padding=new Padding(0,9,0,0)});
   method.Items.AddRange(new object[]{"DINHEIRO","PIX","PIX QR CODE","DEBITO","CREDITO","ALIMENTACAO","ALELO","PLUXXE","TICKET","VR","CREDITO DEPOSITADO"});method.SelectedIndex=0;paymentRow.Controls.Add(method);
   paymentRow.Controls.Add(new Label {Text="Valor R$:",AutoSize=true,Padding=new Padding(0,9,0,0)});paymentRow.Controls.Add(amount);
   paymentRow.Controls.Add(receivedBalance);
   var fillAll=new Button {Text="Preencher saldo de todas",AutoSize=true,Height=36};fillAll.Click+=(s,e)=>FillAllBalances();paymentRow.Controls.Add(fillAll);
   var pay=new Button {Text="Registrar pagamento",AutoSize=true,Height=36,BackColor=Color.FromArgb(24,82,170),ForeColor=Color.White};pay.Click+=(s,e)=>Pay();paymentRow.Controls.Add(pay);
   var saveCredit=new Button {Text="Guardar credito (deposito)",AutoSize=true,Height=36,BackColor=Color.FromArgb(14,112,122),ForeColor=Color.White};saveCredit.Click+=(s,e)=>DepositCustomerCredit();toolsRow.Controls.Add(saveCredit);
   var creditLimit=new Button {Text="Definir limite semanal / mensal",AutoSize=true,Height=36,BackColor=Color.FromArgb(24,82,170),ForeColor=Color.White};creditLimit.Click+=(s,e)=>ConfigureClientCreditLimit();toolsRow.Controls.Add(creditLimit);
   var creditHistory=new Button {Text="Historico de credito",AutoSize=true,Height=36};creditHistory.Click+=(s,e)=>ShowCustomerCreditHistory();toolsRow.Controls.Add(creditHistory);
   var settle=new Button {Text="Receber nas vendas do cliente",AutoSize=true,Height=36,BackColor=Color.FromArgb(12,110,80),ForeColor=Color.White};settle.Click+=(s,e)=>PayAll();paymentRow.Controls.Add(settle);
   var rename=new Button {Text="Corrigir cliente da venda",AutoSize=true,Height=36,BackColor=Color.FromArgb(98,60,154),ForeColor=Color.White};rename.Click+=(s,e)=>CorrectCustomer();toolsRow.Controls.Add(rename);
   var fixDate=new Button {Text="Corrigir data do pagamento",AutoSize=true,Height=36,BackColor=Color.FromArgb(95,64,145),ForeColor=Color.White};fixDate.Click+=(s,e)=>CorrectPaymentDate();toolsRow.Controls.Add(fixDate);
   var audit=new Button {Text="Conferir saldos",AutoSize=true,Height=36,BackColor=Color.FromArgb(145,73,32),ForeColor=Color.White};audit.Click+=(s,e)=>AuditBalances();toolsRow.Controls.Add(audit);
   var inspect=new Button {Text="Itens e pagamentos da venda",AutoSize=true,Height=36};inspect.Click+=(s,e)=>ShowSaleDetails();toolsRow.Controls.Add(inspect);
   var auditHistory=new Button {Text="Histórico de alterações",AutoSize=true,Height=36,BackColor=Color.FromArgb(80,60,125),ForeColor=Color.White};auditHistory.Click+=(s,e)=>ShowItemAuditHistory();toolsRow.Controls.Add(auditHistory);
   var statement=new Button {Text="Extrato do cliente / WhatsApp",AutoSize=true,Height=36,BackColor=Color.FromArgb(15,119,78),ForeColor=Color.White};statement.Click+=(s,e)=>SendCustomerStatement();toolsRow.Controls.Add(statement);
   var receipt=new Button {Text="Cupom / WhatsApp",AutoSize=true,Height=36,BackColor=Color.FromArgb(15,119,78),ForeColor=Color.White};receipt.Click+=(s,e)=>SendReceipt();toolsRow.Controls.Add(receipt);
   var refresh=new Button {Text="Atualizar",AutoSize=true,Height=36};refresh.Click+=(s,e)=>LoadClients();toolsRow.Controls.Add(refresh);
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
   // Mover os comandos existentes para grupos por servico sem perder nenhum Click.
   var content=new Panel{Name="amzgFiadoClientWorkspace",Dock=DockStyle.Fill,BackColor=Color.White};
   // O MESMO LAYOUT do menu Iniciar: categorias azuis à esquerda e ramificações à direita.
   // As ações de Fiado continuam sendo os controles originais, com seus handlers.
   var actionList=new FlowLayoutPanel{Name="amzgFiadoServiceClasses",Dock=DockStyle.Fill,
    FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,
    Padding=new Padding(6,5,4,5),BackColor=Color.FromArgb(29,47,79)};
   var categoryPane=new Panel{Name="amzgFiadoMenuCategories",Dock=DockStyle.Left,
    Width=201,BackColor=Color.FromArgb(18,52,107)};
   var menuTitle=new Label{Text="  ☰  FIADO | SERVIÇOS",Dock=DockStyle.Top,Height=49,
    BackColor=Color.FromArgb(12,39,90),ForeColor=Color.White,
    Font=new Font("Segoe UI",9.5f,FontStyle.Bold),
    TextAlign=ContentAlignment.MiddleLeft};
   var closeMenu=new Button{Name="amzgFiadoCloseMenu",Text="×  Fechar menu",
    Dock=DockStyle.Bottom,Height=34,BackColor=Color.FromArgb(20,37,67),
    ForeColor=Color.White,Font=new Font("Segoe UI",8.5f,FontStyle.Bold),
    TextAlign=ContentAlignment.MiddleLeft,FlatStyle=FlatStyle.Flat};
   closeMenu.FlatAppearance.BorderSize=0;
   categoryPane.Controls.Add(actionList);
   categoryPane.Controls.Add(menuTitle);
   categoryPane.Controls.Add(closeMenu);
   var branchPane=new Panel{Name="amzgFiadoServiceFlyout",Dock=DockStyle.Fill,
    Visible=false,BackColor=Color.FromArgb(246,249,254),
    Padding=new Padding(7,6,6,6)};
   var branchTitle=new Label{Name="amzgFiadoBranchTitle",Dock=DockStyle.Top,Height=36,
    ForeColor=Color.FromArgb(22,75,160),
    Font=new Font("Segoe UI",9f,FontStyle.Bold)};
   var serviceContent=new Panel{Name="amzgFiadoBranchContent",Dock=DockStyle.Fill,
    BackColor=Color.White};
   branchPane.Controls.Add(serviceContent);
   branchPane.Controls.Add(branchTitle);
   var side=new Panel{Name="amzgFiadoCommandsRight",Dock=DockStyle.None,
    Width=201,BackColor=Color.FromArgb(18,52,107),Visible=false,
    BorderStyle=BorderStyle.FixedSingle};
   side.Controls.Add(branchPane);
   side.Controls.Add(categoryPane);
   // Apenas uma categoria ativa de cada vez, exatamente como no menu Iniciar.
   var sectionLabels=new[]{"RECEBIMENTOS","CREDITO E LIMITE",
    "CORRECOES","CONSULTAS E DOCUMENTOS"};
   var groups=new []{
    FiadoServiceGroup("RECEBIMENTOS",true,
     new Label{Text="Forma de pagamento"},method,
     new Label{Text="Valor R$"},amount,receivedBalance,fillAll,pay,settle),
    FiadoServiceGroup("CREDITO E LIMITE",true,
     new Label{Text="Credito R$"},creditDepositAmount,creditSaveTop,
     limitTop,adjustTop,creditPayTop,creditExtractTop),
    FiadoServiceGroup("CORRECOES",true,fixClientName,transferClient,rename,fixDate),
    FiadoServiceGroup("CONSULTAS E DOCUMENTOS",true,
     inspect,audit,auditHistory,statement,receipt,refresh)
   };
   var categoryButtons=new List<Button>();
   Action fitServiceMenu=null;
   Action<int> selectCategory=index=>{
    for(int i=0;i<groups.Length;i++){
     groups[i].Visible=i==index;
     categoryButtons[i].BackColor=i==index?
      Color.FromArgb(30,117,218):Color.FromArgb(23,81,169);
    }
    branchTitle.Text=sectionLabels[index]+"  ›";
    branchPane.Visible=true;
    if(fitServiceMenu!=null)fitServiceMenu();
    groups[index].BringToFront();
    branchPane.BringToFront();
   };
   for(int i=0;i<sectionLabels.Length;i++){
    int index=i;
    var category=new Button{Name="amzgFiadoCategory"+i,
     Text="  "+sectionLabels[i]+"  ›",
     Size=new Size(186,42),MinimumSize=new Size(186,42),
     MaximumSize=new Size(186,42),Margin=new Padding(0,2,0,2),
     BackColor=Color.FromArgb(23,81,169),ForeColor=Color.White,
     TextAlign=ContentAlignment.MiddleLeft,FlatStyle=FlatStyle.Flat,
     AutoEllipsis=true,UseMnemonic=false,
     Font=new Font("Segoe UI",8.3f,FontStyle.Bold)};
    category.FlatAppearance.BorderSize=0;
    category.Click+=(sender,args)=>selectCategory(index);
    categoryButtons.Add(category);
    actionList.Controls.Add(category);
    groups[i].Dock=DockStyle.Fill;
    groups[i].Visible=false;
    serviceContent.Controls.Add(groups[i]);
   }
   Action closeServiceMenu=()=>{
    side.Visible=false;
    branchPane.Visible=false;
    servicesToggle.Text="☰ Serviços";
   };
   closeMenu.Click+=(sender,args)=>closeServiceMenu();
   servicesToggle.Click+=(sender,args)=>{
    if(side.Visible){closeServiceMenu();return;}
    branchPane.Visible=false;
    foreach(var group in groups)group.Visible=false;
    foreach(var category in categoryButtons)category.BackColor=Color.FromArgb(23,81,169);
    side.Visible=true;
    servicesToggle.Text="✕ Fechar menu";
    if(fitServiceMenu!=null)fitServiceMenu();
    side.BringToFront();
   };
   fitServiceMenu=()=>{
    int w=content.ClientSize.Width,h=content.ClientSize.Height;
    if(w<=0||h<=0)return;
    int width=Math.Min(w,branchPane.Visible?463:201);
    side.Bounds=new Rectangle(Math.Max(0,w-width),0,width,h);
   };
   content.Resize+=(sender,args)=>fitServiceMenu();
   // Ao abrir, o menu flutua SOBRE a lista e nao fica atras do Dock.Fill.
   content.Controls.Add(tabs);
   content.Controls.Add(side);
   side.BringToFront();fitServiceMenu();
   split.Panel2.Controls.Add(content);
   split.Panel2.Controls.Add(switchBar);
   split.Panel2.Controls.Add(selected);
   split.Panel2.Controls.Add(localNav);
   foreach(var table in new[]{openSales,paidSales,paymentHistory}){table.RowTemplate.Height=34;table.RowHeadersVisible=false;table.EnableHeadersVisualStyles=false;table.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(220,95,105);table.ColumnHeadersDefaultCellStyle.ForeColor=Color.White;table.DefaultCellStyle.SelectionBackColor=Color.FromArgb(26,88,166);table.DefaultCellStyle.SelectionForeColor=Color.White;table.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(239,245,252);table.Font=new Font("Segoe UI",10);table.SelectionMode=DataGridViewSelectionMode.FullRowSelect;}
   // Acao por venda, disponivel tanto nas vendas pagas quanto nas vendas em aberto.
   foreach(var table in new[]{openSales,paidSales}){
    if(!table.Columns.Contains("ReabrirVenda")){
     table.Columns.Add(new DataGridViewButtonColumn{
      Name="ReabrirVenda",HeaderText="Correcao",Text="Reabrir venda",
      UseColumnTextForButtonValue=true,Width=145,MinimumWidth=130,
      AutoSizeMode=DataGridViewAutoSizeColumnMode.None});
    }
    table.CellContentClick+=(sender,args)=>{
     if(args.RowIndex<0||args.ColumnIndex<0||
      table.Columns[args.ColumnIndex].Name!="ReabrirVenda")return;
     long saleId;
     if(!table.Columns.Contains("venda")||
      !long.TryParse(Convert.ToString(table.Rows[args.RowIndex].Cells["venda"].Value),out saleId)){
      MessageBox.Show("Nao foi possivel identificar esta venda.");return;
     }
     if(ReopenSalePayments(saleId)){SelectClient(customer);saleTabs.SelectedIndex=0;}
    };
   }
   openSales.CellDoubleClick+=(s,e)=>{if(e.RowIndex>=0){saleTabs.SelectedIndex=0;ShowSaleDetails();}};
   paidSales.CellDoubleClick+=(s,e)=>{if(e.RowIndex>=0){saleTabs.SelectedIndex=1;ShowSaleDetails();}};
   Controls.Add(split);
   clientHome=new Panel {Dock=DockStyle.Fill,BackColor=Color.White};
   var homeHeader=new Label {Dock=DockStyle.Top,Height=70,Text="FIADO  |  SELECIONE UM CLIENTE",Font=new Font("Segoe UI",17,FontStyle.Bold),ForeColor=Color.FromArgb(20,73,158),TextAlign=ContentAlignment.MiddleCenter};
   clientHome.Controls.Add(new Label {Dock=DockStyle.Fill,Text="Clique no nome do cliente à esquerda para consultar vendas, itens, pagamentos e emitir cupom.",Font=new Font("Segoe UI",12),TextAlign=ContentAlignment.MiddleCenter});
   clientHome.Controls.Add(homeHeader);
   split.Panel2.Controls.Add(clientHome);clientHome.BringToFront();

   split.Panel2Collapsed=true;
   using(var conn=Open())using(var cmd=new SQLiteCommand("CREATE TABLE IF NOT EXISTS fiado_hidden_clients (customer_name TEXT PRIMARY KEY COLLATE NOCASE)",conn))cmd.ExecuteNonQuery();
   LoadClients();
  }
  SQLiteConnection Open(bool readOnly=false) {var c=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=db,ReadOnly=readOnly,FailIfMissing=true}.ToString());c.Open();return c;}
  DataTable Query(string sql,string p=null,object value=null) {
   using(var c=Open(true))using(var cmd=new SQLiteCommand(sql,c)) {
    if(p!=null)cmd.Parameters.AddWithValue(p,value);
    var dt=new DataTable();using(var a=new SQLiteDataAdapter(cmd))a.Fill(dt);return dt;
   }
  }
  const string CreditFilter="(UPPER(COALESCE(s.payment_method,'')) LIKE '%FIADO%' OR UPPER(COALESCE(s.status,'')) LIKE '%FIADO%' OR EXISTS (SELECT 1 FROM sale_payments fp WHERE fp.sale_id=s.id AND UPPER(TRIM(COALESCE(fp.payment_method,''))) LIKE '%FIADO%'))";
  const string Balance="COALESCE(s.gross_total,0)-COALESCE((SELECT SUM(p.amount) FROM credit_payments p WHERE p.sale_id=s.id),0)";
  static bool InternalFiado(string name){
   string normalized=string.Join(" ",(name??"").Trim().ToUpperInvariant().Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries));
   return normalized=="EMERSON SIMONETTI GILIOTI" || normalized=="EMERSON SIMONETTI GIGLIOTTI"
    || normalized=="EMERSON SIMONE GILIOTI" || normalized=="EMERSON SIMONE GIGLIOTTI"
    || normalized=="LUIZ PAULO GILIOTI" || normalized=="LUIZ PAULO GIGLIOTTI";
  }
  // A grade interna continua existente para pagamentos e selecao multipla.
  // A interface passa a exibir cartoes compactos, responsivos e legiveis.
  void SetupSaleSelection(DataGridView grid, Control panel, bool paid){
   var mark=new DataGridViewCheckBoxColumn{
    Name="marcar",HeaderText="Selecionar",ReadOnly=false,Width=70,
    AutoSizeMode=DataGridViewAutoSizeColumnMode.None};
   grid.Columns.Add(mark);
   grid.ReadOnly=false;grid.MultiSelect=true;
   grid.CellClick+=(sender,e)=>{
    if(e.RowIndex<0||e.ColumnIndex<0||grid.Columns[e.ColumnIndex].Name=="marcar")return;
    var cell=grid.Rows[e.RowIndex].Cells["marcar"];
    cell.Value=!Convert.ToBoolean(cell.Value??false);
   };
   grid.DataBindingComplete+=(sender,e)=>{
    foreach(DataGridViewColumn col in grid.Columns) {
     col.ReadOnly=col.Name!="marcar";
     if(col.Name=="valor"||col.Name=="pago"||col.Name=="saldo")
      col.DefaultCellStyle.Format="N2";
    }
   };
   var bar=new FlowLayoutPanel{Name=paid?"amzgFiadoPaidTop":"amzgFiadoOpenTop",
    Dock=DockStyle.Top,Height=39,WrapContents=false,AutoScroll=true,
    Padding=new Padding(5,2,5,2),BackColor=Color.FromArgb(231,240,252)};
   var all=new CheckBox{Text="Selecionar todas",AutoSize=true,
    Font=new Font("Segoe UI",9,FontStyle.Bold),Margin=new Padding(4,6,11,0)};
   var total=new Label{Text="Selecionado: R$ 0,00",AutoSize=true,
    Font=new Font("Segoe UI",9,FontStyle.Bold),Margin=new Padding(3,8,0,0)};
   bar.Controls.Add(all);bar.Controls.Add(total);
   if(paid){
    var reopenPaid=new Button{Name="amzgFiadoReopenPaid",Text="Reabrir venda / Voltar ao Fiado aberto",
     Width=290,Height=31,BackColor=Color.FromArgb(160,55,35),ForeColor=Color.White};
    reopenPaid.Click+=(sender,args)=>{
     grid.EndEdit();
     DataGridViewRow chosen=grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(row=>
      !row.IsNewRow&&Convert.ToBoolean(row.Cells["marcar"].Value??false));
     if(chosen==null)chosen=grid.CurrentRow;
     if(chosen==null||chosen.IsNewRow){MessageBox.Show("Selecione uma venda paga para reabrir.");return;}
     long saleId;
     if(!long.TryParse(Convert.ToString(chosen.Cells["venda"].Value),out saleId)){
      MessageBox.Show("Venda paga invalida.");return;
     }
     if(ReopenSalePayments(saleId)){
      SelectClient(customer);
      saleTabs.SelectedIndex=0;
     }
    };
    bar.Controls.Add(reopenPaid);
   }
   var cards=new FlowLayoutPanel {
    Name=paid?"amzgFiadoCardsPaid":"amzgFiadoCardsOpen",
    Dock=DockStyle.Fill,AutoScroll=true,WrapContents=false,
    FlowDirection=FlowDirection.TopDown,Padding=new Padding(4,3,4,3),
    BackColor=Color.FromArgb(250,252,255)};
   Action sum=()=>{
    decimal amount=0;
    foreach(DataGridViewRow row in grid.Rows){
     if(row.IsNewRow||!Convert.ToBoolean(row.Cells["marcar"].Value??false))continue;
     object value=row.Cells[paid?"valor":"saldo"].Value;
     if(value!=null&&value!=DBNull.Value)amount+=Convert.ToDecimal(value);
    }
    total.Text="Selecionado: R$ "+amount.ToString("N2");
    if(!paid&&grid==openSales)UpdatePaymentSelection(true);
   };
   Action refreshCards=null;
   refreshCards=()=>{
    if(cards.IsDisposed||grid.IsDisposed)return;
    cards.SuspendLayout();
    try{
     cards.Controls.Clear();
     cards.AutoScrollPosition=new Point(0,0);
     int width=Math.Max(210,cards.ClientSize.Width-cards.Padding.Horizontal-SystemInformation.VerticalScrollBarWidth-6);
     foreach(DataGridViewRow row in grid.Rows){
      if(row.IsNewRow||row.Cells["venda"].Value==null)continue;
      var selectedRow=row;
      long saleId=Convert.ToInt64(row.Cells["venda"].Value);
      string time=Convert.ToString(row.Cells["data"].Value);
      DateTime parsed;
      if(DateTime.TryParse(time,out parsed))time=parsed.ToString("dd/MM/yyyy HH:mm");
      string status=paid?"PAGA":"EM ABERTO";
      decimal gross=Convert.ToDecimal(row.Cells["valor"].Value);
      decimal received=Convert.ToDecimal(row.Cells["pago"].Value);
      decimal open=paid?0m:Convert.ToDecimal(row.Cells["saldo"].Value);
      var card=new Panel{Name="amzgFiadoSaleCard",Width=width,Height=78,
       BackColor=Color.White,BorderStyle=BorderStyle.FixedSingle,
       Margin=new Padding(0,0,0,3)};
      var check=new CheckBox{Name="amzgFiadoCardSelect",Left=9,Top=10,Width=24,Height=24,
       Checked=Convert.ToBoolean(row.Cells["marcar"].Value??false)};
      var title=new Label{Name="amzgFiadoCardTitle",Left=37,Top=4,
       Width=Math.Max(100,width-170),Height=22,
       Text="Venda #"+saleId+"  •  "+time,AutoEllipsis=true,
       Font=new Font("Segoe UI",9.4f,FontStyle.Bold),
       ForeColor=Color.FromArgb(22,70,142)};
      var figures=new Label{Name="amzgFiadoCardAmounts",Left=37,Top=27,
       Width=Math.Max(100,width-46),Height=43,AutoEllipsis=true,
       Text="Total R$ "+gross.ToString("N2")+"   |   Pago R$ "+received.ToString("N2")+
        Environment.NewLine+"Saldo R$ "+open.ToString("N2")+
        (string.IsNullOrWhiteSpace(status)?"":"   • "+status),
       Font=new Font("Segoe UI",8.8f,FontStyle.Regular),
       ForeColor=Color.FromArgb(36,54,77)};
      var details=new Button{Name="amzgFiadoCardDetails",
       Text="Detalhes",Width=101,Height=27,
       Left=Math.Max(105,width-111),Top=4,FlatStyle=FlatStyle.Flat,
       BackColor=Color.FromArgb(22,75,160),ForeColor=Color.White,
       Font=new Font("Segoe UI",8.5f,FontStyle.Bold)};
      details.FlatAppearance.BorderSize=0;
      Action choose=()=>{
       if(selectedRow.DataGridView==null)return;
       if(grid.Columns.Contains("venda"))grid.CurrentCell=selectedRow.Cells["venda"];
       saleTabs.SelectedIndex=paid?1:0;
       ShowSaleDetails();
      };
      details.Click+=(sender,e)=>choose();
      title.DoubleClick+=(sender,e)=>choose();
      figures.DoubleClick+=(sender,e)=>choose();
      card.DoubleClick+=(sender,e)=>choose();
      check.CheckedChanged+=(sender,e)=>{
       if(selectedRow.DataGridView!=null){
        selectedRow.Cells["marcar"].Value=check.Checked;sum();
       }
      };
      card.Controls.Add(check);card.Controls.Add(title);card.Controls.Add(figures);
      card.Controls.Add(details);cards.Controls.Add(card);
     }
     if(cards.Controls.Count==0){
      cards.Controls.Add(new Label{Text=paid?"Nenhuma venda paga.":"Nenhuma venda em aberto.",
       Font=new Font("Segoe UI",10,FontStyle.Bold),
       ForeColor=Color.FromArgb(44,86,145),AutoSize=true,
       Margin=new Padding(10,17,8,7)});
     }
    }finally{cards.ResumeLayout(true);}
   };
   cards.ClientSizeChanged+=(sender,e)=>{
    int width=Math.Max(210,cards.ClientSize.Width-cards.Padding.Horizontal-SystemInformation.VerticalScrollBarWidth-6);
    foreach(Control card in cards.Controls)if(card is Panel){
     card.Width=width;
     var details=card.Controls.Find("amzgFiadoCardDetails",false).FirstOrDefault();
     var title=card.Controls.Find("amzgFiadoCardTitle",false).FirstOrDefault();
     var figures=card.Controls.Find("amzgFiadoCardAmounts",false).FirstOrDefault();
     if(details!=null)details.Left=Math.Max(105,width-details.Width-10);
     if(title!=null)title.Width=Math.Max(100,width-170);
     if(figures!=null)figures.Width=Math.Max(100,width-46);
    }
   };
   all.CheckedChanged+=(sender,e)=>{
    foreach(DataGridViewRow row in grid.Rows)
     if(!row.IsNewRow)row.Cells["marcar"].Value=all.Checked;
    sum();refreshCards();
   };
   grid.CurrentCellDirtyStateChanged+=(sender,e)=>{
    if(grid.IsCurrentCellDirty)grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
   };
   grid.CellValueChanged+=(sender,e)=>{
    if(e.RowIndex>=0&&e.ColumnIndex==grid.Columns["marcar"].Index)sum();
   };
   grid.DataBindingComplete+=(sender,e)=>{
    all.Checked=false;sum();refreshCards();
   };
   // Colocar a grade fora da vista sem eliminar a logica de baixa de fiado.
   // Separar fisicamente o cabecalho da lista: Dock.Fill nao pode cobrir a primeira venda.
   var saleLayout=new TableLayoutPanel{Name=paid?"amzgFiadoPaidLayout":"amzgFiadoOpenLayout",
    Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=Padding.Empty,Padding=Padding.Empty};
   saleLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   saleLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,42));
   saleLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   bar.Dock=DockStyle.Fill;cards.Dock=DockStyle.Fill;
   saleLayout.Controls.Add(bar,0,0);saleLayout.Controls.Add(cards,0,1);
   panel.Controls.Add(saleLayout);
   panel.Controls.Add(grid);
   grid.Visible=false;grid.Dock=DockStyle.None;grid.Bounds=new Rectangle(0,0,1,1);
   saleLayout.BringToFront();
  }
  void BackToFiadoClients(){
   customer="";
   var toggle=Controls.Find("amzgFiadoToggleServices",true).OfType<Button>().FirstOrDefault();
   var back=Controls.Find("amzgFiadoBackClients",true).OfType<Button>().FirstOrDefault();
   var menu=Controls.Find("amzgFiadoCommandsRight",true).OfType<Panel>().FirstOrDefault();
   if(menu!=null)menu.Visible=false;
   if(toggle!=null){toggle.Visible=false;toggle.Text="☰ Serviços";}
   if(back!=null)back.Visible=false;
   detailSplit.Panel2Collapsed=true;detailSplit.Panel1Collapsed=false;
   clientHome.Visible=true;clientHome.BringToFront();
   LoadClients();clientSearch.Focus();
  }
  // Menu de categorias recolhiveis, semelhante ao Iniciar.
  static Panel FiadoServiceGroup(string title,bool expanded,params Control[] controls){
   var group=new Panel{Name="amzgFiadoSection"+title.Replace(" ",""),Width=225,
    BackColor=Color.White,Margin=new Padding(2,2,2,7)};
   var header=new Button{Name="amzgFiadoSectionHeader",Dock=DockStyle.Top,Height=37,
    TextAlign=ContentAlignment.MiddleLeft,ForeColor=Color.White,
    BackColor=Color.FromArgb(22,75,160),FlatStyle=FlatStyle.Flat,
    Font=new Font("Segoe UI",8.7f,FontStyle.Bold)};
   header.FlatAppearance.BorderSize=0;
   var body=new FlowLayoutPanel{Name="amzgFiadoSectionContent",Dock=DockStyle.Fill,
    Padding=new Padding(4,4,3,4),FlowDirection=FlowDirection.TopDown,
    WrapContents=false,AutoScroll=true};
   foreach(Control c in controls){
    c.Dock=DockStyle.None;c.Margin=new Padding(2,2,2,3);
    if(c is Button){
     var button=(Button)c;button.AutoSize=false;button.AutoEllipsis=true;
     button.Height=30;button.Font=new Font("Segoe UI",8.3f,FontStyle.Bold);
     button.FlatStyle=FlatStyle.Flat;button.FlatAppearance.BorderSize=0;
    }else if(c is Label){
     var label=(Label)c;label.AutoSize=false;label.AutoEllipsis=true;
     label.Height=label.Text.StartsWith("Saldo selecionado:")?48:23;
    }else if(c is NumericUpDown)c.Height=28;
    body.Controls.Add(c);
   }
   int openHeight=header.Height+body.Padding.Vertical+5;
   foreach(Control c in body.Controls)openHeight+=c.Height+c.Margin.Vertical;
   Action refresh=()=>{group.Height=expanded?openHeight:header.Height;
    body.Visible=expanded;header.Text=(expanded?"▾ ":"▸ ")+title;};
   header.Click+=(sender,args)=>{expanded=!expanded;refresh();};
   group.Controls.Add(body);group.Controls.Add(header);
   group.Resize+=(sender,args)=>{
    int width=Math.Max(130,group.ClientSize.Width-body.Padding.Horizontal-13);
    foreach(Control c in body.Controls)c.Width=width;
   };
   refresh();
   return group;
  }
  static void CompactFiadoItemGrid(DataGridView grid){
   grid.ScrollBars=ScrollBars.Both;grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.None;
   grid.DataBindingComplete+=(sender,args)=>{
    foreach(DataGridViewColumn col in grid.Columns){
     switch(col.Name.ToLowerInvariant()){
      case "produto":col.Width=185;break;
      case "quantidade":col.Width=85;break;
      case "preco":col.Width=86;break;
      case "valor":col.Width=92;break;
      default:col.Width=105;break;
     }
    }
   };
  }
  void ArrangeClientBoxes(){
   // Cartões compactos, uniformes e sem esticar para ocupar toda a largura.
   const int width=158;
   const int height=49;
   const int gap=9;
   clients.SuspendLayout();
   foreach(Control control in clients.Controls){
    var button=control as Button;
    if(button==null)continue;
    button.AutoSize=false;
    button.MinimumSize=new Size(width,height);
    button.MaximumSize=new Size(width,height);
    button.Size=new Size(width,height);
    button.Margin=new Padding(0,0,gap,gap);
   }
   clients.ResumeLayout();
  }
  void RemoveClientFromFiadoList(){
   if(string.IsNullOrWhiteSpace(clientMarkedForRemoval)){
    MessageBox.Show("Clique primeiro no cliente que deseja excluir da lista.","Fiado");return;
   }
   string name=clientMarkedForRemoval;
   if(MessageBox.Show("Retirar "+name+" da lista de clientes do Fiado?\\nAs vendas, pagamentos e saldos serão preservados no banco de dados.",
    "Confirmar exclusão da lista",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
   try{
    using(var conn=Open()){
     using(var cmd=new SQLiteCommand("INSERT OR IGNORE INTO fiado_hidden_clients(customer_name) VALUES (@name)",conn)){
      cmd.Parameters.AddWithValue("@name",name);cmd.ExecuteNonQuery();
     }
    }
    clientMarkedForRemoval="";customer="";LoadClients();
   }catch(Exception ex){MessageBox.Show(ex.Message,"Excluir cliente");}
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
    var hidden=Query("SELECT customer_name FROM fiado_hidden_clients");
    var hiddenNames=new HashSet<string>(hidden.Rows.Cast<DataRow>().Select(x=>Convert.ToString(x[0])),StringComparer.OrdinalIgnoreCase);
    clients.SuspendLayout();clients.Controls.Clear();
    decimal allBalance=0,internalBalance=0;
    bool selectedClientStillHasFiado=false;
    foreach(DataRow r in dt.Rows) {
     var name=Convert.ToString(r["cliente"]);var balance=Convert.ToDecimal(r["saldo"]);
     // Nunca ocultar clientes com saldo devedor: a lista inicial deve refletir o historico.
     // A exclusao visual so se aplica a contas sem saldo pendente.
     if(hiddenNames.Contains(name) && balance<=0.005m)continue;
     if(string.Equals(name,customer,StringComparison.OrdinalIgnoreCase))selectedClientStillHasFiado=true;
     if(InternalFiado(name))internalBalance+=balance;else allBalance+=balance;
     if(!name.ToUpperInvariant().Contains(clientSearch.Text.Trim().ToUpperInvariant()))continue;
     var b=new Button {Text=name+"  |  R$ "+balance.ToString("N2"),Size=new Size(158,49),AutoSize=false,AutoEllipsis=true,TextAlign=ContentAlignment.MiddleCenter,Font=new Font("Segoe UI",9,FontStyle.Bold),BackColor=Color.White,ForeColor=Color.FromArgb(25,75,150),Tag=name};
     b.Click+=(sender,args)=>{clientMarkedForRemoval=Convert.ToString(((Button)sender).Tag);SelectClient(clientMarkedForRemoval);};
     b.MouseDown+=(sender,args)=>{clientMarkedForRemoval=Convert.ToString(((Button)sender).Tag);};
     clients.Controls.Add(b);
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
   // Sempre mostrar os comandos na barra do topo, nunca dentro do painel oculto.
   var clientBack=Controls.Find("amzgFiadoBackClients",true).OfType<Button>().FirstOrDefault();
   var services=Controls.Find("amzgFiadoToggleServices",true).OfType<Button>().FirstOrDefault();
   if(clientBack!=null)clientBack.Visible=true;
   if(services!=null)services.Visible=true;
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
    selected.Text="FIADO | "+name+"   •   Em aberto R$ "+total.ToString("N2")+
      "   •   Crédito R$ "+credit.ToString("N2");
     var hint=new ToolTip();hint.SetToolTip(selected,limitText);
   }catch(Exception ex){MessageBox.Show(ex.Message,"Erro ao abrir fiado");}
   finally{refreshingClientSales=false;UpdatePaymentSelection(true);}
  }
  void CorrectPaymentDate(){
   if(string.IsNullOrWhiteSpace(customer)){MessageBox.Show("Selecione um cliente.");return;}
   try{
    var rows=Query("SELECT p.id AS id,p.paid_at AS data,p.sale_id AS venda,p.payment_method AS forma,p.amount AS valor FROM credit_payments p INNER JOIN sales s ON s.id=p.sale_id WHERE "+CreditFilter+" AND COALESCE(NULLIF(TRIM(s.customer_name),''),'SEM CLIENTE')=@name ORDER BY p.paid_at DESC,p.id DESC","@name",customer);
    if(rows.Rows.Count==0){MessageBox.Show("Este cliente não possui pagamentos registrados.");return;}
    using(var form=new StyledDialogForm {Text="Corrigir data de pagamento - "+customer,Width=850,Height=530,StartPosition=FormStartPosition.CenterParent}){
     var grid=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,DataSource=rows};
     var bottomBar=new FlowLayoutPanel {Dock=DockStyle.Bottom,Height=54,Padding=new Padding(8)};
     var change=new Button {Text="Alterar data do pagamento selecionado",AutoSize=true,Height=36};
     change.Click+=(s,e)=>{
      if(grid.CurrentRow==null)return;
      var row=((DataRowView)grid.CurrentRow.DataBoundItem).Row;
      long id=Convert.ToInt64(row["id"]);string original=Convert.ToString(row["data"]);
      DateTime parsed;if(!DateTime.TryParse(original,out parsed))parsed=DateTime.Today;
      using(var dialog=new StyledDialogForm {Text="Data do pagamento #"+id,Width=430,Height=265,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false}){
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
    using(var form=new StyledDialogForm {Text="Conferência do Fiado - somente leitura",Width=1080,Height=650,StartPosition=FormStartPosition.CenterParent}){
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
    using(var dlg=new StyledDialogForm{Text="Fiado | Transferir para cliente cadastrado",Width=690,Height=565,MinimumSize=new Size(550,430),StartPosition=FormStartPosition.CenterParent,BackColor=Color.FromArgb(229,242,255)}){
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
        using(var cmd=new SQLiteCommand("SELECT COUNT(*) AS quantity, COALESCE(SUM(CASE WHEN "+Balance+">0 THEN "+Balance+" ELSE 0 END),0) AS debt FROM sales s WHERE "+CreditFilter+" AND TRIM(s.customer_name)=@source COLLATE NOCASE",cn)){
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
       if(count==0){MessageBox.Show("O nome anterior nao possui vendas fiadas. Selecione o nome antigo que possui as vendas.");return;}
       // Confirma explicitamente que ambos os nomes se referem a mesma pessoa.
       string warning="UNIFICAR DOIS NOMES DA MESMA PESSOA?\\n\\nNome antigo: "+source+
        "\\nNome correto: "+destination+
        "\\n\\nVendas fiadas da origem: "+count+
        "\\nSaldo em aberto: R$ "+debt.ToString("N2")+
        "\\n\\nTambem serao vinculadas ao nome correto as demais vendas, os cascos e o historico do credito depositado. Os valores de credito serao somados."+
        "\\n\\nSe ambos tiverem limite de Fiado, sera mantido o limite do nome correto; o limite antigo ficara no registro da unificacao."+
        "\\n\\nO cadastro antigo sera retirado da lista, SEM apagar vendas, pagamentos, valores ou historico."+
        "\\n\\nCONFIRMA QUE E A MESMA PESSOA?";
       if(MessageBox.Show(warning,"CONFIRMAR UNIFICACAO DE CLIENTE",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
       string backup=CreditBackupPath(db,"antes-transferir-fiado-");
       using(var cn=Open(false)){
        using(var copy=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=backup}.ToString())){
         copy.Open();cn.BackupDatabase(copy,"main","main",-1,null,0);
         using(var check=new SQLiteCommand("PRAGMA integrity_check",copy))
          if(Convert.ToString(check.ExecuteScalar())!="ok")throw new Exception("Backup invalido. Transferencia bloqueada.");
        }
        if(new FileInfo(backup).Length==0)throw new Exception("Backup vazio. Transferencia bloqueada.");
        using(var tx=cn.BeginTransaction()){
         Func<string,bool> hasTable=table=>{
          using(var cmd=new SQLiteCommand("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@name",cn,tx)){
           cmd.Parameters.AddWithValue("@name",table);
           return Convert.ToInt64(cmd.ExecuteScalar())>0;
          }
         };
         Func<string,string,string> document=(table,name)=>{
          if(!hasTable(table))return "";
          using(var cmd=new SQLiteCommand("SELECT COALESCE(document,'') FROM customers WHERE name=@name COLLATE NOCASE LIMIT 1",cn,tx)){
           cmd.Parameters.AddWithValue("@name",name);
           return Convert.ToString(cmd.ExecuteScalar()??"").Trim();
          }
         };
         // CPF/CNPJ diferentes indicam cadastros distintos; nao unificar automaticamente.
         string docSource=document("customers",source),docDest=document("customers",destination);
         if(docSource.Length>0&&docDest.Length>0&&!string.Equals(
             System.Text.RegularExpressions.Regex.Replace(docSource,@"[^0-9]",""),
             System.Text.RegularExpressions.Regex.Replace(docDest,@"[^0-9]",""),StringComparison.Ordinal))
          throw new Exception("Os cadastros possuem CPF/CNPJ diferentes. Nenhuma unificacao foi feita.");
         using(var check=new SQLiteCommand("SELECT COUNT(*) FROM customers WHERE name=@dest COLLATE NOCASE",cn,tx)){
          check.Parameters.AddWithValue("@dest",destination);
          if(Convert.ToInt64(check.ExecuteScalar())!=1)throw new Exception("Cadastro de destino nao esta unico ou foi excluido.");
         }
         using(var create=new SQLiteCommand("CREATE TABLE IF NOT EXISTS fiado_customer_transfers(id INTEGER PRIMARY KEY AUTOINCREMENT,source_name TEXT NOT NULL,destination_name TEXT NOT NULL,sales_transferred INTEGER NOT NULL,changed_at TEXT NOT NULL,description TEXT NOT NULL)",cn,tx))
          create.ExecuteNonQuery();
         // Somente tabelas com coluna customer_name; nao altera IDs de venda nem pagamentos.
         Action<string> updateTable=table=>{
          if(!hasTable(table))return;
          using(var cmd=new SQLiteCommand("UPDATE \""+table+"\" SET customer_name=@dest WHERE TRIM(customer_name)=@source COLLATE NOCASE",cn,tx)){
           cmd.Parameters.AddWithValue("@dest",destination);
           cmd.Parameters.AddWithValue("@source",source);
           cmd.ExecuteNonQuery();
          }
         };
         int movedFiado;
         using(var check=new SQLiteCommand("SELECT COUNT(*) FROM sales s WHERE "+CreditFilter+" AND TRIM(s.customer_name)=@source COLLATE NOCASE",cn,tx)){
          check.Parameters.AddWithValue("@source",source);
          if(Convert.ToInt64(check.ExecuteScalar())!=count)
           throw new Exception("Vendas modificadas durante a unificacao. Atualize e tente novamente.");
         }
         // Registros nao fiados tambem precisam apontar para a pessoa correta.
         updateTable("sales");
         movedFiado=Convert.ToInt32(count);
         updateTable("bottle_loans");
         updateTable("customer_credit_entries");
         updateTable("customer_fiado_limit_audit");
         string limitNote="Sem limite anterior";
         if(hasTable("customer_fiado_limits")){
          bool oldLimit=false,newLimit=false;
          decimal oldValue=0;string oldPeriod="";
          using(var limits=new SQLiteCommand("SELECT customer_name,limit_amount,period FROM customer_fiado_limits WHERE customer_name IN (@old,@new) COLLATE NOCASE",cn,tx)){
           limits.Parameters.AddWithValue("@old",source);limits.Parameters.AddWithValue("@new",destination);
           using(var rd=limits.ExecuteReader())while(rd.Read()){
            string n=Convert.ToString(rd[0]);
            if(string.Equals(n,source,StringComparison.OrdinalIgnoreCase)){
             oldLimit=true;oldValue=Convert.ToDecimal(rd[1]);oldPeriod=Convert.ToString(rd[2]);
            }else if(string.Equals(n,destination,StringComparison.OrdinalIgnoreCase))newLimit=true;
           }
          }
          if(oldLimit){
           limitNote="Limite antigo: R$ "+oldValue.ToString("N2")+" / "+oldPeriod+
             (newLimit?" (mantido limite do destino)":" (transferido para destino)");
           if(newLimit){
            using(var remove=new SQLiteCommand("DELETE FROM customer_fiado_limits WHERE customer_name=@name COLLATE NOCASE",cn,tx)){
             remove.Parameters.AddWithValue("@name",source);
             remove.ExecuteNonQuery();
            }
           }else updateTable("customer_fiado_limits");
          }
         }
         // Eliminar apenas o cadastro duplicado; nao remover os dados das vendas.
         if(hasTable("customers")){
          using(var cmd=new SQLiteCommand("DELETE FROM customers WHERE name=@name COLLATE NOCASE",cn,tx)){
           cmd.Parameters.AddWithValue("@name",source);
           cmd.ExecuteNonQuery();
          }
         }
         // Se anteriormente havia uma exclusao logica do destino, permitir que apareca.
         if(hasTable("deleted_customer_names")){
          using(var show=new SQLiteCommand("DELETE FROM deleted_customer_names WHERE customer_name=@name COLLATE NOCASE",cn,tx)){
           show.Parameters.AddWithValue("@name",destination);show.ExecuteNonQuery();
          }
         }
         using(var audit=new SQLiteCommand("INSERT INTO fiado_customer_transfers(source_name,destination_name,sales_transferred,changed_at,description) VALUES(@source,@dest,@count,datetime('now','localtime'),@detail)",cn,tx)){
          audit.Parameters.AddWithValue("@source",source);
          audit.Parameters.AddWithValue("@dest",destination);
          audit.Parameters.AddWithValue("@count",movedFiado);
          audit.Parameters.AddWithValue("@detail","Mesma pessoa confirmada; vendas, cascos, credito depositado e auditoria reunificados. IDs e pagamentos preservados. "+limitNote);
          if(audit.ExecuteNonQuery()!=1)throw new Exception("Nao foi possivel registrar auditoria da unificacao.");
         }
         tx.Commit();
        }
       }
       customer=destination;
       dlg.DialogResult=DialogResult.OK;dlg.Close();
       clientSearch.Clear();LoadClients();SelectClient(destination);
       MessageBox.Show("Nomes unificados: "+source+" agora e "+destination+". Vendas e pagamentos preservados; credito e cascos vinculados ao cadastro correto.\nBackup: "+backup,"Fiado",MessageBoxButtons.OK,MessageBoxIcon.Information);
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
   using(var dlg=new StyledDialogForm{Text="Fiado | Corrigir nome do cliente",Width=550,Height=310,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,BackColor=Color.FromArgb(229,242,255)}){
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
   using(var form=new StyledDialogForm {Text="Corrigir cliente - venda #"+id,Width=510,Height=205,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false}){
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
    var work=Screen.FromControl(this).WorkingArea;
    var form=new StyledDialogForm {Text="Venda #"+id+" | Produtos e pagamentos",
     Width=Math.Min(620,Math.Max(370,work.Width-28)),Height=Math.Min(450,Math.Max(305,work.Height-32)),
     MinimumSize=new Size(360,300),StartPosition=FormStartPosition.CenterParent,
     FormBorderStyle=FormBorderStyle.Sizable};
    var tabs=new TabControl {Dock=DockStyle.Fill};
    var items=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
    items.DataSource=Query("SELECT id,name AS produto,quantity AS quantidade,unit_price AS preco,total AS total FROM sale_items WHERE sale_id=@sale ORDER BY id","@sale",id);
    var payments=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
    payments.DataSource=Query("SELECT paid_at AS data,payment_method AS forma,amount AS valor FROM credit_payments WHERE sale_id=@sale ORDER BY paid_at,id","@sale",id);
    var a=new TabPage("Itens da venda");a.Controls.Add(items);var b=new TabPage("Pagamentos anteriores");b.Controls.Add(payments);
    tabs.TabPages.Add(a);tabs.TabPages.Add(b);form.Controls.Add(tabs);
    var edit=new Button {Text="Editar item selecionado (com auditoria)",Dock=DockStyle.Bottom,Height=42,BackColor=Color.FromArgb(220,95,105),ForeColor=Color.White};
    edit.Click+=(s,e)=>{if(items.CurrentRow==null){MessageBox.Show("Selecione um item da venda.");return;}long itemId=Convert.ToInt64(items.CurrentRow.Cells["id"].Value);form.Close();EditSaleItem(id,itemId);};form.Controls.Add(edit);
    var reopen=new Button {Text="Corrigir baixa / Voltar ao Fiado",Dock=DockStyle.Bottom,Height=42,
     BackColor=Color.FromArgb(155,55,35),ForeColor=Color.White};
    reopen.Click+=(s,e)=>{
     form.Close();
     ReopenSalePayments(id);
    };
    form.Controls.Add(reopen);
    using(form)form.ShowDialog(this);
   }catch(Exception ex){MessageBox.Show(ex.Message,"Erro na consulta");}
  }
  void EditSaleItem(long saleId,long selectedItemId){
   try{
    var rows=Query("SELECT id, name AS produto, quantity AS quantidade, unit_price AS preco, total AS valor FROM sale_items WHERE sale_id=@sale ORDER BY id","@sale",saleId);
    using(var form=new StyledDialogForm {Text="Auditoria de itens - venda #"+saleId,Width=880,Height=550,StartPosition=FormStartPosition.CenterParent}){
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
      using(var dialog=new StyledDialogForm {Text="Editar item #"+itemId,Width=530,Height=340,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false}){
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
    using(var form=new StyledDialogForm {Text="Histórico de alterações - venda #"+saleId,Width=1100,Height=580,StartPosition=FormStartPosition.CenterParent}){
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
    using(var form=new StyledDialogForm {Text="Extrato do cliente - "+customer,Width=720,Height=660,StartPosition=FormStartPosition.CenterParent}){
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
    using(var form=new StyledDialogForm {Text="Cupom da venda #"+id,Width=640,Height=610,StartPosition=FormStartPosition.CenterParent}){
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
    receivedBalance.Text="Selecionado R$ "+selectedTotal.ToString("N2")+
      Environment.NewLine+"Em aberto R$ "+VisibleOpenBalance().ToString("N2");
    if(fill)SetPaymentAmount(selectedTotal);
   }catch(Exception ex){receivedBalance.Text="Selecione uma venda para receber. "+ex.Message;}
  }
  void FillAllBalances(){
   if(string.IsNullOrWhiteSpace(customer)){MessageBox.Show("Selecione um cliente.");return;}
   if(saleTabs!=null&&saleTabs.SelectedIndex!=0){MessageBox.Show("Abra Fiado atual - em aberto.");return;}
   SetPaymentAmount(VisibleOpenBalance());
   receivedBalance.Text="Todas as vendas R$ "+VisibleOpenBalance().ToString("N2")+
     Environment.NewLine+"Aceita recebimento parcial";
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
   decimal available=Math.Min(Math.Max(0m,CustomerCreditBalance(cn,tx,name)),AuthorizedCreditBalance(cn,tx,name));
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
   ChangeAuthorizedCreditBalance(cn,tx,name,-applied);
   return applied;
  }
  // Saldo autorizado separado por cliente: uma nova entrada nao altera a autorizacao anterior.
  static decimal AuthorizedCreditBalance(SQLiteConnection cn,SQLiteTransaction tx,string name){
   using(var create=new SQLiteCommand(@"CREATE TABLE IF NOT EXISTS customer_credit_auto_pool(
    customer_name TEXT PRIMARY KEY COLLATE NOCASE,available NUMERIC NOT NULL DEFAULT 0,
    updated_at TEXT NOT NULL DEFAULT (datetime('now','localtime')))",cn,tx))create.ExecuteNonQuery();
   using(var read=new SQLiteCommand("SELECT available FROM customer_credit_auto_pool WHERE customer_name=@name COLLATE NOCASE",cn,tx)){
    read.Parameters.AddWithValue("@name",name.Trim());
    object value=read.ExecuteScalar();
    if(value!=null&&value!=DBNull.Value)return Math.Max(0m,Convert.ToDecimal(value));
   }
   // Credito existente antes da separacao: manter o abatimento automatico ja vigente.
   decimal legacy=Math.Max(0m,CustomerCreditBalance(cn,tx,name));
   using(var seed=new SQLiteCommand(@"INSERT OR IGNORE INTO customer_credit_auto_pool(customer_name,available)
    VALUES(@name,@amount)",cn,tx)){
    seed.Parameters.AddWithValue("@name",name.Trim());seed.Parameters.AddWithValue("@amount",legacy);
    seed.ExecuteNonQuery();
   }
   return legacy;
  }
  static void ChangeAuthorizedCreditBalance(SQLiteConnection cn,SQLiteTransaction tx,string name,decimal delta){
   decimal current=AuthorizedCreditBalance(cn,tx,name);
   using(var cmd=new SQLiteCommand(@"UPDATE customer_credit_auto_pool SET available=@amount,
    updated_at=datetime('now','localtime') WHERE customer_name=@name COLLATE NOCASE",cn,tx)){
    cmd.Parameters.AddWithValue("@name",name.Trim());
    cmd.Parameters.AddWithValue("@amount",Math.Max(0m,current+delta));
    if(cmd.ExecuteNonQuery()!=1)throw new Exception("Nao foi possivel atualizar o saldo de credito autorizado.");
   }
  }
  static void EnsureDepositAuthorization(SQLiteConnection cn,SQLiteTransaction tx){
   using(var cmd=new SQLiteCommand(@"CREATE TABLE IF NOT EXISTS customer_credit_deposit_authorizations(
    entry_id INTEGER PRIMARY KEY,customer_name TEXT NOT NULL COLLATE NOCASE,
    original_amount NUMERIC NOT NULL,authorized INTEGER NOT NULL DEFAULT 0,
    authorized_at TEXT,created_at TEXT NOT NULL DEFAULT (datetime('now','localtime')))",cn,tx))cmd.ExecuteNonQuery();
  }
  bool AuthorizeSavedDeposit(long entryId){
   if(string.IsNullOrWhiteSpace(customer))return false;
   try{
    EnsureCustomerCredit(db);
    using(var cn=Open(false)){
     string name=customer.Trim();
     decimal amount;
     using(var cmd=new SQLiteCommand(@"SELECT amount FROM customer_credit_entries
      WHERE id=@id AND customer_name=@name COLLATE NOCASE AND entry_type='DEPOSITO'",cn)){
      cmd.Parameters.AddWithValue("@id",entryId);cmd.Parameters.AddWithValue("@name",name);
      object value=cmd.ExecuteScalar();
      if(value==null||value==DBNull.Value){MessageBox.Show("Selecione uma linha DEPOSITO / GUARDAR CREDITO.");return false;}
      amount=Convert.ToDecimal(value);
     }
     if(amount<=0)throw new Exception("Deposito sem saldo positivo.");
     using(var check=new SQLiteCommand("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='customer_credit_deposit_authorizations'",cn)){
      if(Convert.ToInt32(check.ExecuteScalar())>0)
       using(var state=new SQLiteCommand("SELECT authorized FROM customer_credit_deposit_authorizations WHERE entry_id=@id",cn)){
        state.Parameters.AddWithValue("@id",entryId);
        object value=state.ExecuteScalar();
        if(value!=null&&value!=DBNull.Value&&Convert.ToInt32(value)==1){MessageBox.Show("Este credito ja foi autorizado.");return false;}
       }
     }
     decimal wallet=CustomerCreditBalance(cn,null,name);
     decimal pool=AuthorizedCreditBalance(cn,null,name);
     decimal amountToAuthorize=Math.Min(amount,Math.Max(0m,wallet-pool));
     if(amountToAuthorize<=0){MessageBox.Show("Nao ha saldo de credito reservado disponivel para liberar. Confira o extrato antes de continuar.");return false;}
     if(MessageBox.Show("Cliente: "+name+"\\nDeposito #"+entryId+
      "\\nValor original: R$ "+amount.ToString("N2")+
      "\\nValor disponivel para liberar: R$ "+amountToAuthorize.ToString("N2")+
      "\\n\\nLiberar este credito para abatimento automatico nas PROXIMAS vendas?"+
      "\\nNao altera pagamentos anteriores.","Liberar credito guardado",
      MessageBoxButtons.YesNo,MessageBoxIcon.Question,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return false;
     string backup=CreditBackupPath(db,"antes-liberar-credito-");
     using(var dest=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=backup}.ToString())){
      dest.Open();cn.BackupDatabase(dest,"main","main",-1,null,0);
      using(var integrity=new SQLiteCommand("PRAGMA integrity_check",dest))
       if(Convert.ToString(integrity.ExecuteScalar())!="ok")throw new Exception("Backup invalido.");
     }
     if(new FileInfo(backup).Length<=0)throw new Exception("Backup vazio.");
     using(var tx=cn.BeginTransaction()){
      EnsureDepositAuthorization(cn,tx);
      using(var state=new SQLiteCommand(@"INSERT OR IGNORE INTO customer_credit_deposit_authorizations
       (entry_id,customer_name,original_amount,authorized) VALUES(@id,@name,@amount,0)",cn,tx)){
       state.Parameters.AddWithValue("@id",entryId);state.Parameters.AddWithValue("@name",name);
       state.Parameters.AddWithValue("@amount",amount);state.ExecuteNonQuery();
      }
      using(var update=new SQLiteCommand(@"UPDATE customer_credit_deposit_authorizations
       SET authorized=1,authorized_at=datetime('now','localtime')
       WHERE entry_id=@id AND authorized=0",cn,tx)){
       update.Parameters.AddWithValue("@id",entryId);
       if(update.ExecuteNonQuery()!=1)throw new Exception("Credito ja liberado ou alterado.");
      }
      decimal nowWallet=CustomerCreditBalance(cn,tx,name);
      decimal nowPool=AuthorizedCreditBalance(cn,tx,name);
      decimal release=Math.Min(amount,Math.Max(0m,nowWallet-nowPool));
      if(release<=0)throw new Exception("Saldo mudou; operacao cancelada.");
      ChangeAuthorizedCreditBalance(cn,tx,name,release);
      tx.Commit();
     }
     MessageBox.Show("Credito liberado para abatimento automatico futuro. Historico preservado.");
     return true;
    }
   }catch(Exception ex){MessageBox.Show("Nao foi possivel liberar credito: "+ex.Message,"Fiado",MessageBoxButtons.OK,MessageBoxIcon.Error);}
   return false;
  }
  void DepositCustomerCredit(){
   if(string.IsNullOrWhiteSpace(customer)||customer=="SEM CLIENTE"){MessageBox.Show("Selecione um cliente cadastrado.");return;}
   decimal value=creditDepositAmount.Value;
   if(value<=0){MessageBox.Show("Informe o valor antecipado no campo CREDITO DEPOSITADO R$.");return;}
   string payMethod=Convert.ToString(method.SelectedItem);
   if(payMethod=="CREDITO DEPOSITADO"){MessageBox.Show("Escolha a forma realmente recebida (DINHEIRO, PIX etc.), nao CREDITO DEPOSITADO.");return;}
   if(MessageBox.Show("Cliente: "+customer+"\\nCredito recebido: R$ "+value.ToString("N2")+"\\nForma: "+payMethod+"\\n\\nEste valor sera guardado para abater compras fiadas futuras. Confirmar?","Credito do cliente",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
   // Cada novo credito exige decisao explicita sobre uso automatico em vendas futuras.
   var decision=MessageBox.Show("Cliente: "+customer+
    "\\n\\nAutorizar o uso AUTOMATICO somente deste NOVO credito nas proximas vendas fiadas?"+
    "\\nSIM: abater automaticamente enquanto houver saldo de credito."+
    "\\nNAO: guardar este novo credito sem abater automaticamente; creditos anteriores mantem sua autorizacao."+
    "\\n\\nEsta autorizacao nao altera baixas de vendas antigas.",
    "Autorizar abatimento automatico",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question,
    MessageBoxDefaultButton.Button2);
   if(decision==DialogResult.Cancel)return;
   bool authorize=decision==DialogResult.Yes;
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
      // Inicializa com o saldo antigo antes de somar este novo deposito.
      AuthorizedCreditBalance(cn,tx,customer);
      if(authorize)ChangeAuthorizedCreditBalance(cn,tx,customer,value);
      using(var cmd=new SQLiteCommand("INSERT INTO customer_credit_entries(customer_name,amount,entry_type,method,details) VALUES(@client,@amount,'DEPOSITO',@method,'Credito antecipado recebido no Fiado')",cn,tx)){
       cmd.Parameters.AddWithValue("@client",customer.Trim());cmd.Parameters.AddWithValue("@amount",value);cmd.Parameters.AddWithValue("@method",payMethod);
       if(cmd.ExecuteNonQuery()!=1)throw new Exception("Falha ao registrar credito.");
      }
      EnsureDepositAuthorization(cn,tx);
      using(var last=new SQLiteCommand("SELECT last_insert_rowid()",cn,tx)){
       long entryId=Convert.ToInt64(last.ExecuteScalar());
       using(var state=new SQLiteCommand(@"INSERT INTO customer_credit_deposit_authorizations
        (entry_id,customer_name,original_amount,authorized,authorized_at)
        VALUES(@id,@name,@amount,@authorized,CASE WHEN @authorized=1 THEN datetime('now','localtime') ELSE NULL END)",cn,tx)){
        state.Parameters.AddWithValue("@id",entryId);state.Parameters.AddWithValue("@name",customer.Trim());
        state.Parameters.AddWithValue("@amount",value);state.Parameters.AddWithValue("@authorized",authorize?1:0);
        state.ExecuteNonQuery();
       }
      }
      tx.Commit();
     }
    }
    creditDepositAmount.Value=0;LoadClients();SelectClient(customer);
    MessageBox.Show("Credito registrado para "+customer+". Abatimento deste novo credito: "+(authorize?"AUTORIZADO":"NAO AUTORIZADO")+".","Fiado");
   }catch(Exception ex){MessageBox.Show("Credito nao registrado: "+ex.Message,"Fiado",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  }
  // Corrigir o saldo da carteira com lancamento auditavel; nunca reescrever historico.
  void AdjustCustomerCredit(){
   if(string.IsNullOrWhiteSpace(customer)||customer=="SEM CLIENTE"){MessageBox.Show("Selecione o cliente.");return;}
   try{
    EnsureCustomerCredit(db);
    decimal current;
    using(var cn=Open(true))current=CustomerCreditBalance(cn,null,customer);
    using(var dlg=new StyledDialogForm{Text="Ajustar credito depositado - "+customer,Width=500,Height=310,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false}){
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
     using(var cmd=new SQLiteCommand("SELECT id AS Registro,created_at AS Data,entry_type AS Operacao,amount AS Valor,method AS Forma,sale_id AS Venda,details AS Detalhes FROM customer_credit_entries WHERE customer_name=@client COLLATE NOCASE ORDER BY id DESC LIMIT 1000",cn)){
      cmd.Parameters.AddWithValue("@client",customer.Trim());
      using(var adapter=new SQLiteDataAdapter(cmd))adapter.Fill(data);
     }
     // Produtos exibidos na propria linha do extrato, sem modificar lancamentos.
     data.Columns.Add("Produtos",typeof(string));
     data.Columns["Produtos"].SetOrdinal(3);
     using(var products=new SQLiteCommand("SELECT si.name,si.quantity FROM sale_items si JOIN sales s ON s.id=si.sale_id WHERE si.sale_id=@sale AND s.customer_name=@client COLLATE NOCASE ORDER BY si.id",cn)){
      var saleParam=products.Parameters.Add("@sale",DbType.Int64);
      products.Parameters.AddWithValue("@client",customer.Trim());
      foreach(DataRow row in data.Rows){
       if(row["Venda"]==DBNull.Value)continue;
       long saleId;
       if(!long.TryParse(Convert.ToString(row["Venda"]),out saleId))continue;
       saleParam.Value=saleId;
       var descriptions=new System.Collections.Generic.List<string>();
       using(var reader=products.ExecuteReader()){
        while(reader.Read()){
         string itemName=Convert.ToString(reader[0]);
         string quantity=Convert.ToDecimal(reader[1]).ToString("0.###");
         descriptions.Add(quantity+"x "+itemName);
        }
       }
       row["Produtos"]=descriptions.Count>0?string.Join("; ",descriptions.ToArray()):"Venda sem itens localizados";
      }
     }
     decimal deposited=0m,debited=0m,available=0m;
     foreach(DataRow entry in data.Rows){
      decimal movement=Convert.ToDecimal(entry["Valor"]);
      if(movement>0)deposited+=movement;
      else debited+=Math.Abs(movement);
     }
     available=deposited-debited;
     string limitDisplay="Nao configurado";
     using(var check=new SQLiteCommand("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='customer_fiado_limits'",cn)){
      if(Convert.ToInt32(check.ExecuteScalar())>0){
       using(var limit=new SQLiteCommand("SELECT limit_amount,period FROM customer_fiado_limits WHERE customer_name=@client COLLATE NOCASE LIMIT 1",cn)){
        limit.Parameters.AddWithValue("@client",customer.Trim());
        using(var rd=limit.ExecuteReader())if(rd.Read())limitDisplay="R$ "+Convert.ToDecimal(rd[0]).ToString("N2")+" ("+Convert.ToString(rd[1])+")";
       }
      }
     }
     using(var dlg=new StyledDialogForm{Text="Historico de credito - "+customer,Width=1200,Height=740,MinimumSize=new Size(800,520),WindowState=FormWindowState.Maximized,StartPosition=FormStartPosition.CenterParent,BackColor=Color.FromArgb(229,242,255)}){
      var grid=new DataGridView{Dock=DockStyle.Fill,DataSource=data,ReadOnly=true,AllowUserToAddRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.None,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,RowHeadersVisible=false,BackgroundColor=Color.White,ScrollBars=ScrollBars.Both};
      grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(21,75,156);grid.ColumnHeadersDefaultCellStyle.ForeColor=Color.White;grid.EnableHeadersVisualStyles=false;
      // As colunas podem ser geradas somente apos o DataGridView receber o binding.
      grid.AutoGenerateColumns=true;
      grid.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.AutoSize;
      grid.ColumnHeadersDefaultCellStyle.WrapMode=DataGridViewTriState.True;
      grid.DefaultCellStyle.Padding=new Padding(3,5,3,5);
      grid.ReadOnly=true;
      var returnButton=new DataGridViewButtonColumn{Name="VoltarFiado",HeaderText="Acao",Text="Voltar ao Fiado",UseColumnTextForButtonValue=true,Width=135,MinimumWidth=125};
      grid.Columns.Add(returnButton);
      grid.DataBindingComplete+=(sender,args)=>{
       if(grid.Columns["Registro"]!=null)grid.Columns["Registro"].Visible=false;
       if(grid.Columns["VoltarFiado"]!=null){grid.Columns["VoltarFiado"].DisplayIndex=grid.Columns.Count-1;grid.Columns["VoltarFiado"].Width=135;}
       var detailsColumn=grid.Columns["Detalhes"];
       if(detailsColumn!=null){detailsColumn.Width=360;detailsColumn.MinimumWidth=240;}
       var productsColumn=grid.Columns["Produtos"];
       if(productsColumn!=null){productsColumn.Width=520;productsColumn.MinimumWidth=320;}
       foreach(DataGridViewColumn column in grid.Columns){if(column.Name!="Produtos"&&column.Name!="Detalhes"&&column.Name!="VoltarFiado")column.Width=column.Name=="Operacao"?185:column.Name=="Data"?160:column.Name=="Valor"?110:column.Name=="Forma"?190:100;}
       grid.DefaultCellStyle.WrapMode=DataGridViewTriState.True;
       grid.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells;
       grid.RowTemplate.MinimumHeight=38;
       grid.AutoResizeColumnHeadersHeight();
      };
      var hint=new Label{Dock=DockStyle.Top,Height=40,Padding=new Padding(10,10,10,0),Text="Guardar credito = deposito. Selecione um lancamento para corrigir ou estornar; o sistema confere as vendas afetadas.",ForeColor=Color.FromArgb(21,75,156),Font=new Font("Segoe UI",10)};
      var bar=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=112,Padding=new Padding(8),AutoScroll=true,WrapContents=true,BackColor=Color.FromArgb(218,233,250)};
      var show=new Button{Text="Ver produtos da venda",Width=205,Height=32,BackColor=Color.FromArgb(21,75,156),ForeColor=Color.White};
      Action inspect=()=>{
       if(grid.CurrentRow==null || !grid.CurrentRow.Selected){MessageBox.Show("Selecione uma linha do historico.");return;}
       object sale=grid.CurrentRow.Cells["Venda"].Value;
       if(sale==null||sale==DBNull.Value||string.IsNullOrWhiteSpace(Convert.ToString(sale))){MessageBox.Show("Este lancamento nao possui venda vinculada. O historico original foi preservado.");return;}
       long saleId;
       if(!long.TryParse(Convert.ToString(sale),out saleId)){MessageBox.Show("Numero da venda invalido.");return;}
       ShowCreditSaleItems(saleId,Convert.ToString(grid.CurrentRow.Cells["Operacao"].Value),Convert.ToDecimal(grid.CurrentRow.Cells["Valor"].Value));
      };
      show.Click+=(sender,args)=>inspect();
      grid.CellDoubleClick+=(sender,args)=>{if(args.RowIndex>=0 && args.ColumnIndex>=0 && grid.Columns[args.ColumnIndex].Name!="VoltarFiado" && grid.Columns[args.ColumnIndex].Name!="Produtos" && grid.Columns[args.ColumnIndex].Name!="Detalhes")inspect();};
      grid.CellClick+=(sender,args)=>{
       if(args.RowIndex<0||args.ColumnIndex<0)return;
       string col=grid.Columns[args.ColumnIndex].Name;
       if(col!="Produtos"&&col!="Detalhes")return;
       string full=Convert.ToString(grid.Rows[args.RowIndex].Cells[args.ColumnIndex].Value);
       long linkedSale;
       if(col=="Produtos"&&long.TryParse(Convert.ToString(grid.Rows[args.RowIndex].Cells["Venda"].Value),out linkedSale)){
        // Consulta estruturada diretamente na venda; nao depende de texto truncado na grade.
        ShowCreditSaleItems(linkedSale,
         Convert.ToString(grid.Rows[args.RowIndex].Cells["Operacao"].Value),
         Convert.ToDecimal(grid.Rows[args.RowIndex].Cells["Valor"].Value));
        return;
       }
       if(string.IsNullOrWhiteSpace(full)){
        MessageBox.Show("Nao ha detalhes cadastrados para este lancamento.","Historico de credito");
        return;
       }
       using(var detail=new StyledDialogForm{Text=col+" - texto completo",Width=760,Height=420,MinimumSize=new Size(450,300),StartPosition=FormStartPosition.CenterParent}){
        var description=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,
         ScrollBars=ScrollBars.Vertical,WordWrap=true,Font=new Font("Segoe UI",12),Text=full};
        detail.Controls.Add(description);
        detail.ShowDialog(dlg);
       }
      };
      grid.CellContentClick+=(sender,args)=>{
       if(args.RowIndex<0||args.ColumnIndex<0||grid.Columns[args.ColumnIndex].Name!="VoltarFiado")return;
       var row=grid.Rows[args.RowIndex];
       if(row.Cells["Registro"].Value==null)return;
       long entryId=Convert.ToInt64(row.Cells["Registro"].Value);
       if(ReturnCreditSaleToFiado(entryId)){dlg.Close();ShowCustomerCreditHistory();}
      };
      var reopen=new Button{Text="Reabrir baixas da venda",Width=205,Height=32,BackColor=Color.FromArgb(160,55,35),ForeColor=Color.White};
      reopen.Click+=(sender,args)=>{
       if(grid.CurrentRow==null)return;
       long saleId;
       if(!long.TryParse(Convert.ToString(grid.CurrentRow.Cells["Venda"].Value),out saleId)){
        MessageBox.Show("Selecione uma utilizacao vinculada a venda.");return;
       }
       if(ReopenSalePayments(saleId)){dlg.Close();ShowCustomerCreditHistory();}
      };
      var authorizeDeposit=new Button{Text="Liberar credito selecionado",Width=220,Height=32,BackColor=Color.FromArgb(21,90,145),ForeColor=Color.White};
      authorizeDeposit.Click+=(sender,args)=>{
       if(grid.CurrentRow==null)return;
       long entryId;
       if(!long.TryParse(Convert.ToString(grid.CurrentRow.Cells["Registro"].Value),out entryId))return;
       if(AuthorizeSavedDeposit(entryId)){dlg.Close();ShowCustomerCreditHistory();}
      };
      bar.Controls.Add(authorizeDeposit);
      bar.Controls.Add(reopen);
      bar.Controls.Add(show);
      var correct=new Button{Text="Corrigir / retirar lancamento",Width=230,Height=32,BackColor=Color.FromArgb(21,75,156),ForeColor=Color.White};
      correct.Click+=(sender,args)=>{
       if(grid.CurrentRow==null)return;
       long entryId=Convert.ToInt64(grid.CurrentRow.Cells["Registro"].Value);
       string kind=Convert.ToString(grid.CurrentRow.Cells["Operacao"].Value);
       long saleId;
       if((kind=="UTILIZACAO"||kind=="UTILIZACAO_FIADO")&&
          long.TryParse(Convert.ToString(grid.CurrentRow.Cells["Venda"].Value),out saleId)){
        if(ReopenSalePayments(saleId)){dlg.Close();ShowCustomerCreditHistory();}
       }else if(CorrectCreditHistoryEntry(entryId)){dlg.Close();ShowCustomerCreditHistory();}
      };
      bar.Controls.Add(correct);
      var reverse=new Button{Text="Estornar credito guardado",Width=250,Height=32,BackColor=Color.FromArgb(150,55,35),ForeColor=Color.White};
      reverse.Click+=(sender,args)=>{
       if(grid.CurrentRow==null)return;
       long id=Convert.ToInt64(grid.CurrentRow.Cells["Registro"].Value);
       if(ReverseInvalidDeposit(id)){dlg.Close();ShowCustomerCreditHistory();}
      };
      bar.Controls.Add(reverse);
      // Atalho unico para o fluxo comum: retirar credito selecionado.
      // Credito nao utilizado: correcao para zero. Credito ja aplicado:
      // estorno com conferencia das vendas afetadas e backup obrigatorio.
      var removeCredit=new Button{Name="amzgFiadoRemoveCredit",Text="Retirar credito selecionado",Width=235,Height=32,
       BackColor=Color.FromArgb(170,42,42),ForeColor=Color.White};
      removeCredit.Click+=(sender,args)=>{
       if(grid.CurrentRow==null){MessageBox.Show("Selecione o credito que deseja retirar.");return;}
       long id=Convert.ToInt64(grid.CurrentRow.Cells["Registro"].Value);
       string kind=Convert.ToString(grid.CurrentRow.Cells["Operacao"].Value);
       if(kind!="DEPOSITO"&&kind!="AJUSTE_AUDITADO"){
        MessageBox.Show("Selecione um deposito ou ajuste de credito. Para vendas pagas, use Reabrir venda.");return;
       }
       bool changed=kind=="DEPOSITO"?ReverseInvalidDeposit(id):CorrectCreditHistoryEntry(id);
       if(changed){dlg.Close();ShowCustomerCreditHistory();}
      };
      bar.Controls.Add(removeCredit);
      var totals=new Label{Dock=DockStyle.Top,Height=66,Padding=new Padding(12,9,8,5),BackColor=Color.FromArgb(218,233,250),ForeColor=Color.FromArgb(21,75,156),Font=new Font("Segoe UI",11,FontStyle.Bold),Text="Credito inserido: R$ "+deposited.ToString("N2")+"    |    Credito abatido: R$ "+debited.ToString("N2")+"\nSaldo atual: R$ "+available.ToString("N2")+"    |    Limite de Fiado atual: "+limitDisplay};
      var adjust=new Button{Text="Ajustar saldo manualmente",Width=205,Height=32,BackColor=Color.FromArgb(21,75,156),ForeColor=Color.White};
      adjust.Click+=(sender,args)=>{dlg.Close();AdjustCustomerCredit();};
      bar.Controls.Add(adjust);
      var close=new Button{Text="Fechar",Width=110,Height=32};
      close.Click+=(sender,args)=>dlg.Close();
      bar.Controls.Add(close);
      dlg.Controls.Add(grid);dlg.Controls.Add(bar);dlg.Controls.Add(hint);dlg.Controls.Add(totals);
      dlg.Shown+=(sender,args)=>{if(grid.Rows.Count>0)grid.ClearSelection();};
      // Ordem de encaixe: tabela ocupa somente a area livre, sem cobrir cabecalho e botoes.
      grid.BringToFront();
      dlg.ShowDialog(this);
     }
    }
   }catch(Exception ex){MessageBox.Show("Historico indisponivel: "+ex.Message);}
  }
  // Permite reabrir baixas selecionadas de uma venda, mantendo comprovante de auditoria.
  bool ReopenSalePayments(long saleId){
   if(string.IsNullOrWhiteSpace(customer))return false;
   try{
    string name=customer.Trim();
    using(var cn=Open(false)){
     decimal gross;
     using(var cmd=new SQLiteCommand(@"SELECT gross_total FROM sales WHERE id=@sale
      AND TRIM(customer_name)=@name COLLATE NOCASE
      AND UPPER(COALESCE(status,'')) NOT LIKE '%EXCLUID%'",cn)){
      cmd.Parameters.AddWithValue("@sale",saleId);cmd.Parameters.AddWithValue("@name",name);
      object val=cmd.ExecuteScalar();
      if(val==null||val==DBNull.Value)throw new Exception("Venda nao encontrada para este cliente.");
      gross=Convert.ToDecimal(val);
     }
     var payments=new DataTable();
     using(var cmd=new SQLiteCommand(@"SELECT id AS Registro,paid_at AS Data,amount AS Valor,
      payment_method AS Forma,note AS Observacao FROM credit_payments WHERE sale_id=@sale ORDER BY id",cn)){
      cmd.Parameters.AddWithValue("@sale",saleId);
      using(var adapter=new SQLiteDataAdapter(cmd))adapter.Fill(payments);
     }
     if(payments.Rows.Count==0){MessageBox.Show("Nao existem baixas nesta venda.");return false;}
     payments.Columns.Add("Reabrir",typeof(bool));
     foreach(DataRow row in payments.Rows)row["Reabrir"]=false;
     using(var dialog=new StyledDialogForm{Text="Reabrir venda #"+saleId+" - "+name,Width=940,Height=520,
      MinimumSize=new Size(680,420),StartPosition=FormStartPosition.CenterParent}){
      var grid=new DataGridView{Dock=DockStyle.Fill,DataSource=payments,AllowUserToAddRows=false,
       AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,RowHeadersVisible=false,BackgroundColor=Color.White};
      // Colunas podem ser geradas somente apos o binding. Nunca acessar colunas nulas.
      grid.AutoGenerateColumns=true;
      Action configurePayments=()=>{
       if(grid.IsDisposed)return;
       foreach(DataGridViewColumn column in grid.Columns)
        column.ReadOnly=column.Name!="Reabrir";
       var idColumn=grid.Columns["Registro"];
       if(idColumn!=null)idColumn.Visible=false;
       var noteColumn=grid.Columns["Observacao"];
       if(noteColumn!=null)noteColumn.FillWeight=180;
       var selectColumn=grid.Columns["Reabrir"];
       if(selectColumn!=null){selectColumn.DisplayIndex=0;selectColumn.Width=85;selectColumn.FillWeight=65;}
      };
      grid.DataBindingComplete+=(sender,args)=>configurePayments();
      configurePayments();
      grid.CurrentCellDirtyStateChanged+=(sender,args)=>{
       if(grid.IsCurrentCellDirty)grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
      };
      var info=new Label{Dock=DockStyle.Top,Height=85,Padding=new Padding(12),
       Text="Venda #"+saleId+" | Total: R$ "+gross.ToString("N2")+
       "\nMarque as baixas incorretas. Dinheiro realmente recebido deve ficar desmarcado."+
       "\nAs baixas selecionadas retornam ao Fiado; a operacao fica auditada."};
      var confirm=new Button{Text="Confirmar reabertura",Dock=DockStyle.Bottom,Height=42};
      bool completed=false;
      confirm.Click+=(sender,args)=>{
       grid.EndEdit();
       var selected=payments.Rows.Cast<DataRow>().Where(row=>row["Reabrir"]!=DBNull.Value&&Convert.ToBoolean(row["Reabrir"])).ToList();
       if(selected.Count==0){MessageBox.Show("Marque as baixas que deseja reabrir.");return;}
       decimal sum=selected.Sum(row=>Convert.ToDecimal(row["Valor"]));
       if(MessageBox.Show("Reabrir R$ "+sum.ToString("N2")+" da venda #"+saleId+
        "?\nOs pagamentos selecionados serao estornados. Pagamentos reais nao selecionados permanecerao.",
        "Confirmar correcao",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return;
       try{
        string backup=CreditBackupPath(db,"antes-reabrir-venda-");
        using(var dest=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=backup}.ToString())){
         dest.Open();cn.BackupDatabase(dest,"main","main",-1,null,0);
         using(var check=new SQLiteCommand("PRAGMA integrity_check",dest))
          if(Convert.ToString(check.ExecuteScalar())!="ok")throw new Exception("Backup invalido.");
        }
        if(new FileInfo(backup).Length<=0)throw new Exception("Backup vazio.");
        using(var tx=cn.BeginTransaction()){
         using(var create=new SQLiteCommand(@"CREATE TABLE IF NOT EXISTS fiado_payment_reversal_audit(
          id INTEGER PRIMARY KEY AUTOINCREMENT,customer_name TEXT NOT NULL,sale_id INTEGER NOT NULL,
          payment_id INTEGER NOT NULL,amount NUMERIC NOT NULL,payment_method TEXT NOT NULL,
          original_paid_at TEXT,original_note TEXT,reason TEXT NOT NULL,
          reversed_at TEXT NOT NULL DEFAULT (datetime('now','localtime')))",cn,tx))create.ExecuteNonQuery();
         foreach(DataRow row in selected){
          long paymentId=Convert.ToInt64(row["Registro"]);
          decimal amount=Convert.ToDecimal(row["Valor"]);
          string methodName=Convert.ToString(row["Forma"]);
          string note=Convert.ToString(row["Observacao"]);
          using(var verify=new SQLiteCommand(@"SELECT COUNT(*) FROM credit_payments
           WHERE id=@id AND sale_id=@sale AND amount=@amount AND payment_method=@method",cn,tx)){
           verify.Parameters.AddWithValue("@id",paymentId);verify.Parameters.AddWithValue("@sale",saleId);
           verify.Parameters.AddWithValue("@amount",amount);verify.Parameters.AddWithValue("@method",methodName);
           if(Convert.ToInt32(verify.ExecuteScalar())!=1)throw new Exception("Baixa alterada; atualize a janela.");
          }
          if(string.Equals(methodName,"CREDITO_CLIENTE",StringComparison.OrdinalIgnoreCase)){
           // Devolver o valor a carteira sem apagar o lancamento historico original.
           using(var credit=new SQLiteCommand(@"INSERT INTO customer_credit_entries
            (customer_name,amount,entry_type,method,details)
            VALUES(@name,@amount,'ESTORNO_UTILIZACAO','CORRECAO',@details)",cn,tx)){
            credit.Parameters.AddWithValue("@name",name);credit.Parameters.AddWithValue("@amount",amount);
            credit.Parameters.AddWithValue("@details","Reabertura da venda #"+saleId+"; baixa #"+paymentId);
            credit.ExecuteNonQuery();
           }
          }
          using(var audit=new SQLiteCommand(@"INSERT INTO fiado_payment_reversal_audit
           (customer_name,sale_id,payment_id,amount,payment_method,original_paid_at,original_note,reason)
           VALUES(@name,@sale,@id,@amount,@method,@date,@note,'Baixa incorreta reaberta manualmente')",cn,tx)){
           audit.Parameters.AddWithValue("@name",name);audit.Parameters.AddWithValue("@sale",saleId);
           audit.Parameters.AddWithValue("@id",paymentId);audit.Parameters.AddWithValue("@amount",amount);
           audit.Parameters.AddWithValue("@method",methodName);audit.Parameters.AddWithValue("@date",Convert.ToString(row["Data"]));
           audit.Parameters.AddWithValue("@note",note);audit.ExecuteNonQuery();
          }
          using(var del=new SQLiteCommand("DELETE FROM credit_payments WHERE id=@id AND sale_id=@sale",cn,tx)){
           del.Parameters.AddWithValue("@id",paymentId);del.Parameters.AddWithValue("@sale",saleId);
           if(del.ExecuteNonQuery()!=1)throw new Exception("Nao foi possivel estornar a baixa.");
          }
         }
         tx.Commit();
        }
        completed=true;dialog.Close();
       }catch(Exception ex){MessageBox.Show("Reabertura cancelada: "+ex.Message,"Fiado",MessageBoxButtons.OK,MessageBoxIcon.Error);}
      };
      dialog.Controls.Add(grid);dialog.Controls.Add(confirm);dialog.Controls.Add(info);
      dialog.ShowDialog(this);
      if(completed){LoadClients();SelectClient(customer);MessageBox.Show("Venda reaberta no Fiado. Consulte os valores atualizados.");return true;}
     }
    }
   }catch(Exception ex){MessageBox.Show("Erro: "+ex.Message,"Fiado",MessageBoxButtons.OK,MessageBoxIcon.Error);}
   return false;
  }
  // Reabre apenas o abatimento automatico escolhido; pagamentos reais permanecem intactos.
  bool ReturnCreditSaleToFiado(long entryId){
   if(string.IsNullOrWhiteSpace(customer))return false;
   try{
    EnsureCustomerCredit(db);
    string name=customer.Trim();
    using(var cn=Open(false)){
     long saleId;decimal amountApplied;string kind;
     using(var cmd=new SQLiteCommand(@"SELECT entry_type,amount,sale_id FROM customer_credit_entries
      WHERE id=@id AND customer_name=@name COLLATE NOCASE",cn)){
      cmd.Parameters.AddWithValue("@id",entryId);cmd.Parameters.AddWithValue("@name",name);
      using(var rd=cmd.ExecuteReader()){
       if(!rd.Read()){MessageBox.Show("Lancamento nao encontrado.");return false;}
       kind=Convert.ToString(rd[0]);
       amountApplied=-Convert.ToDecimal(rd[1]);
       if(rd[2]==DBNull.Value){MessageBox.Show("Este registro nao tem venda vinculada. Selecione um abatimento de venda.");return false;}
       saleId=Convert.ToInt64(rd[2]);
      }
     }
     if(kind!="UTILIZACAO"||amountApplied<=0){
      MessageBox.Show("Voltar ao Fiado funciona para abatimento automatico de credito. Outros pagamentos exigem conferencia especifica para nao afetar dinheiro recebido.");return false;
     }
     long paymentId;
     using(var cmd=new SQLiteCommand(@"SELECT id FROM credit_payments WHERE sale_id=@sale AND amount=@amount
      AND payment_method='CREDITO_CLIENTE' AND note='Credito antecipado aplicado automaticamente'",cn)){
      cmd.Parameters.AddWithValue("@sale",saleId);cmd.Parameters.AddWithValue("@amount",amountApplied);
      var ids=new List<long>();
      using(var rd=cmd.ExecuteReader())while(rd.Read())ids.Add(Convert.ToInt64(rd[0]));
      if(ids.Count!=1)throw new Exception("Pagamento ambiguo ou ausente na venda #"+saleId+". Nenhuma alteracao feita.");
      paymentId=ids[0];
     }
     using(var cmd=new SQLiteCommand("SELECT COUNT(*) FROM sales WHERE id=@sale AND customer_name=@name COLLATE NOCASE AND UPPER(COALESCE(status,'')) NOT LIKE '%EXCLUID%'",cn)){
      cmd.Parameters.AddWithValue("@sale",saleId);cmd.Parameters.AddWithValue("@name",name);
      if(Convert.ToInt32(cmd.ExecuteScalar())!=1)throw new Exception("Venda excluida ou pertence a outro cliente.");
     }
     if(MessageBox.Show("Venda #"+saleId+" - "+name+"\nAbatimento de credito: R$ "+amountApplied.ToString("N2")+
      "\n\nVoltar este valor ao Fiado? O pagamento em dinheiro, se existir, sera preservado. "+
      "O credito utilizado retornara ao saldo de credito guardado.\nA operacao sera auditada.",
      "Confirmar reabertura de Fiado",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return false;
     string backup=CreditBackupPath(db,"antes-reabrir-fiado-");
     using(var dest=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=backup}.ToString())){
      dest.Open();cn.BackupDatabase(dest,"main","main",-1,null,0);
      using(var check=new SQLiteCommand("PRAGMA integrity_check",dest))
       if(Convert.ToString(check.ExecuteScalar())!="ok")throw new Exception("Backup invalido.");
     }
     if(new FileInfo(backup).Length<=0)throw new Exception("Backup vazio.");
     using(var tx=cn.BeginTransaction()){
      using(var audit=new SQLiteCommand(@"CREATE TABLE IF NOT EXISTS fiado_credit_payment_reversals(
       id INTEGER PRIMARY KEY AUTOINCREMENT,customer_name TEXT NOT NULL,sale_id INTEGER NOT NULL,
       entry_id INTEGER NOT NULL,payment_id INTEGER NOT NULL,amount NUMERIC NOT NULL,
       reversed_at TEXT NOT NULL DEFAULT (datetime('now','localtime')))",cn,tx))audit.ExecuteNonQuery();
      using(var audit=new SQLiteCommand(@"INSERT INTO fiado_credit_payment_reversals
       (customer_name,sale_id,entry_id,payment_id,amount) VALUES(@name,@sale,@entry,@payment,@amount)",cn,tx)){
       audit.Parameters.AddWithValue("@name",name);audit.Parameters.AddWithValue("@sale",saleId);
       audit.Parameters.AddWithValue("@entry",entryId);audit.Parameters.AddWithValue("@payment",paymentId);
       audit.Parameters.AddWithValue("@amount",amountApplied);
       audit.ExecuteNonQuery();
      }
      using(var del=new SQLiteCommand(@"DELETE FROM credit_payments WHERE id=@id AND sale_id=@sale
       AND payment_method='CREDITO_CLIENTE' AND amount=@amount",cn,tx)){
       del.Parameters.AddWithValue("@id",paymentId);del.Parameters.AddWithValue("@sale",saleId);
       del.Parameters.AddWithValue("@amount",amountApplied);
       if(del.ExecuteNonQuery()!=1)throw new Exception("Pagamento mudou; estorno cancelado.");
      }
      using(var del=new SQLiteCommand(@"DELETE FROM customer_credit_entries WHERE id=@id AND
       customer_name=@name COLLATE NOCASE AND entry_type='UTILIZACAO' AND amount=@amount",cn,tx)){
       del.Parameters.AddWithValue("@id",entryId);del.Parameters.AddWithValue("@name",name);
       del.Parameters.AddWithValue("@amount",-amountApplied);
       if(del.ExecuteNonQuery()!=1)throw new Exception("Abatimento mudou; estorno cancelado.");
      }
      tx.Commit();
     }
    }
    LoadClients();SelectClient(customer);
    MessageBox.Show("Abatimento devolvido ao Fiado. O credito guardado foi restaurado. Consulte o historico de auditoria.");
    return true;
   }catch(Exception ex){MessageBox.Show("Nao foi possivel voltar ao Fiado: "+ex.Message,"Fiado",MessageBoxButtons.OK,MessageBoxIcon.Error);return false;}
  }
  // Estorno manual e atomico. Para nao atribuir abatimentos ao deposito errado,
  // esta operacao so aceita carteira com um unico deposito positivo.
  bool ReverseInvalidDeposit(long entryId){
   if(string.IsNullOrWhiteSpace(customer))return false;
   try{
    EnsureCustomerCredit(db);
    string name=customer.Trim();
    using(var cn=Open(false)){
     decimal deposit=0m;
     using(var cmd=new SQLiteCommand("SELECT amount FROM customer_credit_entries WHERE id=@id AND customer_name=@name COLLATE NOCASE AND entry_type='DEPOSITO'",cn)){
      cmd.Parameters.AddWithValue("@id",entryId);cmd.Parameters.AddWithValue("@name",name);
      object val=cmd.ExecuteScalar();
      if(val==null||val==DBNull.Value){MessageBox.Show("Selecione um credito guardado (deposito) no historico.");return false;}
      deposit=Convert.ToDecimal(val);
     }
     using(var cmd=new SQLiteCommand("SELECT COUNT(*) FROM customer_credit_entries WHERE customer_name=@name COLLATE NOCASE AND amount>0",cn)){
      cmd.Parameters.AddWithValue("@name",name);
      if(Convert.ToInt32(cmd.ExecuteScalar())!=1){
       MessageBox.Show("Existem outros creditos guardados ou ajustes positivos no historico. Nao e seguro associar automaticamente os abatimentos a este deposito. Nenhum dado foi alterado.");return false;
      }
     }
     var movements=new List<Tuple<long,long,decimal>>();
     decimal totalUsed=0m;
     using(var cmd=new SQLiteCommand("SELECT id,entry_type,amount,sale_id,details FROM customer_credit_entries WHERE customer_name=@name COLLATE NOCASE AND amount<0 ORDER BY id",cn)){
      cmd.Parameters.AddWithValue("@name",name);
      using(var rd=cmd.ExecuteReader())while(rd.Read()){
       string type=Convert.ToString(rd[1]);decimal value=-Convert.ToDecimal(rd[2]);
       long saleId=0;
       if(type=="UTILIZACAO" && rd[3]!=DBNull.Value)saleId=Convert.ToInt64(rd[3]);
       else if(type=="UTILIZACAO_FIADO"){
        string note=Convert.ToString(rd[4]);
        int mark=note.LastIndexOf("venda #",StringComparison.OrdinalIgnoreCase);
        if(mark<0||!long.TryParse(note.Substring(mark+7).Trim(),out saleId))throw new Exception("Abatimento sem identificacao de venda. Estorno bloqueado.");
       }else throw new Exception("Existe outra saida de credito que exige conferencia manual. Estorno bloqueado.");
       if(saleId<=0)throw new Exception("Venda invalida em abatimento.");
       movements.Add(Tuple.Create(rd.GetInt64(0),saleId,value));
       totalUsed+=value;
      }
     }
     if(totalUsed>deposit+0.005m)throw new Exception("Abatimentos superam o deposito. Conferencia necessaria.");
     if(MessageBox.Show("Cliente: "+name+"\nDeposito ficticio: R$ "+deposit.ToString("N2")+
      "\nAbatimentos a reabrir no Fiado: R$ "+totalUsed.ToString("N2")+
      "\nVendas afetadas: "+movements.Count+
      "\n\nConfirmar estorno com backup e auditoria?","Estornar credito inexistente",
      MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return false;
     string backup=CreditBackupPath(db,"antes-estornar-credito-");
     using(var dest=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=backup}.ToString())){
      dest.Open();cn.BackupDatabase(dest,"main","main",-1,null,0);
      using(var check=new SQLiteCommand("PRAGMA integrity_check",dest))
       if(Convert.ToString(check.ExecuteScalar())!="ok")throw new Exception("Backup invalido. Nada alterado.");
     }
     if(new FileInfo(backup).Length<=0)throw new Exception("Backup vazio.");
     using(var tx=cn.BeginTransaction()){
      using(var audit=new SQLiteCommand(@"CREATE TABLE IF NOT EXISTS customer_credit_reversals
       (id INTEGER PRIMARY KEY AUTOINCREMENT,deposit_id INTEGER NOT NULL,customer_name TEXT NOT NULL,
        deposit_amount NUMERIC NOT NULL,restored_amount NUMERIC NOT NULL,
        reversed_at TEXT NOT NULL DEFAULT (datetime('now','localtime')))",cn,tx))audit.ExecuteNonQuery();
      // Arquivo imutavel dos registros originais antes de qualquer estorno.
      using(var archive=new SQLiteCommand(@"CREATE TABLE IF NOT EXISTS customer_credit_reversal_archive(
       id INTEGER PRIMARY KEY AUTOINCREMENT,deposit_id INTEGER NOT NULL,
       original_entry_id INTEGER,original_payment_id INTEGER,customer_name TEXT NOT NULL,
       sale_id INTEGER,amount NUMERIC NOT NULL,entry_type TEXT NOT NULL,
       method TEXT NOT NULL,details TEXT NOT NULL,
       archived_at TEXT NOT NULL DEFAULT (datetime('now','localtime')))",cn,tx))archive.ExecuteNonQuery();
      using(var snapshot=new SQLiteCommand(@"INSERT INTO customer_credit_reversal_archive
       (deposit_id,original_entry_id,customer_name,sale_id,amount,entry_type,method,details)
       SELECT @deposit,id,customer_name,sale_id,amount,entry_type,method,details
       FROM customer_credit_entries WHERE id=@deposit AND customer_name=@name COLLATE NOCASE",cn,tx)){
       snapshot.Parameters.AddWithValue("@deposit",entryId);snapshot.Parameters.AddWithValue("@name",name);
       if(snapshot.ExecuteNonQuery()!=1)throw new Exception("Falha ao arquivar deposito.");
      }
      foreach(var m in movements){
       string note=m.Item2.ToString();
       string expected=m.Item1.ToString();
       // Identificar o pagamento exato, nunca remover pagamentos reais.
       string sql=@"SELECT id FROM credit_payments WHERE sale_id=@sale AND amount=@amount
        AND payment_method=@method AND note=@note";
       string methodName="CREDITO_CLIENTE";
       string paymentNote;
       using(var kind=new SQLiteCommand("SELECT entry_type FROM customer_credit_entries WHERE id=@id",cn,tx)){
        kind.Parameters.AddWithValue("@id",m.Item1);
        paymentNote=Convert.ToString(kind.ExecuteScalar())=="UTILIZACAO"?
         "Credito antecipado aplicado automaticamente":null;
       }
       if(paymentNote==null){
        sql=@"SELECT id FROM credit_payments WHERE sale_id=@sale AND amount=@amount
         AND payment_method='CREDITO DEPOSITADO' AND note LIKE 'Recebimento %'";
       }
       var matches=new List<long>();
       using(var payment=new SQLiteCommand(sql,cn,tx)){
        payment.Parameters.AddWithValue("@sale",m.Item2);
        payment.Parameters.AddWithValue("@amount",m.Item3);
        payment.Parameters.AddWithValue("@method",methodName);
        payment.Parameters.AddWithValue("@note",paymentNote??"");
        using(var rd=payment.ExecuteReader())while(rd.Read())matches.Add(rd.GetInt64(0));
       }
       if(matches.Count!=1)throw new Exception("Pagamento da venda #"+m.Item2+" ambiguo ou ausente. Nenhum dado alterado.");
       using(var snapshot=new SQLiteCommand(@"INSERT INTO customer_credit_reversal_archive
        (deposit_id,original_entry_id,original_payment_id,customer_name,sale_id,amount,entry_type,method,details)
        SELECT @deposit,e.id,@payment,e.customer_name,@sale,e.amount,e.entry_type,e.method,e.details
        FROM customer_credit_entries e WHERE e.id=@entry AND e.customer_name=@name COLLATE NOCASE",cn,tx)){
        snapshot.Parameters.AddWithValue("@deposit",entryId);snapshot.Parameters.AddWithValue("@payment",matches[0]);
        snapshot.Parameters.AddWithValue("@sale",m.Item2);snapshot.Parameters.AddWithValue("@entry",m.Item1);
        snapshot.Parameters.AddWithValue("@name",name);
        if(snapshot.ExecuteNonQuery()!=1)throw new Exception("Falha ao arquivar abatimento.");
       }
       using(var del=new SQLiteCommand("DELETE FROM credit_payments WHERE id=@id",cn,tx)){
        del.Parameters.AddWithValue("@id",matches[0]);
        if(del.ExecuteNonQuery()!=1)throw new Exception("Falha ao reabrir venda.");
       }
       using(var del=new SQLiteCommand("DELETE FROM customer_credit_entries WHERE id=@id AND customer_name=@name COLLATE NOCASE",cn,tx)){
        del.Parameters.AddWithValue("@id",m.Item1);del.Parameters.AddWithValue("@name",name);
        if(del.ExecuteNonQuery()!=1)throw new Exception("Falha ao estornar abatimento.");
       }
      }
      using(var del=new SQLiteCommand("DELETE FROM customer_credit_entries WHERE id=@id AND customer_name=@name COLLATE NOCASE AND entry_type='DEPOSITO' AND amount=@amount",cn,tx)){
       del.Parameters.AddWithValue("@id",entryId);del.Parameters.AddWithValue("@name",name);del.Parameters.AddWithValue("@amount",deposit);
       if(del.ExecuteNonQuery()!=1)throw new Exception("Deposito foi alterado. Operacao cancelada.");
      }
      using(var audit=new SQLiteCommand("INSERT INTO customer_credit_reversals(deposit_id,customer_name,deposit_amount,restored_amount) VALUES(@id,@name,@deposit,@restored)",cn,tx)){
       audit.Parameters.AddWithValue("@id",entryId);audit.Parameters.AddWithValue("@name",name);
       audit.Parameters.AddWithValue("@deposit",deposit);audit.Parameters.AddWithValue("@restored",totalUsed);audit.ExecuteNonQuery();
      }
      tx.Commit();
     }
    }
    LoadClients();SelectClient(customer);
    MessageBox.Show("Credito ficticio estornado. Abatimentos reabertos no Fiado. Confira os saldos.","Estorno concluido");
    return true;
   }catch(Exception ex){MessageBox.Show("Estorno nao realizado: "+ex.Message,"Seguranca do Fiado",MessageBoxButtons.OK,MessageBoxIcon.Error);return false;}
  }
  bool CorrectCreditHistoryEntry(long entryId){
   if(string.IsNullOrWhiteSpace(customer))return false;
   try{
    EnsureCustomerCredit(db);
    decimal original;string kind;object sale;
    using(var cn=Open(true))
    using(var cmd=new SQLiteCommand("SELECT amount,entry_type,sale_id FROM customer_credit_entries WHERE id=@id AND customer_name=@client COLLATE NOCASE",cn)){
     cmd.Parameters.AddWithValue("@id",entryId);cmd.Parameters.AddWithValue("@client",customer.Trim());
     using(var rd=cmd.ExecuteReader()){
      if(!rd.Read()){MessageBox.Show("Lancamento nao localizado.");return false;}
      original=Convert.ToDecimal(rd[0]);kind=Convert.ToString(rd[1]);sale=rd[2];
     }
    }
    if(sale!=DBNull.Value||!(kind=="DEPOSITO"||kind=="AJUSTE_AUDITADO")){
     MessageBox.Show("Por seguranca, esta correcao so permite depositos e ajustes sem venda vinculada. Abatimentos de vendas exigem estorno da venda.");return false;
    }
    using(var dlg=new StyledDialogForm{Text="Corrigir lancamento - "+customer,Width=480,Height=260,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false}){
     var title=new Label{Left=16,Top=14,Width=430,Height=44,Text="Valor registrado: R$ "+original.ToString("N2")+"\nInforme o valor correto. Zero retira o lancamento do extrato."};
     var value=new NumericUpDown{Left=16,Top=66,Width=200,DecimalPlaces=2,Minimum=-100000000,Maximum=100000000,Value=original};
     var confirm=new Button{Left=16,Top=122,Width=205,Height=36,Text="Salvar correcao",DialogResult=DialogResult.OK};
     var cancel=new Button{Left=235,Top=122,Width=150,Height=36,Text="Cancelar",DialogResult=DialogResult.Cancel};
     dlg.Controls.AddRange(new Control[]{title,value,confirm,cancel});
     if(dlg.ShowDialog(this)!=DialogResult.OK||value.Value==original)return false;
     decimal corrected=value.Value;
     if(kind=="DEPOSITO"&&corrected<0){MessageBox.Show("Credito guardado nao pode ser negativo.");return false;}
     // Impedir cancelamento isolado de deposito ja usado em vendas.
     // Sem estornar os pagamentos vinculados, a divida ficaria incorreta.
     if(kind=="DEPOSITO" && corrected<original){
      using(var checkCn=Open(true))
      using(var used=new SQLiteCommand(@"SELECT COUNT(*) FROM customer_credit_entries
       WHERE customer_name=@client COLLATE NOCASE
       AND entry_type IN ('UTILIZACAO','UTILIZACAO_FIADO') AND amount<0",checkCn)){
       used.Parameters.AddWithValue("@client",customer.Trim());
       if(Convert.ToInt32(used.ExecuteScalar())>0){
        MessageBox.Show("Este cliente possui compras abatidas com credito depositado. "+
         "Nao e seguro retirar apenas o deposito: primeiro e necessario identificar "+
         "e estornar os pagamentos de credito vinculados a cada venda. "+
         "Nenhum registro foi alterado.","Estorno completo necessario",
         MessageBoxButtons.OK,MessageBoxIcon.Warning);
        return false;
       }
      }
     }
     if(MessageBox.Show("Corrigir registro "+entryId+" de R$ "+original.ToString("N2")+" para R$ "+corrected.ToString("N2")+"?\nO saldo sera recalculado e a alteracao sera auditada.","Confirmar correcao",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return false;
     string backup=CreditBackupPath(db,"antes-corrigir-historico-");
     using(var cn=Open(false)){
      using(var dest=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=backup}.ToString())){
       dest.Open();cn.BackupDatabase(dest,"main","main",-1,null,0);
       using(var check=new SQLiteCommand("PRAGMA integrity_check",dest))if(Convert.ToString(check.ExecuteScalar())!="ok")throw new Exception("Backup invalido.");
      }
      if(new FileInfo(backup).Length<=0)throw new Exception("Backup vazio.");
      using(var tx=cn.BeginTransaction()){
       using(var audit=new SQLiteCommand("CREATE TABLE IF NOT EXISTS customer_credit_corrections(id INTEGER PRIMARY KEY AUTOINCREMENT,entry_id INTEGER NOT NULL,customer_name TEXT NOT NULL,old_amount NUMERIC NOT NULL,new_amount NUMERIC NOT NULL,corrected_at TEXT NOT NULL DEFAULT (datetime('now','localtime')))",cn,tx))audit.ExecuteNonQuery();
       using(var cmd=new SQLiteCommand("UPDATE customer_credit_entries SET amount=@amount WHERE id=@id AND customer_name=@client COLLATE NOCASE AND amount=@old AND sale_id IS NULL AND entry_type IN ('DEPOSITO','AJUSTE_AUDITADO')",cn,tx)){
        cmd.Parameters.AddWithValue("@amount",corrected);cmd.Parameters.AddWithValue("@id",entryId);cmd.Parameters.AddWithValue("@client",customer.Trim());cmd.Parameters.AddWithValue("@old",original);
        // A tabela original proibe valor zero; retirar equivale a excluir apos auditar.
        if(corrected==0)cmd.CommandText="DELETE FROM customer_credit_entries WHERE id=@id AND customer_name=@client COLLATE NOCASE AND amount=@old AND sale_id IS NULL AND entry_type IN ('DEPOSITO','AJUSTE_AUDITADO')";
        if(cmd.ExecuteNonQuery()!=1)throw new Exception("Registro foi alterado por outra operacao. Atualize a tela.");
       }
       using(var audit=new SQLiteCommand("INSERT INTO customer_credit_corrections(entry_id,customer_name,old_amount,new_amount) VALUES(@id,@client,@old,@new)",cn,tx)){
        audit.Parameters.AddWithValue("@id",entryId);audit.Parameters.AddWithValue("@client",customer.Trim());audit.Parameters.AddWithValue("@old",original);audit.Parameters.AddWithValue("@new",corrected);audit.ExecuteNonQuery();
       }
       tx.Commit();
      }
     }
     LoadClients();SelectClient(customer);return true;
    }
   }catch(Exception ex){MessageBox.Show("Correcao nao realizada: "+ex.Message);return false;}
  }
  void ShowCreditSaleItems(long saleId,string operation,decimal entryAmount){
   try{
    using(var cn=Open(true)){
     string saleCustomer="";
     decimal total=0m;
     using(var cmd=new SQLiteCommand("SELECT customer_name,COALESCE(gross_total,0) FROM sales WHERE id=@sale LIMIT 1",cn)){
      cmd.Parameters.AddWithValue("@sale",saleId);
      using(var reader=cmd.ExecuteReader()){
       if(!reader.Read()){MessageBox.Show("A venda "+saleId+" nao foi localizada. O abatimento continua registrado no historico.");return;}
       saleCustomer=Convert.ToString(reader[0]);total=Convert.ToDecimal(reader[1]);
      }
     }
     if(!string.Equals((saleCustomer??"").Trim(),(customer??"").Trim(),StringComparison.OrdinalIgnoreCase)){
      MessageBox.Show("A venda vinculada pertence a outro cadastro de cliente. Consulte a auditoria antes de associar produtos.");return;
     }
     var items=new DataTable();
     using(var cmd=new SQLiteCommand("SELECT name AS Produto,quantity AS Quantidade,unit_price AS [Preco unitario],total AS [Total] FROM sale_items WHERE sale_id=@sale ORDER BY id",cn)){
      cmd.Parameters.AddWithValue("@sale",saleId);
      using(var adapter=new SQLiteDataAdapter(cmd))adapter.Fill(items);
     }
     using(var dlg=new StyledDialogForm{Text="Produtos da venda "+saleId+" - "+customer,Width=900,Height=570,MinimumSize=new Size(650,420),StartPosition=FormStartPosition.CenterParent,BackColor=Color.FromArgb(229,242,255)}){
      // Layout em linhas explicitas: o tema global nao pode colocar cabecalho
      // ou rodape sobre a tabela de produtos.
      var host=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(10),Margin=Padding.Empty};
      host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
      host.RowStyles.Add(new RowStyle(SizeType.Absolute,86));
      host.RowStyles.Add(new RowStyle(SizeType.Percent,100));
      host.RowStyles.Add(new RowStyle(SizeType.Absolute,52));
      var summary=new Label{Dock=DockStyle.Fill,AutoSize=false,Padding=new Padding(12,8,8,4),Font=new Font("Segoe UI",11,FontStyle.Bold),ForeColor=Color.FromArgb(21,75,156),Text="Venda "+saleId+"  |  Total: R$ "+total.ToString("N2")+"\\nOperacao: "+operation+"  |  Valor do movimento: R$ "+entryAmount.ToString("N2")};
      var table=new DataGridView{Dock=DockStyle.Fill,Name="amzgFiadoSaleItems",ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoGenerateColumns=true,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,BackgroundColor=Color.White,BorderStyle=BorderStyle.FixedSingle,ColumnHeadersVisible=true,ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.AutoSize,RowTemplate={Height=32}};
      table.EnableHeadersVisualStyles=false;table.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(21,75,156);table.ColumnHeadersDefaultCellStyle.ForeColor=Color.White;
      table.DefaultCellStyle.ForeColor=Color.Black;table.DefaultCellStyle.BackColor=Color.White;
      table.DataSource=items;
      var note=new Label{Dock=DockStyle.Fill,AutoSize=false,Padding=new Padding(12,7,8,0),Text=items.Rows.Count==0?"A venda existe, mas nao ha itens em sale_items. Verifique o registro original; nenhum valor foi alterado.":"Produtos da venda vinculada ao movimento de credito. Consulta sem alterar valores.",ForeColor=Color.FromArgb(21,75,156)};
      host.Controls.Add(summary,0,0);host.Controls.Add(table,0,1);host.Controls.Add(note,0,2);
      dlg.Controls.Add(host);
      dlg.Shown+=(sender,args)=>{table.Refresh();};
      dlg.ShowDialog(this);
     }
    }
   }catch(Exception ex){MessageBox.Show("Nao foi possivel consultar os produtos: "+ex.Message);}
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
  // O limite e rotativo: descontar somente o saldo devedor atual, nao o total comprado no mes.
  public static decimal PeriodFiadoUsed(SQLiteConnection cn,SQLiteTransaction tx,string name,string period){
   const string sql=@"SELECT COALESCE(SUM(CASE
    WHEN COALESCE(s.gross_total,0)-COALESCE((SELECT SUM(cp.amount) FROM credit_payments cp WHERE cp.sale_id=s.id),0)>0
    THEN COALESCE(s.gross_total,0)-COALESCE((SELECT SUM(cp.amount) FROM credit_payments cp WHERE cp.sale_id=s.id),0)
    ELSE 0 END),0)
    FROM sales s WHERE TRIM(s.customer_name)=@client COLLATE NOCASE
    AND (UPPER(COALESCE(s.payment_method,'')) LIKE '%FIADO%'
      OR UPPER(COALESCE(s.status,'')) LIKE '%FIADO%'
      OR EXISTS (SELECT 1 FROM sale_payments sp WHERE sp.sale_id=s.id
       AND UPPER(TRIM(COALESCE(sp.payment_method,''))) LIKE '%FIADO%'))
    AND UPPER(COALESCE(s.status,'')) NOT LIKE '%EXCLUID%'";
   using(var cmd=new SQLiteCommand(sql,cn,tx)){
    cmd.Parameters.AddWithValue("@client",name.Trim());
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
      throw new InvalidOperationException("Limite de Fiado "+period.ToLowerInvariant()+" excedido para "+name+". Limite R$ "+limit.ToString("N2")+", usado R$ "+used.ToString("N2")+", nova compra fiada R$ "+newFiado.ToString("N2")+". Venda nao registrada.");
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
    using(var dialog=new StyledDialogForm{Text="Limite de Fiado - "+customer,Width=480,Height=265,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false}){
     var label=new Label{Text="Limite atual / novo (R$):",Left=16,Top=18,Width=290,Height=28};
     var value=new NumericUpDown{Left=16,Top=51,Width=195,DecimalPlaces=2,Maximum=100000000,Value=Math.Max(0,Math.Min(existing,100000000))};
     var periodLabel=new Label{Text="Renovacao do limite:",Left=235,Top=18,Width=190,Height=28};
     var period=new ComboBox{Left=235,Top=51,Width=195,DropDownStyle=ComboBoxStyle.DropDownList};
     period.Items.AddRange(new object[]{"SEMANAL","MENSAL"});period.SelectedItem=existingPeriod;
     var hint=new Label{Text="Mostra somente o limite vigente. Altere o valor e salve. Semanal: segunda a domingo; mensal: dia 1 ao ultimo dia. Zero bloqueia novas vendas fiadas.",Left=16,Top=97,Width=420,Height=52};
     var save=new Button{Text="Salvar limite do cliente",Left=16,Top=164,Width=220,Height=36,DialogResult=DialogResult.OK};
     var cancel=new Button{Text="Cancelar",Left=250,Top=164,Width=140,Height=36,DialogResult=DialogResult.Cancel};
     dialog.Controls.AddRange(new Control[]{label,value,periodLabel,period,hint,save,cancel});
     if(dialog.ShowDialog(this)!=DialogResult.OK)return;
     string selectedPeriod=Convert.ToString(period.SelectedItem);
     decimal selectedLimit=value.Value;
     // Salvar diretamente o valor atual informado, sem pedir motivo ou confirmacao adicional.
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
