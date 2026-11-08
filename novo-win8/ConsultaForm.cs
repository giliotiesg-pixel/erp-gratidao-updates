using System;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.IO;
using System.Text;
using System.Windows.Forms;
namespace ArmazemGratidao {
 public sealed class ConsultaForm:Form {
  readonly string db;readonly bool credit;
  public bool IsCreditWindow {get{return credit;}}
bool previous=false;bool selectedDate=false;bool selectedMonth=false;
  readonly DateTimePicker dateFilter=new DateTimePicker {Format=DateTimePickerFormat.Short,Width=115};
  readonly DateTimePicker monthFilter=new DateTimePicker {Format=DateTimePickerFormat.Custom,CustomFormat="MMMM yyyy",ShowUpDown=true,Width=175};
  readonly TextBox saleSearch=new TextBox {Width=110};
  readonly Label summary=new Label {Dock=DockStyle.Bottom,Height=36,TextAlign=ContentAlignment.MiddleLeft,Font=new Font("Segoe UI",10,FontStyle.Bold),BackColor=Color.FromArgb(229,242,255)};
  readonly DataGridView master=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
  readonly DataGridView details=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
  bool loadingMaster=false;
  readonly Label heading=new Label {Dock=DockStyle.Top,Height=46,Font=new Font("Segoe UI",13,FontStyle.Bold),TextAlign=ContentAlignment.MiddleLeft};
  // Componentes do painel de vendas. O modulo Fiado mantem seu layout anterior.
  readonly DateTimePicker rangeEnd=new DateTimePicker{Format=DateTimePickerFormat.Short,Width=120};
  readonly ComboBox customerFilter=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,
   Font=new Font("Segoe UI",9f),Width=170};
  readonly Label saleInfo=new Label{Dock=DockStyle.Fill,AutoEllipsis=true,
   ForeColor=Color.FromArgb(27,55,102),Font=new Font("Segoe UI",10f,FontStyle.Bold)};
  readonly Label saleTotal=new Label{Dock=DockStyle.Right,Width=186,
   Font=new Font("Segoe UI",16f,FontStyle.Bold),ForeColor=Color.FromArgb(182,29,49),
   TextAlign=ContentAlignment.MiddleCenter};
  readonly Label footerCount=new Label(),footerToday=new Label(),footerMonth=new Label();
  readonly Label headerToday=new Label(),headerMonth=new Label();
  bool selectedRange=false;bool customerOptionsLoading=false;
  string chosenCustomer="";
  readonly Color salesBlue=Color.FromArgb(18,70,153);
  readonly Color salesLight=Color.FromArgb(242,247,253);
  SplitContainer salesSplit;

  // Estilo nativo, leve e compacto: preserva as funcoes dos controles.
  static void SalesRound(Control control,int radius){
   Action apply=()=>{
    if(control.IsDisposed||control.Width<radius*2+4||control.Height<radius*2+4)return;
    using(var shape=new GraphicsPath()){
     int w=control.Width-1,h=control.Height-1,d=radius*2;
     shape.AddArc(0,0,d,d,180,90);shape.AddArc(w-d,0,d,d,270,90);
     shape.AddArc(w-d,h-d,d,d,0,90);shape.AddArc(0,h-d,d,d,90,90);
     shape.CloseFigure();
     Region former=control.Region;control.Region=new Region(shape);
     if(former!=null)former.Dispose();
    }
   };
   control.Resize+=(sender,args)=>apply();apply();
  }
  static Button SalesButton(string name,string caption,Color back){
   var btn=new Button{Name=name,Text=caption,Dock=DockStyle.Fill,
    Margin=new Padding(3,8,3,8),Padding=new Padding(4,1,4,1),
    FlatStyle=FlatStyle.Flat,BackColor=back,ForeColor=Color.White,
    Cursor=Cursors.Hand,UseMnemonic=false,AutoEllipsis=true,
    TextAlign=ContentAlignment.MiddleCenter,
    Font=new Font("Segoe UI",9.1f,FontStyle.Bold)};
   btn.FlatAppearance.BorderSize=0;
   btn.FlatAppearance.MouseOverBackColor=ControlPaint.Light(back,0.17f);
   SalesRound(btn,10);
   return btn;
  }
  static Label SalesText(string title,Font font,Color color){
   return new Label{Text=title,Font=font,ForeColor=color,
    Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,AutoEllipsis=true};
  }
  void StyleSalesGrid(DataGridView table){
   table.Font=new Font("Segoe UI",9.2f,FontStyle.Regular);
   table.BackgroundColor=Color.White;
   table.BorderStyle=BorderStyle.None;
   table.RowHeadersVisible=false;
   table.ReadOnly=true;
   table.SelectionMode=DataGridViewSelectionMode.FullRowSelect;
   table.MultiSelect=false;
   table.ScrollBars=ScrollBars.Both;
   table.AllowUserToResizeRows=false;
   table.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.None;
   table.RowTemplate.Height=33;
   table.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
   table.ColumnHeadersHeight=38;
   table.ColumnHeadersVisible=true;
   table.EnableHeadersVisualStyles=false;
   table.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(17,71,153);
   table.ColumnHeadersDefaultCellStyle.ForeColor=Color.White;
   table.ColumnHeadersDefaultCellStyle.Font=new Font("Segoe UI",9.5f,FontStyle.Bold);
   table.DefaultCellStyle.ForeColor=Color.FromArgb(31,53,90);
   table.DefaultCellStyle.SelectionBackColor=Color.FromArgb(48,117,209);
   table.DefaultCellStyle.SelectionForeColor=Color.White;
   table.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(241,246,252);
   table.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;
   table.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
  }
  void BuildSalesPage(){
   BackColor=Color.FromArgb(246,249,253);
   StyleSalesGrid(master);
   StyleSalesGrid(details);
   var layout=new TableLayoutPanel{Name="amzgSalesLayout",Dock=DockStyle.Fill,
    ColumnCount=1,RowCount=5,Padding=new Padding(9,0,9,9),Margin=Padding.Empty};
   layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f));
   layout.RowStyles.Add(new RowStyle(SizeType.Absolute,110f));
   layout.RowStyles.Add(new RowStyle(SizeType.Absolute,62f));
   layout.RowStyles.Add(new RowStyle(SizeType.Absolute,72f));
   layout.RowStyles.Add(new RowStyle(SizeType.Percent,100f));
   layout.RowStyles.Add(new RowStyle(SizeType.Absolute,76f));

   var banner=new Panel{Name="amzgSalesBanner",Dock=DockStyle.Fill,Margin=Padding.Empty,
    BackColor=Color.FromArgb(11,47,107)};
   banner.Paint+=(sender,args)=>{
    using(var gradient=new LinearGradientBrush(banner.ClientRectangle,
     Color.FromArgb(11,44,104),Color.FromArgb(17,76,161),0f))
     args.Graphics.FillRectangle(gradient,banner.ClientRectangle);
   };
   var bannerColumns=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,
    Margin=Padding.Empty,Padding=Padding.Empty,BackColor=Color.Transparent};
   bannerColumns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,29));
   bannerColumns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,37));
   bannerColumns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,34));
   var brand=new Label{Name="amzgSalesBrand",Text="ARMAZÉM GRATIDÃO",
    Font=new Font("Segoe UI",17f,FontStyle.Bold),ForeColor=Color.White,
    Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,AutoEllipsis=true};
   var module=new Label{Name="amzgSalesModuleTitle",Text="▣  MÓDULO VENDAS",
    Font=new Font("Segoe UI",21f,FontStyle.Bold),ForeColor=Color.White,
    Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,AutoEllipsis=true};
   var bannerTotals=new TableLayoutPanel{Name="amzgSalesBannerTotals",
    Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,
    Margin=new Padding(5,20,5,20),BackColor=Color.FromArgb(25,73,140)};
   bannerTotals.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
   bannerTotals.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
   bannerTotals.RowStyles.Add(new RowStyle(SizeType.Percent,40));
   bannerTotals.RowStyles.Add(new RowStyle(SizeType.Percent,60));
   bannerTotals.Controls.Add(SalesText("Total do Dia",new Font("Segoe UI",9,FontStyle.Bold),Color.White),0,0);
   bannerTotals.Controls.Add(SalesText("Total do Mês",new Font("Segoe UI",9,FontStyle.Bold),Color.White),1,0);
   foreach(var lbl in new[]{headerToday,headerMonth}){
    lbl.Dock=DockStyle.Fill;lbl.Text="R$ 0,00";
    lbl.Font=new Font("Segoe UI",15.5f,FontStyle.Bold);
    lbl.ForeColor=Color.FromArgb(255,226,84);
    lbl.TextAlign=ContentAlignment.MiddleCenter;
   }
   bannerTotals.Controls.Add(headerToday,0,1);
   bannerTotals.Controls.Add(headerMonth,1,1);
   SalesRound(bannerTotals,12);
   // Logotipo oficial incorporado no executavel; somente no cabecalho,
   // nunca como marca d'agua sobre os dados.
   var branding=new Panel{Dock=DockStyle.Fill,BackColor=Color.Transparent};
   var emblem=new PictureBox{Left=9,Top=15,Width=76,Height=78,
    SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.Transparent};
   try{
    using(var file=typeof(ConsultaForm).Assembly.GetManifestResourceStream(
     "ArmazemGratidao.LogotipoMarcaDagua.jpg")){
     if(file!=null)using(var original=Image.FromStream(file))
      emblem.Image=new Bitmap(original);
    }
   }catch{ /* A marca escrita continua se a imagem falhar. */ }
   emblem.Disposed+=(sender,args)=>{if(emblem.Image!=null)emblem.Image.Dispose();};
   brand.Dock=DockStyle.None;brand.Left=89;brand.Top=19;brand.Height=69;
   brand.TextAlign=ContentAlignment.MiddleLeft;
   brand.Font=new Font("Segoe UI",15f,FontStyle.Bold);
   branding.Resize+=(sender,args)=>{brand.Width=Math.Max(100,branding.Width-94);};
   branding.Controls.Add(emblem);branding.Controls.Add(brand);
   bannerColumns.Controls.Add(branding,0,0);
   bannerColumns.Controls.Add(module,1,0);
   bannerColumns.Controls.Add(bannerTotals,2,0);
   banner.Controls.Add(bannerColumns);
   layout.Controls.Add(banner,0,0);

   var navigation=new TableLayoutPanel{Name="amzgSalesNavigation",
    Dock=DockStyle.Fill,ColumnCount=7,RowCount=1,
    Margin=Padding.Empty,Padding=Padding.Empty};
   foreach(float width in new float[]{19f,13f,14f,15f,14f,12f,13f})
    navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,width));
   var backSlot=new Panel{Name="amzgSalesBackSlot",Dock=DockStyle.Fill,
    Margin=new Padding(3,8,3,8)};
   navigation.Controls.Add(backSlot,0,0);
   var newSale=SalesButton("amzgSalesNewSale","⊕  Nova Venda",salesBlue);
   newSale.Click+=(sender,args)=>{
    var main=Application.OpenForms.OfType<MainForm>().FirstOrDefault();
    if(main!=null)main.OpenPdvFromModule();
    else MessageBox.Show("Abra o PDV pelo menu principal.","Vendas");
   };
   var today=SalesButton("amzgSalesToday","▣  Vendas do Dia",salesBlue);
   today.Click+=(sender,args)=>{previous=false;selectedDate=false;selectedMonth=false;selectedRange=false;LoadMaster();};
   var older=SalesButton("amzgSalesPrevious","▣  Vendas Anteriores",salesBlue);
   older.Click+=(sender,args)=>{previous=true;selectedDate=false;selectedMonth=false;selectedRange=false;LoadMaster();};
   var edit=SalesButton("amzgSalesEdit","✎  Editar Venda",salesBlue);
   var editMenu=new ContextMenuStrip();
   editMenu.Items.Add("Editar produto, quantidade e preço",null,(sender,args)=>ChangeProduct());
   editMenu.Items.Add("Atualizar data / hora",null,(sender,args)=>EditSaleDate());
   editMenu.Items.Add("Visualizar itens em janela",null,(sender,args)=>ShowSaleItems());
   editMenu.Items.Add("Exportar itens CSV",null,(sender,args)=>ExportSale());
   edit.Click+=(sender,args)=>editMenu.Show(edit,new Point(0,edit.Height));
   var remove=SalesButton("amzgSalesDelete","▤  Excluir Venda",salesBlue);
   remove.Click+=(sender,args)=>DeleteSale();
   var reports=SalesButton("amzgSalesReports","▥  Totais / Relatórios",salesBlue);
   var reportMenu=new ContextMenuStrip();
   reportMenu.Items.Add("Totais por dia",null,(sender,args)=>ShowDailyTotals());
   reportMenu.Items.Add("Exportar itens da venda CSV",null,(sender,args)=>ExportSale());
   reportMenu.Items.Add("Ver apenas a data inicial",null,(sender,args)=>{
    selectedDate=true;selectedMonth=false;selectedRange=false;previous=false;LoadMaster();});
   reportMenu.Items.Add("Ver mês selecionado",null,(sender,args)=>{
    selectedMonth=true;selectedDate=false;selectedRange=false;previous=false;LoadMaster();});
   reportMenu.Items.Add("Mês anterior",null,(sender,args)=>{
    monthFilter.Value=monthFilter.Value.AddMonths(-1);
    selectedMonth=true;selectedDate=false;selectedRange=false;previous=false;LoadMaster();});
   reportMenu.Items.Add("Próximo mês",null,(sender,args)=>{
    monthFilter.Value=monthFilter.Value.AddMonths(1);
    selectedMonth=true;selectedDate=false;selectedRange=false;previous=false;LoadMaster();});
   reports.Click+=(sender,args)=>reportMenu.Show(reports,new Point(0,reports.Height));
   foreach(var btn in new[]{newSale,today,older,edit,remove,reports})
    navigation.Controls.Add(btn,navigation.Controls.Count,0);
   FormClosed+=(sender,args)=>{editMenu.Dispose();reportMenu.Dispose();};
   layout.Controls.Add(navigation,0,1);
   // Em telas estreitas (Windows 8 / 1024 px), duas fileiras evitam
   // cortar os nomes dos botoes. Em telas largas continua uma fila.
   bool narrowNav=false;
   Action fitNavigation=()=>{
    if(navigation.IsDisposed)return;
    bool twoRows=layout.ClientSize.Width<1230;
    if(twoRows==narrowNav&&navigation.Controls.Count==7)return;
    narrowNav=twoRows;
    Control[] actions=navigation.Controls.Cast<Control>().ToArray();
    navigation.SuspendLayout();
    try{
     navigation.Controls.Clear();
     navigation.ColumnStyles.Clear();navigation.RowStyles.Clear();
     navigation.ColumnCount=twoRows?4:7;navigation.RowCount=twoRows?2:1;
     if(twoRows){
      for(int i=0;i<4;i++)navigation.ColumnStyles.Add(
       new ColumnStyle(SizeType.Percent,25f));
      navigation.RowStyles.Add(new RowStyle(SizeType.Percent,50f));
      navigation.RowStyles.Add(new RowStyle(SizeType.Percent,50f));
     }else{
      foreach(float weight in new float[]{19f,13f,14f,15f,14f,12f,13f})
       navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,weight));
      navigation.RowStyles.Add(new RowStyle(SizeType.Percent,100f));
     }
     for(int i=0;i<actions.Length;i++){
      int col=twoRows?i%4:i,row=twoRows?i/4:0;
      navigation.Controls.Add(actions[i],col,row);
     }
     layout.RowStyles[1].Height=twoRows?108f:62f;
    }finally{navigation.ResumeLayout(true);}
   };
   layout.SizeChanged+=(sender,args)=>fitNavigation();
   fitNavigation();

   var filters=new TableLayoutPanel{Name="amzgSalesFilters",Dock=DockStyle.Fill,
    ColumnCount=11,RowCount=1,Padding=new Padding(9,6,9,6),
    Margin=new Padding(1,0,1,4),BackColor=Color.FromArgb(233,241,252)};
   float[] widths={7f,3f,10f,4f,10f,7f,18f,11f,9f,11f,10f};
   foreach(float w in widths)filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,w));
   filters.Controls.Add(SalesText("Período:",new Font("Segoe UI",9,FontStyle.Bold),Color.FromArgb(25,52,92)),0,0);
   filters.Controls.Add(SalesText("De:",new Font("Segoe UI",9,FontStyle.Bold),Color.FromArgb(25,52,92)),1,0);
   dateFilter.Format=DateTimePickerFormat.Custom;dateFilter.CustomFormat="dd/MM/yyyy";
   dateFilter.Dock=DockStyle.Fill;dateFilter.Margin=new Padding(1,11,2,11);
   dateFilter.Value=new DateTime(DateTime.Today.Year,DateTime.Today.Month,1);
   filters.Controls.Add(dateFilter,2,0);
   filters.Controls.Add(SalesText("Até:",new Font("Segoe UI",9,FontStyle.Bold),Color.FromArgb(25,52,92)),3,0);
   rangeEnd.Format=DateTimePickerFormat.Custom;rangeEnd.CustomFormat="dd/MM/yyyy";
   rangeEnd.Dock=DockStyle.Fill;rangeEnd.Margin=new Padding(1,11,2,11);
   rangeEnd.Value=DateTime.Today;
   filters.Controls.Add(rangeEnd,4,0);
   filters.Controls.Add(SalesText("Cliente:",new Font("Segoe UI",9,FontStyle.Bold),Color.FromArgb(25,52,92)),5,0);
   customerFilter.Dock=DockStyle.Fill;customerFilter.Margin=new Padding(1,11,2,11);
   filters.Controls.Add(customerFilter,6,0);
   filters.Controls.Add(SalesText("Nº da Venda:",new Font("Segoe UI",8.6f,FontStyle.Bold),Color.FromArgb(25,52,92)),7,0);
   saleSearch.Dock=DockStyle.Fill;saleSearch.Margin=new Padding(1,12,2,12);
   filters.Controls.Add(saleSearch,8,0);
   var search=SalesButton("amzgSalesSearch","⌕  Pesquisar",salesBlue);
   var update=SalesButton("amzgSalesUpdate","↻  Atualizar",Color.FromArgb(56,110,188));
   search.Margin=new Padding(2,8,2,8);
   update.Margin=new Padding(2,8,2,8);
   search.Click+=(sender,args)=>{
    if(saleSearch.Text.Trim().Length>0)SearchSale();
    else if(dateFilter.Value.Date>rangeEnd.Value.Date)
     MessageBox.Show("A data inicial não pode ser posterior à data final.","Vendas");
    else {selectedRange=true;selectedMonth=false;selectedDate=false;previous=false;LoadMaster();}
   };
   update.Click+=(sender,args)=>{if(saleSearch.Text.Trim().Length>0)SearchSale();else LoadMaster();};
   saleSearch.KeyDown+=(sender,args)=>{if(args.KeyCode==Keys.Enter){search.PerformClick();args.SuppressKeyPress=true;}};
   customerFilter.SelectedIndexChanged+=(sender,args)=>{
    if(customerOptionsLoading)return;
    chosenCustomer=customerFilter.SelectedIndex<=0?"":Convert.ToString(customerFilter.SelectedItem);
    LoadMaster();
   };
   filters.Controls.Add(search,9,0);filters.Controls.Add(update,10,0);
   SalesRound(filters,12);
   layout.Controls.Add(filters,0,2);

   salesSplit=new SplitContainer{Name="amzgSalesSplit",Dock=DockStyle.Fill,
    Orientation=Orientation.Vertical,Margin=new Padding(0,10,0,4),
    SplitterWidth=9,
    BackColor=Color.FromArgb(236,244,253)};
   // Painel esquerdo: lista das vendas, colunas fixas legiveis.
   var left=new Panel{Name="amzgSalesListPanel",Dock=DockStyle.Fill,
    BackColor=Color.White,Padding=new Padding(2)};
   var leftHeader=new Label{Name="amzgSalesListTitle",Dock=DockStyle.Top,Height=40,
    BackColor=salesBlue,ForeColor=Color.White,
    Font=new Font("Segoe UI",11f,FontStyle.Bold),TextAlign=ContentAlignment.MiddleLeft,
    Text="  Vendas do Dia"};
   left.Controls.Add(master);left.Controls.Add(leftHeader);
   salesSplit.Panel1.Controls.Add(left);
   // Painel direito: dados e itens da venda selecionada, sem popup obrigatorio.
   var right=new Panel{Name="amzgSalesDetailPanel",Dock=DockStyle.Fill,
    BackColor=Color.White,Padding=new Padding(2)};
   heading.Name="amzgSalesDetailTitle";
   heading.Height=40;heading.BackColor=salesBlue;heading.ForeColor=Color.White;
   heading.Font=new Font("Segoe UI",11f,FontStyle.Bold);
   heading.Padding=new Padding(12,0,0,0);heading.Text="DETALHES DA VENDA";
   var saleHeader=new Panel{Name="amzgSalesDetailSummary",Dock=DockStyle.Top,Height=102,
    BackColor=Color.FromArgb(234,241,251)};
   var infoCaption=new Label{Dock=DockStyle.Left,Width=145,Text="  Cliente:\n  Data / Hora:\n  Forma(s) Pgto:",
    Font=new Font("Segoe UI",9.2f,FontStyle.Bold),
    ForeColor=Color.FromArgb(22,58,121),Padding=new Padding(5,5,0,0)};
   var valuePanel=new Panel{Dock=DockStyle.Fill,Padding=new Padding(0,5,0,0)};
   saleInfo.Text="Selecione uma venda\n\n";
   valuePanel.Controls.Add(saleInfo);
   var totalPanel=new Panel{Dock=DockStyle.Right,Width=195,
    BackColor=Color.FromArgb(244,246,251)};
   totalPanel.Controls.Add(new Label{Dock=DockStyle.Top,Height=27,
    Text="Total da Venda",Font=new Font("Segoe UI",10f,FontStyle.Bold),
    ForeColor=Color.FromArgb(22,58,121),TextAlign=ContentAlignment.MiddleCenter});
   saleTotal.Text="R$ 0,00";totalPanel.Controls.Add(saleTotal);
   saleHeader.Controls.Add(valuePanel);saleHeader.Controls.Add(infoCaption);saleHeader.Controls.Add(totalPanel);
   right.Controls.Add(details);right.Controls.Add(saleHeader);right.Controls.Add(heading);
   salesSplit.Panel2.Controls.Add(right);
   layout.Controls.Add(salesSplit,0,3);
   salesSplit.SizeChanged+=(sender,args)=>{
    if(salesSplit.IsDisposed||salesSplit.Width<710)return;
    int target=(int)(salesSplit.Width*0.51);
    int min=300;
    int max=salesSplit.Width-salesSplit.SplitterWidth-300;
    if(max>=min){
     int clamped=Math.Max(min,Math.Min(max,target));
     if(salesSplit.SplitterDistance!=clamped)salesSplit.SplitterDistance=clamped;
    }
   };
   var bottom=new TableLayoutPanel{Name="amzgSalesTotalsBar",Dock=DockStyle.Fill,
    ColumnCount=4,RowCount=1,Margin=new Padding(0,5,0,0),
    Padding=new Padding(10,6,10,6),BackColor=Color.FromArgb(13,61,138)};
   bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,24));
   bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,26));
   bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,28));
   bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,22));
   foreach(var label in new[]{footerCount,footerToday,footerMonth}){
    label.Dock=DockStyle.Fill;label.TextAlign=ContentAlignment.MiddleCenter;
    label.ForeColor=Color.White;label.Font=new Font("Segoe UI",10.5f,FontStyle.Bold);
    label.AutoEllipsis=true;
   }
   bottom.Controls.Add(footerCount,0,0);
   bottom.Controls.Add(footerToday,1,0);
   bottom.Controls.Add(footerMonth,2,0);
   var daily=SalesButton("amzgSalesDailyTotals","▥  Consultar Totais por Dia",Color.FromArgb(24,94,181));
   daily.Margin=new Padding(4,7,4,7);
   daily.Click+=(sender,args)=>ShowDailyTotals();
   bottom.Controls.Add(daily,3,0);
   SalesRound(bottom,12);layout.Controls.Add(bottom,0,4);
   Controls.Add(layout);
   LoadCustomerOptions();
  }

  void LoadCustomerOptions(){
   if(credit)return;
   customerOptionsLoading=true;
   try{
    customerFilter.Items.Clear();
    customerFilter.Items.Add("Todos os Clientes");
    var customers=Query("SELECT DISTINCT TRIM(customer_name) AS nome FROM sales "+
     "WHERE TRIM(COALESCE(customer_name,''))<>'' ORDER BY nome COLLATE NOCASE LIMIT 2000");
    foreach(DataRow row in customers.Rows){
     var name=Convert.ToString(row["nome"]);
     if(!string.IsNullOrWhiteSpace(name))customerFilter.Items.Add(name);
    }
    customerFilter.SelectedIndex=0;
   }catch(Exception){customerFilter.Items.Clear();customerFilter.Items.Add("Todos os Clientes");customerFilter.SelectedIndex=0;}
   finally{customerOptionsLoading=false;}
  }

  public ConsultaForm(string path,bool fiado){
   db=path;credit=fiado;
   Text=fiado?"Fiado - Clientes e Vendas":"Vendas - Itens por Venda";
   Width=1360;Height=850;MinimumSize=new Size(1020,660);
   Font=new Font("Segoe UI",9.5f);
   BackColor=Color.FromArgb(246,249,253);
   var working=Screen.FromControl(this).WorkingArea;
   Size=new Size(Math.Min(Width,working.Width),Math.Min(Height,working.Height));
   if(fiado){
    // Nao alterar o modulo Fiado por causa do novo visual de Vendas.
    var split=new SplitContainer{Dock=DockStyle.Fill,
     Orientation=Orientation.Horizontal,SplitterDistance=465};
    split.Panel1.Controls.Add(master);
    split.Panel2.Controls.Add(details);
    Controls.Add(split);Controls.Add(heading);
    heading.Text="FIADO | Selecione um cliente";
    foreach(var table in new[]{master,details}){
     table.Font=new Font("Segoe UI",10f);
     table.BackgroundColor=Color.White;
     table.RowHeadersVisible=false;
     table.EnableHeadersVisualStyles=false;
     table.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(22,75,160);
     table.ColumnHeadersDefaultCellStyle.ForeColor=Color.White;
    }
   }else BuildSalesPage();
   master.SelectionChanged+=(sender,args)=>{if(!loadingMaster)LoadDetails();};
   // Clique seleciona e atualiza o painel direito. Duplo clique abre a janela antiga.
   master.CellDoubleClick+=(sender,args)=>{if(args.RowIndex>=0&&!loadingMaster&&!credit)ShowSaleItems();};
   Activated+=(sender,args)=>{if(!credit&&!loadingMaster)LoadMaster();};
   LoadMaster();
  }

  // Atualiza somente a lista de vendas aberta após uma venda ser gravada.
  public void RefreshAfterRecordedSale(){
   if(IsDisposed||credit)return;
   if(InvokeRequired){BeginInvoke(new Action(RefreshAfterRecordedSale));return;}
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
    "(SELECT COUNT(*) FROM sale_items si WHERE si.sale_id=s.id) AS itens,"+
    "COALESCE(day.total,0) AS 'Total do dia' "+
    "FROM sales s LEFT JOIN (SELECT date(created_at) AS dia,SUM(COALESCE(gross_total,0)) AS total "+
    "FROM sales GROUP BY date(created_at)) day ON day.dia=date(s.created_at)";
  }
  void ShowDailyTotals(){
   if(credit)return;
   try{
    string condition=SalesFilterSql();
    var days=QueryFiltered("SELECT date(created_at) AS 'Data',COUNT(*) AS 'Vendas',"+
     "ROUND(SUM(COALESCE(gross_total,0)),2) AS 'Total vendido (R$)' FROM sales WHERE "+condition+
     " GROUP BY date(created_at) ORDER BY date(created_at) DESC");
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
  string SalesFilterSql(){
   string condition;
   if(selectedRange)condition="created_at >= @start AND created_at < date(@end,'+1 day')";
   else if(selectedMonth)condition="created_at >= @date AND created_at < date(@date,'+1 month')";
   else if(selectedDate)condition="created_at >= @date AND created_at < date(@date,'+1 day')";
   else condition=previous?"created_at < date('now','localtime')":
    "created_at >= date('now','localtime') AND created_at < date('now','localtime','+1 day')";
   if(!credit&&!string.IsNullOrEmpty(chosenCustomer))
    condition+=" AND TRIM(COALESCE(customer_name,''))=@customer";
   return condition;
  }
  string SalesFilterParameter(){return selectedDate||selectedMonth?"@date":null;}
  object SalesFilterValue(){
   if(selectedMonth)return monthFilter.Value.ToString("yyyy-MM-01");
   return selectedDate?(object)dateFilter.Value.ToString("yyyy-MM-dd"):null;
  }
  DataTable QueryFiltered(string sql){
   using(var connection=new SQLiteConnection(new SQLiteConnectionStringBuilder{
    DataSource=db,ReadOnly=true,FailIfMissing=true}.ToString())){
    connection.Open();
    using(var command=new SQLiteCommand(sql,connection)){
     if(selectedMonth)command.Parameters.AddWithValue("@date",monthFilter.Value.ToString("yyyy-MM-01"));
     else if(selectedDate)command.Parameters.AddWithValue("@date",dateFilter.Value.ToString("yyyy-MM-dd"));
     if(selectedRange){
      command.Parameters.AddWithValue("@start",dateFilter.Value.ToString("yyyy-MM-dd"));
      command.Parameters.AddWithValue("@end",rangeEnd.Value.ToString("yyyy-MM-dd"));
     }
     if(!string.IsNullOrEmpty(chosenCustomer))command.Parameters.AddWithValue("@customer",chosenCustomer);
     var result=new DataTable();
     using(var adapter=new SQLiteDataAdapter(command))adapter.Fill(result);
     return result;
    }
   }
  }
  string SalesFilterTitle(){
   return selectedRange?"Vendas de "+dateFilter.Value.ToString("dd/MM/yyyy")+" a "+rangeEnd.Value.ToString("dd/MM/yyyy"):selectedMonth?"Vendas de "+monthFilter.Value.ToString("MMMM yyyy"):selectedDate?"Vendas de "+dateFilter.Value.ToString("dd/MM/yyyy"):previous?"Vendas anteriores":"Vendas de hoje";
  }
  // A numeracao e cronologica; IDs de vendas nunca mudam (itens, pagamentos e Fiado).
  void PrepareSalesList(DataTable rows){
   var positions=new System.Collections.Generic.Dictionary<long,long>();
   using(var conn=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=db,ReadOnly=true,FailIfMissing=true}.ToString())){
    conn.Open();
    using(var cmd=new SQLiteCommand("SELECT id FROM sales ORDER BY datetime(created_at),id",conn))
    using(var rd=cmd.ExecuteReader()){
     long position=0;while(rd.Read())positions[Convert.ToInt64(rd["id"])]=++position;
    }
   }
   var sequence=rows.Columns.Add("Ordem cronológica",typeof(long));
   var display=rows.Columns.Add("Venda para consultar",typeof(string));display.SetOrdinal(0);
   foreach(DataRow item in rows.Rows){
    long id=Convert.ToInt64(item["venda"]);long sequenceNumber;
    if(!positions.TryGetValue(id,out sequenceNumber))sequenceNumber=0;
    item[sequence]=sequenceNumber;
    item[display]="Venda nº "+sequenceNumber+"  |  "+Convert.ToString(item["cliente"])+"  |  "+Convert.ToString(item["data"])+"  |  R$ "+Convert.ToDecimal(item["total"]).ToString("N2");
   }
  }
  void ConfigureSalesColumns(){
   if(credit||master.Columns.Count==0)return;
   master.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
   // A numeracao exibida e a ordem cronologica; a chave ID continua intacta.
   foreach(DataGridViewColumn col in master.Columns)col.Visible=
    col.Name=="Ordem cronológica"||col.Name=="cliente"||
    col.Name=="data"||col.Name=="itens"||col.Name=="total";
   var number=master.Columns["Ordem cronológica"];
   number.HeaderText="Nº Venda";number.DisplayIndex=0;number.FillWeight=74;number.MinimumWidth=73;
   var client=master.Columns["cliente"];
   client.HeaderText="Cliente";client.DisplayIndex=1;client.FillWeight=183;client.MinimumWidth=130;
   var date=master.Columns["data"];
   date.HeaderText="Data / Hora";date.DisplayIndex=2;date.FillWeight=160;date.MinimumWidth=122;
   var items=master.Columns["itens"];
   items.HeaderText="Itens";items.DisplayIndex=3;items.FillWeight=52;items.MinimumWidth=51;
   var total=master.Columns["total"];
   total.HeaderText="Total (R$)";total.DisplayIndex=4;total.FillWeight=100;total.MinimumWidth=90;
   total.DefaultCellStyle.Format="N2";
   total.DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleRight;
   number.DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleCenter;
   items.DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleCenter;
  }

  void LoadMaster(){
   if(loadingMaster||IsDisposed||Disposing)return;
   loadingMaster=true;
   try{
    var rows=credit?
     Query("SELECT COALESCE(NULLIF(TRIM(customer_name),''),'SEM CLIENTE') AS cliente,COUNT(*) AS vendas,SUM(COALESCE(gross_total,0)) AS total_fiado FROM sales WHERE UPPER(COALESCE(payment_method,'')) LIKE '%FIADO%' GROUP BY COALESCE(NULLIF(TRIM(customer_name),''),'SEM CLIENTE') ORDER BY cliente"):
     QueryFiltered(SalesWithDailyTotalsSql()+" WHERE "+
      SalesFilterSql().Replace("created_at","s.created_at")+
      " ORDER BY datetime(s.created_at) DESC,s.id DESC LIMIT 3000");
    if(!credit)PrepareSalesList(rows);
    master.DataSource=rows;
    if(!credit){
     ConfigureSalesColumns();
     var listTitle=Controls.Find("amzgSalesListTitle",true).FirstOrDefault() as Label;
     if(listTitle!=null)listTitle.Text="  "+SalesFilterTitle()+
      (rows.Rows.Count>=3000?" (primeiras 3.000)":"");
     var totals=QueryFiltered("SELECT COUNT(*) AS quantidade,COALESCE(SUM(gross_total),0) AS total "+
      "FROM sales WHERE "+SalesFilterSql());
     var daily=Query("SELECT COUNT(*) AS quantidade,COALESCE(SUM(gross_total),0) AS total "+
      "FROM sales WHERE created_at >= date('now','localtime') "+
      "AND created_at < date('now','localtime','+1 day')");
     var monthly=Query("SELECT COUNT(*) AS quantidade,COALESCE(SUM(gross_total),0) AS total "+
      "FROM sales WHERE created_at >= @date AND created_at < date(@date,'+1 month')",
      "@date",(selectedMonth?monthFilter.Value:DateTime.Today).ToString("yyyy-MM-01"));
     long selectedCount=Convert.ToInt64(totals.Rows[0]["quantidade"]);
     long todayCount=Convert.ToInt64(daily.Rows[0]["quantidade"]);
     decimal todayTotal=Convert.ToDecimal(daily.Rows[0]["total"]);
     decimal monthlyTotal=Convert.ToDecimal(monthly.Rows[0]["total"]);
     string dayText="R$ "+todayTotal.ToString("N2");
     string monthText="R$ "+monthlyTotal.ToString("N2");
     footerCount.Text="Vendas do Dia: "+todayCount.ToString("N0");
     footerToday.Text="Total Vendido Hoje: "+dayText;
     footerMonth.Text="Total do Mês: "+monthText;
     headerToday.Text=dayText;
     headerMonth.Text=monthText;
     summary.Text=SalesFilterTitle()+" | Quantidade: "+selectedCount+
      " | TOTAL VENDIDO: R$ "+Convert.ToDecimal(totals.Rows[0]["total"]).ToString("N2")+
      " | TOTAL DO MÊS: "+monthText;
    }
   }catch(Exception ex){MessageBox.Show(ex.Message,"Erro ao abrir módulo");}
   finally{loadingMaster=false;LoadDetails();}
  }

  void ShowSaleItems(){
   try{
    long id=SelectedSale();
    var sale=Query("SELECT created_at,customer_name,gross_total,payment_method FROM sales WHERE id=@id","@id",id);
    if(sale.Rows.Count==0)throw new Exception("Venda não encontrada.");
    var items=Query("SELECT barcode AS 'Código',name AS 'Produto',quantity AS 'Quantidade',unit_price AS 'Preço (R$)',total AS 'Valor (R$)' FROM sale_items WHERE sale_id=@id ORDER BY id","@id",id);
    using(var dialog=new Form{Text="Produtos da venda #"+id,Width=850,Height=560,MinimumSize=new Size(620,400),StartPosition=FormStartPosition.CenterParent,BackColor=Color.FromArgb(229,242,255)}){
     var header=new Label{Dock=DockStyle.Top,Height=92,Padding=new Padding(14,9,8,4),Font=new Font("Segoe UI",12,FontStyle.Bold),ForeColor=Color.FromArgb(20,73,158),BackColor=Color.FromArgb(221,238,255)};
     var row=sale.Rows[0];
     header.Text="VENDA #"+id+"  |  "+Convert.ToString(row["created_at"])+Environment.NewLine+
      "Cliente: "+Convert.ToString(row["customer_name"])+"  |  Pagamento: "+Convert.ToString(row["payment_method"])+
      Environment.NewLine+"Total da venda: R$ "+Convert.ToDecimal(row["gross_total"]).ToString("N2")+"  |  "+items.Rows.Count+" produto(s)";
     var list=new DataGridView{Dock=DockStyle.Fill,DataSource=items,ReadOnly=true,AllowUserToAddRows=false,RowHeadersVisible=false,
      AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,RowTemplate={Height=38},BackgroundColor=Color.White,
      ColumnHeadersHeight=46,ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
      EnableHeadersVisualStyles=false};
     list.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(22,75,160);
     list.ColumnHeadersDefaultCellStyle.ForeColor=Color.White;
     list.ColumnHeadersDefaultCellStyle.Font=new Font("Segoe UI",10,FontStyle.Bold);
     list.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(238,244,252);
     dialog.Controls.Add(list);dialog.Controls.Add(header);dialog.ShowDialog(this);
    }
   }catch(Exception ex){MessageBox.Show("Não foi possível abrir os produtos: "+ex.Message,"Vendas",MessageBoxButtons.OK,MessageBoxIcon.Error);}
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
   long input;
   if(!long.TryParse(saleSearch.Text.Trim(),out input)||input<=0){
    MessageBox.Show("Informe um número de venda válido.","Vendas");return;
   }
   try{
    // O número exibido na lista é a ordem cronológica, não o ID interno.
    long found=0,order=0;
    var positions=Query("SELECT id FROM sales ORDER BY datetime(created_at),id");
    foreach(DataRow row in positions.Rows){
     order++;
     if(order==input){found=Convert.ToInt64(row["id"]);break;}
    }
    // Compatibilidade com buscas antigas feitas diretamente pelo ID.
    if(found==0)found=input;
    loadingMaster=true;
    try{
     var rows=Query(SalesWithDailyTotalsSql()+" WHERE s.id=@id","@id",found);
     PrepareSalesList(rows);
     master.DataSource=rows;
     ConfigureSalesColumns();
    }finally{loadingMaster=false;}
    LoadDetails();
    if(((DataTable)master.DataSource).Rows.Count==0){
     var label=Controls.Find("amzgSalesListTitle",true).FirstOrDefault() as Label;
     if(label!=null)label.Text="  Venda não encontrada";
     details.DataSource=null;
    }else{
     var label=Controls.Find("amzgSalesListTitle",true).FirstOrDefault() as Label;
     if(label!=null)label.Text="  Resultado da busca • venda nº "+input;
     summary.Text="Consulta individual: venda nº "+input;
    }
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
     if(!credit){saleInfo.Text="Selecione uma venda";saleTotal.Text="R$ 0,00";heading.Text="  DETALHES DA VENDA";}
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
     var info=Query("SELECT customer_name,created_at,payment_method,gross_total FROM sales WHERE id=@id","@id",selected);
     string ordinal=master.CurrentRow!=null&&master.Columns.Contains("Ordem cronológica")?
      Convert.ToString(master.CurrentRow.Cells["Ordem cronológica"].Value):Convert.ToString(selected);
     title="  DETALHES DA VENDA  #"+ordinal;
     if(info.Rows.Count>0){
      var sale=info.Rows[0];
      saleInfo.Text=Convert.ToString(sale["customer_name"])+Environment.NewLine+
       Convert.ToString(sale["created_at"])+Environment.NewLine+
       Convert.ToString(sale["payment_method"]);
      saleTotal.Text="R$ "+Convert.ToDecimal(sale["gross_total"]).ToString("N2");
     }else{
      saleInfo.Text="Venda não encontrada";saleTotal.Text="R$ 0,00";
     }
    }
    if(!IsDisposed&&!details.IsDisposed){
     details.DataSource=result;
     heading.Text=title;
     if(!credit){
      foreach(DataGridViewColumn col in details.Columns){
       col.Visible=col.Name=="produto"||col.Name=="quantidade"||
        col.Name=="preco"||col.Name=="valor";
      }
      details.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
      var product=details.Columns["produto"];
      product.HeaderText="Produto";product.DisplayIndex=0;
      product.FillWeight=230;product.MinimumWidth=160;
      var qty=details.Columns["quantidade"];
      qty.HeaderText="Qtd.";qty.DisplayIndex=1;qty.FillWeight=55;qty.MinimumWidth=55;
      qty.DefaultCellStyle.Format="N3";
      var price=details.Columns["preco"];
      price.HeaderText="Valor Unit. (R$)";price.DisplayIndex=2;
      price.FillWeight=98;price.MinimumWidth=85;
      price.DefaultCellStyle.Format="N2";
      var total=details.Columns["valor"];
      total.HeaderText="Total (R$)";total.DisplayIndex=3;
      total.FillWeight=98;total.MinimumWidth=85;
      total.DefaultCellStyle.Format="N2";
      total.DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleRight;
     }
    }
   }catch(Exception ex){
    if(!details.IsDisposed)details.DataSource=null;
    // Mostrar a origem do erro, se ainda houver outra condição não prevista.
    MessageBox.Show("Não foi possível consultar os detalhes."+Environment.NewLine+ex.ToString(),"Erro ao consultar detalhes",MessageBoxButtons.OK,MessageBoxIcon.Error);
   }finally{loadingDetails=false;}
  }
 }
}
